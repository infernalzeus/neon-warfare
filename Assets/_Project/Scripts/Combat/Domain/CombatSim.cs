using System.Collections.Generic;
using NW.Domain;

namespace NW.Combat.Domain
{
    /// <summary>
    /// Fixed-tick five-lane combat simulation (doc 11). Player units march +X from
    /// their core at x=0; enemy units march −X from x=LaneLength. 4 ground lanes
    /// each carry two capture pylons (x=33, x=66); holding a pylon advances spawn
    /// points, holding both pylons of a lane grants lane control (+10% speed),
    /// controlling 3+ ground lanes triggers SURGE (enemy core takes +15%).
    /// Class triangle: Assault > Special > Heavy > Assault (+35% on advantage).
    ///
    /// Engine-free and deterministic: seeded Rng, fixed TickDelta, ordered event
    /// list per tick. Views interpolate from UnitState and consume Events.
    /// </summary>
    public sealed class CombatSim
    {
        public const float TickDelta = 0.05f;
        public const int GroundLanes = 3;
        public const int AirLane = 3;
        public const int LaneCount = 4;
        public const float LaneLength = 100f;

        /// <summary>How far along its lane a unit materialises. Was 2 -- two percent of the
        /// lane, which put every spawn inside the core sprite, so a deploy effect played
        /// underneath the core and could not be seen at all. 8 clears the core and gives the
        /// view a spot to draw a visible deploy pad. Applied symmetrically to both sides, so
        /// the marching distance between the two armies is unchanged.</summary>
        public const float DeploySpawnX = 8f;
        public const float ClassTriangleBonus = 1.35f;
        public const int MaxActivePerTeam = 24;

        public const float CoreDefenseRange = 8f;  // units within this radius take core damage
        public const float CoreDamagePerSec = 50f; // core damage rate — kills intruders in 1-4s
        public const float LaneSpeedBonus = 1.10f;
        public const float SurgeDamageMult = 1.15f;
        public const float SlowMult = 0.7f;
        public const float Act2Time = 150f, Act3Time = 330f;

        public IReadOnlyList<UnitState> Units => _units;
        public IReadOnlyList<PylonState> Pylons => _pylons;
        /// <summary>Per ground lane: smoothed war-progress seam, 0..100 (presentation/score only).</summary>
        public float[] Frontline { get; } = { 50f, 50f, 50f, 50f };
        public float PlayerCoreHp { get; private set; }
        public float EnemyCoreHp { get; private set; }
        public float PlayerCoreMax { get; }
        public float EnemyCoreMax { get; }
        public bool Finished { get; private set; }
        public Team Winner { get; private set; }
        public float Time { get; private set; }
        /// <summary>Fixed-step index, incremented once per <see cref="Tick"/>. Ghost recording
        /// and playback both key off this so an action lands on the same step it was made.</summary>
        public int TickCount { get; private set; }
        public int Act { get; private set; } = 1;
        public Owner SurgeOwner { get; private set; } = Owner.Neutral;
        public int TelegraphLane { get; private set; } = -1;   // -1 = no pending wave
        public float TelegraphRemaining { get; private set; }
        public List<CombatEvent> Events { get; } = new(64);

        private readonly List<UnitState> _units = new(64);
        private readonly PylonState[] _pylons = new PylonState[GroundLanes * 2];
        private readonly Rng _rng;
        private readonly DirectorConfig _director;
        private float _directorBank;
        private ulong _nextId = 1;
        private readonly int[] _activeCount = new int[2];

        public CombatSim(float playerCoreHp, float enemyCoreHp, DirectorConfig director, Rng rng)
        {
            PlayerCoreHp = PlayerCoreMax = playerCoreHp;
            EnemyCoreHp = EnemyCoreMax = enemyCoreHp;
            _director = director;
            _rng = rng;
            for (int lane = 0; lane < GroundLanes; lane++)
                for (int i = 0; i < 2; i++)
                    _pylons[lane * 2 + i] = new PylonState
                    {
                        Lane = lane, Index = i, X = i == 0 ? LaneLength / 3f : LaneLength * 2f / 3f,
                    };
        }

