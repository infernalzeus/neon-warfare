using System.IO;
using NW.App;
using UnityEditor;
using UnityEngine;

namespace NW.Editor
{
    /// <summary>
    /// One-off diagnostic tool: dumps the REAL procedurally-generated troop textures
    /// (exactly what NeonArt.Unit() builds and BattlefieldView renders) to PNG files on
    /// disk, so they can be reviewed as actual images instead of a hand-drawn approximation.
    /// Run headless via: Unity.exe -batchmode -nographics -quit -projectPath <proj>
    ///   -executeMethod NW.Editor.TroopArtExporter.ExportCyberTheme -exportDir <absolute path>
    /// </summary>
    public static class TroopArtExporter
    {
        static readonly string[] CyberArtIds =
        {
            "cybdrone", "cybtrooper", "cybsniper", "cybmech", "cybshield",
            "cybinter", "cybhacker", "cybtitan", "cybturret",
        };
        static readonly string[] SynthArtIds =
        {
            "synbot", "synracer", "synlaser", "syncruiser", "synbouncer",
            "synspeeder", "synkeytar", "synobelisk", "synpylon",
        };
        static readonly string[] BiopunkArtIds =
        {
            "spore", "mutant", "stinger", "crawler", "carapace",
            "swarm", "mycelium", "hive", "pod",
        };
        static readonly string[] MedievalArtIds =
        {
            "pigeon", "knight", "archer", "siege", "paladin",
            "rogue", "wizard", "golem", "ballista",
        };
        static readonly string[] IndustrialArtIds =
        {
            "rivetbot", "worker", "gunner", "crane", "bulkhead",
            "ornithopter", "engineer", "furnace", "gatling",
        };
        static readonly string[] SakuraArtIds =
        {
            "wisp", "shinobi", "yumi", "tanuki", "shrine",
            "kite", "onmyoji", "kami", "torii",
        };
        static readonly string[] SolarArtIds =
        {
            "ember", "guardian", "raycaster", "forgewalker", "aegis",
            "phoenix", "pyromancer", "colossus", "heliostat",
        };
        static readonly string[] DawnArtIds =
        {
            "sprite", "wanderer", "seeker", "caravan", "ward",
            "glider", "oracle", "sentinel", "beacon",
        };

        [MenuItem("NW/Export Cyber Troop Art")]
        public static void ExportCyberTheme() => ExportTheme(0, CyberArtIds);

        [MenuItem("NW/Export Synthwave Troop Art")]
        public static void ExportSynthTheme() => ExportTheme(1, SynthArtIds);

        [MenuItem("NW/Export Biopunk Troop Art")]
        public static void ExportBiopunkTheme() => ExportTheme(2, BiopunkArtIds);

        [MenuItem("NW/Export Medieval Troop Art")]
        public static void ExportMedievalTheme() => ExportTheme(3, MedievalArtIds);

        [MenuItem("NW/Export Industrial Troop Art")]
        public static void ExportIndustrialTheme() => ExportTheme(4, IndustrialArtIds);

        [MenuItem("NW/Export Sakura Troop Art")]
        public static void ExportSakuraTheme() => ExportTheme(5, SakuraArtIds);

        [MenuItem("NW/Export Solar Troop Art")]
        public static void ExportSolarTheme() => ExportTheme(6, SolarArtIds);

        [MenuItem("NW/Export Dawn Troop Art")]
        public static void ExportDawnTheme() => ExportTheme(7, DawnArtIds);

        /// <summary>Diagnostic-page entry point: dumps every unit in every theme's full
        /// pose set to one folder, so an HTML page can play the real in-game walk/attack
        /// cycle for the whole roster in one pass. Run via -executeMethod
        /// NW.Editor.TroopArtExporter.ExportAllThemes -exportDir <path></summary>
        [MenuItem("NW/Export ALL Troop Art")]
        public static void ExportAllThemes()
        {
            ExportTheme(0, CyberArtIds);
            ExportTheme(1, SynthArtIds);
            ExportTheme(2, BiopunkArtIds);
            ExportTheme(3, MedievalArtIds);
            ExportTheme(4, IndustrialArtIds);
            ExportTheme(5, SakuraArtIds);
            ExportTheme(6, SolarArtIds);
            ExportTheme(7, DawnArtIds);
        }

        static void ExportTheme(int themeIndex, string[] ids)
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);

