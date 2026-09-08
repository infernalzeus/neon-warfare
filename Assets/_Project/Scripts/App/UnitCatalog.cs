using System.Collections.Generic;
using NW.Combat.Domain;

namespace NW.App
{
    /// <summary>
    /// The single source of truth for unit statistics.
    ///
    /// These numbers used to live as local variables inside BattleSession.AddFallbackUnits,
    /// where nothing else could reach them. So two other systems kept their OWN copies:
    /// TroopStats (which feeds the troop info panel) and the level-select demo (which invented
    /// its own damage and march speed). All three had drifted, and every one of the nine units
    /// displayed at least one wrong number -- shield-bot was shown with 180 HP against mech's
    /// 220, when the real values are 250 and 200, so the info panel reversed which of the two
    /// is tougher.
    ///
    /// Everything now reads from here: the battle, the info panel and the demo.
    /// </summary>
    public static class UnitCatalog
    {
        static readonly Dictionary<string, UnitSpec> _specs = new Dictionary<string, UnitSpec>
        {
            ["drone"] = new UnitSpec {
                Id = "drone", Class = UnitClass.Assault,
                MaxHp = 30, Damage = 5, AttackCooldown = 0.9f,
                Speed = 4.5f, Range = 2, DirectorCost = 5,
            },
            ["trooper"] = new UnitSpec {
                Id = "trooper", Class = UnitClass.Assault,
                MaxHp = 80, Damage = 10, AttackCooldown = 1.1f,
                Speed = 4f, Range = 2, DirectorCost = 10, CanCapture = true,
            },
            ["sniper"] = new UnitSpec {
                Id = "sniper", Class = UnitClass.Assault,
                MaxHp = 45, Damage = 28, AttackCooldown = 2.5f,
                Speed = 3.5f, Range = 25, DirectorCost = 14,
            },
            ["mech"] = new UnitSpec {
                Id = "mech", Class = UnitClass.Heavy,
                MaxHp = 200, Damage = 20, AttackCooldown = 1.6f,
                Speed = 3, Range = 3, DirectorCost = 20,
            },
            ["shield-bot"] = new UnitSpec {
                Id = "shield-bot", Class = UnitClass.Heavy,
                MaxHp = 250, Damage = 6, AttackCooldown = 1.3f,
                Speed = 2.5f, Range = 2, DirectorCost = 18, CanCapture = true,
            },
            ["interceptor"] = new UnitSpec {
                Id = "interceptor", Class = UnitClass.Air,
                MaxHp = 50, Damage = 8, AttackCooldown = 0.8f,
                Speed = 6f, Range = 5,
                IsAir = true, TargetsAir = true, TargetsGround = true, DirectorCost = 12,
            },
            ["hacker"] = new UnitSpec {
                Id = "hacker", Class = UnitClass.Special,
                MaxHp = 60, Damage = 14, AttackCooldown = 1.8f,
                Speed = 5, Range = 14, DirectorCost = 16, CanCapture = true,
                SpawnAtPylon = true,
            },
            ["titan"] = new UnitSpec {
                Id = "titan", Class = UnitClass.Heavy,
                MaxHp = 500, Damage = 35, AttackCooldown = 2.0f,
                Speed = 1.5f, Range = 4, AoeRadius = 2f, DirectorCost = 40,
            },
            // Turret: player-only static emplacement — excluded from the enemy SpawnTable
            ["turret"] = new UnitSpec {
                Id = "turret", Class = UnitClass.Heavy,
                MaxHp = 180, Damage = 14, AttackCooldown = 1.4f,
                Speed = 0, Range = 20,
                TargetsAir = true, TargetsGround = true, DirectorCost = 0,
                SpawnAtPylon = true,
            },
        };

        /// <summary>The canonical spec, or null if the id is unknown.</summary>
        public static UnitSpec Get(string canonicalId)
            => canonicalId != null && _specs.TryGetValue(canonicalId, out var s) ? s : null;

        public static bool TryGet(string canonicalId, out UnitSpec spec)
            => _specs.TryGetValue(canonicalId ?? "", out spec);

        public static IEnumerable<string> Ids => _specs.Keys;

        /// <summary>Hits needed for `attacker` to kill `target`, using the real numbers.
        /// The demo used to normalise every duel to six hits regardless of damage.</summary>
        public static int HitsToKill(string attackerId, string targetId)
        {
            var a = Get(attackerId); var t = Get(targetId);
            if (a == null || t == null || a.Damage <= 0f) return 6;
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt(t.MaxHp / a.Damage));
        }
    }
}