        // ------------------------------------------------------------ spawning

        /// <summary>
        /// Deploy a unit into a lane. Air units route to the air lane regardless.
        /// Spawn X = the team's most forward owned pylon in that lane (doc 11 §3.2).
        /// Returns the unit's sim index, or -1 if the field is full.
        /// </summary>
        public int Spawn(UnitSpec spec, Team team, int lane = -1)
        {
            if (Finished || _activeCount[(int)team] >= MaxActivePerTeam) return -1;

            lane = spec.IsAir ? AirLane
                : lane is >= 0 and < GroundLanes ? lane
                : _rng.Next(GroundLanes);

            // Only turret/hacker (SpawnAtPylon) deploy from owned pylons.
            // All other units — including all enemy units — always spawn at their core.
            float sx;
            if (spec.SpawnAtPylon && team == Team.Player && lane < GroundLanes)
            {
                sx = PylonSpawnX(lane);
                if (sx <= 2f) return -1; // no owned pylon in this lane → block
            }
            else
            {
                sx = team == Team.Player ? DeploySpawnX : LaneLength - DeploySpawnX;
            }

            // Reuse a dead slot to keep the list flat; identity lives in Id, not index.
            UnitState u = null;
            int index = -1;
            for (int i = 0; i < _units.Count; i++)
                if (!_units[i].Alive) { u = _units[i]; index = i; break; }
            if (u == null)
            {
                u = new UnitState();
                index = _units.Count;
                _units.Add(u);
            }

            u.Id = _nextId++;
            u.Spec = spec;
            u.Team = team;
            u.Lane = lane;
            u.X = sx;
            u.Hp = spec.MaxHp;
            u.Cooldown = 0f;
            u.Stun = 0f;
            u.Slow = 0f;
            u.Alive = true;
            u.TargetIndex = -1;
            _activeCount[(int)team]++;
            Events.Add(new CombatEvent(CombatEventType.Spawned, unit: index, target: lane, team: team));
            return index;
        }

        // Returns the most-forward player-owned pylon position in the lane + 2f,
        // or 2f (player base) if no pylons are owned. Player-side only.
        private float PylonSpawnX(int lane)
        {
            float x = 2f;
            for (int i = 0; i < 2; i++)
            {
                PylonState p = _pylons[lane * 2 + i];
                if (p.Owner != Owner.Player) continue;
                float px = p.X + 2f;
                if (px > x) x = px;
            }
            return x;
        }

        // ---------------------------------------------------------------- tick

        /// <summary>Advance one fixed step. Call with accumulated real time externally.</summary>
        public void Tick()
        {
            if (Finished) return;
            Time += TickDelta;
            TickCount++;
            UpdateAct();
            RunDirector();

            for (int i = 0; i < _units.Count; i++)
            {
                UnitState u = _units[i];
                if (!u.Alive) continue;
                if (u.Cooldown > 0f) u.Cooldown -= TickDelta;
                if (u.Slow > 0f) u.Slow -= TickDelta;
                if (u.Stun > 0f) { u.Stun -= TickDelta; continue; }   // stunned: no move, no attack

                if (!TryGetTarget(i, u, out int targetIdx))
                {
                    MarchOrSiege(i, u);
                    continue;
                }

                u.TargetIndex = targetIdx;
                if (u.Cooldown <= 0f)
                {
                    Attack(i, u, targetIdx);
                    u.Cooldown = u.Spec.AttackCooldown;
                }
            }

            UpdatePylons();
            UpdateFrontlines();
            CoreDefense();
        }

        private void UpdateAct()
        {
            int act = Time >= Act3Time ? 3 : Time >= Act2Time ? 2 : 1;
            if (act != Act)
            {
                Act = act;
                Events.Add(new CombatEvent(CombatEventType.ActChanged, amount: act));
            }
        }

        private float EffectiveSpeed(UnitState u)
        {
            float s = u.Spec.Speed;
            if (u.Slow > 0f) s *= SlowMult;
            if (u.Lane < GroundLanes && LaneController(u.Lane) == (u.Team == Team.Player ? Owner.Player : Owner.Enemy))
                s *= LaneSpeedBonus;
            return s;
        }

