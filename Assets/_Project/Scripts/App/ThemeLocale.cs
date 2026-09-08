namespace NW.App
{
    /// <summary>
    /// Per-theme localised names for gem currencies and troop units.
    /// All gameplay logic stays on the canonical IDs (Energy, drone, etc.);
    /// this layer is display-only, applied in HUD, level select and cosmetics screen.
    /// </summary>
    public static class ThemeLocale
    {
        // ── currency names ────────────────────────────────────────────────────

        static readonly string[][] _gemNames =
        {
            // 0  CYBER BLUE
            new[] { "ENERGY",  "PLASMA",  "NANO",    "QUANTUM", "DATA"   },
            // 1  NEON PURPLE / Synthwave
            new[] { "SYNTH",   "ETHER",   "VOID",    "PULSE",   "STATIC" },
            // 2  ACID GREEN / Biopunk
            new[] { "BIO",     "TOXIN",   "NANO",    "SPORE",   "DATA"   },
            // 3  BLOOD RED / Medieval
            new[] { "WOOD",    "IRON",    "STONE",   "GOLD",    "RUNE"   },
            // 4  GHOST / Industrial
            new[] { "GOLD",    "MANGAN",  "COPPER",  "COBALT",  "SILICA" },
            // 5  SAKURA / Kawaii
            new[] { "STARS",   "HEARTS",  "PETALS",  "DREAMS",  "SPARKS" },
            // 6  SOLAR
            new[] { "SOLAR",   "PLASMA",  "ETHER",   "LIGHT",   "PRISM"  },
            // 7  DAWN
            new[] { "FLUX",    "CRYSTAL", "AURA",    "WAVE",    "SPARK"  },
        };

        // Three-char abbreviations shown in HUD currency sidebar header
        static readonly string[][] _gemAbbrev =
        {
            new[] { "NRG", "PLS", "NAN", "QNT", "DAT" },  // 0 Cyber
            new[] { "SYN", "ETH", "VID", "PLS", "STC" },  // 1 Purple
            new[] { "BIO", "TOX", "NAN", "SPR", "DAT" },  // 2 Green
            new[] { "WOD", "IRN", "STN", "GLD", "RUN" },  // 3 Medieval
            new[] { "GLD", "MNG", "CPR", "COB", "SLC" },  // 4 Ghost
            new[] { "STR", "HRT", "PTL", "DRM", "SPK" },  // 5 Sakura
            new[] { "SOL", "PLS", "ETH", "LGT", "PRM" },  // 6 Solar
            new[] { "FLX", "CRY", "AUR", "WAV", "SPK" },  // 7 Dawn
        };

        // Single-char used in unit cost rows
        static readonly string[][] _gemShort =
        {
            new[] { "E", "P", "N", "Q", "D" },  // 0 Cyber
            new[] { "S", "E", "V", "P", "X" },  // 1 Purple
            new[] { "B", "T", "N", "R", "D" },  // 2 Green
            new[] { "W", "I", "S", "G", "R" },  // 3 Medieval
            new[] { "G", "M", "C", "O", "X" },  // 4 Ghost
            new[] { "*", "♥", "✿", "☽", "✦" },  // 5 Sakura
            new[] { "S", "P", "E", "L", "R" },  // 6 Solar
            new[] { "F", "C", "A", "W", "K" },  // 7 Dawn
        };

        // Universal gem colors — fixed across all themes so players can read the board consistently
        static readonly UnityEngine.Color[] _gemColors =
        {
            new UnityEngine.Color(1.00f, 0.75f, 0.10f), // 0 – gold
            new UnityEngine.Color(0.90f, 0.10f, 0.80f), // 1 – magenta
            new UnityEngine.Color(0.10f, 0.90f, 0.40f), // 2 – green
            new UnityEngine.Color(0.10f, 0.70f, 1.00f), // 3 – cyan
            new UnityEngine.Color(0.85f, 0.85f, 0.90f), // 4 – silver
        };

        // ── troop names ───────────────────────────────────────────────────────

        // Format: [themeIndex][troopId] — themes 3/4 fully remap; 1/2/5/6/7 partially
        static readonly (string id, string name)[][] _troopNames =
        {
            // 0  Cyber
            // Cyber used to fall through to the engine's generic catalogue names (DRONE,
            // TROOPER, INTRCPTR...). Now that it has its own nine builders it gets its own
            // nine names -- machine-precise, all 7 characters or fewer so they fit a level
            // -select chip on one line, and none colliding with the other 63.
            new[]
            {
                ("drone",        "PROBE"),
                ("trooper",      "LANCER"),
                ("sniper",       "RAILGUN"),
                ("mech",         "STRIDER"),
                ("shield-bot",   "BASTION"),
                ("interceptor",  "TALON"),
                ("hacker",       "CIPHER"),
                ("titan",        "ATLAS"),
                ("turret",       "SENTRY"),
            },
            // 1  Neon Purple / Synthwave
            new[]
            {
                ("drone",        "HOLOBOT"),
                ("trooper",      "RACER"),
                ("turret",       "PYLON"),
                ("sniper",       "GHOST"),
                ("mech",         "CRUISER"),
                ("shield-bot",   "WARDEN"),
                ("interceptor",  "SPEEDER"),
                ("hacker",       "RUNNER"),
                ("titan",        "OBELISK"),
            },
            // 2  Acid Green / Biopunk
            new[]
            {
                // Aligned to the art ids. The old set clashed -- STINGER was the interceptor
                // while the sniper is the unit with the proboscis -- and the hyphenated names
                // clipped in the deploy cards.
                ("drone",        "SPORE"),
                ("trooper",      "MUTANT"),
                ("turret",       "POD"),
                ("sniper",       "STINGER"),
                ("mech",         "CRAWLER"),
                ("shield-bot",   "SHELL"),
                ("interceptor",  "SWARM"),
                ("hacker",       "BLOOM"),
                ("titan",        "HIVE"),
            },
            // 3  Medieval
            new[]
            {
                ("drone",        "PIGEON"),
                ("trooper",      "KNIGHT"),
                ("turret",       "SCORPIO"),
                ("sniper",       "ARCHER"),
                ("mech",         "SIEGE"),
                ("shield-bot",   "PALADIN"),
                ("interceptor",  "ROGUE"),
                ("hacker",       "WIZARD"),
                ("titan",        "GOLEM"),
            },
            // 4  Ghost / Industrial
            new[]
            {
                // Aligned to the art ids. CRAWLER belonged to the Biopunk mech, and the old
                // names described units that no longer exist.
                ("drone",        "RIVET"),
                ("trooper",      "WORKER"),
                ("turret",       "GATLING"),
                ("sniper",       "GUNNER"),
                ("mech",         "CRANE"),
                ("shield-bot",   "BULWARK"),
                ("interceptor",  "THOPTER"),
                ("hacker",       "SPARK"),
                ("titan",        "FURNACE"),      // was COLOSSUS -- clashed with the Solar titan
            },
            // 5  Sakura / Kawaii
            new[]
            {
                ("drone",        "WISP"),
                ("trooper",      "SHINOBI"),
                ("turret",       "TORII"),
                ("sniper",       "YUMI"),
                ("mech",         "TANUKI"),
                ("shield-bot",   "SHRINE"),
                ("interceptor",  "KITE"),
                ("hacker",       "ONMYOJI"),
                ("titan",        "KAMI"),
            },
            // 6  Solar
            new[]
            {
                // Names follow the art now. The old set predated the Solar models and
                // described units that no longer exist -- a "DAWN-SNIPER" in the Solar theme,
                // and hyphenated two-word names that clipped in the troop chips.
                ("drone",        "EMBER"),
                ("trooper",      "HERALD"),
                ("turret",       "MIRROR"),
                ("sniper",       "PRISM"),
                ("mech",         "FORGE"),
                ("shield-bot",   "AEGIS"),
                ("interceptor",  "PHOENIX"),
                ("hacker",       "BRAZIER"),
                ("titan",        "HELIOS"),
            },
            // 7  Dawn
            new[]
            {
                ("drone",        "SPRITE"),
                ("trooper",      "PILGRIM"),
                ("turret",       "BEACON"),
                ("sniper",       "SEEKER"),
                ("mech",         "CARAVAN"),
                ("shield-bot",   "WARD"),
                ("interceptor",  "GLIDER"),
                ("hacker",       "ORACLE"),
                ("titan",        "VIGIL"),
            },
        };

        // ── public accessors ──────────────────────────────────────────────────

        static int Theme => UnityEngine.Mathf.Clamp(GameSettings.ThemeIndex, 0, _gemNames.Length - 1);

        public static string[] GemNames  => _gemNames [Theme];
        public static string[] GemAbbrev => _gemAbbrev[Theme];
        public static string[] GemShort  => _gemShort [Theme];
        public static UnityEngine.Color[] GemColors => _gemColors;

        /// <summary>Themed display name for a gem kind by index (0-4).</summary>
        public static string GemName(int kind)
            => _gemNames[Theme][UnityEngine.Mathf.Clamp(kind, 0, 4)];

        /// <summary>Universal gem color — fixed across all themes for board readability.</summary>
        public static UnityEngine.Color GemColor(int kind)
            => _gemColors[UnityEngine.Mathf.Clamp(kind, 0, 4)];

        /// <summary>Themed display name for a troop by its canonical id.</summary>
        public static string TroopName(string canonicalId)
        {
            int t = Theme;
            if (t < _troopNames.Length && _troopNames[t] != null)
            {
                foreach (var (id, name) in _troopNames[t])
                    if (id == canonicalId) return name;
            }
            return canonicalId.Replace("-", " ").ToUpper();
        }

        /// <summary>
        /// Art id to use when rendering a troop in NeonArt.Unit().
        /// Returns the canonical id for most themes; Medieval overrides a few.
        /// </summary>
        public static string ArtId(string canonicalId)
        {
            return Theme switch
            {
                0 => canonicalId switch  // Cyber
                {
                    // Cyber is the theme the ORIGINAL sprites belong to, but it does not edit
                    // them. The canonical trooper/sniper/mech/titan/turret/drone/interceptor/
                    // hacker/shield-bot builders stay frozen as the fallback that every
                    // unmapped id in every theme renders as; Cyber gets its own nine on top,
                    // exactly like the other seven. Delete these nine lines and the game falls
                    // straight back to the untouched originals.
                    "drone"       => "cybdrone",
                    "trooper"     => "cybtrooper",
                    "sniper"      => "cybsniper",
                    "mech"        => "cybmech",
                    "shield-bot"  => "cybshield",
                    "interceptor" => "cybinter",
                    "hacker"      => "cybhacker",
                    "titan"       => "cybtitan",
                    "turret"      => "cybturret",
                    _             => canonicalId,
                },
                1 => canonicalId switch  // Synthwave
                {
                    // These are NEW art ids, not the base holobot/racer/speeder sprites.
                    // Those originals stay untouched as the fallback set every theme is
                    // measured against; Synthwave gets its own cosmetic on top.
                    "drone"       => "synbot",
                    "trooper"     => "synracer",
                    "sniper"      => "synlaser",
                    "mech"        => "syncruiser",
                    "shield-bot"  => "synbouncer",
                    "interceptor" => "synspeeder",
                    "hacker"      => "synkeytar",
                    "titan"       => "synobelisk",
                    "turret"      => "synpylon",
                    _             => canonicalId,
                },
                2 => canonicalId switch  // Biopunk
                {
                    "drone"       => "spore",
                    "trooper"     => "mutant",
                    "sniper"      => "stinger",
                    "mech"        => "crawler",
                    "shield-bot"  => "carapace",
                    "interceptor" => "swarm",
                    "hacker"      => "mycelium",
                    "titan"       => "hive",
                    "turret"      => "pod",
                    _             => canonicalId,
                },
                3 => canonicalId switch  // Medieval
                {
                    "drone"       => "pigeon",
                    "trooper"     => "knight",
                    "mech"        => "siege",
                    "hacker"      => "wizard",
                    "interceptor" => "rogue",
                    "titan"       => "golem",   // medieval titan has its own carved-stone art
                    "sniper"      => "archer",
                    "shield-bot"  => "paladin",
                    "turret"      => "ballista",
                    _             => canonicalId,
                },
                4 => canonicalId switch  // Industrial
                {
                    "drone"       => "rivetbot",
                    "trooper"     => "worker",
                    "sniper"      => "gunner",
                    "mech"        => "crane",
                    "shield-bot"  => "bulkhead",
                    "interceptor" => "ornithopter",
                    "hacker"      => "engineer",
                    "titan"       => "furnace",
                    "turret"      => "gatling",
                    _             => canonicalId,
                },
                5 => canonicalId switch  // Sakura
                {
                    "drone"       => "wisp",
                    "trooper"     => "shinobi",
                    "sniper"      => "yumi",
                    "mech"        => "tanuki",
                    "shield-bot"  => "shrine",
                    "interceptor" => "kite",
                    "hacker"      => "onmyoji",
                    "titan"       => "kami",
                    "turret"      => "torii",
                    _             => canonicalId,
                },
                6 => canonicalId switch  // Solar
                {
                    // Solar has its own nine builders -- cast bronze, radiate crowns, pteruges.
                    // (It briefly aliased the Medieval geometry; that was rejected and rebuilt.)
                    "drone"       => "ember",
                    "trooper"     => "guardian",
                    "sniper"      => "raycaster",
                    "mech"        => "forgewalker",
                    "hacker"      => "pyromancer",
                    "interceptor" => "phoenix",
                    "titan"       => "colossus",
                    "shield-bot"  => "aegis",
                    "turret"      => "heliostat",
                    _             => canonicalId,
                },
                7 => canonicalId switch  // Dawn
                {
                    "drone"       => "sprite",
                    "trooper"     => "wanderer",
                    "sniper"      => "seeker",
                    "mech"        => "caravan",
                    "shield-bot"  => "ward",
                    "interceptor" => "glider",
                    "hacker"      => "oracle",
                    "titan"       => "sentinel",
                    "turret"      => "beacon",
                    _             => canonicalId,
                },
                _ => canonicalId,
            };
        }
    }
}
