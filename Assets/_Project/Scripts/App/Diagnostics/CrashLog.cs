using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if NW_FIREBASE
using Firebase.Extensions;
using Firebase.Firestore;
#endif

namespace NW.App
{
    /// <summary>
    /// Minimal crash / error reporter — "where did it fail?" telemetry, not a full analytics
    /// platform.
    ///
    /// Captures uncaught C# exceptions (and, opt-in, <c>Debug.LogError</c>), tags each with build
    /// + device info and a short breadcrumb trail, and ships it to the Firestore
    /// <c>diagnostics</c> collection so failures on real devices show up in the Firebase console.
    ///
    ///  • De-duplicated and rate-limited so one bad frame can't spam the database.
    ///  • Always written to a local file first, then flushed to Firestore on the next launch —
    ///    so an offline crash is still captured.
    ///  • No personal data: a random per-launch <c>sessionId</c> only. Never the pilot name,
    ///    never the install id.
    ///  • Degrades cleanly: without <c>NW_FIREBASE</c> (or offline) it just keeps the local file.
    ///
    /// It will NOT catch a hard native crash that kills the process instantly. For that, add
    /// Firebase Crashlytics (FirebaseCrashlytics.unitypackage) later — this and Crashlytics
    /// coexist fine.
    /// </summary>
    public static class CrashLog
    {
        const int    MaxReportsPerSession = 12;
        const int    MinSecondsBetween    = 3;
        const int    MessageCap           = 1800;
        const int    StackCap             = 7000;
        const int    BreadcrumbCount      = 20;
        const int    MaxFlushPerLaunch    = 50;
        const string Collection           = "diagnostics";

        static readonly ConcurrentQueue<Report> _queue = new ConcurrentQueue<Report>();
        static readonly HashSet<int>            _seen  = new HashSet<int>();
        static readonly Queue<string>           _trail = new Queue<string>();
        static readonly object _seenLock  = new object();
        static readonly object _trailLock = new object();

        static bool   _started;
        static int    _sent;
        static volatile bool _enabled       = true;   // cached — the log callback runs off-thread
        static volatile bool _includeErrors = false;
        static string _sessionId, _appVersion, _unityVersion, _platform, _deviceModel, _os;
        static string _pendingPath;

        struct Report { public string kind, message, stack, trail; }

        [Serializable]
        class DiagRecord
        {
            public string ts, kind, message, stack, trail, scene,
                          appVersion, unityVersion, platform, deviceModel, os, sessionId;
        }

        // ------------------------------------------------------------ init ---

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (_started) return;
            _started = true;

            _sessionId    = Guid.NewGuid().ToString("N").Substring(0, 16);
            _appVersion   = Application.version;
            _unityVersion = Application.unityVersion;
            _platform     = Application.platform.ToString();
            _deviceModel  = SafeInfo(() => SystemInfo.deviceModel);
            _os           = SafeInfo(() => SystemInfo.operatingSystem);
            _pendingPath  = Path.Combine(Application.persistentDataPath, "diag_pending.jsonl");
            _enabled       = GameSettings.DiagnosticsEnabled;
            _includeErrors = GameSettings.DiagnosticsIncludeErrors;

            Application.logMessageReceivedThreaded += OnLog;

            var go = new GameObject("[CrashLogPump]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Pump>();
        }

        static string SafeInfo(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }

        /// <summary>Drop a breadcrumb. The last <see cref="BreadcrumbCount"/> are attached to the
        /// next crash report — cheap way to record "what was happening".</summary>
        public static void Note(string crumb)
        {
            if (string.IsNullOrEmpty(crumb)) return;
            lock (_trailLock)
            {
                _trail.Enqueue($"{DateTime.UtcNow:HH:mm:ss} {crumb}");
                while (_trail.Count > BreadcrumbCount) _trail.Dequeue();
            }
        }

        // ---------------------------------------------------- log callback ---
        // Runs on ANY thread. Do not touch Unity API here — enqueue only.

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if (!_enabled) return;
            bool want = type == LogType.Exception
                     || (type == LogType.Error && _includeErrors);
            if (!want || _sent >= MaxReportsPerSession) return;

