namespace NW.App
{
    /// <summary>
    /// Reference stats for each troop type, used for the Level Select info panel.
    /// Values are representative of the default UnitDef balance; update when tuning changes.
    /// </summary>
    public static class TroopStats
    {
        public struct Info
        {
            public float  MaxHp;
            public float  Speed;
            public float  AttackCooldown;  // seconds; APS = 1 / AttackCooldown
            public float  Range;
            public string AttackType;      // short badge label
            public string AttackAnim;      // what physically animates during the attack
            public string Description;     // flavor / tactical summary
        }

        static readonly System.Collections.Generic.Dictionary<string, Info> _data =
            new System.Collections.Generic.Dictionary<string, Info>
        {
            ["drone"] = new Info {
                MaxHp = 60f, Speed = 14f, AttackCooldown = 1.2f, Range = 8f,
                AttackType  = "RANGED",
                AttackAnim  = "Whole body dive-bombs toward target, detonates on impact, recoils upward.",
                Description = "Fast aerial unit. Drops from above to strafe ground targets with its undercarriage stinger cannon. Fragile but hard to pin down."
            },
            ["trooper"] = new Info {
                MaxHp = 100f, Speed = 8f, AttackCooldown = 1.0f, Range = 1.5f,
                AttackType  = "MELEE / RANGED",
                AttackAnim  = "Melee: fist arm lunges forward into enemy. Ranged: rifle raises, barrel flash on fire.",
                Description = "Standard infantry. Versatile and cost-efficient — can brawl up close or lay down suppressive fire. The backbone of any frontline push."
            },
            ["sniper"] = new Info {
                MaxHp = 70f, Speed = 6f, AttackCooldown = 2.5f, Range = 20f,
                AttackType  = "LONG RANGE",
                AttackAnim  = "Scope glow intensifies before firing; barrel recoils sharply backward on shot.",
                Description = "Long-range specialist. Kneels to stabilize devastating shots that punch through multiple targets. Low mobility — position carefully."
            },
            ["mech"] = new Info {
                MaxHp = 220f, Speed = 5f, AttackCooldown = 1.8f, Range = 10f,
                AttackType  = "HEAVY RANGED",
                AttackAnim  = "Cannon arm rotates toward target, extends outward, then fires with a recoil burst.",
                Description = "Heavy bipedal war machine with articulated legs and a rotating shoulder cannon. Slow but nearly impossible to push back once entrenched."
            },
            ["shield-bot"] = new Info {
                MaxHp = 180f, Speed = 6f, AttackCooldown = 1.4f, Range = 2f,
                AttackType  = "MELEE / DEFLECT",
                AttackAnim  = "Melee: hex shield plate slams forward. Ranged: deflects projectile, counter-fires with right arm.",
                Description = "Defensive front-liner. Hexagonal energy grid on its shield absorbs incoming rounds. Stored energy fuels a staggering shield bash."
            },
            ["interceptor"] = new Info {
                MaxHp = 80f, Speed = 18f, AttackCooldown = 0.8f, Range = 12f,
                AttackType  = "AIR STRAFE",
                AttackAnim  = "Banks and tilts entire delta-wing frame, sweeps diagonally across lane in a strafing pass.",
                Description = "High-speed air superiority unit. Swept delta wings allow sharp banking for diagonal strafing runs. Hard to hit, hits hard."
            },
            ["hacker"] = new Info {
                MaxHp = 90f, Speed = 7f, AttackCooldown = 1.6f, Range = 15f,
                AttackType  = "SPECIAL — TENDRILS",
                AttackAnim  = "Data tendrils extend outward in an upward fan and latch onto the target, pulsing energy drain.",
                Description = "Cyber-warfare unit. Cloaked in a hooded shell, it deploys data-stream tendrils that disrupt targeting systems and drain enemy HP over time."
            },
            ["titan"] = new Info {
                MaxHp = 500f, Speed = 3f, AttackCooldown = 2.2f, Range = 5f,
                AttackType  = "MELEE + DUAL CANNON",
                AttackAnim  = "Melee: heavy arm arcs in a wide swing. Ranged: both shoulder cannons fire simultaneously with massive recoil.",
                Description = "Apex heavy-assault unit. Near-impenetrable armor plating and dual shoulder cannons that fire simultaneously. Every step shakes the ground."
            },
            ["turret"] = new Info {
                MaxHp = 150f, Speed = 0f, AttackCooldown = 1.0f, Range = 18f,
                AttackType  = "FIXED — LONG RANGE",
                AttackAnim  = "Barrel assembly rotates to track the target, then fires with visible recoil.",
                Description = "Static gun emplacement mounted on a tripod. Cannot move, but its stable platform enables accurate rapid-fire at extreme range."
            },
        };

        public static bool TryGet(string id, out Info info)
        {
            // The prose (attack type, animation note, description) stays here; every NUMBER
            // now comes from UnitCatalog, which is what the battle actually runs on. This
            // table used to carry its own values and all nine units disagreed with combat --
            // shield-bot was listed at 180 HP against mech's 220 when the real figures are
            // 250 and 200, so the panel reversed which of the two is tougher.
            bool have = _data.TryGetValue(id, out info);
            var spec = UnitCatalog.Get(id);
            if (spec != null)
            {
                info.MaxHp          = spec.MaxHp;
                info.Speed          = spec.Speed;
                info.AttackCooldown = spec.AttackCooldown;
                info.Range          = spec.Range;
            }
            return have || spec != null;
        }

        public static float APS(Info info) => info.AttackCooldown > 0f ? 1f / info.AttackCooldown : 0f;

        public static string RangeLabel(float range)
        {
            if (range <= 2f)  return "Melee";
            if (range <= 5f)  return "Short";
            if (range <= 12f) return "Mid";
            if (range <= 18f) return "Long";
            return "Extreme";
        }
    }
}
