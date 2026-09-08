using System;
using System.Collections.Generic;
using UnityEngine;
using NW.Board.Domain;
using NW.Combat.Domain;

namespace NW.Data
{
    /// <summary>Resource amounts for a cost or payout. Index = GemKind.</summary>
    [Serializable]
    public struct CostBundle
    {
        public int energy, plasma, nano, quantum, data;

        public int Get(GemKind kind) => kind switch
        {
            GemKind.Energy => energy,
            GemKind.Plasma => plasma,
            GemKind.Nano => nano,
            GemKind.Quantum => quantum,
            GemKind.Data => data,
            _ => 0
        };
    }

    [CreateAssetMenu(menuName = "NW/Defs/Unit", fileName = "unit_")]
    public class UnitDef : ScriptableObject
    {
        [Header("Identity")]
        public string id = "";
        public string displayName = "";
        [TextArea] public string description = "";
        public UnitClass unitClass;

        [Header("Cost & pacing")]
        public CostBundle cost;
        public float compileCooldown = 3f;
        public int unlockMissionIndex;

        [Header("Combat")]
        public float maxHp = 100f;
        public float damage = 10f;
        public float attackCooldown = 1f;
        public float speed = 8f;
        public float range = 1.5f;
        public float aoeRadius;
        public bool isAir;
        public bool targetsAir;
        public bool targetsGround = true;
        public int directorCost = 10;

        [Header("View")]
        public Sprite portrait;
        public GameObject viewPrefab;

        /// <summary>Bridge to the engine-free combat domain.</summary>
        public UnitSpec ToSpec() => new()
        {
            Id = id,
            Class = unitClass,
            MaxHp = maxHp,
            Damage = damage,
            AttackCooldown = attackCooldown,
            Speed = speed,
            Range = range,
            AoeRadius = aoeRadius,
            IsAir = isAir,
            TargetsAir = targetsAir,
            TargetsGround = targetsGround,
            DirectorCost = directorCost,
        };
    }

    [CreateAssetMenu(menuName = "NW/Defs/Hero", fileName = "hero_")]
    public class HeroDef : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        [TextArea] public string fantasy = "";
        [TextArea] public string passiveDescription = "";
        [TextArea] public string activeDescription = "";
        public CostBundle activeCost;
        [TextArea] public string ultimateDescription = "";
        [Range(0f, 1f)] public float ultChargePerQuantumMatch = 0.04f;
        [Range(0f, 1f)] public float ultChargePerKill = 0.01f;
        public Sprite portrait;
        public GameObject viewPrefab;
    }

    public enum ProtocolRarity { Common, Rare, Exotic }

    [CreateAssetMenu(menuName = "NW/Defs/Protocol", fileName = "protocol_")]
    public class ProtocolDef : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        [TextArea] public string description = "";
        public ProtocolRarity rarity;
        // Effects are authored as composable entries; the first archetypes are
        // implemented in code keyed by effectKey, parameterized here (doc 07 §5).
        public string effectKey = "";
        public float magnitude = 1f;
    }

    [CreateAssetMenu(menuName = "NW/Defs/Mission", fileName = "mission_")]
    public class MissionDef : ScriptableObject
    {
        [Header("Identity")]
        public string id = "";
        public string displayName = "";
        [TextArea(2, 4)] public string briefing = "";
        public int sector = 1;
        public bool isElite;
        public bool isBoss;

        [Header("Cores")]
        public float playerCoreHp = 1000f;
        public float enemyCoreHp = 800f;

        [Header("Enemy director (doc 04 §1.4)")]
        public float baseBudgetPerSecond = 3f;
        public float rampPer90Seconds = 1f;
        public List<DirectorEntry> spawnTable = new();

        [Header("Rewards (doc 04 §2)")]
        public int credits = 100;
        public int dataFragmentsFirstClear = 25;
        public int blueprintShards;

        [Serializable]
        public struct DirectorEntry
        {
            public UnitDef unit;
            public float weight;
        }

        public DirectorConfig ToDirectorConfig(float difficultyMult)
        {
            var cfg = new DirectorConfig
            {
                BaseBudgetPerSecond = baseBudgetPerSecond,
                RampPer90Seconds = rampPer90Seconds,
                DifficultyMult = difficultyMult,
            };
            foreach (DirectorEntry e in spawnTable)
                if (e.unit != null)
                    cfg.SpawnTable.Add((e.unit.ToSpec(), e.weight));
            return cfg;
        }
    }

    /// <summary>Central content registry. One asset; id-keyed lookups; saves store ids.</summary>
    [CreateAssetMenu(menuName = "NW/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<UnitDef> units = new();
        public List<HeroDef> heroes = new();
        public List<ProtocolDef> protocols = new();
        public List<MissionDef> missions = new();

        private Dictionary<string, UnitDef> _unitById;

        public UnitDef GetUnit(string id)
        {
            _unitById ??= Build();
            return _unitById.TryGetValue(id, out UnitDef def) ? def : null;
        }

        private Dictionary<string, UnitDef> Build()
        {
            var map = new Dictionary<string, UnitDef>();
            foreach (UnitDef u in units)
                if (u != null && !string.IsNullOrEmpty(u.id))
                    map[u.id] = u;
            return map;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _unitById = null;
            var seen = new HashSet<string>();
            foreach (UnitDef u in units)
                if (u != null && !string.IsNullOrEmpty(u.id) && !seen.Add(u.id))
                    Debug.LogError($"[GameDatabase] Duplicate unit id '{u.id}'", this);
        }
#endif
    }
}