            int key = unchecked(((message ?? "").GetHashCode() * 31) ^ FirstFrame(stackTrace).GetHashCode());
            lock (_seenLock) { if (!_seen.Add(key)) return; }

            string trail;
            lock (_trailLock) trail = string.Join("\n", _trail.ToArray());

            _queue.Enqueue(new Report
            {
                kind    = type.ToString(),
                message = Cap(message, MessageCap),
                stack   = Cap(stackTrace, StackCap),
                trail   = trail,
            });
        }

        static string FirstFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            int nl = stack.IndexOf('\n');
            return nl < 0 ? stack : stack.Substring(0, nl);
        }

        static string Cap(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n) + " …[cut]";

        // ------------------------------------------------ main-thread pump ---

        sealed class Pump : MonoBehaviour
        {
            float _flushAt = 2f;   // flush last session's local file shortly after launch
            float _settingsAt;

            void Update()
            {
                if (Time.unscaledTime >= _settingsAt)
                {
                    _settingsAt = Time.unscaledTime + 5f;
                    _enabled       = GameSettings.DiagnosticsEnabled;
                    _includeErrors = GameSettings.DiagnosticsIncludeErrors;
                }

                if (_flushAt > 0f && Time.unscaledTime >= _flushAt)
                {
                    _flushAt = 0f;
                    FlushPendingFile();
                }

                if (_queue.IsEmpty || Time.unscaledTime - _lastSend < MinSecondsBetween) return;
                if (!_queue.TryDequeue(out var r)) return;

                _lastSend = Time.unscaledTime;
                _sent++;

                string json = ToJson(r, SceneManager.GetActiveScene().name);
                Append(json);
                TrySend(json);
            }
        }

        static float _lastSend = -999f;

        static string ToJson(Report r, string scene)
        {
            var e = new DiagRecord
            {
                ts = DateTime.UtcNow.ToString("o"),
                kind = r.kind, message = r.message, stack = r.stack, trail = r.trail,
                scene = scene, appVersion = _appVersion, unityVersion = _unityVersion,
                platform = _platform, deviceModel = _deviceModel, os = _os, sessionId = _sessionId,
            };
            return JsonUtility.ToJson(e);
        }

        static void Append(string json)
        {
            try { File.AppendAllText(_pendingPath, json + "\n"); } catch { /* best effort */ }
        }

        static void FlushPendingFile()
        {
            string[] lines;
            try
            {
                if (!File.Exists(_pendingPath)) return;
                lines = File.ReadAllLines(_pendingPath);
            }
            catch { return; }
            if (lines.Length == 0) { TryDelete(_pendingPath); return; }

#if NW_FIREBASE
            if (!Leaderboard.IsOnline) return;   // retry next launch
            int n = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (n++ >= MaxFlushPerLaunch) break;
                SendRaw(line);
            }
            TryDelete(_pendingPath);
#endif
        }

        static void TrySend(string json)
        {
#if NW_FIREBASE
            if (Leaderboard.IsOnline) SendRaw(json);
#endif
        }

#if NW_FIREBASE
        static void SendRaw(string json)
        {
            try
            {
                var rec = JsonUtility.FromJson<DiagRecord>(json);
                if (rec == null) return;
                var data = new Dictionary<string, object>
                {
                    ["ts"]           = rec.ts ?? "",
                    ["kind"]         = rec.kind ?? "",
                    ["message"]      = rec.message ?? "",
                    ["stack"]        = rec.stack ?? "",
                    ["trail"]        = rec.trail ?? "",
                    ["scene"]        = rec.scene ?? "",
                    ["appVersion"]   = rec.appVersion ?? "",
                    ["unityVersion"] = rec.unityVersion ?? "",
                    ["platform"]     = rec.platform ?? "",
                    ["deviceModel"]  = rec.deviceModel ?? "",
                    ["os"]           = rec.os ?? "",
                    ["sessionId"]    = rec.sessionId ?? "",
                    ["serverTs"]     = FieldValue.ServerTimestamp,
                };
                FirebaseFirestore.DefaultInstance.Collection(Collection).Document().SetAsync(data);
            }
            catch { /* diagnostics must never throw */ }
        }
#endif

        static void TryDelete(string p) { try { File.Delete(p); } catch { } }
    }
}