            int prevTheme = GameSettings.ThemeIndex;
            GameSettings.ThemeIndex = themeIndex;
            try
            {
                foreach (string id in ids)
                {
                    // Full pose set: 0 idle, 1/2/6/7 walk cycle (this is the real in-game
                    // order BattlefieldView cycles through: Walk4 = {1,2,6,7}), 3/4 attack
                    // windup+strike, 5 flinch. Exporting all of them is what lets an HTML
                    // page actually PLAY the walk/attack cycle instead of showing two
                    // static frames.
                    ExportOne(id, pose: 0, dir);
                    if (NeonArt.HasPoses(id))
                        foreach (int pose in new[] { 1, 2, 6, 7, 3, 4, 5 })
                            ExportOne(id, pose, dir);
                }
                Debug.Log($"[TroopArtExporter] Wrote {ids.Length} unit(s) to {dir}");
            }
            finally
            {
                GameSettings.ThemeIndex = prevTheme;
            }
        }

        static void ExportOne(string id, int pose, string dir)
        {
            Texture2D tex = NeonArt.Unit(id, player: true, pose: pose);
            byte[] png = tex.EncodeToPNG();
            string path = Path.Combine(dir, $"{id}_pose{pose}.png");
            File.WriteAllBytes(path, png);
        }

        /// <summary>Which theme's palette an id belongs to, by scanning the same per-theme
        /// arrays ExportTheme uses. ExportRig used to guess this from an "syn"-prefix check,
        /// which rendered every non-Cyber/Synth rigged id (Knight, Guardian, Yumi, ...) in the
        /// wrong theme's colours -- authored-art ids mostly don't care, but this is the actual
        /// source of truth instead of a shortcut that only covered 2 of 8 themes.</summary>
        static int ThemeOf(string id)
        {
            string[][] themes = { CyberArtIds, SynthArtIds, BiopunkArtIds, MedievalArtIds,
                                   IndustrialArtIds, SakuraArtIds, SolarArtIds, DawnArtIds };
            for (int t = 0; t < themes.Length; t++)
                foreach (string a in themes[t])
                    if (a == id) return t;
            return 0;
        }

        /// <summary>Diagnostic for the rigged-limb path: dumps the torso-only body texture plus
        /// each of the 6 rotatable limb segments, hanging straight down at rest exactly as
        /// BattlefieldView receives them before rotating each one live. Run via -executeMethod
        /// NW.Editor.TroopArtExporter.ExportRig -rigId cybtrooper -exportDir <path></summary>
        [MenuItem("NW/Export Cyber Rig (Lancer)")]
        public static void ExportRig()
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);
            string id = GetArg("-rigId") ?? "cybtrooper";
            bool isPlayer = GetArg("-player") != "0";
            ExportOneRig(id, isPlayer, dir);
        }

        static void ExportOneRig(string id, bool isPlayer, string dir)
        {
            string suffix = isPlayer ? "" : "_enemy";
            int prevTheme = GameSettings.ThemeIndex;
            GameSettings.ThemeIndex = ThemeOf(id);
            try
            {
                if (!NeonArt.HasLimbRig(id))
                {
                    Debug.LogError($"[TroopArtExporter] '{id}' has no limb rig.");
                    return;
                }
                File.WriteAllBytes(Path.Combine(dir, $"{id}{suffix}_rigbody.png"),
                    NeonArt.UnitNoLimbs(id, isPlayer).EncodeToPNG());
                for (int seg = 0; seg < NeonArt.LimbSegCount; seg++)
                    File.WriteAllBytes(Path.Combine(dir, $"{id}{suffix}_rigLimb{seg}.png"),
                        NeonArt.UnitLimb(id, isPlayer, seg).EncodeToPNG());
                Debug.Log($"[TroopArtExporter] Wrote rig body + {NeonArt.LimbSegCount} limb(s) for '{id}' ({(isPlayer?"player":"enemy")}) to {dir}");
            }
            finally
            {
                GameSettings.ThemeIndex = prevTheme;
            }
        }

        static readonly string[] AllRiggedIds =
        {
            "kami", "cybtrooper", "cybsniper", "cybtitan", "cybshield",
            "synracer", "synlaser", "synbouncer", "synobelisk",
            "mutant", "carapace",
            "knight", "archer", "paladin",
            "worker", "gunner", "bulkhead", "engineer",
            "shinobi", "yumi", "shrine",
            "guardian", "raycaster", "aegis", "pyromancer",
            "seeker", "ward",
        };

        static readonly string[] AllMultiLegIds = { "stinger", "crawler", "hive" };

        /// <summary>Dumps rig components (body + limb/leg segments) for every rigged unit in
        /// the game, player side only, so a review page can show the whole roster's live
        /// motion without opening the editor. Run via -executeMethod
        /// NW.Editor.TroopArtExporter.ExportAllRigs -exportDir <path></summary>
        [MenuItem("NW/Export ALL Rigs")]
        public static void ExportAllRigs()
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);

            foreach (string id in AllRiggedIds)
                ExportOneRig(id, true, dir);
            foreach (string id in AllMultiLegIds)
                ExportOneMultiLegRig(id, true, dir);
            WriteRigManifest(dir);
            Debug.Log($"[TroopArtExporter] Wrote {AllRiggedIds.Length} 2-leg rig(s) + {AllMultiLegIds.Length} multi-leg rig(s) to {dir}");
        }

        /// <summary>Joint pivots + stride amplitude for every rigged id, as JSON -- the same
        /// numbers DriveLimbRig/DriveMultiLegRig use in BattlefieldView, so a browser-side
        /// reproduction of the rig math positions each layer identically instead of guessing.</summary>
        static void WriteRigManifest(string dir)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n  \"bipeds\": {\n");
            for (int u = 0; u < AllRiggedIds.Length; u++)
            {
                string id = AllRiggedIds[u];
                NeonArt.RigSwingDeg(id, out float hipDeg, out float kneeDeg, out float armDeg);
                sb.Append($"    \"{id}\": {{ \"hipDeg\": {hipDeg}, \"kneeDeg\": {kneeDeg}, \"armDeg\": {armDeg}, \"joints\": [\n");
                for (int seg = 0; seg < NeonArt.LimbSegCount; seg++)
                {
                    var info = NeonArt.LimbInfo(id, seg);
                    sb.Append($"      {{ \"x\": {info.Joint.x}, \"y\": {info.Joint.y}, \"parent\": {info.Parent}, \"near\": {(info.Near ? "true" : "false")} }}{(seg < NeonArt.LimbSegCount - 1 ? "," : "")}\n");
                }
                sb.Append($"    ] }}{(u < AllRiggedIds.Length - 1 ? "," : "")}\n");
            }
            sb.Append("  },\n  \"multiLegs\": {\n");
            for (int u = 0; u < AllMultiLegIds.Length; u++)
            {
                string id = AllMultiLegIds[u];
                int n = NeonArt.MultiLegSegCount(id);
                sb.Append($"    \"{id}\": {{ \"hips\": [\n");
                for (int seg = 0; seg < n; seg++)
                {
                    var hip = NeonArt.MultiLegHip01(id, seg);
                    sb.Append($"      {{ \"x\": {hip.x}, \"y\": {hip.y} }}{(seg < n - 1 ? "," : "")}\n");
                }
                sb.Append($"    ] }}{(u < AllMultiLegIds.Length - 1 ? "," : "")}\n");
            }
            sb.Append("  }\n}\n");
            File.WriteAllText(Path.Combine(dir, "rig_manifest.json"), sb.ToString());
        }

        /// <summary>Diagnostic for the multi-leg rig (stinger, crawler, hive): dumps the
        /// torso-only body texture plus each leg-group segment, at rest, exactly as
        /// BattlefieldView receives them before rotating each one live. Run via
        /// -executeMethod NW.Editor.TroopArtExporter.ExportMultiLegRig -rigId stinger -exportDir <path></summary>
        [MenuItem("NW/Export Multi-Leg Rig")]
        public static void ExportMultiLegRig()
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);
            string id = GetArg("-rigId") ?? "stinger";
            bool isPlayer = GetArg("-player") != "0";
            ExportOneMultiLegRig(id, isPlayer, dir);
        }

        static void ExportOneMultiLegRig(string id, bool isPlayer, string dir)
        {
            string suffix = isPlayer ? "" : "_enemy";
            int prevTheme = GameSettings.ThemeIndex;
            GameSettings.ThemeIndex = 2; // Biopunk -- all three multi-leg ids live there
            try
            {
                if (!NeonArt.HasMultiLegRig(id))
                {
                    Debug.LogError($"[TroopArtExporter] '{id}' has no multi-leg rig.");
                    return;
                }
                File.WriteAllBytes(Path.Combine(dir, $"{id}{suffix}_mlegbody.png"),
                    NeonArt.UnitNoMultiLegs(id, isPlayer).EncodeToPNG());
                int n = NeonArt.MultiLegSegCount(id);
                for (int seg = 0; seg < n; seg++)
                    File.WriteAllBytes(Path.Combine(dir, $"{id}{suffix}_mlegLeg{seg}.png"),
                        NeonArt.UnitMultiLeg(id, isPlayer, seg).EncodeToPNG());
                Debug.Log($"[TroopArtExporter] Wrote multi-leg body + {n} leg(s) for '{id}' ({(isPlayer?"player":"enemy")}) to {dir}");
            }
            finally
            {
                GameSettings.ThemeIndex = prevTheme;
            }
        }

        /// <summary>Diagnostic for the rotor rig (currently only "cybdrone"): dumps the
        /// body-without-blades texture plus each hub's blade-pair texture. Run via
        /// -executeMethod NW.Editor.TroopArtExporter.ExportRotorRig -exportDir <path></summary>
        [MenuItem("NW/Export Cyber Rotor Rig (Probe)")]
        public static void ExportRotorRig()
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);
            const string id = "cybdrone";

            int prevTheme = GameSettings.ThemeIndex;
            GameSettings.ThemeIndex = 0; // Cyber
            try
            {
                if (!NeonArt.HasRotorRig(id))
                {
                    Debug.LogError($"[TroopArtExporter] '{id}' has no rotor rig.");
                    return;
                }
                File.WriteAllBytes(Path.Combine(dir, $"{id}_rotorbody.png"),
                    NeonArt.UnitNoRotor(id, true).EncodeToPNG());
                for (int side = 0; side < 2; side++)
                    File.WriteAllBytes(Path.Combine(dir, $"{id}_rotor{side}.png"),
                        NeonArt.UnitRotor(id, true, side).EncodeToPNG());
                Debug.Log($"[TroopArtExporter] Wrote rotor body + 2 blade texture(s) for '{id}' to {dir}");
            }
            finally
            {
                GameSettings.ThemeIndex = prevTheme;
            }
        }

        static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
