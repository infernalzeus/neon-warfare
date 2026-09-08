using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Reads and writes <see cref="GhostRecord"/> blobs.
    ///
    ///  • Bundled pool  — read-only <c>TextAsset</c>s under <c>Resources/Ghosts/</c>,
    ///    the seed opponents shipped with the build.
    ///  • Local pool    — <c>{persistentDataPath}/ghosts/*.json</c>, one file per match
    ///    the player finishes. Capped at <see cref="MaxLocal"/>; oldest pruned.
    /// </summary>
    public static class GhostStore
    {
        public const int MaxLocal = 60;

        static string Dir
        {
            get
            {
                string d = Path.Combine(Application.persistentDataPath, "ghosts");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        // ---------------------------------------------------------------- write

        /// <summary>Persist a sealed record. Returns the record (with <c>id</c> filled) or
        /// null if it was empty / unserialisable.</summary>
        public static GhostRecord Save(GhostRecord rec)
        {
            if (rec == null || rec.actions == null || rec.actions.Count == 0) return null;
            try
            {
                string stem = $"{Mathf.Clamp(rec.level, 0, 99):D2}_{rec.mmr:D4}_" +
                              $"{System.DateTime.UtcNow:yyyyMMddHHmmssfff}_{rec.result}";
                rec.id = stem;
                File.WriteAllText(Path.Combine(Dir, stem + ".json"), JsonUtility.ToJson(rec));
                Prune();
                return rec;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GhostStore] save failed: {e.Message}");
                return null;
            }
        }

        static void Prune()
        {
            try
            {
                var files = new List<string>(Directory.GetFiles(Dir, "*.json"));
                if (files.Count <= MaxLocal) return;
                files.Sort((a, b) => File.GetCreationTimeUtc(a).CompareTo(File.GetCreationTimeUtc(b)));
                for (int i = 0; i < files.Count - MaxLocal; i++)
                    File.Delete(files[i]);
            }
            catch { /* pruning is best-effort */ }
        }

        // ----------------------------------------------------------------- read

        /// <summary>Every ghost available to challenge — bundled first, then local, newest
        /// local first. Never throws; a bad file is skipped.</summary>
        public static List<GhostRecord> All()
        {
            var list = new List<GhostRecord>();

            var bundled = Resources.LoadAll<TextAsset>("Ghosts");
            foreach (var ta in bundled)
            {
                var r = Parse(ta.text);
                if (r != null) { r.bundled = true; if (string.IsNullOrEmpty(r.id)) r.id = ta.name; list.Add(r); }
            }

            try
            {
                var files = new List<string>(Directory.GetFiles(Dir, "*.json"));
                files.Sort((a, b) => File.GetCreationTimeUtc(b).CompareTo(File.GetCreationTimeUtc(a)));
                foreach (var f in files)
                {
                    var r = Parse(File.ReadAllText(f));
                    if (r != null) { if (string.IsNullOrEmpty(r.id)) r.id = Path.GetFileNameWithoutExtension(f); list.Add(r); }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GhostStore] read failed: {e.Message}");
            }

            return list;
        }

        /// <summary>One record by its <c>id</c> (bundled name or local file stem), or null.</summary>
        public static GhostRecord ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var r in All())
                if (r.id == id) return r;
            return null;
        }

        static GhostRecord Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var r = JsonUtility.FromJson<GhostRecord>(json);
                if (r == null || r.actions == null || r.actions.Count == 0) return null;
                return r;
            }
            catch { return null; }
        }
    }
}