        private void MarchOrSiege(int index, UnitState u)
        {
            float dir = u.Team == Team.Player ? 1f : -1f;
            float coreX = u.Team == Team.Player ? LaneLength : 0f;
            float distToCore = u.Team == Team.Player ? coreX - u.X : u.X - coreX;

            if (distToCore <= u.Spec.Range)
            {
                if (u.Cooldown <= 0f)
                {
                    DamageCore(index, u);
                    u.Cooldown = u.Spec.AttackCooldown;
                }
                return;
            }
            u.X += dir * EffectiveSpeed(u) * TickDelta;
        }

        private void DamageCore(int index, UnitState u)
        {
            Team victim = u.Team == Team.Player ? Team.Enemy : Team.Player;
            float dmg = u.Spec.Damage;
            // SURGE: the surging team strikes the exposed core harder (doc 11 §2.3).
            if (SurgeOwner == (u.Team == Team.Player ? Owner.Player : Owner.Enemy))
                dmg *= SurgeDamageMult;

            if (victim == Team.Enemy) EnemyCoreHp -= dmg;
            else PlayerCoreHp -= dmg;
            Events.Add(new CombatEvent(CombatEventType.CoreHit, unit: index, amount: dmg, team: victim));

            if (EnemyCoreHp <= 0f) Finish(Team.Player);
            else if (PlayerCoreHp <= 0f) Finish(Team.Enemy);
        }

        // Cores deal continuous damage to any unit (air or ground) that has breached the
        // defense perimeter. Applies both ways: player core kills enemy intruders,
        // enemy core kills player units that reach it.
        private void CoreDefense()
        {
            if (Finished) return;
            float dmg = CoreDamagePerSec * TickDelta;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState u = _units[i];
                if (!u.Alive) continue;
                bool inPlayerCoreZone = u.Team == Team.Enemy && u.X <= CoreDefenseRange;
                bool inEnemyCoreZone  = u.Team == Team.Player && (LaneLength - u.X) <= CoreDefenseRange;
                if (!inPlayerCoreZone && !inEnemyCoreZone) continue;
                u.Hp -= dmg;
                if (u.Hp <= 0f) Kill(i);
            }
        }

        /// <summary>Apply passive HP regen to the player core (capped at max).</summary>
        public void HealPlayerCore(float amount)
        {
            if (Finished) return;
            PlayerCoreHp = System.Math.Min(PlayerCoreMax, PlayerCoreHp + amount);
        }

        /// <summary>Incoming crossover attack from the opposing player in competitive mode.</summary>
        public void DamagePlayerCore(float amount)
        {
            if (Finished) return;
            PlayerCoreHp -= amount;
            Events.Add(new CombatEvent(CombatEventType.CoreHit, amount: amount, team: Team.Player));
            if (PlayerCoreHp <= 0f)
            {
                PlayerCoreHp = 0f;
                Finished = true;
                Winner = Team.Enemy;
                Events.Add(new CombatEvent(CombatEventType.Finished, team: Team.Enemy));
            }
        }

        // ------------------------------------------------------------ fighting

