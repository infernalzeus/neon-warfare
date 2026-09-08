using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using NW.Combat.Domain;
using NW.Data;

namespace NW.Editor
{
    /// <summary>
    /// Generates/updates UnitDef assets + the GameDatabase from balance-seed.json.
    /// Re-runnable: existing assets are updated in place (object references such as
    /// portraits and prefabs survive regeneration).
    /// Menu: NW → Generate Database From Seed.
    /// </summary>
    public static class DatabaseGenerator
    {
        private const string SeedPath = "Assets/_Project/Data/balance-seed.json";
        private const string UnitDir = "Assets/_Project/Data/Units";
        private const string DbPath = "Assets/Resources/GameDatabase.asset";

        [Serializable] private class Seed { public SeedUnit[] units; }
        [Serializable] private class SeedUnit
        {
            public string id, name, cls;
            public int energy, plasma, nano, quantum, data, dirCost;
            public float cd, hp, dmg, atkCd, speed, range, aoe;
            public bool air, tAir;
        }

        [MenuItem("NW/Generate Database From Seed")]
        public static void Generate()
        {
            if (!File.Exists(SeedPath)) { Debug.LogError($"Seed not found: {SeedPath}"); return; }
            var seed = JsonUtility.FromJson<Seed>(File.ReadAllText(SeedPath));
            Directory.CreateDirectory(UnitDir);
            Directory.CreateDirectory("Assets/Resources");

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DbPath);
            }
            db.units.Clear();

            foreach (SeedUnit s in seed.units)
            {
                string assetPath = $"{UnitDir}/unit_{s.id}.asset";
                var def = AssetDatabase.LoadAssetAtPath<UnitDef>(assetPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<UnitDef>();
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.id = s.id;
                def.displayName = s.name;
                def.unitClass = (UnitClass)Enum.Parse(typeof(UnitClass), s.cls);
                def.cost = new CostBundle
                {
                    energy = s.energy, plasma = s.plasma, nano = s.nano,
                    quantum = s.quantum, data = s.data
                };
                def.compileCooldown = s.cd;
                def.maxHp = s.hp;
                def.damage = s.dmg;
                def.attackCooldown = s.atkCd;
                def.speed = s.speed;
                def.range = s.range;
                def.aoeRadius = s.aoe;
                def.isAir = s.air;
                def.targetsAir = s.tAir;
                def.directorCost = s.dirCost;
                EditorUtility.SetDirty(def);
                db.units.Add(def);
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NW] Database generated: {db.units.Count} units → {DbPath}");
        }
    }
}
