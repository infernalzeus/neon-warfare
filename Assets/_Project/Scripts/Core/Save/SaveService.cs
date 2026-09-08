using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NW.Core.Save
{
    /// <summary>Persistent profile, v1 schema (doc 07 §7). Mid-battle state is never saved.</summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;
        public string savedAtUtc = "";

        // Campaign
        public int highestMission;          // linear index, 0-based
        public List<int> missionStars = new();

        // Meta currencies (doc 04 §2.1)
        public int credits;
        public int dataFragments;
        public int quantumCores;
        public int blueprintShards;

        // Progression
        public List<string> ownedTechNodes = new();
        public List<string> unlockedUnits = new() { "drone", "shock_trooper" };
        public List<int> buildingLevels = new() { 0, 0, 0, 0, 0 };
        public List<HeroSave> heroes = new();
        public List<string> stableProtocols = new();

        public int checksum; // FNV-1a of the payload with checksum zeroed
    }

    [Serializable]
    public class HeroSave
    {
        public string id = "";
        public int xp;
        public int ascension;
        public List<string> skillNodes = new();
    }

    /// <summary>
    /// Versioned JSON save with atomic write (temp + replace), 3 rotating backups
    /// and checksum recovery. Lives under persistentDataPath (Steam auto-cloud
    /// compatible).
    /// </summary>
    public sealed class SaveService
    {
        private const int BackupCount = 3;
        private readonly string _path;

        public SaveData Data { get; private set; } = new();

        public SaveService(string fileName = "profile.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public void Save()
        {
            Data.savedAtUtc = DateTime.UtcNow.ToString("o");
            Data.checksum = 0;
            Data.checksum = Fnv1a(JsonUtility.ToJson(Data));
            string json = JsonUtility.ToJson(Data, prettyPrint: true);

            RotateBackups();
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        /// <summary>Load newest valid file (main, then backups). Returns false on fresh profile.</summary>
        public bool Load()
        {
            foreach (string candidate in Candidates())
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(candidate));
                    if (data == null || !ChecksumValid(data)) continue;
                    Data = Migrate(data);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] Failed reading {candidate}: {e.Message}");
                }
            }
            Data = new SaveData();
            return false;
        }

        private static SaveData Migrate(SaveData data)
        {
            // Ordered migration chain; add a case per schema bump.
            switch (data.schemaVersion)
            {
                case 1: break;
                default:
                    Debug.LogWarning($"[Save] Unknown schema {data.schemaVersion}, using as-is.");
                    break;
            }
            return data;
        }

        private static bool ChecksumValid(SaveData data)
        {
            int stored = data.checksum;
            data.checksum = 0;
            bool ok = Fnv1a(JsonUtility.ToJson(data)) == stored;
            data.checksum = stored;
            return ok;
        }

        private IEnumerable<string> Candidates()
        {
            yield return _path;
            for (int i = 1; i <= BackupCount; i++) yield return $"{_path}.bak{i}";
        }

        private void RotateBackups()
        {
            if (!File.Exists(_path)) return;
            string oldest = $"{_path}.bak{BackupCount}";
            if (File.Exists(oldest)) File.Delete(oldest);
            for (int i = BackupCount - 1; i >= 1; i--)
            {
                string from = $"{_path}.bak{i}";
                if (File.Exists(from)) File.Move(from, $"{_path}.bak{i + 1}");
            }
            File.Copy(_path, $"{_path}.bak1");
        }

        private static int Fnv1a(string s)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in s) { hash ^= c; hash *= 16777619; }
                return (int)hash;
            }
        }
    }
}