        private bool TryGetTarget(int selfIndex, UnitState u, out int targetIdx)
        {
            targetIdx = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                if (i == selfIndex) continue;
                UnitState other = _units[i];
                if (!other.Alive || other.Team == u.Team) continue;
                if (!CanHit(u.Spec, other)) continue;
                // Same-lane rule for ground; air units and anti-air shoot across lanes.
                if (!u.Spec.IsAir && !other.Spec.IsAir && other.Lane != u.Lane) continue;

                float dist = u.X > other.X ? u.X - other.X : other.X - u.X;
                if (dist <= u.Spec.Range && dist < bestDist)
                {
                    bestDist = dist;
                    targetIdx = i;
                }
            }
            return targetIdx >= 0;
        }

        private static bool CanHit(UnitSpec attacker, UnitState target) =>
            target.Spec.IsAir ? attacker.TargetsAir : attacker.TargetsGround;

        private void Attack(int attackerIdx, UnitState attacker, int targetIdx)
        {
            UnitState target = _units[targetIdx];
            if (attacker.Spec.AoeRadius > 0f)
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    UnitState other = _units[i];
                    if (!other.Alive || other.Team == attacker.Team) continue;
                    if (!CanHit(attacker.Spec, other)) continue;
                    float d = other.X > target.X ? other.X - target.X : target.X - other.X;
                    if (d <= attacker.Spec.AoeRadius)
                        DealDamage(attackerIdx, i, attacker.Spec, other.Spec);
                }
            }
            else
            {
                DealDamage(attackerIdx, targetIdx, attacker.Spec, target.Spec);
            }
        }

        private void DealDamage(int attackerIdx, int targetIdx, UnitSpec attacker, UnitSpec defender)
        {
            float mult = TriangleMult(attacker.Class, defender.Class);
            ApplyDamage(attackerIdx, targetIdx, attacker.Damage * mult, isCounter: mult > 1f);
        }

        private void ApplyDamage(int attackerIdx, int targetIdx, float dmg, bool isCounter = false)
        {
            UnitState target = _units[targetIdx];
            target.Hp -= dmg;
            Events.Add(new CombatEvent(CombatEventType.Hit, unit: attackerIdx, target: targetIdx,
                amount: dmg, flag: isCounter));
            if (target.Hp <= 0f)
                Kill(targetIdx);
        }

        private void Kill(int targetIdx)
        {
            UnitState target = _units[targetIdx];
            target.Alive = false;
            _activeCount[(int)target.Team]--;
            Events.Add(new CombatEvent(CombatEventType.Died, unit: targetIdx, team: target.Team,
                flag: target.Spec.Class == UnitClass.Heavy));
        }

        /// <summary>Assault > Special > Heavy > Assault. Air is outside the triangle.</summary>
        public static float TriangleMult(UnitClass attacker, UnitClass defender) =>
            (attacker == UnitClass.Assault && defender == UnitClass.Special) ||
            (attacker == UnitClass.Special && defender == UnitClass.Heavy) ||
            (attacker == UnitClass.Heavy && defender == UnitClass.Assault)
                ? ClassTriangleBonus : 1f;

        // ------------------------------------------------------ board crossover
        // Called by BattleSession when the Compiler fires into the war (doc 12 §4).

        /// <summary>Line Laser: damage every enemy ground unit in one lane.</summary>
        public void DamageLaneEnemies(int lane, float dmg)
        {
            if (Finished) return;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState u = _units[i];
                if (u.Alive && u.Team == Team.Enemy && u.Lane == lane)
                {
                    u.Hp -= dmg;
                    if (u.Hp <= 0f) Kill(i);
                }
            }
        }

        /// <summary>Orbital Strike (combo ×5): hit the highest-HP enemy.</summary>
        public void DamageStrongestEnemy(float dmg)
        {
            if (Finished) return;
            int best = -1;
            float bestHp = 0f;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState u = _units[i];
                if (u.Alive && u.Team == Team.Enemy && u.Hp > bestHp) { bestHp = u.Hp; best = i; }
            }
            if (best < 0) return;
            _units[best].Hp -= dmg;
            if (_units[best].Hp <= 0f) Kill(best);
        }

        /// <summary>EMP (combo ×8 / double Singularity): stun every enemy.</summary>
        public void StunEnemies(float seconds)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Alive && _units[i].Team == Team.Enemy && _units[i].Stun < seconds)
                    _units[i].Stun = seconds;
        }

        /// <summary>Singularity shockwave: slow every enemy.</summary>
        public void SlowEnemies(float seconds)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Alive && _units[i].Team == Team.Enemy && _units[i].Slow < seconds)
                    _units[i].Slow = seconds;
        }

        /// <summary>Ground lane currently holding the most enemies (vertical laser target).</summary>
        public int BusiestEnemyLane()
        {
            int best = 0, bestCount = -1;
            for (int lane = 0; lane < GroundLanes; lane++)
            {
                int count = 0;
                for (int i = 0; i < _units.Count; i++)
                    if (_units[i].Alive && _units[i].Team == Team.Enemy && _units[i].Lane == lane) count++;
                if (count > bestCount) { bestCount = count; best = lane; }
            }
            return best;
        }

        // ------------------------------------------------------------- pylons

        public Owner LaneController(int lane)
        {
            Owner a = _pylons[lane * 2].Owner, b = _pylons[lane * 2 + 1].Owner;
            return a == b && a != Owner.Neutral ? a : Owner.Neutral;
        }

        /// <summary>True if the player owns at least one pylon in a ground lane (turret placement gate).</summary>
        public bool PlayerOwnsAnyPylon(int lane) =>
            lane >= 0 && lane < GroundLanes &&
            (_pylons[lane * 2].Owner == Owner.Player || _pylons[lane * 2 + 1].Owner == Owner.Player);

        public int ControlledLanes(Owner owner)
        {
            int n = 0;
            for (int lane = 0; lane < GroundLanes; lane++)
                if (LaneController(lane) == owner) n++;
            return n;
        }

        private void UpdatePylons()
        {
            for (int p = 0; p < _pylons.Length; p++)
            {
                PylonState pylon = _pylons[p];

                // Ownership is crossing-based: any alive unit that has moved past a pylon's
                // X threshold claims it for their team. No standing/waiting required.
                bool player = false, enemy = false;
                for (int i = 0; i < _units.Count; i++)
                {
                    UnitState u = _units[i];
                    if (!u.Alive || u.Lane != pylon.Lane || u.Spec.IsAir) continue;
                    if (u.Team == Team.Player && u.X >= pylon.X) player = true;
                    if (u.Team == Team.Enemy  && u.X <= pylon.X) enemy  = true;
                    if (player && enemy) break;
                }

                pylon.Contested = player && enemy;

                // Determine new owner: clear winner takes it; contested or empty → keep current.
                Owner newOwner;
                if (player && !enemy)       newOwner = Owner.Player;
                else if (enemy && !player)  newOwner = Owner.Enemy;
                else                        newOwner = pylon.Owner;

                // Mirror progress for the view's ring fill (100 = player, -100 = enemy, 0 = neutral).
                pylon.Progress = newOwner == Owner.Player ?  100f
                               : newOwner == Owner.Enemy  ? -100f : 0f;

                if (newOwner != pylon.Owner)
                {
                    Owner before = LaneController(pylon.Lane);
                    pylon.Owner = newOwner;
                    Events.Add(new CombatEvent(CombatEventType.PylonCaptured, target: p,
                        team: newOwner == Owner.Player ? Team.Player : Team.Enemy));
                    Owner after = LaneController(pylon.Lane);
                    if (after != before)
                        Events.Add(new CombatEvent(CombatEventType.LaneControlChanged,
                            target: pylon.Lane,
                            team: after == Owner.Player ? Team.Player : Team.Enemy,
                            flag: after != Owner.Neutral));
                    UpdateSurge();
                }
            }
        }

        private void UpdateSurge()
        {
            Owner surge = ControlledLanes(Owner.Player) >= 2 ? Owner.Player
                        : ControlledLanes(Owner.Enemy) >= 2 ? Owner.Enemy
                        : Owner.Neutral;
            if (surge == SurgeOwner) return;
            if (SurgeOwner != Owner.Neutral)
                Events.Add(new CombatEvent(CombatEventType.SurgeChanged,
                    team: SurgeOwner == Owner.Player ? Team.Player : Team.Enemy, flag: false));
            SurgeOwner = surge;
            if (surge != Owner.Neutral)
                Events.Add(new CombatEvent(CombatEventType.SurgeChanged,
                    team: surge == Owner.Player ? Team.Player : Team.Enemy, flag: true));
        }

        private void UpdateFrontlines()
        {
            for (int lane = 0; lane < GroundLanes; lane++)
            {
                float maxPlayer = float.MinValue, minEnemy = float.MaxValue;
                for (int i = 0; i < _units.Count; i++)
                {
                    UnitState u = _units[i];
                    if (!u.Alive || u.Lane != lane) continue;
                    if (u.Team == Team.Player) { if (u.X > maxPlayer) maxPlayer = u.X; }
                    else { if (u.X < minEnemy) minEnemy = u.X; }
                }

                float target;
                if (maxPlayer > float.MinValue && minEnemy < float.MaxValue)
                    target = (maxPlayer + minEnemy) * 0.5f;
                else if (maxPlayer > float.MinValue) target = maxPlayer;
                else if (minEnemy < float.MaxValue) target = minEnemy;
                else continue;   // empty lane: seam holds its ground

                if (target < 5f) target = 5f;
                if (target > 95f) target = 95f;
                float t = 2f * TickDelta;
                Frontline[lane] += (target - Frontline[lane]) * (t > 1f ? 1f : t);
            }
        }

        // ------------------------------------------------------------ director

        /// <summary>
        /// Budget-driven, lane-aware enemy spawner (doc 11 §3.3). Banks credits;
        /// at WaveThreshold it telegraphs a lane for TelegraphSeconds, then bursts
        /// its bank into that lane. 60% it defends its weakest frontline, 40% it
        /// reinforces its strongest push. Acts scale the budget rate ×1/1.6/2.2.
        /// </summary>
        private void RunDirector()
        {
            if (_director.SpawnTable.Count == 0) return;
            float actMult = Act == 3 ? 2.2f : Act == 2 ? 1.6f : 1f;
            float rate = _director.BaseBudgetPerSecond
                       * (1f + Time / 90f * _director.RampPer90Seconds)
                       * _director.DifficultyMult * actMult;
            _directorBank += rate * TickDelta;
            if (_directorBank > _director.MaxBank) _directorBank = _director.MaxBank;

            if (TelegraphLane < 0)
            {
                if (_directorBank < _director.WaveThreshold) return;
                TelegraphLane = PickWaveLane();
                TelegraphRemaining = _director.TelegraphSeconds;
                Events.Add(new CombatEvent(CombatEventType.WaveTelegraph, target: TelegraphLane,
                    amount: TelegraphRemaining, team: Team.Enemy));
                return;
            }

            TelegraphRemaining -= TickDelta;
            if (TelegraphRemaining > 0f) return;

            int lane = TelegraphLane;
            TelegraphLane = -1;
            int spawned = 0;
            while (true)
            {
                UnitSpec pick = WeightedPick();
                if (pick == null || pick.DirectorCost > _directorBank) break;
                if (Spawn(pick, Team.Enemy, lane) < 0) break;
                _directorBank -= pick.DirectorCost;
                spawned++;
            }
            if (spawned > 0)
                Events.Add(new CombatEvent(CombatEventType.WaveSpawned, amount: spawned,
                    target: lane, team: Team.Enemy));
        }

        private int PickWaveLane()
        {
            // Defend (60%): lane where the player has pushed deepest (highest frontline).
            // Snowball (40%): lane where the enemy push is deepest (lowest frontline).
            bool defend = _rng.Next01() < 0.6f;
            int best = 0;
            float bestVal = defend ? float.MinValue : float.MaxValue;
            for (int lane = 0; lane < GroundLanes; lane++)
            {
                float f = Frontline[lane];
                if (defend ? f > bestVal : f < bestVal) { bestVal = f; best = lane; }
            }
            return best;
        }

        private UnitSpec WeightedPick()
        {
            float total = 0f;
            foreach (var (_, weight) in _director.SpawnTable) total += weight;
            if (total <= 0f) return null;
            float roll = _rng.Next01() * total;
            foreach (var (spec, weight) in _director.SpawnTable)
            {
                roll -= weight;
                if (roll <= 0f) return spec;
            }
            return _director.SpawnTable[_director.SpawnTable.Count - 1].spec;
        }

        private void Finish(Team winner)
        {
            Finished = true;
            Winner = winner;
            Events.Add(new CombatEvent(CombatEventType.Finished, team: winner));
        }
    }
}
