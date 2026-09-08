using System.Collections.Generic;

namespace NW.Combat.Domain
{
    public enum UnitClass : byte { Assault, Heavy, Air, Special }

    public enum Team : byte { Player = 0, Enemy = 1 }

    /// <summary>Pylon/lane ownership. Neutral until captured (doc 11 §2).</summary>
    public enum Owner : byte { Neutral, Player, Enemy }

    /// <summary>
    /// Immutable unit stat block. Mirrors NW.Data.UnitDef but stays engine-free so
    /// the simulator and tests can run headless. Built from defs at battle load.
    /// </summary>
    public sealed class UnitSpec
    {
        public string Id = "";
        public UnitClass Class;
        public float MaxHp;
        public float Damage;          // per hit
        public float AttackCooldown;  // seconds between hits
        public float Speed;           // world units / second (0 = stationary turret)
        public float Range;           // attack reach
        public float AoeRadius;       // 0 = single target
        public bool IsAir;            // occupies the air lane
        public bool TargetsAir;       // can hit air units
        public bool TargetsGround = true;
        public bool CanCapture = false; // can contest / capture pylons (infantry-type only)
        public bool SpawnAtPylon = false; // if true, deploys to most-forward owned pylon (turret/hacker)
        public int DirectorCost = 10; // enemy-director budget price
    }

    public sealed class UnitState
    {
        public ulong Id;       // stable across the battle; never recycled (views key on this)
        public UnitSpec Spec = null!;
        public Team Team;
        public int Lane;       // 0..GroundLanes-1, or CombatSim.AirLane
        public float X;        // position along the lane (player core at 0, enemy at LaneLength)
        public float Hp;
        public float Cooldown; // time until next attack allowed
        public float Stun;     // seconds of remaining stun (EMP)
        public float Slow;     // seconds of remaining slow (Singularity shockwave)
        public bool Alive;
        public int TargetIndex = -1; // index into sim unit list, -1 = none
    }

    /// <summary>One capture node on a ground lane (doc 11 §2.1).</summary>
    public sealed class PylonState
    {
        public int Lane;        // ground lane 0..3
        public int Index;       // 0 = P1 (x=33), 1 = P2 (x=66)
        public float X;
        public Owner Owner = Owner.Neutral;
        public float Progress;  // −100 (enemy) .. +100 (player)
        public bool Contested;  // both teams in zone this tick (progress frozen)
    }

    public enum CombatEventType : byte
    {
        Spawned,            // Unit index, Team
        Hit,                // Attacker index, Target index, Amount; Flag = counter hit
        Died,               // Unit index, Team; Flag = elite (heavy class)
        CoreHit,            // Attacker index, Amount (Team = core owner)
        WaveTelegraph,      // Target = lane, Amount = telegraph seconds remaining at emit
        WaveSpawned,        // Amount = director burst size, Target = lane
        PylonCaptured,      // Target = pylon id (lane*2+index), Team = new owner
        LaneControlChanged, // Target = lane, Team = controller (Flag=false ⇒ control lost to neutral)
        SurgeChanged,       // Team, Flag = active
        ActChanged,         // Amount = act number (1..3)
        Finished            // Team = winner
    }

    public readonly struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly int Unit;
        public readonly int Target;
        public readonly float Amount;
        public readonly Team Team;
        public readonly bool Flag;

        public CombatEvent(CombatEventType type, int unit = -1, int target = -1,
            float amount = 0, Team team = Team.Player, bool flag = false)
        {
            Type = type; Unit = unit; Target = target; Amount = amount; Team = team; Flag = flag;
        }
    }

    /// <summary>Mission tuning consumed by the enemy director (doc 11 §3.3).</summary>
    public sealed class DirectorConfig
    {
        public float BaseBudgetPerSecond = 3f;
        public float RampPer90Seconds = 1f;     // budget_rate = base × (1 + t/90 × ramp)
        public float DifficultyMult = 1f;
        public float MaxBank = 90f;             // burst-saving cap
        public float WaveThreshold = 20f;       // bank level that triggers a telegraphed wave
        public float TelegraphSeconds = 2f;     // warning lead before the wave releases
        public List<(UnitSpec spec, float weight)> SpawnTable = new();
    }
}
