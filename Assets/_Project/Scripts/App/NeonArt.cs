using System.Collections.Generic;
using UnityEngine;
using NW.Board.Domain;

namespace NW.App
{
    /// <summary>
    /// Procedural Texture2D factory — neon-war gem icons, unit silhouettes, and glow
    /// maps generated at runtime via pixel-painting. All textures are cached.
    /// </summary>
    public static class NeonArt
    {
        const int S = 64;

        static readonly Dictionary<string, Texture2D> _cache = new();

        // ── public API ───────────────────────────────────────────────────────

        /// <param name="style">0=electric 1=prismatic 2=toxic 3=ember 4=chrome — from NeonTheme.Active.GemStyle</param>
        /// <summary>The armory token: a hex coin with a bevelled rim and a cut core. Drawn once
        /// and animated by TokenIconAnim in the UI, so the shop's currency reads as a THING rather
        /// than a word. `phase` cuts the shine at a different angle so a few frames can be cycled.</summary>
        /// <summary>A soft round dot with a radial falloff, as a Sprite so any UI Image can
        /// wear it. Every particle in the game was an untextured Image -- a hard rectangle --
        /// which is why trails and deploy effects read as blocks of colour rather than light.
        /// One shared sprite turns all of them into glowing points.</summary>
        static Sprite _softDot;
        public static Sprite SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int D = 64;
            var px = new Color[D * D];
            float m = D / 2f;
            for (int y = 0; y < D; y++)
                for (int x = 0; x < D; x++)
                {
                    float dx = (x + 0.5f - m) / m, dy = (y + 0.5f - m) / m;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) { px[y * D + x] = new Color(1f, 1f, 1f, 0f); continue; }
                    // bright core, quick falloff, long soft tail -- reads as a light, not a disc
                    float a = Mathf.Pow(1f - d, 2.2f);
                    a = Mathf.Clamp01(a + Mathf.Pow(1f - d, 8f) * 0.85f);
                    px[y * D + x] = new Color(1f, 1f, 1f, a);
                }
            var tex = new Texture2D(D, D, TextureFormat.RGBA32, false)
                      { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            _softDot = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f));
            return _softDot;
        }

        public static Texture2D TokenIcon(int phase = 0)
            => Get($"tokenicon_{phase}_t{ArtThemeIdx}", () => BuildTokenIcon(phase));

        static Texture2D BuildTokenIcon(int phase)
        {
            var px = Blank();
            float m = S / 2f;

            // SHARD CLUSTER — three gold shards locked around a blue spark.
            // Deliberately NOT a coin and not a hexagon: every gem on the match board is a
            // faceted solid, so a currency that shares that language competes with them. A
            // ring of separate pieces around a light reads as its own thing at any size.
            var goldHi = new Color(1.00f, 0.94f, 0.74f);
            var gold   = new Color(0.88f, 0.68f, 0.16f);
            var goldDk = new Color(0.36f, 0.25f, 0.05f);
            var sparkC = new Color(0.43f, 0.80f, 1.00f);

            // 3 shards repeat every 120 degrees, so the 8 phases step 15 degrees each --
            // a full visual cycle without ever repeating a frame.
            float rot = phase * 15f;

            for (int i = 0; i < 3; i++)
            {
                float a  = (i * 120f + rot) * Mathf.Deg2Rad;
                float cx = m + Mathf.Cos(a) * 13f;
                float cy = m + Mathf.Sin(a) * 13f;

                // a wedge pointing back toward the centre
                float b = a + Mathf.PI;
                var p0 = new Vector2(cx + Mathf.Cos(b) * 11f,        cy + Mathf.Sin(b) * 11f);
                var p1 = new Vector2(cx + Mathf.Cos(b + 2.3f) * 9.5f, cy + Mathf.Sin(b + 2.3f) * 9.5f);
                var p2 = new Vector2(cx + Mathf.Cos(b - 2.3f) * 9.5f, cy + Mathf.Sin(b - 2.3f) * 9.5f);

                // the face light depends on which way the shard is turned, so the cluster
                // glints as it rotates instead of sliding a highlight across a flat disc
                float lit = 0.55f + 0.45f * Mathf.Cos(a - 2.2f);
                var face = new Color(Mathf.Lerp(gold.r, goldHi.r, lit),
                                     Mathf.Lerp(gold.g, goldHi.g, lit),
                                     Mathf.Lerp(gold.b, goldHi.b, lit));

                FillPoly(px, new[] { p0, p1, p2 }, face);
                DrawLine(px, p0.x, p0.y, p1.x, p1.y, 1.8f, goldDk);
                DrawLine(px, p1.x, p1.y, p2.x, p2.y, 1.8f, goldDk);
                DrawLine(px, p2.x, p2.y, p0.x, p0.y, 1.8f, goldDk);
                // inner facet crease
                DrawLine(px, p0.x, p0.y, (p1.x + p2.x) * 0.5f, (p1.y + p2.y) * 0.5f, 1.2f,
                         new Color(goldHi.r, goldHi.g, goldHi.b, 0.55f));
            }

            // the spark at the centre, breathing on the same 8-frame cycle
            float pulse = 1f + Mathf.Sin(phase / 8f * Mathf.PI * 2f) * 0.16f;
            for (int r = 5; r >= 1; r--)
            {
                float rr = r * 2.6f * pulse;
                float al = 0.16f + (5 - r) * 0.17f;
                FillCircle(px, m, m, rr, new Color(sparkC.r, sparkC.g, sparkC.b, al));
            }
            FillCircle(px, m, m, 3.4f * pulse, new Color(0.95f, 0.99f, 1f));
            return Make(px);
        }

        public static Texture2D Gem(int kind, int style = 0)
            => Get($"gem{kind}_{style}", () => BuildGem(kind, style));

        // pose: 0 neutral stance · 1 stride (legs apart) · 2 pass (legs together, foot lifted)
        public static Texture2D Unit(string id, bool player, int pose = 0)
            => Get($"unit3d_{id}_{(player ? 1 : 0)}_p{pose}_t{ArtThemeIdx}", () => BuildUnit3D(id, player, 0, pose));

        // Component-rig layers: body without weapon / weapon only, same 128 canvas so
        // a child RawImage overlays 1:1 and can recoil/aim independently.
        public static Texture2D UnitBody(string id, bool player, int pose = 0)
            => Get($"unit3d_{id}_{(player ? 1 : 0)}_b_p{pose}_t{ArtThemeIdx}", () => BuildUnit3D(id, player, 1, pose));

        public static Texture2D UnitWeapon(string id, bool player)
            => Get($"unit3d_{id}_{(player ? 1 : 0)}_w_t{ArtThemeIdx}", () => BuildUnit3D(id, player, 2));

        /// <summary>
        /// True when this art id is drawn from AUTHORED ART (gradient plates + livery) rather
        /// than lit primitives. These textures already colour themselves: steel stays steel and
        /// only cape/plume/heraldry take the team colour. The view layer must NOT multiply them
        /// by the full team tint or the livery system is flattened back into a solid red/blue
        /// silhouette -- which is exactly the "enemy knight is fully red" bug.
        /// </summary>
        public static bool IsAuthoredArt(string id)
            => id is "knight" or "wizard" or "golem" or "siege" or "rogue"
                  or "pigeon" or "archer" or "paladin" or "ballista"
                  or "guardian" or "ember" or "raycaster" or "forgewalker" or "aegis"
                  or "phoenix" or "pyromancer" or "colossus" or "heliostat"
                  or "spore" or "mutant" or "stinger" or "crawler" or "carapace"
                  or "swarm" or "mycelium" or "hive" or "pod"
                  or "rivetbot" or "gunner" or "bulkhead" or "crane" or "furnace"
                  or "ornithopter" or "engineer" or "gatling"
                  or "sprite" or "seeker" or "ward" or "caravan" or "glider"
                  or "oracle" or "sentinel" or "beacon"
                  or "wisp" or "shinobi" or "yumi" or "shrine" or "tanuki"
                  or "kite" or "onmyoji" or "kami" or "torii"
                  // Synthwave -- extruded chrome
                  or "synbot" or "synracer" or "synlaser" or "syncruiser" or "synbouncer"
                  or "synspeeder" or "synkeytar" or "synobelisk" or "synpylon"
                  // Cyber -- cold composite. NEW ids: the canonical sprites stay frozen
                  or "cybdrone" or "cybtrooper" or "cybsniper" or "cybmech" or "cybshield"
                  or "cybinter" or "cybhacker" or "cybtitan" or "cybturret"
                  or "worker" or "wanderer";

        /// <summary>True when this art id has a separate weapon layer.</summary>
        public static bool HasWeaponPart(string id)
            => id is "mech" or "siege" or "turret" or "titan";

        /// <summary>True when this art id has walk-cycle pose frames (legged walkers).</summary>
        public static bool HasPoses(string id)
            => id is "trooper" or "racer" or "mutant" or "guardian"
                  or "knight" or "worker" or "shield-bot" or "sniper"
                  or "archer" or "paladin" or "ballista"
                  or "mech" or "siege" or "titan"
                  or "drone" or "pigeon" or "sprite"          // rotor blur
                  or "interceptor" or "speeder" or "rogue"    // engine pulse
                  or "hacker" or "wizard" or "wanderer"       // cloak sway
                  or "golem"                                  // stone stride
                  // Solar Forge -- Medieval geometry under different paint
                  or "guardian" or "ember" or "raycaster" or "forgewalker" or "aegis"
                  or "phoenix" or "pyromancer" or "colossus" or "heliostat"
                  // Biopunk -- grown
                  or "spore" or "mutant" or "stinger" or "crawler" or "carapace"
                  or "swarm" or "mycelium" or "hive" or "pod"
                  // Industrial -- stamped
                  or "rivetbot" or "gunner" or "bulkhead" or "crane" or "furnace"
                  or "ornithopter" or "engineer" or "gatling"
                  // Dawn -- woven
                  or "sprite" or "seeker" or "ward" or "caravan" or "glider"
                  or "oracle" or "sentinel" or "beacon"
                  // Sakura -- folded
                  or "wisp" or "shinobi" or "yumi" or "shrine" or "tanuki"
                  or "kite" or "onmyoji" or "kami" or "torii"
                  // Synthwave -- extruded chrome
                  or "synbot" or "synracer" or "synlaser" or "syncruiser" or "synbouncer"
                  or "synspeeder" or "synkeytar" or "synobelisk" or "synpylon"
                  // Cyber -- cold composite. NEW ids: the canonical sprites stay frozen
                  or "cybdrone" or "cybtrooper" or "cybsniper" or "cybmech" or "cybshield"
                  or "cybinter" or "cybhacker" or "cybtitan" or "cybturret";

        /// <summary>True when this art id has dedicated attack pose frames (windup/strike).</summary>
        public static bool HasAttackPoses(string id)
            => UsesSideRig(id)
            || id is "mech" or "siege" or "titan"
                  or "drone" or "pigeon" or "sprite"
                  or "interceptor" or "speeder" or "rogue"
                  or "hacker" or "wizard" or "wanderer"
                  or "golem" or "turret" or "ballista"
                  or "ember" or "forgewalker" or "phoenix" or "pyromancer"
                  or "colossus" or "heliostat"
                  or "spore" or "crawler" or "swarm" or "mycelium" or "hive" or "pod"
                  or "rivetbot" or "crane" or "furnace" or "ornithopter" or "engineer" or "gatling"
                  or "sprite" or "caravan" or "glider" or "oracle" or "sentinel" or "beacon"
                  or "wisp" or "tanuki" or "kite" or "onmyoji" or "kami" or "torii"
                  // Synthwave -- extruded chrome
                  or "synbot" or "synracer" or "synlaser" or "syncruiser" or "synbouncer"
                  or "synspeeder" or "synkeytar" or "synobelisk" or "synpylon"
                  // Cyber -- cold composite. NEW ids: the canonical sprites stay frozen
                  or "cybdrone" or "cybtrooper" or "cybsniper" or "cybmech" or "cybshield"
                  or "cybinter" or "cybhacker" or "cybtitan" or "cybturret";


        // ══════════════════════════════════════════════════════════════════════
        //  LIMB RIG  —  continuous motion instead of baked stills
        //
        //  A baked sprite is a STILL. Four of them played in sequence is a slideshow:
        //  the thigh is at one angle, then another, with nothing in between. That is
        //  the disconnection -- no number of extra baked phases removes it, because
        //  the mechanic itself is discrete.
        //
        //  So the walk stops being baked. The torso is baked once with NO limbs, each
        //  limb is baked once hanging straight down from its own joint, and the view
        //  rotates them every frame from a continuous phase. This is exactly how the
        //  weapon layer already works (part 2 + localRotation), extended to limbs.
        //
        //  Every limb texture is a FULL 128x128 canvas aligned 1:1 with the body, so
        //  the layers stack with zero offset and the pivot alone places the joint.
        //  Shins parent to thighs, so a knee bend composes with the hip swing.
        //
        //  Attacks are untouched: windup, strike and flinch stay baked, because those
        //  poses are authored per unit and read better hand-placed than interpolated.
        // ══════════════════════════════════════════════════════════════════════

        public struct LimbSeg
        {
            public Vector2 Joint;   // pivot, in 0..1 of the canvas
            public int     Parent;  // -1 = attach to the unit root, else index of the parent segment
            public bool    Near;    // near-side limbs draw in FRONT of the body
        }

        /// <summary>Segment order: 0 far thigh, 1 far shin, 2 near thigh, 3 near shin,
        /// 4 far arm, 5 near arm.</summary>
        public const int LimbSegCount = 6;

        struct RigDef
        {
            public Vector2 HipF, HipN, ShF, ShN;   // joints in 128-space
            public float   Thigh, Shin, Arm;       // neutral segment lengths
            public float   RThighF, RShinF, RThighN, RShinN, RArmF, RArmN;
            public Color   LegF, LegN, ArmF, ArmN;
        }

        static readonly System.Collections.Generic.Dictionary<string, RigDef> _rigs =
            new System.Collections.Generic.Dictionary<string, RigDef>
        {
            // Kami — joints read straight off the builder so the rigged figure lands
            // exactly where the baked one did.
            { "kami", new RigDef {
                HipF = new Vector2(58f, 44f), HipN = new Vector2(70f, 44f),
                ShF  = new Vector2(50f, 92f), ShN  = new Vector2(78f, 92f),
                Thigh = 20f, Shin = 18f, Arm = 18f,
                RThighF = 9.5f, RShinF = 8f, RThighN = 11f, RShinN = 9.2f,
                RArmF = 10f, RArmN = 11.5f,
                LegF = new Color(0.34f, 0.13f, 0.12f), LegN = new Color(0.55f, 0.22f, 0.20f),
                ArmF = new Color(0.50f, 0.20f, 0.18f), ArmN = new Color(0.60f, 0.24f, 0.22f) } },
        };

        /// <summary>True when this art id renders as a body plus rotatable limb layers.</summary>
        public static bool HasLimbRig(string id) => _rigs.ContainsKey(id);

        /// <summary>Where each segment pivots, and what it hangs from.</summary>
        public static LimbSeg LimbInfo(string id, int seg)
        {
            if (!_rigs.TryGetValue(id, out var r)) return default;
            switch (seg)
            {
                case 0: return new LimbSeg { Joint = r.HipF / 128f, Parent = -1, Near = false };
                case 1: return new LimbSeg { Joint = new Vector2(r.HipF.x, r.HipF.y - r.Thigh) / 128f,
                                             Parent = 0, Near = false };
                case 2: return new LimbSeg { Joint = r.HipN / 128f, Parent = -1, Near = true };
                case 3: return new LimbSeg { Joint = new Vector2(r.HipN.x, r.HipN.y - r.Thigh) / 128f,
                                             Parent = 2, Near = true };
                case 4: return new LimbSeg { Joint = r.ShF / 128f, Parent = -1, Near = false };
                default:return new LimbSeg { Joint = r.ShN / 128f, Parent = -1, Near = true };
            }
        }

        /// <summary>The body with every rigged limb removed.</summary>
        public static Texture2D UnitNoLimbs(string id, bool player)
            => Get($"rig_{id}_{(player ? 1 : 0)}_body_t{ArtThemeIdx}",
                   () => BuildUnit3D(id, player, 3, 0));

        /// <summary>One limb segment, hanging straight down from its joint.</summary>
        public static Texture2D UnitLimb(string id, bool player, int seg)
            => Get($"rig_{id}_{(player ? 1 : 0)}_l{seg}_t{ArtThemeIdx}",
                   () => BuildLimbSeg(id, player, seg));

        static Texture2D BuildLimbSeg(string id, bool player, int seg)
        {
            const int R = 128;
            var px = new Color[R * R];
            if (!_rigs.TryGetValue(id, out var r)) return MakeUnitTex(px, R);

            Color tint = player ? new Color(0.78f, 0.93f, 1.00f) : new Color(1.00f, 0.65f, 0.52f);
            var L     = new Vector3(-0.45f, 0.70f, 0.55f).normalized;
            var H     = (L + Vector3.forward).normalized;
            var fillL = new Vector3(0.38f, 0.08f, 0.50f).normalized;
            var p     = MakeUnitP3DP(tint, 0.32f, 0.55f);

            Vector2 a, b; float rad; Color col;
            switch (seg)
            {
                case 0: a = r.HipF; b = a - new Vector2(0f, r.Thigh); rad = r.RThighF; col = r.LegF; break;
                case 1: a = r.HipF - new Vector2(0f, r.Thigh); b = a - new Vector2(0f, r.Shin);
                        rad = r.RShinF; col = r.LegF; break;
                case 2: a = r.HipN; b = a - new Vector2(0f, r.Thigh); rad = r.RThighN; col = r.LegN; break;
                case 3: a = r.HipN - new Vector2(0f, r.Thigh); b = a - new Vector2(0f, r.Shin);
                        rad = r.RShinN; col = r.LegN; break;
                case 4: a = r.ShF; b = a - new Vector2(0f, r.Arm); rad = r.RArmF; col = r.ArmF; break;
                default:a = r.ShN; b = a - new Vector2(0f, r.Arm); rad = r.RArmN; col = r.ArmN; break;
            }
            P3DLimb(px, R, a.x, a.y, b.x, b.y, rad, col, 0.2f, 0.3f, L, H, fillL);

            // shins carry the foot, arms carry the hand
            Vector2 V(float x, float y) => new Vector2(x, y);
            if (seg == 1 || seg == 3)
            {
                float w = seg == 3 ? 12f : 10f, h = seg == 3 ? 8f : 7f;
                var top = new Color(SakPaperDk.r * 0.62f, SakPaperDk.g * 0.62f, SakPaperDk.b * 0.62f);
                var bot = new Color(SakPaperDk.r * 0.42f, SakPaperDk.g * 0.42f, SakPaperDk.b * 0.42f);
                P3DPlate(px, R, new[]{ V(b.x - w, b.y + h), V(b.x + w, b.y + h),
                                       V(b.x + w * 0.78f, b.y - h * 0.85f), V(b.x - w * 0.78f, b.y - h * 0.85f) },
                         top, bot, 2f);
            }
            else if (seg >= 4)
            {
                FillCircleR(px, R, b.x, b.y - 4f, seg == 5 ? 8.2f : 7.4f,
                            seg == 5 ? new Color(0.62f, 0.26f, 0.24f) : new Color(0.55f, 0.22f, 0.20f));
            }
            return MakeUnitTex(px, R);
        }

        public static Texture2D Glow(Color c)
            => Get($"glow_{CK(c)}", () => BuildGlow(c));

        public static Texture2D EmptyCell(int skin = 0)
            => Get($"empty_{skin}", () => BuildEmpty(skin));

        /// <summary>Border-only texture for special gem tiles (transparent center, colored frame).</summary>
        public static Texture2D SpecialBorder(SpecialKind special)
            => Get($"sbrd{(int)special}", () => BuildSpecialBorder(special));

        // ── gem palettes ─────────────────────────────────────────────────────

        static readonly Color[] GP =
        {
            new Color(1.00f, 0.75f, 0.10f), // 0 Energy  – gold
            new Color(0.90f, 0.10f, 0.80f), // 1 Plasma  – magenta
            new Color(0.10f, 0.90f, 0.40f), // 2 Nano    – green
            new Color(0.10f, 0.70f, 1.00f), // 3 Quantum – cyan
            new Color(0.80f, 0.85f, 0.95f), // 4 Data    – silver
        };

        // ── gem builder ──────────────────────────────────────────────────────

        static Texture2D BuildGem(int k, int style = 0)
        {
            var px = Blank();
            Color c = GP[k];
            float m = S / 2f;

            // GEM HUE IS GAMEPLAY DATA, NOT DECORATION.
            // Style used to reach into the colour: chrome (style 4) lerped 60% toward grey and
            // then overwrote the lit faces with near-white, and every style mixed 52-70% white
            // into the facets on top of that. On Industrial the result was five pastel tokens
            // that were genuinely hard to tell apart -- and you match by colour, so that is a
            // legibility failure, not a look. Style now changes FACET TREATMENT only; the base
            // hue is untouchable and the white mixing is cut so the hue dominates.
            Color dark   = Mix(c, Color.black, 0.62f);  // shadow border + dark faces
            Color shadow = Mix(c, Color.black, 0.34f);  // base fill
            Color mid    = Mix(c, Color.black, 0.10f);  // mid face
            Color bright = Mix(c, Color.white, 0.30f);  // lit face   (was 0.52)
            Color rim    = Mix(c, Color.white, 0.46f);  // rim        (was 0.70)

            if (style == 4)
            {
                // chrome now reads as a harder, cooler cut -- same hue, sharper contrast
                rim    = Mix(c, new Color(0.92f, 0.95f, 1f), 0.42f);
                bright = Mix(c, new Color(0.86f, 0.90f, 0.98f), 0.26f);
                dark   = Mix(c, new Color(0.10f, 0.13f, 0.20f), 0.62f);
            }
            if (style == 3)
            {
                // ember warms the highlight without bleaching the body
                rim    = Mix(rim,    new Color(1f, 0.90f, 0.55f), 0.30f);
                bright = Mix(bright, new Color(1f, 0.82f, 0.45f), 0.26f);
            }

            // Light direction: upper-left (x decreasing, y increasing in texture = top-left on screen)
            var lightDir = new Vector2(-0.707f, 0.707f);

            // ── Layer 0: Outer atmosphere glow ────────────────────────────────────
            switch (style)
            {
                case 2: Glow2D(px, m, m, 30f, A(new Color(0.15f, 0.85f, 0.10f), 0.33f)); break;
                case 3: Glow2D(px, m, m, 33f, A(new Color(1.00f, 0.42f, 0.05f), 0.35f)); break;
                case 4: Glow2D(px, m, m, 26f, A(new Color(0.50f, 0.58f, 0.70f), 0.26f)); break;
                default: Glow2D(px, m, m, 30f, A(c, 0.42f)); break;
            }

            // ── Layers 1–4: Shape-specific crystal body ──────────────────────────
            switch (k)
            {
                case 0: // Energy — gold hexagon crystal
                {
                    // L1: Border (dark 2px wider)
                    FillPoly(px, Hex(m, m, 24f), A(dark, 0.95f));

                    // L2-3: Faceted body — 6 triangular faces shaded by light direction
                    var verts = Hex(m, m, 22f);
                    var ctr   = new Vector2(m, m);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 v0 = verts[i], v1 = verts[(i + 1) % 6];
                        float dot = Vector2.Dot(((v0 + v1) * 0.5f - ctr).normalized, lightDir);
                        float b   = dot * 0.5f + 0.5f;
                        Color face = b > 0.5f ? Mix(mid, bright, (b - 0.5f) * 2f) : Mix(dark, mid, b * 2f);
                        FillPoly(px, new[] { ctr, v0, v1 }, A(face, 0.94f));
                    }

                    // Inner bevel (smaller hex adds depth)
                    var inner = Hex(m, m, 13f);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 v0 = inner[i], v1 = inner[(i + 1) % 6];
                        float dot = Vector2.Dot(((v0 + v1) * 0.5f - ctr).normalized, lightDir);
                        float b   = dot * 0.5f + 0.5f;
                        FillPoly(px, new[] { ctr, v0, v1 }, A(Mix(shadow, bright, b * 0.62f), 0.52f));
                    }

                    // L4a: Refraction streaks (internal crystal-light scatter)
                    DrawLine(px, m - 15f, m - 17f, m +  9f, m + 13f, 1.2f, A(Color.white, 0.20f));
                    DrawLine(px, m -  6f, m - 20f, m + 14f, m +  6f, 0.8f, A(Color.white, 0.13f));

                    // L4b: Rim light on upper-left facing edges
                    DrawLine(px, verts[1].x, verts[1].y, verts[2].x, verts[2].y, 2.4f, A(rim, 0.80f));
                    DrawLine(px, verts[2].x, verts[2].y, verts[3].x, verts[3].y, 2.2f, A(rim, 0.65f));
                    DrawLine(px, verts[4].x, verts[4].y, verts[5].x, verts[5].y, 1.4f, A(rim, 0.25f));
                    break;
                }

                case 1: // Plasma — magenta 4-pointed star crystal
                {
                    // L1: Border
                    FillStar(px, m, m, 25f, 10.5f, 4, A(dark, 0.92f));

                    // L2: Dark base star
                    FillStar(px, m, m, 23f, 9f, 4, A(shadow, 0.96f));

                    // L3: Lit upper-left face — offset inner star creates facet feel
                    FillStar(px, m - 2f, m + 2f, 18f, 6.5f, 4, A(bright, 0.32f));
                    FillStar(px, m,      m,       14f, 5.5f, 4, A(bright, 0.72f)); // inner core

                    // L4a: Refraction — axial lines through arms
                    DrawLine(px, m, m - 21f, m, m + 21f, 1.0f, A(Color.white, 0.18f));
                    DrawLine(px, m - 21f, m, m + 21f, m, 1.0f, A(Color.white, 0.18f));

                    // L4b: Rim light on upper and left arm tips (upper-left region)
                    // Top arm edges
                    DrawLine(px, m - 4f, m - 23f, m + 4f, m - 15f, 2.2f, A(rim, 0.75f));
                    // Left arm edges
                    DrawLine(px, m - 23f, m - 4f, m - 15f, m + 4f, 2.0f, A(rim, 0.68f));
                    // Subtle on top-right
                    DrawLine(px, m + 4f, m - 23f, m + 15f, m - 4f, 1.5f, A(rim, 0.30f));
                    break;
                }

                case 2: // Nano — green equilateral triangle crystal
                {
                    var tv  = Tri(m, m, 23f);
                    var ctr = new Vector2(m, m);

                    // L1: Border
                    FillPoly(px, Tri(m, m, 25f), A(dark, 0.92f));

                    // L2-3: Faceted body — 3 triangular faces
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 v0 = tv[i], v1 = tv[(i + 1) % 3];
                        float dot = Vector2.Dot(((v0 + v1) * 0.5f - ctr).normalized, lightDir);
                        float b   = dot * 0.5f + 0.5f;
                        Color face = b > 0.5f ? Mix(mid, bright, (b - 0.5f) * 2f) : Mix(dark, mid, b * 2f);
                        FillPoly(px, new[] { ctr, v0, v1 }, A(face, 0.92f));
                    }

                    // Inner bevel
                    var ti = Tri(m, m, 13f);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 v0 = ti[i], v1 = ti[(i + 1) % 3];
                        float dot = Vector2.Dot(((v0 + v1) * 0.5f - ctr).normalized, lightDir);
                        float b   = dot * 0.5f + 0.5f;
                        FillPoly(px, new[] { ctr, v0, v1 }, A(Mix(shadow, bright, b * 0.58f), 0.48f));
                    }

                    // Circuit nodes at vertices (preserved, brightened)
                    foreach (var v in Tri(m, m, 20f))
                    {
                        FillCircle(px, v.x, v.y, 3.8f, A(bright, 0.92f));
                        Glow2D(px, v.x, v.y, 6f, A(c, 0.48f));
                    }

                    // L4a: Refraction
                    DrawLine(px, m - 13f, m - 11f, m + 9f, m + 15f, 1.0f, A(Color.white, 0.18f));

                    // L4b: Rim light — top edge (brightest, facing light) and left edge
                    // tv[2]=left-pt, tv[1]=right-pt both at top; tv[0]=bottom
                    DrawLine(px, tv[2].x, tv[2].y, tv[1].x, tv[1].y, 2.4f, A(rim, 0.80f)); // top edge
                    DrawLine(px, tv[2].x, tv[2].y, tv[0].x, tv[0].y, 2.0f, A(rim, 0.55f)); // left edge
                    break;
                }

                case 3: // Quantum — cyan circle crystal + orbit ring
                {
                    // L1: Border
                    FillCircle(px, m, m, 20f, A(dark, 0.92f));

                    // L2-3: Faceted body — 16-segment pie with brightness by angle
                    FillCircle(px, m, m, 18f, A(shadow, 0.95f));
                    int segs = 16;
                    for (int i = 0; i < segs; i++)
                    {
                        float a0 = i * 2f * Mathf.PI / segs;
                        float a1 = (i + 1) * 2f * Mathf.PI / segs;
                        float am = (a0 + a1) * 0.5f;
                        float dot = Vector2.Dot(new Vector2(Mathf.Cos(am), Mathf.Sin(am)), lightDir);
                        float b   = dot * 0.5f + 0.5f;
                        Color face = b > 0.5f ? Mix(mid, bright, (b - 0.5f) * 2f) : Mix(dark, mid, b * 2f);
                        var v0 = new Vector2(m + Mathf.Cos(a0) * 18f, m + Mathf.Sin(a0) * 18f);
                        var v1 = new Vector2(m + Mathf.Cos(a1) * 18f, m + Mathf.Sin(a1) * 18f);
                        FillPoly(px, new[] { new Vector2(m, m), v0, v1 }, A(face, 0.78f));
                    }

                    // Inner bright circle for depth
                    FillCircle(px, m, m, 11f, A(bright, 0.48f));

                    // Orbit ring + electron
                    DrawRing(px, m, m, 27f, 1.8f, A(c, 0.72f));
                    FillCircle(px, m + 27f, m, 3.8f, A(bright, 0.92f));
                    Glow2D(px, m + 27f, m, 5f, A(c, 0.65f));

                    // L4a: Refraction radial lines
                    DrawLine(px, m - 2f, m + 2f, m - 15f, m + 13f, 1.0f, A(Color.white, 0.18f));
                    DrawLine(px, m + 2f, m + 2f, m + 11f, m + 13f, 0.8f, A(Color.white, 0.12f));

                    // L4b: Rim light arc (upper-left 180° — angles 45°..225° = PI/4..5*PI/4)
                    for (int seg = 0; seg < 8; seg++)
                    {
                        float a0 = Mathf.PI * 0.25f + seg * (Mathf.PI / 8f);
                        float a1 = a0 + Mathf.PI / 8f;
                        float am = (a0 + a1) * 0.5f;
                        var rp0 = new Vector2(m + Mathf.Cos(a0) * 18.8f, m + Mathf.Sin(a0) * 18.8f);
                        var rp1 = new Vector2(m + Mathf.Cos(a1) * 18.8f, m + Mathf.Sin(a1) * 18.8f);
                        float rimStr = 1f - Mathf.Abs(am - Mathf.PI * 0.75f) / (Mathf.PI * 0.5f);
                        DrawLine(px, rp0.x, rp0.y, rp1.x, rp1.y, 2.5f, A(rim, rimStr * 0.78f));
                    }
                    break;
                }

                case 4: // Data — silver chip crystal
                {
                    // L1: Border
                    FillRect(px, m - 19f, m - 19f, 38f, 38f, A(dark, 0.94f));

                    // L2: Base chip
                    FillRect(px, m - 17f, m - 17f, 34f, 34f, A(shadow, 0.96f));

                    // L3: Diagonal facet split (upper-left brighter, lower-right darker)
                    // Upper half (top-left + top-right + bottom-left triangle = upper-left quadrant)
                    FillPoly(px, new Vector2[]
                    {
                        new Vector2(m - 17f, m + 17f),
                        new Vector2(m + 17f, m + 17f),
                        new Vector2(m - 17f, m - 17f),
                    }, A(bright, 0.32f));
                    // Top strip (top-left, top-right, center)
                    FillPoly(px, new Vector2[]
                    {
                        new Vector2(m - 17f, m + 17f),
                        new Vector2(m + 17f, m + 17f),
                        new Vector2(m,       m),
                    }, A(Mix(mid, bright, 0.65f), 0.55f));

                    // Grid lines
                    Color gridCol = Mix(dark, shadow, 0.5f);
                    for (int i = 1; i < 4; i++)
                    {
                        float step = i * 8.5f;
                        DrawLine(px, m - 17f + step, m - 17f, m - 17f + step, m + 17f, 0.8f, A(gridCol, 0.65f));
                        DrawLine(px, m - 17f, m - 17f + step, m + 17f, m - 17f + step, 0.8f, A(gridCol, 0.65f));
                    }

                    // Inner bright sub-chip
                    FillRect(px, m - 9f, m - 9f, 18f, 18f, A(bright, 0.68f));

                    // Connector pins
                    for (int i = 0; i < 3; i++)
                    {
                        float o = -6f + i * 6f;
                        FillRect(px, m + 17f, m + o - 1f, 5f, 2f, A(mid, 0.92f));
                        FillRect(px, m - 22f, m + o - 1f, 5f, 2f, A(mid, 0.92f));
                        FillRect(px, m + o - 1f, m + 17f, 2f, 5f, A(mid, 0.92f));
                        FillRect(px, m + o - 1f, m - 22f, 2f, 5f, A(mid, 0.92f));
                    }

                    // L4a: Refraction diagonal
                    DrawLine(px, m - 14f, m + 14f, m + 14f, m - 14f, 1.2f, A(Color.white, 0.18f));

                    // L4b: Rim light on top + left edges (upper-left facing)
                    DrawLine(px, m - 17f, m + 17f, m + 17f, m + 17f, 2.4f, A(rim, 0.80f)); // top
                    DrawLine(px, m - 17f, m - 17f, m - 17f, m + 17f, 2.2f, A(rim, 0.68f)); // left
                    break;
                }
            }

            // ── Layer 5: Core glow ────────────────────────────────────────────────
            Glow2D(px, m, m, 9f, A(Color.white, 0.72f));

            // ── Layer 6: Style overlay + specular highlight ───────────────────────
            ApplyGemStyleOverlay(px, c, bright, m, style);
            FillCircle(px, m - 7f, m + 8f, 3.5f, A(Color.white, style == 4 ? 0.75f : 0.62f));
            return Make(px);
        }

        // Overlay effects that distinguish each visual style.
        static void ApplyGemStyleOverlay(Color[] px, Color c, Color hi, float m, int style)
        {
            switch (style)
            {
                case 1: // Prismatic — diagonal crystal facet cut lines + inner rotated hex
                    DrawLine(px, m - 18f, m + 16f, m + 10f, m - 16f, 1.0f, A(Color.white,              0.28f));
                    DrawLine(px, m -  8f, m + 20f, m + 18f, m - 12f, 0.8f, A(new Color(0.9f,0.8f,1f),  0.18f));
                    DrawLine(px, m - 18f, m -  6f, m + 14f, m + 18f, 0.8f, A(Color.white,              0.14f));
                    FillPoly(px, HexRotated(m, m, 11f, 15f), A(Color.white, 0.10f));
                    break;

                case 2: // Toxic — drip lines + bubble tips + outer acid ring
                    for (int d = 0; d < 3; d++)
                    {
                        float dx = m - 8f + d * 8f;
                        DrawLine(px, dx, m - 14f, dx, m - 24f - d * 2f, 1.5f, A(new Color(0.30f, 1f, 0.20f), 0.50f));
                        FillCircle(px, dx, m - 25f - d * 2f, 2.5f, A(new Color(0.35f, 1f, 0.25f), 0.65f));
                    }
                    DrawRing(px, m, m, 25f, 1f, A(new Color(0.15f, 0.85f, 0.10f), 0.22f));
                    break;

                case 3: // Ember — warm inner core bloom + spark dots at perimeter
                    Glow2D(px, m, m, 13f, A(new Color(1f, 0.95f, 0.55f), 0.34f));
                    for (int s = 0; s < 5; s++)
                    {
                        float ang = s * (Mathf.PI * 2f / 5f) + 0.2f;
                        float sr  = 21f + (s % 2 == 0 ? 3f : 0f);
                        FillCircle(px, m + Mathf.Cos(ang) * sr, m + Mathf.Sin(ang) * sr,
                            1.8f, A(new Color(1f, 0.65f, 0.15f), 0.68f));
                    }
                    break;

                case 4: // Chrome — horizontal specular shine lines
                    DrawLine(px, m - 18f, m + 9f, m + 16f, m + 9f, 1.8f, A(Color.white, 0.40f));
                    DrawLine(px, m - 14f, m + 3f, m + 12f, m + 3f, 1.2f, A(Color.white, 0.24f));
                    DrawLine(px, m - 10f, m - 3f, m +  8f, m - 3f, 0.8f, A(Color.white, 0.14f));
                    break;
            }
        }

        // ── special border ───────────────────────────────────────────────────

        static Texture2D BuildSpecialBorder(SpecialKind special)
        {
            var px = Blank();
            float m = S / 2f;

            // The four specials used to differ by BORDER COLOUR ONLY -- a 9px ring at 92% alpha
            // with faint 60%-alpha arrow marks buried inside it. The ring swallowed the marks, so
            // the player had to memorise four colours to know what a token would do.
            //
            // The behaviour now leads. Edge chevrons state direction and leave the gem body
            // visible, because colour is what you match on; the ring is demoted to a thin rim.
            // The set reads as one family: H is the mark, V is the same mark rotated, Cross is
            // both at once. Singularity is the odd one out on purpose -- rings collapsing INWARD
            // toward a white core, the only inward motion in the set, so it can never be mistaken
            // for a laser.
            Color col = special switch
            {
                SpecialKind.LaserH      => new Color(1f, 0.95f, 0.30f, 1f),
                SpecialKind.LaserV      => new Color(0.45f, 0.90f, 1f,  1f),
                SpecialKind.Cross       => new Color(1f, 0.60f, 0.22f, 1f),
                SpecialKind.Singularity => new Color(0.82f, 0.50f, 1f,  1f),
                _                       => Color.clear,
            };
            if (special == SpecialKind.None) return Make(px);

            // thin rim only -- enough to say "this token is special", not enough to hide it
            const int BW = 3;
            for (int py = 0; py < S; py++)
                for (int px2 = 0; px2 < S; px2++)
                {
                    int edge = System.Math.Min(System.Math.Min(px2, S-1-px2),
                                               System.Math.Min(py,  S-1-py));
                    if (edge >= BW) continue;
                    Set(px, px2, py, new Color(col.r, col.g, col.b, 0.85f - edge * 0.18f));
                }

            var ink = new Color(0.06f, 0.05f, 0.09f, 0.92f);

            // A chevron pointing outward from (bx,by) along (dx,dy). Drawn dark first and bright
            // over it, so the mark holds on a pale gold gem and a dark cobalt one alike.
            void Chevron(float bx, float by, float dx, float dy)
            {
                float px2 = -dy, py2 = dx;          // perpendicular
                float tipX = bx + dx * 7f,  tipY = by + dy * 7f;
                float aX   = bx + px2 * 8f, aY   = by + py2 * 8f;
                float bX   = bx - px2 * 8f, bY   = by - py2 * 8f;
                DrawLine(px, aX, aY, tipX, tipY, 7.5f, ink);
                DrawLine(px, bX, bY, tipX, tipY, 7.5f, ink);
                DrawLine(px, aX, aY, tipX, tipY, 4.2f, col);
                DrawLine(px, bX, bY, tipX, tipY, 4.2f, col);
                DrawLine(px, aX, aY, tipX, tipY, 1.6f, new Color(1f, 1f, 1f, 0.85f));
                DrawLine(px, bX, bY, tipX, tipY, 1.6f, new Color(1f, 1f, 1f, 0.85f));
            }

            switch (special)
            {
                case SpecialKind.LaserH:
                    Chevron(m - 15f, m, -1f, 0f);
                    Chevron(m + 15f, m,  1f, 0f);
                    DrawLine(px, m - 9f, m, m + 9f, m, 3.0f, A(col, 0.45f));
                    break;

                case SpecialKind.LaserV:
                    Chevron(m, m - 15f, 0f, -1f);
                    Chevron(m, m + 15f, 0f,  1f);
                    DrawLine(px, m, m - 9f, m, m + 9f, 3.0f, A(col, 0.45f));
                    break;

                case SpecialKind.Cross:
                    // literally the two laser marks combined -- row AND column, not a blast radius
                    Chevron(m - 16f, m, -1f, 0f);
                    Chevron(m + 16f, m,  1f, 0f);
                    Chevron(m, m - 16f, 0f, -1f);
                    Chevron(m, m + 16f, 0f,  1f);
                    DrawLine(px, m - 8f, m, m + 8f, m, 2.6f, A(col, 0.45f));
                    DrawLine(px, m, m - 8f, m, m + 8f, 2.6f, A(col, 0.45f));
                    break;

                case SpecialKind.Singularity:
                    Glow2D(px, m, m, 22f, new Color(0.10f, 0.02f, 0.16f, 0.45f));
                    DrawRing(px, m, m, 22f, 2.0f, A(col, 0.40f));
                    DrawRing(px, m, m, 16f, 2.6f, A(col, 0.62f));
                    DrawRing(px, m, m, 10f, 3.0f, A(col, 0.85f));
                    FillCircle(px, m, m, 5.0f, new Color(1f, 1f, 1f, 0.98f));
                    Glow2D(px, m, m, 12f, new Color(1f, 0.94f, 1f, 0.45f));
                    break;
            }
            return Make(px);
        }

        // ── empty cell ───────────────────────────────────────────────────────

        static Texture2D BuildEmpty(int skin = 0)
        {
            var px = Blank();
            switch (skin)
            {
                default:
                case 0: // Classic — dark blue flat with 1px border
                {
                    Color bg = new Color(0.07f, 0.08f, 0.13f, 0.95f);
                    Color bd = new Color(0.14f, 0.20f, 0.36f, 0.55f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    for (int x = 1; x < S - 1; x++) { Set(px, x, 1, bd); Set(px, x, S - 2, bd); }
                    for (int y = 1; y < S - 1; y++) { Set(px, 1, y, bd); Set(px, S - 2, y, bd); }
                    break;
                }
                case 1: // Crystal — dark blue with inner radial glow
                {
                    Color bg = new Color(0.04f, 0.06f, 0.14f, 0.97f);
                    Color bd = new Color(0.20f, 0.38f, 0.80f, 0.50f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    float m = S / 2f;
                    Glow2D(px, m, m, S * 0.38f, new Color(0.15f, 0.30f, 0.70f, 0.28f));
                    for (int x = 1; x < S - 1; x++) { Set(px, x, 1, bd); Set(px, x, S - 2, bd); }
                    for (int y = 1; y < S - 1; y++) { Set(px, 1, y, bd); Set(px, S - 2, y, bd); }
                    break;
                }
                case 2: // Pixel — 8-bit dark checkerboard
                {
                    Color a = new Color(0.06f, 0.07f, 0.12f, 0.95f);
                    Color b = new Color(0.10f, 0.11f, 0.18f, 0.95f);
                    Color bd = new Color(0.20f, 0.22f, 0.32f, 0.60f);
                    int block = S / 8;
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            Set(px, x, y, ((x / block + y / block) % 2 == 0) ? a : b);
                    for (int x = 0; x < S; x++) { Set(px, x, 0, bd); Set(px, x, S - 1, bd); }
                    for (int y = 0; y < S; y++) { Set(px, 0, y, bd); Set(px, S - 1, y, bd); }
                    break;
                }
                case 3: // Glitch — horizontal scanline bands with occasional bright artifact
                {
                    Color bg   = new Color(0.05f, 0.06f, 0.10f, 0.95f);
                    Color scan = new Color(0.12f, 0.14f, 0.22f, 0.70f);
                    Color art  = new Color(0.30f, 0.50f, 0.80f, 0.18f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    for (int y2 = 0; y2 < S; y2 += 4)
                        for (int x2 = 0; x2 < S; x2++) Set(px, x2, y2, scan);
                    int ga = S / 3, gb = 2 * S / 3;
                    for (int x2 = 0; x2 < S; x2++) { Set(px, x2, ga, art); Set(px, x2, gb, art); }
                    Color bd = new Color(0.22f, 0.28f, 0.50f, 0.45f);
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bd); Set(px, x2, S - 2, bd); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bd); Set(px, S - 2, y2, bd); }
                    break;
                }

                // ── theme-driven board skins (4–8) — active when no cosmetic override ──

                case 4: // Hex-grid — Cyber Blue / Solar / Dawn
                {
                    Color bg = new Color(0.04f, 0.06f, 0.10f, 0.97f);
                    Color ln = new Color(0.10f, 0.22f, 0.45f, 0.50f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    float hm = S / 2f;
                    foreach (float rr in new[] { S * 0.43f, S * 0.26f })
                    {
                        float lineA = rr == S * 0.43f ? 1.0f : 0.8f;
                        Color lnA   = A(ln, rr == S * 0.43f ? 0.5f : 0.28f);
                        for (int a = 0; a < 6; a++)
                        {
                            float ang0 = a       * Mathf.PI / 3f + Mathf.PI / 6f;
                            float ang1 = (a + 1) * Mathf.PI / 3f + Mathf.PI / 6f;
                            DrawLine(px, hm + Mathf.Cos(ang0) * rr, hm + Mathf.Sin(ang0) * rr,
                                         hm + Mathf.Cos(ang1) * rr, hm + Mathf.Sin(ang1) * rr, lineA, lnA);
                        }
                    }
                    Color bde = new Color(0.12f, 0.24f, 0.50f, 0.40f);
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bde); Set(px, x2, S - 2, bde); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bde); Set(px, S - 2, y2, bde); }
                    break;
                }

                case 5: // Scanline — Neon Purple / Sakura
                {
                    Color bg   = new Color(0.06f, 0.03f, 0.11f, 0.97f);
                    Color scan = new Color(0.14f, 0.08f, 0.26f, 0.65f);
                    Color bde  = new Color(0.40f, 0.15f, 0.70f, 0.40f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    for (int y2 = 0; y2 < S; y2 += 5)
                        for (int x2 = 0; x2 < S; x2++) Set(px, x2, y2, scan);
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bde); Set(px, x2, S - 2, bde); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bde); Set(px, S - 2, y2, bde); }
                    break;
                }

                case 6: // Organic membrane — Acid Green
                {
                    Color bg   = new Color(0.02f, 0.06f, 0.02f, 0.97f);
                    Color cell = new Color(0.06f, 0.18f, 0.06f, 0.55f);
                    Color bde  = new Color(0.10f, 0.50f, 0.12f, 0.45f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    DrawRing(px, S * 0.30f, S * 0.35f, S * 0.28f, 1.2f, cell);
                    DrawRing(px, S * 0.70f, S * 0.62f, S * 0.25f, 1.2f, cell);
                    DrawRing(px, S * 0.50f, S * 0.50f, S * 0.18f, 1.0f, A(cell, 0.38f));
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bde); Set(px, x2, S - 2, bde); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bde); Set(px, S - 2, y2, bde); }
                    break;
                }

                case 7: // Stone — Blood Red / Medieval
                {
                    Color bg     = new Color(0.10f, 0.03f, 0.03f, 0.97f);
                    Color mortar = new Color(0.05f, 0.02f, 0.02f, 0.72f);
                    Color bde    = new Color(0.50f, 0.10f, 0.08f, 0.45f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    int half = S / 2;
                    for (int x2 = 0; x2 < S; x2++) Set(px, x2, half, mortar);
                    for (int y2 = 0;    y2 < half; y2++) Set(px, S / 3,     y2, mortar);
                    for (int y2 = half; y2 < S;    y2++) Set(px, 2 * S / 3, y2, mortar);
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bde); Set(px, x2, S - 2, bde); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bde); Set(px, S - 2, y2, bde); }
                    break;
                }

                case 8: // Riveted steel — Ghost / Industrial
                {
                    Color bg    = new Color(0.08f, 0.09f, 0.11f, 0.97f);
                    Color seam  = new Color(0.12f, 0.14f, 0.18f, 0.40f);
                    Color rivet = new Color(0.35f, 0.40f, 0.50f, 0.70f);
                    Color bde   = new Color(0.30f, 0.35f, 0.45f, 0.45f);
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    int sm = S / 2;
                    for (int x2 = 0; x2 < S; x2++) Set(px, x2, sm, seam);
                    for (int y2 = 0; y2 < S; y2++) Set(px, sm, y2, seam);
                    float rv = 5f;
                    FillCircle(px, rv, rv, 2.5f, rivet);     FillCircle(px, S - rv, rv, 2.5f, rivet);
                    FillCircle(px, rv, S - rv, 2.5f, rivet); FillCircle(px, S - rv, S - rv, 2.5f, rivet);
                    FillCircle(px, sm, rv, 2f, rivet); FillCircle(px, sm, S - rv, 2f, rivet);
                    FillCircle(px, rv, sm, 2f, rivet); FillCircle(px, S - rv, sm, 2f, rivet);
                    for (int x2 = 1; x2 < S - 1; x2++) { Set(px, x2, 1, bde); Set(px, x2, S - 2, bde); }
                    for (int y2 = 1; y2 < S - 1; y2++) { Set(px, 1, y2, bde); Set(px, S - 2, y2, bde); }
                    break;
                }
            }
            return Make(px);
        }

        // ── unit builder ─────────────────────────────────────────────────────

        static Texture2D BuildUnit(string id, bool player)
        {
            var px = Blank();
            Color c  = player ? new Color(0f, 0.9f, 1f) : new Color(1f, 0.35f, 0.1f);
            Color hi = Mix(c, Color.white, 0.40f);
            Color dk = Mix(c, Color.black, 0.42f);
            float m  = S / 2f;
            float d  = player ? 1f : -1f; // +1 = faces right (player), -1 = faces left (enemy)

            Glow2D(px, m, m, 30f, A(c, 0.18f));

            switch (id)
            {
                case "drone":
                    // Quadrotor: circle body, 4 diagonal arms with rotor rings
                    FillCircle(px, m, m, 9f, c);
                    FillCircle(px, m, m, 5f, hi);
                    FillCircle(px, m, m, 2f, Color.white);
                    foreach (float ang in new[] { -135f, -45f, 45f, 135f })
                    {
                        float ra = ang * Mathf.Deg2Rad;
                        float rx = m + Mathf.Cos(ra) * 16f, ry = m + Mathf.Sin(ra) * 16f;
                        DrawLine(px, m, m, rx, ry, 2.5f, c);
                        DrawRing(px, rx, ry, 7f, 1.8f, c);
                        FillCircle(px, rx, ry, 2.5f, hi);
                    }
                    break;

                case "trooper":
                    // Humanoid: legs, torso+armor plate, gun arm, helmeted head with visor
                    FillRect(px, m - 9f, m - 23f, 7f, 14f, dk);       // left leg
                    FillRect(px, m + 2f,  m - 23f, 7f, 14f, dk);       // right leg
                    FillRect(px, m - 10f, m - 9f,  20f, 19f, c);       // torso
                    FillRect(px, m - 7f,  m - 6f,  14f, 13f, A(hi, 0.55f)); // armor plate
                    FillRect(px, m + d * 10f, m + 1f, d * 14f, 5f, hi); // gun arm
                    FillRect(px, m + d * 23f, m,     d * 5f,  7f, c);  // barrel
                    FillCircle(px, m, m + 14f, 8f, c);                 // head
                    DrawLine(px, m - 5f, m + 14f, m + 5f, m + 14f, 2f, Color.white); // visor
                    Glow2D(px, m, m + 14f, 4f, A(Color.white, 0.35f));
                    break;

                case "mech":
                    // Tank: track base, wide hull, top turret, cannon
                    FillRect(px, m - 21f, m - 22f, 42f, 10f, dk);     // tracks
                    for (int i = 0; i < 5; i++)
                        FillRect(px, m - 19f + i * 9f, m - 21f, 7f, 8f, Mix(dk, Color.black, 0.3f));
                    FillRect(px, m - 18f, m - 12f, 36f, 20f, c);      // hull
                    FillRect(px, m - 12f, m - 8f,  24f, 12f, A(hi, 0.5f)); // hull plate
                    FillCircle(px, m, m + 12f, 11f, c);               // turret base
                    FillCircle(px, m, m + 12f,  7f, hi);              // turret top
                    Glow2D(px, m, m + 12f, 5f, A(Color.white, 0.25f));
                    FillRect(px, m,           m + 10f, d * 24f, 5f, hi); // barrel
                    FillRect(px, m + d * 23f, m + 10f, d * 5f,  5f, c); // muzzle
                    break;

                case "interceptor":
                    // Delta-wing fighter: fuselage, swept wings, nose dot, engine glow
                    DrawLine(px, m - d * 23f, m, m + d * 25f, m, 5f, c);
                    FillPoly(px, new[] {
                        new Vector2(m,           m),
                        new Vector2(m - d * 8f,  m + 18f),
                        new Vector2(m - d * 20f, m + 5f)
                    }, A(c, 0.88f));
                    FillPoly(px, new[] {
                        new Vector2(m,           m),
                        new Vector2(m - d * 8f,  m - 18f),
                        new Vector2(m - d * 20f, m - 5f)
                    }, A(c, 0.88f));
                    FillCircle(px, m + d * 25f, m, 4f, Color.white);  // nose
                    Glow2D(px, m - d * 23f, m, 12f, new Color(1f, 0.5f, 0.1f, 0.9f)); // engine
                    DrawLine(px, m - d * 4f, m - 5f, m - d * 4f, m + 5f, 1.5f, hi);  // cockpit
                    break;

                case "turret":
                    // Static emplacement: base ring, armored body, twin barrels
                    FillCircle(px, m, m - 5f, 20f, dk);               // base plate
                    DrawRing(px, m, m - 5f, 20f, 2.5f, c);
                    FillCircle(px, m, m, 13f, c);                      // body
                    FillCircle(px, m, m,  8f, hi);
                    Glow2D(px, m, m, 5f, A(Color.white, 0.3f));
                    FillRect(px, m,           m + 4f, d * 26f, 5f, hi); // top barrel
                    FillRect(px, m,           m - 4f, d * 26f, 5f, hi); // bottom barrel
                    FillRect(px, m + d * 25f, m + 4f, d * 4f,  5f, c); // muzzle top
                    FillRect(px, m + d * 25f, m - 4f, d * 4f,  5f, c); // muzzle bottom
                    DrawRing(px, m, m, 16f, 1.5f, A(c, 0.45f));
                    break;

                case "sniper":
                    // Prone operative: flat body, long suppressed rifle, scope
                    FillRect(px, m - 20f, m - 5f, 24f,  9f, c);          // torso (prone)
                    FillCircle(px, m + 5f, m,     6.5f, c);               // helmeted head
                    FillCircle(px, m + 5f, m,     3.5f, hi);
                    DrawLine(px, m - 8f, m, m + d * 28f, m, 3.5f, hi);   // rifle barrel
                    FillRect(px, m - 10f, m - 9f, d * 9f, 5f, dk);       // stock
                    FillCircle(px, m + d * 19f, m - 7f, 4.5f, hi);       // scope body
                    DrawLine(px, m + d * 19f, m - 11f, m + d * 19f, m - 3f, 1.5f, Color.white); // scope lens
                    Glow2D(px, m + d * 28f, m, 5f, A(c, 0.55f));         // muzzle glow
                    break;

                case "shield-bot":
                    // Compact armored body + large energy-shield slab on forward arm
                    FillRect(px, m - 9f,   m - 18f, 18f, 27f, c);         // torso
                    FillRect(px, m - 7f,   m - 14f, 14f, 20f, A(hi, 0.5f)); // armor plate
                    FillCircle(px, m,      m + 14f, 7.5f, c);              // head
                    FillCircle(px, m,      m + 14f, 4.0f, hi);
                    // Shield arm (facing forward)
                    FillRect(px, m,        m - 14f, d * 15f, 23f, hi);     // shield slab
                    DrawRing(px, m + d*14f,m - 3f,  11f, 2.5f, A(c, 0.50f)); // shield edge ring
                    Glow2D(px, m + d*14f,  m - 3f,  13f, A(c, 0.30f));    // energy field glow
                    // Back arm
                    FillRect(px, m - d*8f, m - 4f, -d * 7f, 10f, dk);     // back arm
                    break;

                case "hacker":
                    // Floating techno-orb with circuit-tendril data streams
                    Glow2D(px, m, m + 2f, 20f, A(c, 0.35f));              // aura
                    FillCircle(px, m, m + 2f, 14f, A(c, 0.75f));          // outer orb
                    FillCircle(px, m, m + 2f,  9f, hi);                   // inner orb
                    // Circuit tendrils
                    DrawLine(px, m, m + 2f, m + d * 22f, m + 7f, 1.5f, c);
                    DrawLine(px, m, m + 2f, m + d * 20f, m - 6f, 1.5f, c);
                    DrawLine(px, m, m + 2f, m - d * 17f, m + 11f, 1.2f, A(c, 0.6f));
                    // Tendril data nodes
                    FillCircle(px, m + d * 22f, m + 7f,  3.5f, hi);
                    FillCircle(px, m + d * 20f, m - 6f,  3.5f, hi);
                    FillCircle(px, m - d * 17f, m + 11f, 2.5f, A(hi, 0.7f));
                    // Core eye
                    FillCircle(px, m, m + 2f, 4.5f, Color.white);
                    Glow2D(px, m, m + 2f, 5f, A(Color.white, 0.55f));
                    break;

                case "titan":
                    // Massive armored mech: wide hull, heavy tracks, dual shoulder cannons
                    Glow2D(px, m, m, 31f, A(c, 0.22f));                   // power aura
                    // Tracks
                    FillRect(px, m - 22f, m - 24f, 44f, 10f, dk);
                    for (int ti = 0; ti < 4; ti++)
                        FillRect(px, m - 20f + ti * 11f, m - 23f, 8f, 7f, Mix(dk, Color.black, 0.4f));
                    // Main hull
                    FillRect(px, m - 18f, m - 14f, 36f, 22f, c);
                    FillRect(px, m - 12f, m - 10f, 24f, 14f, A(hi, 0.50f));
                    // Shoulder mounts
                    FillRect(px, m - 20f, m + 9f,   8f, 8f, hi);         // left shoulder
                    FillRect(px, m + 12f, m + 9f,   8f, 8f, hi);         // right shoulder
                    // Cannon barrels (both face the enemy direction d)
                    FillRect(px, m - 18f, m + 11f, d * 24f, 4f, c);      // left cannon
                    FillRect(px, m + 14f, m + 11f, d * 24f, 4f, c);      // right cannon
                    // Head
                    FillRect(px, m - 9f,  m + 8f,  18f, 14f, c);
                    FillRect(px, m - 6f,  m + 10f, 12f,  6f, A(hi, 0.6f));
                    Glow2D(px, m, m + 15f, 5f, A(Color.white, 0.2f));
                    break;

                // ── medieval variants ─────────────────────────────────────────────

                case "pigeon":
                    // Scout pigeon: oval body, beak, swept wings, tail
                    FillCircle(px, m, m,     14f, c);           // body
                    FillCircle(px, m, m,      8f, hi);           // breast highlight
                    FillCircle(px, m, m + 9f, 6f, c);           // head
                    FillCircle(px, m, m + 9f, 3f, hi);
                    // Beak
                    FillPoly(px, new[] {
                        new Vector2(m,          m + 13f),
                        new Vector2(m + d*8f,   m + 9f),
                        new Vector2(m,          m + 6f),
                    }, dk);
                    // Left wing
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m - 22f,   m + 3f),
                        new Vector2(m - 16f,   m - 8f),
                    }, A(c, 0.80f));
                    // Right wing
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m + 22f,   m + 3f),
                        new Vector2(m + 16f,   m - 8f),
                    }, A(c, 0.80f));
                    // Tail
                    FillPoly(px, new[] {
                        new Vector2(m,         m - 10f),
                        new Vector2(m - 8f,    m - 22f),
                        new Vector2(m + 8f,    m - 22f),
                    }, dk);
                    Glow2D(px, m, m + 9f, 4f, A(Color.white, 0.20f));
                    break;

                case "knight":
                    // Armored knight: legs, heavy torso, sword arm, great-helm
                    FillRect(px, m - 9f,  m - 22f, 7f, 14f, dk);       // left leg
                    FillRect(px, m + 2f,  m - 22f, 7f, 14f, dk);       // right leg
                    FillRect(px, m - 11f, m - 8f,  22f, 22f, c);       // torso (wide armor)
                    FillRect(px, m - 8f,  m - 4f,  16f, 16f, A(hi, 0.50f)); // chest plate
                    // Shield (forward arm)
                    FillRect(px, m + d*10f, m - 6f, d*15f, 20f, A(c, 0.90f));
                    DrawRing(px, m + d*17f, m + 4f, 8f, 1.5f, A(hi, 0.55f));
                    // Sword arm
                    FillRect(px, m - d*10f, m + 4f, -d*18f, 4f, hi);  // blade
                    FillRect(px, m - d*10f, m + 2f, -d*4f,  8f, dk);   // crossguard
                    // Great helm
                    FillRect(px, m - 7f,  m + 9f,  14f, 14f, c);
                    FillRect(px, m - 4f,  m + 11f, 8f,  4f,  Color.black);  // visor slit
                    Glow2D(px, m, m + 16f, 5f, A(c, 0.18f));
                    break;

                case "siege":
                    // Trebuchet: wide wheeled base, counterweight arm, sling
                    // Wheels
                    DrawRing(px, m - 14f, m - 20f, 8f,  2.5f, dk);
                    DrawRing(px, m + 14f, m - 20f, 8f,  2.5f, dk);
                    FillCircle(px, m - 14f, m - 20f, 4f, Mix(dk, Color.black, 0.3f));
                    FillCircle(px, m + 14f, m - 20f, 4f, Mix(dk, Color.black, 0.3f));
                    // Frame
                    FillRect(px, m - 18f, m - 14f, 36f, 6f,  c);       // axle beam
                    FillRect(px, m - 4f,  m - 14f, 8f,  26f, c);       // vertical post
                    // Arm (angled, raised)
                    DrawLine(px, m, m + 12f, m + d*26f, m + 24f, 4f, hi);   // throwing arm
                    DrawLine(px, m, m + 12f, m - d*14f, m + 4f,  3.5f, dk); // counterweight arm
                    FillCircle(px, m - d*14f, m + 4f, 7f, dk);              // counterweight
                    // Sling/bucket
                    FillCircle(px, m + d*26f, m + 24f, 5f, hi);
                    Glow2D(px, m + d*26f, m + 24f, 7f, A(c, 0.30f));
                    break;

                case "wizard":
                    // Wizard: robe, pointy hat, staff with spell orb
                    // Robe (wide triangle base)
                    FillPoly(px, new[] {
                        new Vector2(m,      m + 28f),
                        new Vector2(m-18f,  m - 14f),
                        new Vector2(m+18f,  m - 14f),
                    }, A(c, 0.85f));
                    FillRect(px, m - 10f, m - 14f, 20f, 10f, c);        // upper robe
                    // Pointed hat
                    FillPoly(px, new[] {
                        new Vector2(m,      m + 30f),
                        new Vector2(m - 11f, m + 18f),
                        new Vector2(m + 11f, m + 18f),
                    }, dk);
                    FillRect(px, m - 13f, m + 16f, 26f, 4f, dk);        // hat brim
                    // Face
                    FillCircle(px, m, m + 12f, 6.5f, Mix(c, Color.white, 0.45f));
                    // Staff
                    DrawLine(px, m + d*7f, m - 14f, m + d*9f, m + 18f, 2f, hi);
                    // Spell orb on staff tip
                    Glow2D(px, m + d*8f, m + 20f, 10f, A(c, 0.50f));
                    FillCircle(px, m + d*8f, m + 20f, 5f, A(Color.white, 0.70f));
                    // Staff crystal runes
                    for (int rs = 0; rs < 3; rs++)
                    {
                        float ry = m - 8f + rs * 8f;
                        FillCircle(px, m + d*8f, ry, 2f, A(hi, 0.60f));
                    }
                    break;

                case "rogue":
                    // Rogue: crouched cloaked figure, twin daggers
                    // Cloak body (hunched)
                    FillPoly(px, new[] {
                        new Vector2(m,       m + 10f),
                        new Vector2(m - 14f, m - 10f),
                        new Vector2(m + 14f, m - 10f),
                    }, A(dk, 0.95f));
                    FillRect(px, m - 10f, m - 10f, 20f, 12f, A(dk, 0.85f)); // upper body
                    // Hood
                    FillCircle(px, m, m + 14f, 8f, dk);
                    FillCircle(px, m, m + 14f, 4f, A(Color.black, 0.70f));  // shadowed face
                    // Right dagger (forward)
                    DrawLine(px, m + d*8f,  m, m + d*26f, m + 8f, 2f, hi);
                    FillCircle(px, m + d*26f, m + 8f, 2.5f, Color.white);
                    // Left dagger (back-guard)
                    DrawLine(px, m - d*5f, m - 4f, m - d*20f, m + 4f, 1.5f, A(hi, 0.70f));
                    FillCircle(px, m - d*20f, m + 4f, 2f, A(hi, 0.60f));
                    // Cloak shadow
                    Glow2D(px, m, m - 4f, 14f, A(dk, 0.20f));
                    break;

                // ── synthwave variants ────────────────────────────────────────────

                case "holobot":
                    // Holographic drone: translucent hexagonal frame, glowing core, scan ring
                    DrawRing(px, m, m, 20f, 2.5f, A(c, 0.55f));
                    DrawRing(px, m, m, 13f, 1.5f, A(c, 0.40f));
                    Glow2D(px, m, m, 18f, A(c, 0.30f));
                    FillCircle(px, m, m, 7f, A(c, 0.70f));
                    FillCircle(px, m, m, 4f, hi);
                    Glow2D(px, m, m, 6f, A(Color.white, 0.55f));
                    // Hex frame vertices
                    foreach (var v2 in Hex(m, m, 20f))
                    {
                        FillCircle(px, v2.x, v2.y, 2.5f, hi);
                        Glow2D(px, v2.x, v2.y, 4f, A(c, 0.45f));
                    }
                    // scan line
                    DrawLine(px, m - 20f, m + 6f, m + 20f, m + 6f, 0.8f, A(c, 0.30f));
                    DrawLine(px, m - 20f, m - 2f, m + 20f, m - 2f, 0.8f, A(c, 0.22f));
                    break;

                case "racer":
                    // Speed-suit pilot: aerodynamic helmet, streamlined body, thruster pack
                    FillRect(px, m - 7f,  m - 22f, 6f, 12f, dk);        // left leg
                    FillRect(px, m + 1f,  m - 22f, 6f, 12f, dk);        // right leg
                    FillRect(px, m - 9f,  m - 10f, 18f, 17f, c);        // torso
                    FillRect(px, m - 6f,  m - 7f,  12f, 11f, A(hi, 0.50f)); // chest stripe
                    // Thruster pack (back)
                    FillRect(px, m - d*9f, m - 6f,  -d*6f, 14f, dk);
                    Glow2D(px, m - d*12f, m - 6f, 6f, new Color(1f, 0.4f, 1f, 0.65f));
                    // Gun arm (forward)
                    FillRect(px, m + d*9f,  m + 2f, d*12f, 4f, hi);
                    FillRect(px, m + d*20f, m + 1f, d*4f,  6f, c);      // muzzle
                    // Aerodynamic helmet
                    FillCircle(px, m, m + 12f, 7.5f, c);
                    FillRect(px, m - 6f, m + 12f, 12f, 4f, A(dk, 0.80f)); // visor
                    Glow2D(px, m, m + 14f, 4f, new Color(0.9f, 0.1f, 1f, 0.40f));
                    break;

                case "speeder":
                    // Neon hover-bike silhouette — sleek fuselage, swept aero-wing, afterburner glow
                    DrawLine(px, m - d*24f, m, m + d*26f, m, 6f, c);         // fuselage
                    FillPoly(px, new[] {
                        new Vector2(m,           m),
                        new Vector2(m - d*10f,   m + 17f),
                        new Vector2(m - d*22f,   m + 6f),
                    }, A(c, 0.85f));
                    FillPoly(px, new[] {
                        new Vector2(m,           m),
                        new Vector2(m - d*10f,   m - 17f),
                        new Vector2(m - d*22f,   m - 6f),
                    }, A(c, 0.85f));
                    FillCircle(px, m + d*26f, m, 5f, Color.white);            // nose
                    Glow2D(px, m - d*24f, m, 14f, new Color(0.9f, 0.1f, 1f, 0.85f)); // afterburner
                    DrawLine(px, m - d*2f, m - 4f, m - d*2f, m + 4f, 1.5f, hi);     // cockpit
                    break;

                // ── biopunk variants ──────────────────────────────────────────────

                case "spore":
                    // Organic spore pod: teardrop body, pulsing membrane, tendril launchers
                    FillCircle(px, m, m - 3f, 16f, A(c, 0.80f));
                    FillCircle(px, m, m - 3f, 10f, A(hi, 0.55f));
                    FillCircle(px, m, m - 3f,  5f, A(Color.white, 0.45f));
                    Glow2D(px, m, m - 3f, 14f, A(c, 0.35f));
                    DrawRing(px, m, m - 3f, 20f, 1.5f, A(c, 0.28f));
                    // Tendril spore launchers
                    for (int s = 0; s < 4; s++)
                    {
                        float ang = s * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                        float ex = m + Mathf.Cos(ang) * 24f, ey = (m - 3f) + Mathf.Sin(ang) * 24f;
                        DrawLine(px, m + Mathf.Cos(ang) * 16f, (m - 3f) + Mathf.Sin(ang) * 16f,
                                     ex, ey, 1.5f, A(c, 0.55f));
                        FillCircle(px, ex, ey, 3f, hi);
                        Glow2D(px, ex, ey, 4f, A(c, 0.40f));
                    }
                    break;

                case "mutant":
                    // Biopunk brute: hunched body, organic armour growths, toxin injector arm
                    FillRect(px, m - 10f, m - 22f, 8f, 14f, dk);            // left leg
                    FillRect(px, m +  2f, m - 22f, 8f, 14f, dk);            // right leg
                    FillRect(px, m - 13f, m - 8f,  26f, 20f, c);            // wide torso
                    FillRect(px, m - 10f, m - 5f,  20f, 14f, A(hi, 0.45f)); // chest plate
                    // Organic carapace spines
                    FillPoly(px, new[] { new Vector2(m - 8f, m + 8f), new Vector2(m - 14f, m + 18f), new Vector2(m - 5f, m + 8f) }, A(dk, 0.90f));
                    FillPoly(px, new[] { new Vector2(m + 8f, m + 8f), new Vector2(m + 14f, m + 18f), new Vector2(m + 5f, m + 8f) }, A(dk, 0.90f));
                    // Injector arm
                    FillRect(px, m + d*13f, m + 2f, d*14f, 6f, hi);
                    FillCircle(px, m + d*26f, m + 5f, 4f, new Color(0.2f, 1f, 0.1f, 0.85f));
                    Glow2D(px, m + d*26f, m + 5f, 5f, A(c, 0.45f));
                    // Mutant head with eye-cluster
                    FillCircle(px, m, m + 14f, 8.5f, c);
                    FillCircle(px, m - 3f, m + 15f, 2.5f, new Color(0.1f, 1f, 0.1f));
                    FillCircle(px, m + 3f, m + 15f, 2.5f, new Color(0.1f, 1f, 0.1f));
                    FillCircle(px, m,      m + 12f, 2f,   new Color(0.3f, 1f, 0.2f));
                    Glow2D(px, m, m + 14f, 5f, A(c, 0.25f));
                    break;

                // ── sakura variants ───────────────────────────────────────────────

                case "wisp":
                    // Fairy wisp: soft glowing orb with petal-like wings and sparkle trail
                    Glow2D(px, m, m, 26f, A(c, 0.28f));
                    FillCircle(px, m, m, 12f, A(c, 0.75f));
                    FillCircle(px, m, m,  7f, hi);
                    FillCircle(px, m, m,  3f, Color.white);
                    Glow2D(px, m, m, 5f, A(Color.white, 0.60f));
                    // Petal wings (4 soft lobes)
                    for (int s = 0; s < 4; s++)
                    {
                        float ang = s * Mathf.PI * 0.5f;
                        float wx = m + Mathf.Cos(ang) * 18f, wy = m + Mathf.Sin(ang) * 18f;
                        FillCircle(px, wx, wy, 7f, A(c, 0.40f));
                        Glow2D(px, wx, wy, 8f, A(c, 0.20f));
                    }
                    // Sparkle dots
                    for (int s = 0; s < 5; s++)
                    {
                        float ang = s * Mathf.PI * 2f / 5f + 0.3f;
                        float sx = m + Mathf.Cos(ang) * 25f, sy = m + Mathf.Sin(ang) * 25f;
                        FillCircle(px, sx, sy, 1.5f, A(Color.white, 0.70f));
                    }
                    break;

                case "kunoichi":
                    // Ninja: crouched stance, mask, twin sai blades, flowing sash
                    // Legs (crouched, wide)
                    FillRect(px, m - 12f, m - 18f, 10f, 10f, dk);
                    FillRect(px, m +  2f, m - 18f, 10f, 10f, dk);
                    FillRect(px, m - 12f, m - 8f,  24f, 16f, c);         // torso
                    FillRect(px, m -  9f, m - 5f,  18f, 10f, A(hi, 0.45f)); // gi front
                    // Flowing sash
                    FillPoly(px, new[] {
                        new Vector2(m - 8f, m - 8f),
                        new Vector2(m - 14f, m - 24f),
                        new Vector2(m - 4f,  m - 8f),
                    }, A(c, 0.65f));
                    // Head with mask
                    FillCircle(px, m, m + 12f, 7.5f, c);
                    FillRect(px, m - 5f, m + 11f, 10f, 4f, dk);           // mask
                    // Eyes above mask
                    FillCircle(px, m - 3f, m + 16f, 1.5f, Color.white);
                    FillCircle(px, m + 3f, m + 16f, 1.5f, Color.white);
                    // Sai weapons
                    DrawLine(px, m + d*8f,  m - 2f, m + d*26f, m - 2f, 2.5f, hi);   // main prong
                    DrawLine(px, m + d*11f, m + 2f, m + d*17f, m + 5f, 1.5f, hi);   // side guard
                    DrawLine(px, m + d*11f, m - 6f, m + d*17f, m - 9f, 1.5f, hi);
                    FillCircle(px, m + d*26f, m - 2f, 2f, Color.white);
                    break;

                // ── solar variants ─────────────────────────────────────────────────

                case "guardian":
                    // Sun guardian: radiant armor, solar-disc shield, light lance
                    FillRect(px, m - 9f,  m - 22f, 7f, 14f, dk);         // legs
                    FillRect(px, m + 2f,  m - 22f, 7f, 14f, dk);
                    FillRect(px, m - 12f, m - 8f,  24f, 20f, c);         // torso
                    FillRect(px, m - 9f,  m - 5f,  18f, 14f, A(hi, 0.55f)); // chest plate
                    // Solar disc shield
                    FillCircle(px, m + d*13f, m, 14f, A(c, 0.75f));
                    DrawRing(px, m + d*13f, m, 14f, 2f, A(hi, 0.70f));
                    Glow2D(px, m + d*13f, m, 10f, A(new Color(1f, 0.9f, 0.3f), 0.40f));
                    // Sun rays on shield
                    for (int s = 0; s < 6; s++)
                    {
                        float ang = s * Mathf.PI / 3f;
                        float rx0 = m + d*13f + Mathf.Cos(ang) * 8f,  ry0 = m + Mathf.Sin(ang) * 8f;
                        float rx1 = m + d*13f + Mathf.Cos(ang) * 13f, ry1 = m + Mathf.Sin(ang) * 13f;
                        DrawLine(px, rx0, ry0, rx1, ry1, 1.2f, A(Color.white, 0.45f));
                    }
                    // Light lance — extends forward toward enemy
                    DrawLine(px, m + d*5f, m + 2f, m + d*32f, m + 2f, 4f, hi);
                    Glow2D(px, m + d*32f, m + 2f, 7f, A(new Color(1f, 0.9f, 0.3f), 0.55f));
                    // Helm
                    FillCircle(px, m, m + 13f, 8f, c);
                    FillRect(px, m - 4f, m + 13f, 8f, 4f, A(new Color(1f, 0.9f, 0.3f), 0.55f)); // visor
                    Glow2D(px, m, m + 15f, 5f, A(Color.white, 0.22f));
                    break;

                // ── dawn variants ──────────────────────────────────────────────────

                case "sprite":
                    // Nature sprite: small translucent body, leaf wings, nature glow
                    Glow2D(px, m, m, 22f, A(c, 0.20f));
                    FillCircle(px, m, m, 9f, A(c, 0.65f));
                    FillCircle(px, m, m, 5f, hi);
                    Glow2D(px, m, m, 4f, A(Color.white, 0.45f));
                    // Leaf wings (two pairs)
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m - 18f,   m + 5f),
                        new Vector2(m - 10f,   m - 14f),
                    }, A(c, 0.55f));
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m + 18f,   m + 5f),
                        new Vector2(m + 10f,   m - 14f),
                    }, A(c, 0.55f));
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m - 13f,   m + 12f),
                        new Vector2(m - 6f,    m - 5f),
                    }, A(c, 0.38f));
                    FillPoly(px, new[] {
                        new Vector2(m,         m),
                        new Vector2(m + 13f,   m + 12f),
                        new Vector2(m + 6f,    m - 5f),
                    }, A(c, 0.38f));
                    // Body dot
                    FillCircle(px, m, m + 14f, 4f, c);
                    FillCircle(px, m, m + 14f, 2f, hi);
                    break;

                case "wanderer":
                    // Cloaked traveler: wide hooded cloak, glowing staff, serene glow
                    // Cloak
                    FillPoly(px, new[] {
                        new Vector2(m,       m + 28f),
                        new Vector2(m - 20f, m - 12f),
                        new Vector2(m + 20f, m - 12f),
                    }, A(c, 0.80f));
                    FillRect(px, m - 12f, m - 12f, 24f, 10f, c);            // upper cloak
                    // Staff
                    DrawLine(px, m + d*8f, m - 14f, m + d*10f, m + 20f, 2.5f, hi);
                    // Crystal at staff top
                    Glow2D(px, m + d*9f, m + 22f, 12f, A(c, 0.50f));
                    FillCircle(px, m + d*9f, m + 22f, 6f, A(c, 0.75f));
                    FillCircle(px, m + d*9f, m + 22f, 3f, Color.white);
                    Glow2D(px, m + d*9f, m + 22f, 5f, A(Color.white, 0.55f));
                    // Hood
                    FillCircle(px, m, m + 14f, 9f, c);
                    FillCircle(px, m, m + 14f, 5f, A(dk, 0.80f));           // shadowed face
                    // Glowing eyes
                    FillCircle(px, m - 2.5f, m + 15f, 1.5f, new Color(1f, 0.9f, 0.5f, 0.90f));
                    FillCircle(px, m + 2.5f, m + 15f, 1.5f, new Color(1f, 0.9f, 0.5f, 0.90f));
                    break;

                // ── industrial variants ───────────────────────────────────────────

                case "worker":
                    // Factory worker: chunky boots, overalls, riveted safety helmet, rivet gun
                    FillRect(px, m - 10f, m - 24f, 8f, 12f, dk);              // left leg
                    FillRect(px, m +  2f, m - 24f, 8f, 12f, dk);              // right leg
                    FillRect(px, m - 11f, m - 13f, 4f, 5f, A(dk, 0.75f));    // left knee pad
                    FillRect(px, m +  7f, m - 13f, 4f, 5f, A(dk, 0.75f));    // right knee pad
                    FillRect(px, m - 12f, m - 7f,  24f, 20f, c);              // overalls body
                    FillRect(px, m -  9f, m - 3f,  18f, 14f, A(hi, 0.38f));  // bib/chest panel
                    // Bib straps
                    DrawLine(px, m - 4f, m + 8f, m - 4f, m - 4f, 2f, A(hi, 0.55f));
                    DrawLine(px, m + 4f, m + 8f, m + 4f, m - 4f, 2f, A(hi, 0.55f));
                    // Rivet gun (chunky industrial tool, faces forward)
                    FillRect(px, m + d * 11f, m + 1f, d * 13f, 8f, hi);      // gun body
                    FillRect(px, m + d * 23f, m + 2f, d *  5f, 6f, dk);      // barrel tip
                    FillRect(px, m + d * 15f, m - 4f, d *  6f, 4f, c);       // top vent
                    // Safety helmet (round with brim — distinct from trooper's sleek helmet)
                    FillCircle(px, m, m + 15f, 9f, c);
                    FillRect(px, m - 12f, m + 8f, 24f, 5f, A(c, 0.72f));     // brim
                    FillCircle(px, m, m + 15f, 5f, hi);
                    // Work goggles (two circular lenses, not a visor)
                    FillCircle(px, m - 5f, m + 15f, 3f, A(dk, 0.90f));
                    FillCircle(px, m + 5f, m + 15f, 3f, A(dk, 0.90f));
                    DrawLine(px, m - 2f, m + 15f, m + 2f, m + 15f, 1.5f, dk); // goggle bridge
                    Glow2D(px, m, m + 15f, 5f, A(Color.white, 0.18f));
                    break;

                default:
                    FillRect(px, m - 10f, m - 16f, 20f, 32f, c);
                    FillCircle(px, m, m, 8f, hi);
                    break;
            }

            return Make(px);
        }

        // ── 3D gameplay unit builder (replaces 2D BuildUnit for all game views) ──

        static Texture2D BuildUnit3D(string id, bool player, int part = 0, int pose = 0)
        {
            // Rasterize at the resolution the builders were authored for (s = R/128 = 1).
            // At 64 every feature landed on half-pixels — heads were 8px blobs.
            const int R = 128;
            var px = new Color[R * R];

            Color tint = player ? new Color(0.78f, 0.93f, 1.00f) : new Color(1.00f, 0.65f, 0.52f);
            var   L    = new Vector3(-0.45f, 0.70f, 0.55f).normalized;
            var   H    = (L + Vector3.forward).normalized;
            var fillL  = new Vector3(0.38f, 0.08f, 0.50f).normalized;

            var p = MakeUnitP3DP(tint, 0.32f, 0.55f);

            // part: 0 = full, 1 = body only, 2 = weapon only.
            // Non-split ids draw everything for 0/1 and stay transparent for 2.
            if (part == 2 && !HasWeaponPart(id))
                return MakeUnitTex(px, R);

            if (part != 2)
                P3DEllipseGlow(px, R, R / 2f, 6f, 20f, 4f, new Color(0f, 0f, 0f, 0.45f));

            switch (id)
            {
                case "drone":
                    P3DBuildDrone   (px, R, p, L, H, fillL, pose); break;

                // ---- DAWN: woven and lit from within ----
                case "sprite":
                    P3DBuildSprite    (px, R, p, L, H, fillL, pose); break;
                case "seeker":
                    P3DBuildSeeker    (px, R, p, L, H, fillL, part, pose); break;
                case "ward":
                    P3DBuildWard      (px, R, p, L, H, fillL, pose); break;
                case "caravan":
                    P3DBuildCaravan   (px, R, p, L, H, fillL, part, pose); break;
                case "glider":
                    P3DBuildGlider    (px, R, p, L, H, fillL, pose); break;
                case "oracle":
                    P3DBuildOracle    (px, R, p, L, H, fillL, pose); break;
                case "sentinel":
                    P3DBuildSentinel  (px, R, p, L, H, fillL, part, pose); break;
                case "beacon":
                    P3DBuildBeacon    (px, R, p, L, H, fillL, part, pose); break;
                case "pigeon":
                    P3DBuildPigeon  (px, R, p, L, H, fillL, pose); break;
                case "ember":
                    P3DBuildEmber      (px, R, p, L, H, fillL, pose); break;
                case "colossus":
                    P3DBuildColossus   (px, R, p, L, H, fillL, part, pose); break;
                case "heliostat":
                    P3DBuildHeliostat  (px, R, p, L, H, fillL, part, pose); break;
                case "holobot":
                    P3DBuildHoloOrb (px, R, p, L, H, fillL); break;

                // ---- CYBER: cold composite, exposed mechanism ----
                case "cybdrone":
                    P3DBuildCybDrone    (px, R, p, L, H, fillL, pose); break;
                case "cybtrooper":
                    P3DBuildCybTrooper  (px, R, p, L, H, fillL, pose); break;
                case "cybsniper":
                    P3DBuildCybSniper   (px, R, p, L, H, fillL, pose); break;
                case "cybmech":
                    P3DBuildCybMech     (px, R, p, L, H, fillL, part, pose); break;
                case "cybshield":
                    P3DBuildCybShield   (px, R, p, L, H, fillL, pose); break;
                case "cybinter":
                    P3DBuildCybInter    (px, R, p, L, H, fillL, pose); break;
                case "cybhacker":
                    P3DBuildCybHacker   (px, R, p, L, H, fillL, pose); break;
                case "cybtitan":
                    P3DBuildCybTitan    (px, R, p, L, H, fillL, part, pose); break;
                case "cybturret":
                    P3DBuildCybTurret   (px, R, p, L, H, fillL, part, pose); break;

                // ---- SYNTHWAVE: extruded chrome, lit from the grid ----
                case "synbot":
                    P3DBuildSynBot      (px, R, p, L, H, fillL, pose); break;
                case "synracer":
                    P3DBuildSynRacer    (px, R, p, L, H, fillL, pose); break;
                case "synlaser":
                    P3DBuildSynLaser    (px, R, p, L, H, fillL, pose); break;
                case "syncruiser":
                    P3DBuildSynCruiser  (px, R, p, L, H, fillL, part, pose); break;
                case "synbouncer":
                    P3DBuildSynBouncer  (px, R, p, L, H, fillL, pose); break;
                case "synspeeder":
                    P3DBuildSynSpeeder  (px, R, p, L, H, fillL, pose); break;
                case "synkeytar":
                    P3DBuildSynKeytar   (px, R, p, L, H, fillL, pose); break;
                case "synobelisk":
                    P3DBuildSynObelisk  (px, R, p, L, H, fillL, part, pose); break;
                case "synpylon":
                    P3DBuildSynPylon    (px, R, p, L, H, fillL, part, pose); break;

                // ---- SAKURA: folded and lacquered ----
                case "wisp":
                    P3DBuildWisp        (px, R, p, L, H, fillL, pose); break;
                case "shinobi":
                    P3DBuildShinobi     (px, R, p, L, H, fillL, pose); break;
                case "yumi":
                    P3DBuildYumi        (px, R, p, L, H, fillL, part, pose); break;
                case "shrine":
                    P3DBuildShrineGuard (px, R, p, L, H, fillL, pose); break;
                case "tanuki":
                    P3DBuildTanuki      (px, R, p, L, H, fillL, part, pose); break;
                case "kite":
                    P3DBuildKite        (px, R, p, L, H, fillL, pose); break;
                case "onmyoji":
                    P3DBuildOnmyoji     (px, R, p, L, H, fillL, pose); break;
                case "kami":
                    P3DBuildKami        (px, R, p, L, H, fillL, part, pose); break;
                case "torii":
                    P3DBuildTorii       (px, R, p, L, H, fillL, part, pose); break;

                // ---- BIOPUNK: own geometry, grown not built ----
                case "spore":
                    P3DBuildSpore     (px, R, p, L, H, fillL, pose); break;
                case "mutant":
                    P3DBuildMutant    (px, R, p, L, H, fillL, pose); break;
                case "stinger":
                    P3DBuildStinger   (px, R, p, L, H, fillL, part, pose); break;
                case "crawler":
                    P3DBuildCrawler   (px, R, p, L, H, fillL, part, pose); break;
                case "carapace":
                    P3DBuildCarapace  (px, R, p, L, H, fillL, pose); break;
                case "swarm":
                    P3DBuildSwarm     (px, R, p, L, H, fillL, pose); break;
                case "mycelium":
                    P3DBuildMycelium  (px, R, p, L, H, fillL, pose); break;
                case "hive":
                    P3DBuildHive      (px, R, p, L, H, fillL, part, pose); break;
                case "pod":
                    P3DBuildPod       (px, R, p, L, H, fillL, part, pose); break;
                case "trooper": case "racer":
                    P3DBuildTrooper (px, R, p, L, H, fillL, pose); break;
                case "knight":
                    P3DBuildKnight  (px, R, p, L, H, fillL, pose); break;

                // ---- SOLAR FORGE: own geometry, six wired; three still aliased below ----
                case "guardian":
                    P3DBuildGuardian   (px, R, p, L, H, fillL, pose); break;
                case "raycaster":
                    P3DBuildRaycaster  (px, R, p, L, H, fillL, part, pose); break;
                case "aegis":
                    P3DBuildAegis      (px, R, p, L, H, fillL, pose); break;
                case "forgewalker":
                    P3DBuildForgewalker(px, R, p, L, H, fillL, part, pose); break;
                case "phoenix":
                    P3DBuildPhoenix    (px, R, p, L, H, fillL, pose); break;
                case "pyromancer":
                    P3DBuildPyromancer (px, R, p, L, H, fillL, pose); break;
                // ---- INDUSTRIAL: riveted, stamped, badly repaired ----
                case "worker":
                    P3DBuildWorker      (px, R, p, L, H, fillL, pose); break;
                case "rivetbot":
                    P3DBuildRivetbot    (px, R, p, L, H, fillL, pose); break;
                case "gunner":
                    P3DBuildGunner      (px, R, p, L, H, fillL, part, pose); break;
                case "bulkhead":
                    P3DBuildBulkhead    (px, R, p, L, H, fillL, pose); break;
                case "crane":
                    P3DBuildCrane       (px, R, p, L, H, fillL, part, pose); break;
                case "furnace":
                    P3DBuildFurnace     (px, R, p, L, H, fillL, part, pose); break;
                case "ornithopter":
                    P3DBuildOrnithopter (px, R, p, L, H, fillL, pose); break;
                case "engineer":
                    P3DBuildEngineer    (px, R, p, L, H, fillL, pose); break;
                case "gatling":
                    P3DBuildGatling     (px, R, p, L, H, fillL, part, pose); break;
                case "wanderer":
                    P3DBuildWanderer(px, R, p, L, H, fillL, pose); break;
                case "mech":
                    P3DBuildMech        (px, R, p, L, H, fillL, part, pose); break;
                case "siege":
                    P3DBuildSiege       (px, R, p, L, H, fillL, part, pose); break;
                case "interceptor": case "speeder":
                    P3DBuildInterceptor (px, R, p, L, H, fillL, pose); break;
                case "rogue":
                    P3DBuildRogue       (px, R, p, L, H, fillL, pose); break;
                case "ballista":
                    P3DBuildBallista(px, R, p, L, H, fillL, part, pose); break;
                case "turret":
                    P3DBuildTurret  (px, R, p, L, H, fillL, part, pose); break;
                case "archer":
                    P3DBuildArcher  (px, R, p, L, H, fillL, part, pose); break;
                case "sniper":
                    P3DBuildSniper  (px, R, p, L, H, fillL, part, pose); break;
                case "paladin":
                    P3DBuildPaladin (px, R, p, L, H, fillL, pose); break;
                case "shield-bot":
                    P3DBuildShieldBot(px, R, p, L, H, fillL, pose); break;
                case "hacker":
                    P3DBuildHacker  (px, R, p, L, H, fillL, pose); break;
                case "wizard":
                    P3DBuildWizard  (px, R, p, L, H, fillL, pose); break;
                case "golem":
                    P3DBuildGolem   (px, R, p, L, H, fillL, part, pose); break;
                case "titan":
                    P3DBuildTitan   (px, R, p, L, H, fillL, part, pose); break;
                default:
                    P3DSphere(px, R, R / 2f, R / 2f, 14f, tint, 0.32f, 0.55f, L, H, fillL);
                    break;
            }

            P3DOutline(px, R);
            return MakeUnitTex(px, R);
        }

        // Dark 1px contour around solid geometry — the silhouette pop that makes
        // small sprites read against busy lanes. Glows/shadows (alpha < .88) are ignored.
        static void P3DOutline(Color[] px, int R)
        {
            var oc  = new Color(0.02f, 0.03f, 0.07f, 0.88f);
            var src = (Color[])px.Clone();
            for (int y = 0; y < R; y++)
                for (int x = 0; x < R; x++)
                {
                    int i = y * R + x;
                    if (src[i].a > 0.45f) continue; // already part of the body
                    bool edge =
                        (x > 0     && src[i - 1].a > 0.88f) ||
                        (x < R - 1 && src[i + 1].a > 0.88f) ||
                        (y > 0     && src[i - R].a > 0.88f) ||
                        (y < R - 1 && src[i + R].a > 0.88f);
                    if (edge) px[i] = oc;
                }
        }

        // Mipmaps + trilinear: battlefield rects are ~30px, a raw 128→30 downscale
        // aliases badly ("pixelated" shimmer in motion). Mips fix it for free.
        static Texture2D MakeUnitTex(Color[] px, int R)
        {
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, true);
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static P3DP MakeUnitP3DP(Color tint, float metallic, float smoothness)
        {
            // Iteration 5: stronger value separation — deeper shadow tone, hotter
            // highlights and visors, so forms read at 30px like CoC sprites do.
            Color dark  = new Color(tint.r * 0.48f, tint.g * 0.48f, tint.b * 0.48f);
            Color light = new Color(Mathf.Min(tint.r * 1.40f, 1f), Mathf.Min(tint.g * 1.40f, 1f), Mathf.Min(tint.b * 1.40f, 1f));
            Color vis   = new Color(Mathf.Min(tint.r * 0.70f + 0.50f, 1f), Mathf.Min(tint.g * 0.70f + 0.40f, 1f), 1f);
            return new P3DP {
                head = tint,  body = tint,    leg  = dark,
                arm  = tint,  shoulder = light, chest = light,
                platform  = new Color(tint.r * 0.18f, tint.g * 0.18f, tint.b * 0.18f),
                visor     = vis, visorGlow = vis, emission = vis,
                metallic  = metallic, smoothness = smoothness, emissionStr = 0f
            };
        }

        // Quadrotor drone — central body sphere, 4 diagonal arm spars, rotor hubs, structural diamond frame
        // pose 1/2 alternate the rotor-disc profile so the blades read as SPINNING. A quadrotor
        // with frozen rotors just slides through the air.
        static void P3DBuildDrone(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s  = R / 128f;
            float cx = R * .5f, cy = R * .5f;
            // blur disc: wide+thin on one frame, tighter+brighter on the next
            // Blade streaks, not a soft disc: a glow's quadratic falloff washes out to nothing at
            // the rim, so an alternating "disc size" was invisible at unit scale. Two crossed
            // streaks swapping dominance each frame reads unmistakably as blades spinning.
            bool spin = pose == 1 || pose == 2 || Atk(pose);
            float diveY = pose == 4 ? -6f : (pose == 3 ? 3f : 0f);   // rear up on the dive
            float axW = pose == 2 ? 4.0f : 15f;    // horizontal streak
            float axH = pose == 2 ? 13f  : 2.2f;
            float axA = spin ? 0.85f : 0.45f;
            float bxW = pose == 2 ? 15f  : 4.0f;   // crossed streak, the quiet one
            float bxH = pose == 2 ? 2.2f : 13f;
            float bxA = spin ? 0.30f : 0.0f;
            foreach (float deg in new[] { -135f, -45f, 45f, 135f })
            {
                float ra = deg * Mathf.Deg2Rad;
                float rx = cx + Mathf.Cos(ra) * 36f * s, ry = cy + Mathf.Sin(ra) * 36f * s;
                P3DEllipseGlow(px, R, (cx + rx) * .5f, (cy + ry) * .5f,
                    Mathf.Abs(rx - cx) * .55f + 2f * s, Mathf.Abs(ry - cy) * .55f + 2f * s,
                    new Color(p.body.r, p.body.g, p.body.b, 0.55f));
                P3DSphere(px, R, rx, ry, 9f * s, p.shoulder, p.metallic, p.smoothness, L, H, fillL);
                P3DEllipseGlow(px, R, rx, ry, axW * s, axH * s, new Color(p.visor.r, p.visor.g, p.visor.b, axA));
                if (bxA > 0f)
                    P3DEllipseGlow(px, R, rx, ry, bxW * s, bxH * s, new Color(p.visor.r, p.visor.g, p.visor.b, bxA));
            }
            // Diamond frame connecting adjacent rotor hubs
            float rMid = 18f * s * 0.707f; // midpoint distance from centre to frame bar centre
            Color frmC = new Color(p.body.r * 0.88f, p.body.g * 0.88f, p.body.b * 0.88f, 0.52f);
            P3DEllipseGlow(px, R, cx,       cy - rMid, rMid + 2f*s, 2.2f*s, frmC); // top bar
            P3DEllipseGlow(px, R, cx + rMid, cy,       2.2f*s, rMid + 2f*s, frmC); // right bar
            P3DEllipseGlow(px, R, cx,       cy + rMid, rMid + 2f*s, 2.2f*s, frmC); // bottom bar
            P3DEllipseGlow(px, R, cx - rMid, cy,       2.2f*s, rMid + 2f*s, frmC); // left bar
            P3DSphere(px, R, cx, (cy + diveY*s), 20f * s, p.body,     p.metallic,       p.smoothness,       L, H, fillL);
            P3DSphere(px, R, cx, (cy + diveY*s), 12.5f * s, p.shoulder, p.metallic + .1f, p.smoothness + .08f, L, H, fillL);
            if (Atk(pose))   // weapon hot on the dive
                P3DEllipseGlow(px, R, cx + 14f*s, (cy + diveY*s) - 6f*s, 7f*s, 5f*s,
                    new Color(1f, 0.9f, 0.5f, pose == 4 ? 0.95f : 0.5f));
            P3DSphere(px, R, cx, cy,  6f * s, Color.white, 0.95f, 0.98f, L, H, fillL);
            P3DEllipseGlow(px, R, cx, cy, 10f * s, 10f * s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.60f));
        }

        // Floating energy orb — holobot / spore pod / wisp spirit
        static void P3DBuildHoloOrb(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL)
        {
            float s  = R / 128f;
            float cx = R * .5f, cy = R * .5f;
            P3DEllipseGlow(px, R, cx, cy, 32f * s, 32f * s, new Color(p.body.r, p.body.g, p.body.b, 0.28f));
            P3DEllipseGlow(px, R, cx, cy, 26f * s,  5f * s, new Color(p.visor.r, p.visor.g, p.visor.b, 0.25f));
            P3DEllipseGlow(px, R, cx, cy, 20f * s,  3f * s, new Color(p.visor.r, p.visor.g, p.visor.b, 0.18f));
            P3DSphere(px, R, cx, cy, 20f * s, new Color(p.body.r, p.body.g, p.body.b, 0.72f), p.metallic, p.smoothness, L, H, fillL);
            P3DSphere(px, R, cx, cy, 13f * s, p.shoulder, p.metallic + .1f, p.smoothness + .08f, L, H, fillL);
            P3DSphere(px, R, cx, cy,  6f * s, Color.white, 0.95f, 0.98f, L, H, fillL);
            P3DEllipseGlow(px, R, cx, cy, 11f * s, 11f * s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.60f));
        }

        // Bipedal walker mech — articulated legs with knees, wide hull, sphere turret, barrel glow
        // part: 0 full · 1 body only · 2 cannon arm only  ·  pose: walk-cycle leg frames
        static void P3DBuildMech(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int part = 0, int pose = 0)
        {
            float s = R / 128f;
            float cx = R * .5f;
            if (part == 2)
            {
                P3DEllipseGlow(px, R, cx + 30f*s, 95f*s, 14f*s, 4.5f*s, new Color(p.arm.r*1.1f, p.arm.g*1.1f, p.arm.b*1.1f, 0.75f));
                P3DSphere(px, R, cx + 44f*s, 95f*s, 6f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
                return;
            }
            // Side-profile stride: the legs swing fore/aft through real knee angles instead of
            // lifting in place. A bipedal walker whose feet only bob reads as hovering.
            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy;
            // contact / passing, not a near-far colour swap -- see the note on the side rig
            if (pose == 1)      { nKx =  14f; nKy = 31f; nFx =  22f; nFy = 11f;
                                  fKx = -10f; fKy = 29f; fFx = -18f; fFy = 10f; }
            else if (pose == 2) { nKx =  -4f; nKy = 34f; nFx =  -8f; nFy = 18f;
                                  fKx =  12f; fKy = 30f; fFx =  20f; fFy = 10f; }
            else if (pose == 6) { nKx = -10f; nKy = 29f; nFx = -18f; nFy = 10f;
                                  fKx =  14f; fKy = 31f; fFx =  22f; fFy = 11f; }
            else if (pose == 7) { nKx =  12f; nKy = 30f; nFx =  20f; nFy = 10f;
                                  fKx =  -4f; fKy = 34f; fFx =  -8f; fFy = 18f; }
            else if (pose == 3) { nKx =  10f; nKy = 29f; nFx =  16f; nFy = 10f;   // brace to fire
                                  fKx = -12f; fKy = 29f; fFx = -20f; fFy = 10f; }
            else if (pose == 4) { nKx =  12f; nKy = 28f; nFx =  18f; nFy = 10f;   // absorb recoil
                                  fKx = -14f; fKy = 28f; fFx = -22f; fFy = 10f; }
            else                { nKx =   5f; nKy = 30f; nFx =   6f; nFy = 10f;
                                  fKx =  -6f; fKy = 30f; fFx =  -7f; fFy = 10f; }
            // Hull rocks back as the cannon fires — the legs plant, the body takes the kick.
            float hullDx = pose == 3 ? -3f : (pose == 4 ? -7f : 0f);
            float hullDy = pose == 4 ? -2f : 0f;
            Color mFar = new Color(p.leg.r*0.62f, p.leg.g*0.62f, p.leg.b*0.62f, p.leg.a);
            // Far leg first (darkened), then the near leg over it
            P3DLimb  (px, R, cx - 3f*s, 50f*s, (cx + fKx*s), fKy*s, 8.0f*s, mFar, p.metallic*.65f, p.smoothness*.75f, L, H, fillL);
            P3DLimb  (px, R, (cx + fKx*s), fKy*s, (cx + fFx*s), fFy*s, 7.0f*s, mFar, p.metallic*.65f, p.smoothness*.75f, L, H, fillL);
            P3DSphere(px, R, (cx + fFx*s), fFy*s, 9.0f*s, mFar, p.metallic, p.smoothness, L, H, fillL);
            P3DLimb  (px, R, cx + 3f*s, 50f*s, (cx + nKx*s), nKy*s, 9.0f*s, p.leg, p.metallic*.7f, p.smoothness*.8f, L, H, fillL);
            P3DLimb  (px, R, (cx + nKx*s), nKy*s, (cx + nFx*s), nFy*s, 8.0f*s, p.leg, p.metallic*.7f, p.smoothness*.8f, L, H, fillL);
            P3DSphere(px, R, (cx + nKx*s), nKy*s, 8.4f*s, p.leg, p.metallic, p.smoothness, L, H, fillL);
            P3DSphere(px, R, (cx + nFx*s), nFy*s, 10.0f*s, p.leg, p.metallic, p.smoothness, L, H, fillL);
            // Hull
            float mhx = cx + hullDx*s;
            P3DCylinder(px, R, mhx, (46f+hullDy)*s, (80f+hullDy)*s, 22f*s, p.body, p.metallic, p.smoothness, L, H, fillL);
            P3DSphere  (px, R, mhx, (80f+hullDy)*s, 22f*s,          p.body, p.metallic, p.smoothness, L, H, fillL);
            P3DChestOverlay(px, R, mhx, (62f+hullDy)*s, 18f*s, 18f*s, p.chest, p.metallic, p.smoothness, H);
            // Turret + cannon — bigger head sphere for silhouette. Rides the hull, so it takes
            // the same recoil offset; leaving it on cx would detach the turret from the body.
            P3DSphere  (px, R, mhx, (96f+hullDy)*s, 16f*s, p.shoulder, p.metallic, p.smoothness, L, H, fillL);
            P3DEllipseGlow(px, R, mhx, (96f+hullDy)*s, 7f*s, 7f*s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.45f));
            if (part != 1)
            {
                P3DEllipseGlow(px, R, mhx + 30f*s, (95f+hullDy)*s, 14f*s, 4.5f*s, new Color(p.arm.r*1.1f, p.arm.g*1.1f, p.arm.b*1.1f, 0.75f));
                P3DSphere(px, R, mhx + 44f*s, (95f+hullDy)*s, 6f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
            }
        }

        // Delta-wing interceptor — fuselage sphere chain, swept wings, engine glow
        // pose 1/2 pulse the engine flare; 3/4 are the strafing attack (nose drops, cannons hot).
        static void P3DBuildInterceptor(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            float cx = R * .5f, cy = R * .5f;
            // Engine flare behind the craft — length alternates so the drive reads as LIVE.
            float flare = pose == 1 ? 26f : (pose == 2 ? 17f : (Atk(pose) ? 32f : 21f));
            float flareA = Atk(pose) ? 0.85f : (pose == 1 ? 0.72f : 0.55f);
            P3DEllipseGlow(px, R, cx - (16f + flare * 0.5f)*s, cy, flare*0.5f*s, 5.5f*s,
                new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, flareA));
            P3DEllipseGlow(px, R, cx - (16f + flare * 0.32f)*s, cy, flare*0.30f*s, 3.2f*s,
                new Color(1f, 1f, 1f, flareA * 0.55f));
            // Swept delta wings (top-view silhouette, drawn before fuselage spheres)
            Color wingC     = new Color(p.body.r,     p.body.g,     p.body.b,     0.86f);
            Color wingEdgeC = new Color(p.shoulder.r, p.shoulder.g, p.shoulder.b, 0.65f);
            FillPolyR(px, R, new Vector2[] {                   // upper wing
                new Vector2(cx + 4f*s, cy),
                new Vector2(cx - 12f*s, cy),
                new Vector2(cx - 22f*s, cy + 22f*s)
            }, wingC);
            FillPolyR(px, R, new Vector2[] {                   // lower wing
                new Vector2(cx + 4f*s, cy),
                new Vector2(cx - 12f*s, cy),
                new Vector2(cx - 22f*s, cy - 22f*s)
            }, wingC);
            DrawLineR(px, R, cx + 4f*s, cy, cx - 22f*s, cy + 22f*s, 1.4f*s*2f, wingEdgeC);  // upper leading edge
            DrawLineR(px, R, cx + 4f*s, cy, cx - 22f*s, cy - 22f*s, 1.4f*s*2f, wingEdgeC);  // lower leading edge
            for (float bx = cx - 32f*s; bx <= cx + 32f*s; bx += 4f*s)
                P3DSphere(px, R, bx, cy, 5.5f*s, p.body, p.metallic, p.smoothness, L, H, fillL);
            P3DChestOverlay(px, R, cx - 10f*s, cy + 10f*s, 22f*s, 12f*s, p.body, p.metallic * .75f, p.smoothness * .85f, H);
            P3DChestOverlay(px, R, cx - 10f*s, cy - 10f*s, 22f*s, 12f*s, p.body, p.metallic * .75f, p.smoothness * .85f, H);
            P3DSphere(px, R, cx + 32f*s, cy, 7f*s, p.shoulder, p.metallic, p.smoothness, L, H, fillL);
            P3DEllipseGlow(px, R, cx - 32f*s, cy, 14f*s, 14f*s, new Color(1f, 0.45f, 0.08f, 0.85f));
            P3DSphere(px, R, cx - 28f*s, cy, 5f*s, new Color(1f, 0.60f, 0.15f), 0.80f, 0.90f, L, H, fillL);
            P3DSphere(px, R, cx + 8f*s, cy, 5f*s, p.visor, 0.80f, 0.90f, L, H, fillL);
            if (Atk(pose))   // cannons hot during the strafing pass
                P3DEllipseGlow(px, R, cx + 30f*s, cy, 7f*s, 4f*s, new Color(1f, 0.92f, 0.55f, 0.9f));
        }

        // Static gun emplacement — tripod base legs, armoured sphere body, twin barrel glows
        // part: 0 full · 1 base only · 2 barrel assembly only
        // pose 3/4: the housing rocks back on its tripod as the barrels fire.
        static void P3DBuildTurret(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int part = 0, int pose = 0)
        {
            float s = R / 128f;
            float cx = R * .5f - (pose == 4 ? 5f*s : (pose == 3 ? -2f*s : 0f)), cy = R * .5f - 4f*s;
            if (part == 2)
            {
                P3DEllipseGlow(px, R, cx + 24f*s, cy + 5f*s, 12f*s, 4f*s, new Color(p.arm.r, p.arm.g, p.arm.b, 0.80f));
                P3DEllipseGlow(px, R, cx + 24f*s, cy - 5f*s, 12f*s, 4f*s, new Color(p.arm.r, p.arm.g, p.arm.b, 0.80f));
                P3DSphere(px, R, cx + 36f*s, cy + 5f*s, 5f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
                P3DSphere(px, R, cx + 36f*s, cy - 5f*s, 5f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
                return;
            }
            // Tripod legs (3 lines spreading from base)
            Color legC = new Color(p.leg.r, p.leg.g, p.leg.b, 0.88f);
            DrawLineR(px, R, cx, cy - 4f*s, cx,          cy - 22f*s, 4.5f*s*2f, legC); // center leg
            DrawLineR(px, R, cx, cy - 4f*s, cx - 18f*s,  cy - 22f*s, 4.0f*s*2f, legC); // left leg
            DrawLineR(px, R, cx, cy - 4f*s, cx + 18f*s,  cy - 22f*s, 4.0f*s*2f, legC); // right leg
            P3DEllipseGlow(px, R, cx,          cy - 22f*s, 5f*s, 2f*s, new Color(p.leg.r, p.leg.g, p.leg.b, 0.65f));
            P3DEllipseGlow(px, R, cx - 18f*s,  cy - 22f*s, 5f*s, 2f*s, new Color(p.leg.r, p.leg.g, p.leg.b, 0.55f));
            P3DEllipseGlow(px, R, cx + 18f*s,  cy - 22f*s, 5f*s, 2f*s, new Color(p.leg.r, p.leg.g, p.leg.b, 0.55f));
            // Body
            P3DSphere(px, R, cx, cy, 18f*s, p.body,    p.metallic,       p.smoothness,       L, H, fillL);
            P3DSphere(px, R, cx, cy, 12f*s, p.shoulder, p.metallic + .1f, p.smoothness + .1f, L, H, fillL);
            P3DEllipseGlow(px, R, cx, cy, 8f*s, 8f*s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.40f));
            // Twin barrels
            if (part != 1)
            {
                P3DEllipseGlow(px, R, cx + 24f*s, cy + 5f*s, 12f*s, 4f*s, new Color(p.arm.r, p.arm.g, p.arm.b, 0.80f));
                P3DEllipseGlow(px, R, cx + 24f*s, cy - 5f*s, 12f*s, 4f*s, new Color(p.arm.r, p.arm.g, p.arm.b, 0.80f));
                P3DSphere(px, R, cx + 36f*s, cy + 5f*s, 5f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
                P3DSphere(px, R, cx + 36f*s, cy - 5f*s, 5f*s, p.arm, p.metallic, p.smoothness, L, H, fillL);
            }
        }

        // Kneeling sniper — crouched body, rifle extended right, scope glow at muzzle
        // part: 0 full · 1 body only · 2 rifle assembly only
        // Marksman — side rig with the rifle baked to the weapon hand. It used to be locked in a
        // permanent kneel with no pose frames, so it slid down the lane frozen while every other
        // infantry unit walked. The rifle stays horizontal regardless of pose, so the arm frames
        // read as carry -> brace -> fire rather than swinging the barrel around.
        static void P3DBuildSniper(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            P3DSideRig(px, R, p, L, H, fillL, pose, 7.0f, 11f, 14f, 104f, false, out var nHand, out _);
            // Rifle: horizontal from the hand, with scope and muzzle glow
            float bx0 = nHand.x - 5f, bx1 = nHand.x + 32f, by = nHand.y;
            Color steel = new Color(p.arm.r * 0.75f, p.arm.g * 0.78f, p.arm.b * 0.85f);
            P3DLimb  (px,R, bx0*s, by*s, bx1*s, by*s, 3.0f*s, steel, p.metallic, p.smoothness, L,H,fillL);
            P3DSphere(px,R, (nHand.x + 9f)*s, (by + 6f)*s, 3.6f*s, p.shoulder, p.metallic, p.smoothness, L,H,fillL);
            P3DEllipseGlow(px,R, bx1*s, by*s, 6f*s, 5f*s,
                new Color(p.visor.r, p.visor.g, p.visor.b, 0.55f));
        }

        // Shield-bot — compact torso + head + large energy shield plate
        // Shield bot — side rig, heavy stance, big forward barrier on the lead arm.
        static void P3DBuildShieldBot(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            P3DSideRig(px, R, p, L, H, fillL, pose, 8.2f, 14f, 14f, 104f, false, out var nHand, out _);
            // Barrier: a tall rounded slab carried in front, pushed forward on the strike pose
            float bx = nHand.x + (pose == 4 ? 12f : pose == 3 ? 2f : 7f);
            Color plate = new Color(p.shoulder.r*0.9f, p.shoulder.g*0.95f, p.shoulder.b*1.0f);
            P3DLimb(px,R, bx*s, (nHand.y-16f)*s, bx*s, (nHand.y+22f)*s, 7.5f*s, plate, p.metallic, p.smoothness, L,H,fillL);
            P3DEllipseGlow(px,R, bx*s, (nHand.y+3f)*s, 6f*s, 17f*s,
                new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.30f));
        }

        // Hacker — cloaked hood figure with data-tendril arms radiating upward
        // The cloak hides the legs by design, so locomotion reads as the hem SWAYING, not a
        // walk cycle. pose 1/2 billow it side to side; 3/4 lean into the tendril cast.
        static void P3DBuildHacker(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s  = R / 128f;
            float sway = pose == 1 ? 3f : (pose == 2 ? -3f : (Atk(pose) ? 5f : 0f));
            float cx = R * .5f + sway*s, cy = R * .5f - 4f*s;  // body base center
            // Cloak body: wide tapered cylinder (bottom wide, shoulders narrow)
            P3DCylinder(px, R, cx, 14f*s, 54f*s, 18f*s, new Color(p.body.r*.55f, p.body.g*.55f, p.body.b*.55f), p.metallic*.5f, p.smoothness*.6f, L, H, fillL);
            P3DCylinder(px, R, cx, 42f*s, 62f*s, 12f*s, new Color(p.body.r*.65f, p.body.g*.65f, p.body.b*.65f), p.metallic*.6f, p.smoothness*.7f, L, H, fillL);
            // Hood sphere (large, dark with glow rim)
            P3DSphere(px, R, cx, 68f*s, 14f*s, new Color(p.body.r*.45f, p.body.g*.45f, p.body.b*.5f), p.metallic*.5f, p.smoothness*.6f, L, H, fillL);
            P3DEllipseGlow(px, R, cx, 68f*s, 17f*s, 17f*s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.28f));
            // Eye glow inside hood
            P3DSphere(px, R, cx, 68f*s, 5f*s, Color.white, 1f, 0.98f, L, H, fillL);
            P3DEllipseGlow(px, R, cx, 68f*s, 8f*s, 8f*s, new Color(p.visorGlow.r, p.visorGlow.g, p.visorGlow.b, 0.72f));
            // Outer ambient glow
            P3DEllipseGlow(px, R, cx, cy + 8f*s, 30f*s, 30f*s, new Color(p.body.r, p.body.g, p.body.b, 0.22f));
            // Data tendrils — upward fan angles (not symmetric scatter)
            float[] tendAngles = { 90f, 50f, 130f, 160f };
            float[] tendDists  = { 32f, 28f,  28f,  24f };
            for (int i = 0; i < 4; i++)
            {
                float ta = tendAngles[i] * Mathf.Deg2Rad;
                float tx = cx + Mathf.Cos(ta) * tendDists[i] * s;
                float ty = cy + Mathf.Sin(ta) * tendDists[i] * s;
                P3DEllipseGlow(px, R, (cx + tx) * .5f, (cy + ty) * .5f,
                    Mathf.Abs(tx - cx) * .5f + s, Mathf.Abs(ty - cy) * .5f + s,
                    new Color(p.visor.r, p.visor.g, p.visor.b, 0.40f));
                P3DSphere(px, R, tx, ty, 4f*s, p.visor, 0.70f, 0.85f, L, H, fillL);
            }
        }

        // Titan — massive armoured mech: heavy tracks, wide hull, dual shoulder cannons, head
        // part: 0 full · 1 body only · 2 cannons only  ·  pose: alternating track roll
        // SIDE-FACING siege walker, rebuilt. The old titan was a symmetric front view -- twin
        // shoulders at cx+/-30 and twin barrels at cx+/-58 -- so it faced the camera while
        // marching sideways, and swinging its legs only read as the stance splaying open.
        // Now the whole chassis faces +x like every other walker: legs stride near/far, the
        // near arm and barrel carry over the far ones, and both guns point down-lane.
        // part: 0 full - 1 chassis only - 2 barrels only   pose: 0 idle 1/2 stride 3 brace 4 recoil
        static void P3DBuildTitan(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int part = 0, int pose = 0)
        {
            float s = R / 128f;
            float cx = R * .5f;
            Color far = new Color(p.leg.r*0.60f, p.leg.g*0.60f, p.leg.b*0.60f, p.leg.a);
            Color farArm = new Color(p.arm.r*0.58f, p.arm.g*0.58f, p.arm.b*0.58f, 1f);
            Color barrel = new Color(p.arm.r*0.82f, p.arm.g*0.82f, p.arm.b*0.82f, 1f);

            // Recoil shifts the whole upper chassis back along -x.
            // Attack frames are exaggerated: this is a 500 HP siege platform and its shot has to
            // land as an event. Pose 3 hauls the chassis back and squats; pose 4 slams it forward.
            float hullDx = pose == 3 ? -12f : (pose == 4 ? 10f : 0f);
            float hullDy = pose == 3 ?  -4f : (pose == 4 ? -2f : 0f);

            if (part == 2)
            {
                // Barrels only, both pointing forward, near over far.
                DrawLineR(px, R, (cx + 6f + hullDx)*s, (74f + hullDy)*s, (cx + 46f + hullDx)*s, (72f + hullDy)*s, 5f*s*2f, farArm);
                P3DEllipseGlow(px, R, (cx + 46f + hullDx)*s, (72f + hullDy)*s, 4.5f*s, 4.5f*s,
                    new Color(p.arm.r, p.arm.g, p.arm.b, 0.55f));
                DrawLineR(px, R, (cx + 8f + hullDx)*s, (82f + hullDy)*s, (cx + 54f + hullDx)*s, (80f + hullDy)*s, 6f*s*2f, barrel);
                P3DEllipseGlow(px, R, (cx + 54f + hullDx)*s, (80f + hullDy)*s, 5.5f*s, 5.5f*s,
                    new Color(p.arm.r*1.2f, p.arm.g*1.2f, p.arm.b*1.2f, 0.85f));
                return;
            }

            // Stride: near and far leg swing fore/aft about the hip.
            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy, sway;
            // Pose 1 is CONTACT (legs spread, both feet planted); pose 2 is PASSING (the rear
            // leg swings through with the knee raised and the foot lifted clear of the ground).
            // These two used to be the same positions with near/far swapped, which changed the
            // shading but not the outline -- so a walking heavy looked like it was flickering
            // rather than stepping.
            if (pose == 1)      { nKx =  20f; nKy = 26f; nFx =  32f; nFy =  8f;
                                  fKx = -18f; fKy = 24f; fFx = -28f; fFy =  8f; sway =  3f; }
            else if (pose == 2) { nKx =  -6f; nKy = 32f; nFx = -12f; nFy = 18f;
                                  fKx =  16f; fKy = 25f; fFx =  28f; fFy =  8f; sway = -1f; }
            else if (pose == 6) { nKx = -18f; nKy = 24f; nFx = -28f; nFy =  8f;
                                  fKx =  20f; fKy = 26f; fFx =  32f; fFy =  8f; sway = -3f; }
            else if (pose == 7) { nKx =  16f; nKy = 25f; nFx =  28f; nFy =  8f;
                                  fKx =  -6f; fKy = 32f; fFx = -12f; fFy = 18f; sway =  1f; }
            else if (pose == 3) { nKx =  20f; nKy = 24f; nFx =  30f; nFy =  8f;
                                  fKx = -20f; fKy = 24f; fFx = -30f; fFy =  8f; sway =  0f; }
            else if (pose == 4) { nKx =  22f; nKy = 23f; nFx =  32f; nFy =  8f;
                                  fKx = -22f; fKy = 23f; fFx = -32f; fFy =  8f; sway =  0f; }
            else                { nKx =  12f; nKy = 25f; nFx =  14f; nFy =  8f;
                                  fKx = -12f; fKy = 25f; fFx = -14f; fFy =  8f; sway =  0f; }

            P3DEllipseGlow(px, R, cx, R * .5f, 36f*s, 30f*s, new Color(p.body.r, p.body.g, p.body.b, 0.18f));

            // far leg
            P3DLimb  (px, R, (cx - 6f)*s, 42f*s, (cx + fKx)*s, fKy*s, 10.5f*s, far, p.metallic*.55f, p.smoothness*.65f, L, H, fillL);
            P3DLimb  (px, R, (cx + fKx)*s, fKy*s, (cx + fFx)*s, fFy*s, 9f*s,   far, p.metallic*.55f, p.smoothness*.65f, L, H, fillL);
            P3DSphere(px, R, (cx + fFx)*s, fFy*s, 11f*s, far, p.metallic, p.smoothness, L, H, fillL);
            // near leg
            P3DLimb  (px, R, (cx + 6f)*s, 42f*s, (cx + nKx)*s, nKy*s, 12f*s, p.leg, p.metallic*.6f, p.smoothness*.7f, L, H, fillL);
            P3DLimb  (px, R, (cx + nKx)*s, nKy*s, (cx + nFx)*s, nFy*s, 10f*s, p.leg, p.metallic*.6f, p.smoothness*.7f, L, H, fillL);
            P3DSphere(px, R, (cx + nKx)*s, nKy*s, 10.5f*s, p.leg, p.metallic, p.smoothness, L, H, fillL);
            P3DSphere(px, R, (cx + nFx)*s, nFy*s, 12.5f*s, p.leg, p.metallic, p.smoothness, L, H, fillL);

            float hx = cx + hullDx;
            // far arm behind the hull
            P3DSphere(px, R, (hx - 8f)*s, (76f + hullDy + sway*0.3f)*s, 11f*s, farArm, p.metallic*.7f, p.smoothness*.7f, L, H, fillL);
            if (part != 1)
            {
                DrawLineR(px, R, (hx + 6f)*s, (74f + hullDy)*s, (hx + 46f)*s, (72f + hullDy)*s, 5f*s*2f, farArm);
                P3DEllipseGlow(px, R, (hx + 46f)*s, (72f + hullDy)*s, 4.5f*s, 4.5f*s,
                    new Color(p.arm.r, p.arm.g, p.arm.b, 0.55f));
            }

            // chassis: deep torso in profile, not a wide slab
            P3DCylinder(px, R, hx, (34f + hullDy)*s, (74f + hullDy)*s, 21f*s, p.body, p.metallic, p.smoothness, L, H, fillL);
            P3DSphere  (px, R, hx, (74f + hullDy)*s, 21f*s, p.body, p.metallic, p.smoothness, L, H, fillL);
            P3DChestOverlay(px, R, (hx + 8f)*s, (56f + hullDy)*s, 12f*s, 18f*s, p.chest, p.metallic, p.smoothness, H);

            // near shoulder + forward barrel, carried over the hull
            P3DSphere(px, R, (hx + 8f)*s, (82f + hullDy - sway*0.3f)*s, 13.5f*s, p.shoulder, p.metallic, p.smoothness, L, H, fillL);
            if (part != 1)
            {
                DrawLineR(px, R, (hx + 8f)*s, (82f + hullDy)*s, (hx + 54f)*s, (80f + hullDy)*s, 6f*s*2f, barrel);
                P3DEllipseGlow(px, R, (hx + 54f)*s, (80f + hullDy)*s, 5.5f*s, 5.5f*s,
                    new Color(p.arm.r*1.2f, p.arm.g*1.2f, p.arm.b*1.2f, 0.85f));
            }

            // sensor head set forward on the shoulders, visor facing down-lane
            P3DSphere(px, R, (hx + 4f)*s, (96f + hullDy)*s, 11f*s, p.head, p.metallic, p.smoothness, L, H, fillL);
            P3DVisor (px, R, (hx + 9f)*s, (97f + hullDy)*s, 8f*s, p.visor, p.visorGlow);
        }

        // ── 3-D unit preview (software-rendered pseudo-3D for Armory cards) ────

        /// <summary>
        /// Software-rendered 3-D preview for the Armory skin cards.
        /// artId is ThemeLocale.ArtId("trooper") so the silhouette matches the active theme's troop.
        /// skinIdx is the TroopSkin enum value and controls material (colour, metallic, emission).
        /// </summary>
        public static Texture2D UnitPreview3D(string artId, int skinIdx)
            => Get($"prev3d_{artId}_{skinIdx}", () => BuildUnitPreview3D(artId, skinIdx));

        struct P3DP
        {
            public Color head, body, leg, arm, shoulder, chest, platform, visor, visorGlow, emission;
            public float metallic, smoothness, emissionStr;
        }

        static P3DP SkinParams(int idx)
        {
            switch (idx)
            {
                case 1: { // GOLDEN
                    var g = new Color(1.00f, 0.78f, 0.08f);
                    return new P3DP { head=g, body=g, leg=new Color(0.82f,0.60f,0.05f),
                        arm=g, shoulder=Mix(g,Color.white,0.28f), chest=Mix(g,Color.white,0.45f),
                        platform=new Color(0.50f,0.38f,0.06f),
                        visor=new Color(1f,0.95f,0.55f), visorGlow=new Color(1f,0.85f,0.20f),
                        emission=new Color(1f,0.80f,0.18f), metallic=0.92f, smoothness=0.90f, emissionStr=0.16f }; }
                case 2: { // CHROME
                    var c = new Color(0.68f,0.76f,0.90f);
                    return new P3DP { head=c, body=c, leg=new Color(0.52f,0.60f,0.75f),
                        arm=c, shoulder=Color.white, chest=Mix(c,Color.white,0.55f),
                        platform=new Color(0.28f,0.32f,0.40f),
                        visor=new Color(0.72f,0.88f,1f), visorGlow=new Color(0.50f,0.78f,1f),
                        emission=new Color(0.65f,0.82f,1f), metallic=1.0f, smoothness=0.97f, emissionStr=0.08f }; }
                case 3: { // INFERNO
                    var f = new Color(0.48f,0.07f,0.02f);
                    return new P3DP { head=f, body=f, leg=new Color(0.30f,0.04f,0.01f),
                        arm=f, shoulder=new Color(0.58f,0.09f,0.02f), chest=new Color(0.22f,0.04f,0.01f),
                        platform=new Color(0.12f,0.02f,0.01f),
                        visor=new Color(1f,0.48f,0.06f), visorGlow=new Color(1f,0.28f,0.00f),
                        emission=new Color(1f,0.35f,0.02f), metallic=0.12f, smoothness=0.20f, emissionStr=0.38f }; }
                case 4: { // PHANTOM
                    var p = new Color(0.30f,0.10f,0.55f);
                    return new P3DP { head=p, body=p, leg=new Color(0.20f,0.06f,0.38f),
                        arm=p, shoulder=new Color(0.42f,0.16f,0.72f), chest=new Color(0.16f,0.05f,0.30f),
                        platform=new Color(0.10f,0.03f,0.20f),
                        visor=new Color(0.74f,0.46f,1f), visorGlow=new Color(0.65f,0.36f,1f),
                        emission=new Color(0.65f,0.36f,1f), metallic=0.45f, smoothness=0.65f, emissionStr=0.38f }; }
                case 5: { // COSMIC
                    var o = new Color(0.18f,0.04f,0.42f);
                    return new P3DP { head=o, body=o, leg=new Color(0.12f,0.02f,0.30f),
                        arm=o, shoulder=new Color(0.28f,0.08f,0.60f), chest=new Color(0.10f,0.02f,0.26f),
                        platform=new Color(0.06f,0.01f,0.16f),
                        visor=new Color(0.38f,0.78f,1f), visorGlow=new Color(0.22f,0.64f,1f),
                        emission=new Color(0.32f,0.68f,1f), metallic=0.55f, smoothness=0.78f, emissionStr=0.42f }; }
                default: { // DEFAULT
                    var d = new Color(0.14f,0.52f,0.88f);
                    return new P3DP { head=d, body=d, leg=new Color(0.09f,0.37f,0.65f),
                        arm=d, shoulder=Mix(d,Color.white,0.22f), chest=Mix(d,Color.white,0.32f),
                        platform=new Color(0.16f,0.20f,0.28f),
                        visor=new Color(0.50f,0.92f,1f), visorGlow=new Color(0.28f,0.84f,1f),
                        emission=new Color(0.28f,0.84f,1f), metallic=0.35f, smoothness=0.58f, emissionStr=0.10f }; }
            }
        }

        static Texture2D BuildUnitPreview3D(string artId, int skinIdx)
        {
            const int R = 128;
            var px = new Color[R * R];
            var p  = SkinParams(skinIdx);

            // Light rig — y-up to match Unity texture convention (y=0 bottom)
            var L     = new Vector3(-0.45f, 0.70f, 0.55f).normalized;
            var H     = (L + Vector3.forward).normalized;   // Blinn half-vector
            var fillL = new Vector3(0.38f, 0.08f, 0.50f).normalized;

            // Ground shadow + platform (shared by all units)
            P3DEllipseGlow(px, R, 64, 14, 28f, 7f, new Color(0f, 0f, 0f, 0.65f));
            P3DPlatform(px, R, 64, 14, p.platform);

            // Dispatch silhouette to theme-specific unit builder
            switch (artId)
            {
                case "knight": P3DBuildKnight  (px, R, p, L, H, fillL, 0); break;
                case "worker": P3DBuildWorker  (px, R, p, L, H, fillL, 0); break;
                case "wanderer": P3DBuildWanderer(px, R, p, L, H, fillL, 0); break;
                default:       P3DBuildTrooper (px, R, p, L, H, fillL, 0); break;
            }

            // Skin emission / shadow tint
            if (skinIdx == 3) P3DInfernoOverlay(px, R, p.emission);
            else if (p.emissionStr > 0.01f) P3DShadowTint(px, R, p.emission, p.emissionStr);

            var tex = new Texture2D(R, R, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ── per-unit 3-D body builders ────────────────────────────────────────

        // Sci-fi humanoid — trooper, racer, guardian, mutant, kunoichi, and all fallbacks
        // ── shared side-profile humanoid rig ────────────────────────────────────
        // Facing +x (enemies get a uv mirror). One joint table drives every humanoid so
        // walkers stay consistent; each unit passes its own proportions and draws its own
        // props at the returned hand positions.
        // pose: 0 idle · 1 near-leg forward · 2 near-leg back · 3 attack windup · 4 attack strike
        // cols: nKnee xy, nFoot xy, fKnee xy, fFoot xy, nElb xy, nHand xy, fElb xy, fHand xy
        // ── walk phases ──────────────────────────────────────────────────────
        // The pose contract is 0 idle · 1,2,6,7 walk · 3 windup · 4 strike · 5 flinch.
        // Phases 6 and 7 are the SECOND HALF of the stride, added so a walk is a real
        // four-beat loop (contact, pass, contact on the other side, pass) instead of two
        // poses swapping. They are numbered 6/7 rather than 3/4 so every existing attack
        // and flinch index keeps its meaning.

        /// <summary>Builders that author all four walk phases.</summary>
        static int Pose8(int pose) => Mathf.Clamp(pose, 0, 7);

        /// <summary>Builders that only authored the original six poses: fold the two extra
        /// walk phases back onto their existing walk frames so they behave exactly as before.</summary>
        static int Pose6(int pose)
        {
            // Written out longhand ON PURPOSE. The body was originally Mathf.Clamp(pose, 0, 5),
            // and the same script that added this helper then ran a blanket replace of that exact
            // string with "Pose6(pose)" -- rewriting this method into a call to itself. Every
            // builder that folds phases 6/7 blew the stack, which took out the troop chips, the
            // demo panel and the battle screen. No clampable expression here for a text pass to
            // catch a second time.
            if (pose >= 6) return pose - 5;
            if (pose < 0)  return 0;
            if (pose > 5)  return 5;
            return pose;
        }

        /// <summary>Windup or strike. Replaces the old greater-or-equal-3 test, which silently became
        /// true for walk phases 6 and 7 and would have drawn every walking unit mid-attack.</summary>
        static bool Atk(int q) => q == 3 || q == 4;

        static readonly float[,] SideJoints = new float[8, 16]
        {
            { 66,38, 67,19, 60,38, 59,19, 66,73, 68,63, 60,73, 59,63 },
            // Walk rows 1 and 2 used to hold the SAME four leg positions with near and far
            // simply traded, so the silhouette was pixel-identical between frames and only the
            // shading flipped -- the legs never appeared to move. Row 1 is now a CONTACT pose
            // (legs spread, both feet planted) and row 2 a PASSING pose (rear leg swinging
            // through with the knee raised and the foot 8 units OFF THE GROUND). The legs still
            // alternate, so there is no limp, but the outline genuinely changes shape.
            // 1 — contact, near foot leads, both planted. Arms at full swing.
            { 73,38, 80,19, 56,36, 48,19, 57,73, 52,65, 70,73, 76,66 },
            // 2 — pass, near leg swings through with the knee up and the foot off the ground
            { 60,42, 56,27, 68,37, 74,19, 64,73, 65,64, 61,73, 60,64 },
            { 58,38, 54,19, 68,38, 72,19, 54,80, 48,74, 66,74, 70,70 },
            { 70,39, 76,20, 56,38, 50,19, 74,82, 86,82, 56,74, 52,68 },
            // 5 — flinch: braced back on the rear foot, both arms thrown up and back
            { 58,37, 52,19, 66,38, 70,19, 58,79, 53,84, 68,78, 73,82 },
            // 6 — contact mirrored: FAR foot leads. This is the beat a two-frame cycle
            //     could never hold, and the reason the old walk read as a toggle.
            { 56,36, 48,19, 73,38, 80,19, 70,73, 76,66, 57,73, 52,65 },
            // 7 — pass mirrored: far leg swings through lifted
            { 68,37, 74,19, 60,42, 56,27, 64,73, 65,64, 61,73, 60,64 },
        };

        /// <summary>True when this art id uses the side-profile rig (walk + attack poses).</summary>
        static bool UsesSideRig(string id)
            => id is "trooper" or "racer" or "mutant" or "guardian"
                  or "knight" or "worker" or "shield-bot" or "sniper"
                  or "archer" or "paladin"
                  or "guardian" or "raycaster" or "aegis"
                  or "mutant" or "stinger" or "carapace"
                  or "gunner" or "bulkhead" or "engineer"
                  or "seeker" or "ward"
                  or "shinobi" or "yumi" or "shrine"
                  or "synracer" or "synlaser" or "synbouncer" or "synkeytar"
                  or "cybtrooper" or "cybsniper" or "cybshield" or "cybhacker";

        static void P3DSideRig(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int pose, float legR, float torsoR, float headR, float headY, bool backpack,
            out Vector2 nHand, out Vector2 fHand)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            float J(int c) => SideJoints[q, c];

            Color farLeg = new Color(p.leg.r * 0.60f, p.leg.g * 0.60f, p.leg.b * 0.60f, p.leg.a);
            Color farArm = new Color(p.arm.r * 0.60f, p.arm.g * 0.60f, p.arm.b * 0.60f, p.arm.a);

            const float HIPX = 63f, HIPY = 56f, SHX = 63f, SHY = 84f;
            nHand = new Vector2(J(10), J(11));
            fHand = new Vector2(J(14), J(15));

            // far side (behind the torso)
            P3DLimb  (px,R, SHX*s, SHY*s, J(12)*s, J(13)*s, legR*0.66f*s, farArm, p.metallic*.8f, p.smoothness*.85f, L,H,fillL);
            P3DLimb  (px,R, J(12)*s, J(13)*s, J(14)*s, J(15)*s, legR*0.58f*s, farArm, p.metallic*.8f, p.smoothness*.85f, L,H,fillL);
            P3DLimb  (px,R, HIPX*s, HIPY*s, J(4)*s, J(5)*s, legR*0.92f*s, farLeg, p.metallic*.7f, p.smoothness*.8f, L,H,fillL);
            P3DLimb  (px,R, J(4)*s, J(5)*s, J(6)*s, J(7)*s, legR*0.79f*s, farLeg, p.metallic*.7f, p.smoothness*.8f, L,H,fillL);
            P3DSphere(px,R, J(6)*s, J(7)*s, legR*0.86f*s, farLeg, p.metallic, p.smoothness, L,H,fillL);

            // near leg
            P3DLimb  (px,R, HIPX*s, HIPY*s, J(0)*s, J(1)*s, legR*s, p.leg, p.metallic*.8f, p.smoothness*.85f, L,H,fillL);
            P3DLimb  (px,R, J(0)*s, J(1)*s, J(2)*s, J(3)*s, legR*0.84f*s, p.leg, p.metallic*.8f, p.smoothness*.85f, L,H,fillL);
            P3DSphere(px,R, J(0)*s, J(1)*s, legR*0.84f*s, p.leg, p.metallic, p.smoothness, L,H,fillL);
            P3DSphere(px,R, J(2)*s, J(3)*s, legR*0.92f*s, p.leg, p.metallic, p.smoothness, L,H,fillL);

            // torso
            if (backpack)
                P3DSphere(px,R, 55f*s, 79f*s, torsoR*0.66f*s, farLeg, p.metallic*.7f, p.smoothness*.7f, L,H,fillL);
            P3DCylinder(px,R, HIPX*s, 54f*s, 66f*s, torsoR*0.83f*s, p.body, p.metallic, p.smoothness, L,H,fillL);
            P3DCylinder(px,R, HIPX*s, 64f*s, 88f*s, torsoR*s,        p.body, p.metallic, p.smoothness, L,H,fillL);
            P3DSphere  (px,R, HIPX*s, 88f*s, torsoR*s,               p.body, p.metallic, p.smoothness, L,H,fillL);
            P3DChestOverlay(px,R, 67f*s, 76f*s, torsoR*0.58f*s, 12f*s, p.chest, p.metallic, p.smoothness, H);

            // near arm over the torso
            P3DSphere(px,R, SHX*s, SHY*s, torsoR*0.75f*s, p.shoulder, p.metallic, p.smoothness, L,H,fillL);
            P3DLimb  (px,R, SHX*s, SHY*s, J(8)*s, J(9)*s, legR*0.71f*s, p.arm, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DLimb  (px,R, J(8)*s, J(9)*s, J(10)*s, J(11)*s, legR*0.63f*s, p.arm, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DSphere(px,R, J(10)*s, J(11)*s, legR*0.61f*s, p.arm, p.metallic, p.smoothness, L,H,fillL);

            // neck + head; visor sits on the FRONT of the skull
            P3DCylinder(px,R, 64f*s, (headY-20f)*s, (headY-12f)*s, 5.0f*s, p.body, p.metallic, p.smoothness, L,H,fillL);
            float headX = q == 5 ? 62f : 67f;   // flinch snaps the head back off the blow
            P3DSphere  (px,R, headX*s, headY*s, headR*s, p.head, p.metallic, p.smoothness, L,H,fillL);
            P3DVisor   (px,R, (headX+headR*0.34f)*s, (headY+1f)*s, headR*0.70f*s, p.visor, p.visorGlow);
        }

        // Standard infantry — the reference build for the side rig.
        static void P3DBuildTrooper(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            P3DSideRig(px, R, p, L, H, fillL, pose, 7.6f, 12f, 15f, 106f, true, out _, out _);
        }

        // Heavy-armored knight — Medieval theme
        // ── ARCHER — medieval Sniper, authored art ─────────────────────────────
        // Longbow held across the body: drawn on the windup, loosed on the strike. Light kit
        // (no plate) so it reads as fragile next to a knight, with a livery hood and quiver.
        static void P3DBuildArcher(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color cloth   = ArtLivery(p);
            Color clothDk = ArtLiveryDk(p);
            Color leather = ArtLeather;
            Color leatherDk = new Color(ArtLeather.r*0.45f, ArtLeather.g*0.45f, ArtLeather.b*0.45f);
            Color wood    = new Color(0.46f, 0.32f, 0.17f);
            Color steel   = ArtSteelHi(p);
            Color farDk   = ArtSteelFar(p);

            Vector2 hip = V(63, 56), shN = V(64, 84), shF = V(56, 84);
            Vector2 nKnee = V(J(0), J(1)), nFoot = V(J(2), J(3));
            Vector2 fKnee = V(J(4), J(5)), fFoot = V(J(6), J(7));
            Vector2 nElb  = V(J(8), J(9)), nHand = V(J(10), J(11));
            Vector2 fElb  = V(J(12), J(13)), fHand = V(J(14), J(15));
            Vector2 head  = V(q == 5 ? 61 : 66, 104);

            // far limbs
            P3DLimb(px,R, shF.x, shF.y, fElb.x, fElb.y, 4.6f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fElb.x, fElb.y, fHand.x, fHand.y, 4.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x, hip.y, fKnee.x, fKnee.y, 6.2f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fKnee.x, fKnee.y, fFoot.x, fFoot.y, 5.4f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            // near leg + boot
            P3DLimb(px,R, hip.x, hip.y, nKnee.x, nKnee.y, 6.8f*s, leatherDk, 0.2f, 0.3f, L,H,fillL);
            P3DLimb(px,R, nKnee.x, nKnee.y, nFoot.x, nFoot.y, 5.8f*s, leatherDk, 0.2f, 0.3f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(nFoot.x-7f*s,nFoot.y+3f*s), new Vector2(nFoot.x+8f*s,nFoot.y+3f*s),
                                  new Vector2(nFoot.x+6f*s,nFoot.y-4f*s), new Vector2(nFoot.x-6f*s,nFoot.y-4f*s) },
                     leather, leatherDk, 1.6f);
            // quiver on the back
            P3DPlate(px,R, new[]{ V(50,86), V(58,88), V(62,62), V(54,60) }, leather, leatherDk, 1.8f);
            for (int i = 0; i < 3; i++)
                DrawLineR(px,R, (52f+i*3f)*s, 88f*s, (53f+i*3f)*s, 98f*s, 1.5f*s, wood);
            // jerkin
            P3DPlate(px,R, new[]{ V(54,88), V(72,88), V(74,70), V(68,56), V(58,56), V(52,70) },
                     cloth, clothDk, 2.2f);
            DrawLineR(px,R, 52f*s, 74f*s, 74f*s, 80f*s, 3f*s, leather);   // baldric
            // near arm
            P3DLimb(px,R, shN.x, shN.y, nElb.x, nElb.y, 5.0f*s, cloth, p.metallic*.6f, p.smoothness*.7f, L,H,fillL);
            P3DLimb(px,R, nElb.x, nElb.y, nHand.x, nHand.y, 4.4f*s, cloth, p.metallic*.6f, p.smoothness*.7f, L,H,fillL);
            // hood
            P3DPlate(px,R, new[]{
                new Vector2(head.x-10f*s, head.y-10f*s), new Vector2(head.x+10f*s, head.y-10f*s),
                new Vector2(head.x+9f*s, head.y+8f*s), new Vector2(head.x-2f*s, head.y+13f*s),
                new Vector2(head.x-11f*s, head.y+6f*s)
            }, cloth, clothDk, 2f);
            P3DEllipseGlow(px,R, head.x+4f*s, head.y-1f*s, 3f*s, 2.6f*s, new Color(1f,0.88f,0.5f,0.9f));

            // longbow: held forward, string drawn back on the windup, loosed on the strike
            {
                float draw = q == 3 ? 11f : (q == 4 ? -2f : 5f);
                Vector2 grip = Vector2.Lerp(nHand, V(78, 78), 0.55f);
                Vector2 top = new Vector2(grip.x + 4f*s, grip.y + 30f*s);
                Vector2 bot = new Vector2(grip.x + 4f*s, grip.y - 30f*s);
                // limbs of the bow
                DrawLineR(px,R, grip.x+9f*s, grip.y, top.x, top.y, 3.2f*s, wood);
                DrawLineR(px,R, grip.x+9f*s, grip.y, bot.x, bot.y, 3.2f*s, wood);
                // string, pulled back by `draw`
                Vector2 nock = new Vector2(grip.x - draw*s, grip.y);
                DrawLineR(px,R, top.x, top.y, nock.x, nock.y, 1.2f*s, new Color(0.85f,0.83f,0.75f));
                DrawLineR(px,R, bot.x, bot.y, nock.x, nock.y, 1.2f*s, new Color(0.85f,0.83f,0.75f));
                if (q == 4)   // arrow away
                {
                    DrawLineR(px,R, (grip.x+16f*s), grip.y, (grip.x+44f*s), grip.y, 1.8f*s, wood);
                    P3DPlate(px,R, new[]{ new Vector2(grip.x+44f*s, grip.y+3f*s),
                                          new Vector2(grip.x+52f*s, grip.y),
                                          new Vector2(grip.x+44f*s, grip.y-3f*s) }, steel, farDk, 1.2f);
                }
                else
                {
                    DrawLineR(px,R, nock.x, nock.y, (grip.x+18f*s), grip.y, 1.6f*s, wood);
                    P3DPlate(px,R, new[]{ new Vector2(grip.x+18f*s, grip.y+3f*s),
                                          new Vector2(grip.x+25f*s, grip.y),
                                          new Vector2(grip.x+18f*s, grip.y-3f*s) }, steel, farDk, 1.2f);
                }
            }
        }

        // ── PALADIN — medieval Shield-bot, authored art ────────────────────────
        // The tower shield IS the silhouette, per the audit: broad, short, and the barrier
        // dominates the outline. Heavy plate, winged helm, mace behind the shield.
        static void P3DBuildPaladin(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color steelHi = ArtSteelHi(p), steel = ArtSteelBase(p);
            Color steelDk = ArtSteelDk(p), farDk = ArtSteelFar(p);
            Color livery = ArtLivery(p), liveryDk = ArtLiveryDk(p);

            Vector2 hip = V(62, 54), shN = V(63, 82), shF = V(55, 82);
            Vector2 nKnee = V(J(0)-2f, J(1)), nFoot = V(J(2)-2f, J(3));
            Vector2 fKnee = V(J(4)-2f, J(5)), fFoot = V(J(6)-2f, J(7));
            Vector2 nElb  = V(J(8), J(9)), nHand = V(J(10), J(11));
            Vector2 head  = V(q == 5 ? 58 : 63, 100);

            // far limbs + legs (short and thick)
            P3DLimb(px,R, shF.x, shF.y, V(J(12),J(13)).x, V(J(12),J(13)).y, 5.6f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x, hip.y, fKnee.x, fKnee.y, 8.4f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fKnee.x, fKnee.y, fFoot.x, fFoot.y, 7.4f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x, hip.y, nKnee.x, nKnee.y, 9.4f*s, steelDk, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px,R, nKnee.x, nKnee.y, nFoot.x, nFoot.y, 8.2f*s, steelDk, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            // sabatons
            foreach (var f in new[]{ nFoot, fFoot })
                P3DPlate(px,R, new[]{ new Vector2(f.x-9f*s,f.y+4f*s), new Vector2(f.x+10f*s,f.y+4f*s),
                                      new Vector2(f.x+8f*s,f.y-5f*s), new Vector2(f.x-8f*s,f.y-5f*s) },
                         steelDk, farDk, 1.8f);
            // broad cuirass
            P3DPlate(px,R, new[]{ V(48,86), V(76,86), V(79,66), V(72,50), V(52,50), V(45,66) },
                     steelHi, steel, 2.4f);
            DrawLineR(px,R, 46f*s, 76f*s, 78f*s, 76f*s, 2.6f*s, ArtGold);
            DrawLineR(px,R, 47f*s, 62f*s, 77f*s, 68f*s, 3.2f*s, livery);      // livery sash
            // winged great-helm
            P3DPlate(px,R, new[]{
                new Vector2(head.x-11f*s, head.y-11f*s), new Vector2(head.x+11f*s, head.y-11f*s),
                new Vector2(head.x+12f*s, head.y+7f*s), new Vector2(head.x, head.y+13f*s),
                new Vector2(head.x-12f*s, head.y+7f*s)
            }, steelHi, steelDk, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s, head.y-1f*s), new Vector2(head.x+9f*s, head.y-1f*s),
                                  new Vector2(head.x+9f*s, head.y+2.4f*s), new Vector2(head.x-9f*s, head.y+2.4f*s) },
                     new Color(0.05f,0.05f,0.08f), new Color(0.02f,0.02f,0.04f), 1f);
            // helm wings
            foreach (float side in new[]{ -1f, 1f })
                P3DPlate(px,R, new[]{
                    new Vector2(head.x + side*10f*s, head.y+4f*s),
                    new Vector2(head.x + side*22f*s, head.y+12f*s),
                    new Vector2(head.x + side*20f*s, head.y+2f*s)
                }, ArtGoldHi, ArtGold, 1.4f);
            // mace arm behind the shield
            P3DLimb(px,R, shN.x, shN.y, nElb.x, nElb.y, 6f*s, steel, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DLimb(px,R, nElb.x, nElb.y, nHand.x, nHand.y, 5.2f*s, steel, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            {
                float sw = q == 4 ? 18f : (q == 3 ? -10f : 4f);
                DrawLineR(px,R, nHand.x, nHand.y, (nHand.x+sw*s), (nHand.y+18f*s), 3.4f*s, ArtLeather);
                FillCircleR(px,R, (nHand.x+sw*s), (nHand.y+18f*s), 6.4f*s, steelHi);
            }
            // TOWER SHIELD — the silhouette
            {
                float px0 = q == 4 ? 16f : (q == 3 ? 4f : 10f);
                float bx = 64f + px0;
                P3DPlate(px,R, new[]{
                    V(bx-12, 92), V(bx+12, 92), V(bx+14, 58), V(bx+4, 34), V(bx-8, 34), V(bx-14, 58)
                }, steel, steelDk, 2.6f);
                // border + heraldic cross in livery
                P3DPolyLine(px,R, new[]{
                    V(bx-9, 88), V(bx+9, 88), V(bx+11, 58), V(bx+3, 40), V(bx-6, 40), V(bx-11, 58)
                }, ArtGold, 2.2f);
                DrawLineR(px,R, (bx+1)*s, 90f*s, (bx+1)*s, 38f*s, 3.4f*s, livery);
                DrawLineR(px,R, (bx-11)*s, 70f*s, (bx+13)*s, 70f*s, 3.4f*s, livery);
                FillCircleR(px,R, (bx+1)*s, 70f*s, 4.4f*s, ArtGoldHi);
            }
        }

        // ── BALLISTA — medieval Turret, authored art ───────────────────────────
        // A mounted bolt-thrower on a timber trestle. Static by design; the bow arms flex and
        // the bolt loads/looses through the attack frames.
        static void P3DBuildBallista(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color woodHi = new Color(0.60f, 0.44f, 0.25f);
            Color wood   = new Color(0.40f, 0.28f, 0.15f);
            Color woodDk = new Color(0.20f, 0.14f, 0.07f);
            Color iron   = new Color(0.44f, 0.47f, 0.52f);
            Color steel  = ArtSteelHi(p);
            Color livery = ArtLivery(p);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 32f*s, 7f*s, new Color(0f,0f,0f,0.45f));

            // trestle legs
            P3DPlate(px,R, new[]{ V(44,16), V(52,16), V(66,58), V(58,58) }, wood, woodDk, 2.2f);
            P3DPlate(px,R, new[]{ V(78,16), V(86,16), V(70,58), V(62,58) }, woodHi, wood, 2.2f);
            DrawLineR(px,R, 50f*s, 34f*s, 80f*s, 34f*s, 3.2f*s, wood);      // cross brace
            // pivot bed
            P3DPlate(px,R, new[]{ V(52,58), V(78,58), V(76,68), V(54,68) }, woodHi, wood, 2.2f);
            FillCircleR(px,R, 65f*s, 63f*s, 4.4f*s, iron);

            // stock, aimed down-lane
            float recoil = q == 4 ? -7f : (q == 3 ? 3f : 0f);
            float sx = 64f + recoil;
            P3DPlate(px,R, new[]{ V(sx-16, 66), V(sx+34, 70), V(sx+34, 62), V(sx-16, 58) }, woodHi, wood, 2.2f);
            // bow arms — flex back on the windup, snap forward on the loose
            float flex = q == 3 ? 12f : (q == 4 ? -6f : 4f);
            Vector2 hubT = V(sx+24, 78), hubB = V(sx+24, 52);
            DrawLineR(px,R, (sx+22)*s, 66f*s, hubT.x, hubT.y, 4f*s, wood);
            DrawLineR(px,R, (sx+22)*s, 66f*s, hubB.x, hubB.y, 4f*s, wood);
            // string
            Vector2 nock = V(sx - flex, 66);
            DrawLineR(px,R, hubT.x, hubT.y, nock.x, nock.y, 1.4f*s, new Color(0.85f,0.83f,0.75f));
            DrawLineR(px,R, hubB.x, hubB.y, nock.x, nock.y, 1.4f*s, new Color(0.85f,0.83f,0.75f));
            // bolt
            if (q == 4)
            {
                DrawLineR(px,R, (sx+30)*s, 66f*s, (sx+58)*s, 66f*s, 2.2f*s, wood);
                P3DPlate(px,R, new[]{ V(sx+58,69), V(sx+68,66), V(sx+58,63) }, steel, iron, 1.4f);
            }
            else
            {
                DrawLineR(px,R, nock.x, nock.y, (sx+34)*s, 66f*s, 2.2f*s, wood);
                P3DPlate(px,R, new[]{ V(sx+34,69), V(sx+43,66), V(sx+34,63) }, steel, iron, 1.4f);
            }
            // livery pennant on the trestle
            P3DPlate(px,R, new[]{ V(48,58), V(58,56), V(56,42), V(46,44) }, livery, ArtLiveryDk(p), 1.8f);
        }

        // ── SIEGE — medieval Mech, authored art ────────────────────────────────
        // Timber trebuchet on cart wheels. Wheels alternate on the walk frames, the throwing
        // arm hauls back on the windup and releases forward on the strike, counterweight swings
        // opposite. Wood and iron stay fixed; the banner carries the livery.
        static void P3DBuildSiege(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color woodHi = new Color(0.62f, 0.45f, 0.26f);
            Color wood   = new Color(0.42f, 0.29f, 0.16f);
            Color woodDk = new Color(0.22f, 0.15f, 0.08f);
            Color iron   = new Color(0.42f, 0.45f, 0.50f);
            Color ironHi = new Color(0.68f, 0.72f, 0.78f);
            Color livery = ArtLivery(p);

            // roll: the cart rocks and the wheels shift between walk frames
            float roll = q == 1 ? 1.6f : (q == 2 ? -1.6f : 0f);
            float recoil = q == 4 ? -6f : (q == 3 ? 3f : 0f);

            P3DEllipseGlow(px, R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));

            // wheels
            void Wheel(float cx, float rr, bool near)
            {
                Color rim = near ? wood : woodDk;
                FillCircleR(px, R, cx*s, 17f*s, rr*s, rim);
                FillCircleR(px, R, cx*s, 17f*s, (rr-3f)*s, near ? woodDk : new Color(0.14f,0.10f,0.06f));
                FillCircleR(px, R, cx*s, 17f*s, 3.2f*s, iron);
                for (int i = 0; i < 6; i++)
                {
                    // 6 spokes are 60 deg apart, so half a spoke pitch -- the angle of maximum
                    // perceived rotation -- is 30 deg. roll swings +-1.6, so the multiplier has
                    // to be 18.75; at 14 the wheel turned only 22 deg and read as juddering.
                    float a = (i * 60f + roll * 18.75f) * Mathf.Deg2Rad;
                    DrawLineR(px, R, cx*s, 17f*s,
                        (cx + Mathf.Cos(a) * (rr - 2f)) * s, (17f + Mathf.Sin(a) * (rr - 2f)) * s,
                        1.8f*s, near ? woodHi : woodDk);
                }
            }
            Wheel(48f + recoil * 0.3f, 12f, false);
            Wheel(80f + recoil * 0.3f, 13f, true);

            // cart bed + frame
            float bx = 64f + recoil;
            P3DPlate(px, R, new[]{ V(bx-26, 24), V(bx+26, 24), V(bx+24, 40), V(bx-24, 40) }, woodHi, wood, 2.2f);
            DrawLineR(px, R, (bx-24)*s, 32f*s, (bx+24)*s, 32f*s, 1.6f*s, woodDk);
            // A-frame uprights
            P3DPlate(px, R, new[]{ V(bx-12, 38), V(bx-6, 38), V(bx+2, 86), V(bx-4, 86) }, woodHi, wood, 2f);
            P3DPlate(px, R, new[]{ V(bx+10, 38), V(bx+16, 38), V(bx+6, 86), V(bx, 86) }, wood, woodDk, 2f);
            // iron pivot collar
            FillCircleR(px, R, (bx+1)*s, 86f*s, 6.2f*s, iron);
            FillCircleR(px, R, (bx+1)*s, 86f*s, 3.2f*s, ironHi);

            // throwing arm: hauled back on windup, released forward on strike
            float armDeg = q == 3 ? 128f : (q == 4 ? 28f : (q == 5 ? 96f : 74f));
            Vector2 piv = V(bx + 1f, 86f);
            Vector2 dir = RotP(new Vector2(1f, 0f), Vector2.zero, armDeg);
            Vector2 tip = new Vector2(piv.x + dir.x * 44f * s, piv.y + dir.y * 44f * s);
            Vector2 btt = new Vector2(piv.x - dir.x * 20f * s, piv.y - dir.y * 20f * s);
            DrawLineR(px, R, btt.x, btt.y, tip.x, tip.y, 5.6f*s, wood);
            DrawLineR(px, R, btt.x, btt.y, tip.x, tip.y, 2.2f*s, woodHi);
            // counterweight on the short end
            P3DPlate(px, R, new[]{
                new Vector2(btt.x-7f*s, btt.y-7f*s), new Vector2(btt.x+7f*s, btt.y-7f*s),
                new Vector2(btt.x+6f*s, btt.y+7f*s), new Vector2(btt.x-6f*s, btt.y+7f*s)
            }, iron, new Color(0.20f,0.22f,0.26f), 2f);
            // sling + payload
            Vector2 sling = new Vector2(tip.x + dir.x * 4f * s, tip.y + dir.y * 4f * s - 10f * s);
            DrawLineR(px, R, tip.x, tip.y, sling.x, sling.y, 1.4f*s, ArtLeather);
            if (q != 4) FillCircleR(px, R, sling.x, sling.y, 5f*s, new Color(0.38f,0.36f,0.32f));
            else P3DEllipseGlow(px, R, tip.x + 16f*s, tip.y + 6f*s, 9f*s, 8f*s,
                     new Color(livery.r, livery.g, livery.b, 0.55f));

            // livery banner off the rear upright
            P3DPlate(px, R, new[]{
                V(bx-14, 76), V(bx-4, 74), V(bx-6, 56), V(bx-16, 58)
            }, livery, ArtLiveryDk(p), 1.8f);
        }

        // ── ROGUE — medieval Interceptor, authored art ─────────────────────────
        // A cloaked flyer on a bat-wing glider, not a delta craft: membrane wings that beat
        // between frames, hood, and a thrown blade on the attack.
        static void P3DBuildRogue(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color cloak   = ArtLivery(p);
            Color cloakDk = ArtLiveryDk(p);
            Color leather = ArtLeather;
            Color steel   = ArtSteelHi(p);

            // wing beat
            float beat = q == 1 ? 10f : (q == 2 ? -8f : (Atk(q) ? 14f : 0f));

            // far wing
            P3DPlate(px, R, new[]{
                V(64, 64), V(26, 78 + beat), V(20, 66 + beat), V(34, 60), V(48, 58)
            }, cloakDk, new Color(cloakDk.r*0.6f, cloakDk.g*0.6f, cloakDk.b*0.65f), 2f);
            // near wing — membrane with finger struts
            P3DPlate(px, R, new[]{
                V(64, 60), V(30, 74 - beat), V(22, 58 - beat), V(38, 50), V(54, 52)
            }, cloak, cloakDk, 2.2f);
            DrawLineR(px, R, 62f*s, 60f*s, (30f)*s, (74f-beat)*s, 1.6f*s, cloakDk);
            DrawLineR(px, R, 62f*s, 60f*s, (24f)*s, (62f-beat)*s, 1.4f*s, cloakDk);
            DrawLineR(px, R, 62f*s, 60f*s, (38f)*s, (51f)*s, 1.4f*s, cloakDk);

            // body: hooded, tucked
            P3DPlate(px, R, new[]{ V(56, 46), V(74, 48), V(78, 62), V(70, 70), V(56, 66) }, cloak, cloakDk, 2.2f);
            // hood
            P3DPlate(px, R, new[]{ V(70, 62), V(84, 66), V(86, 76), V(76, 80), V(68, 72) }, cloakDk,
                new Color(cloakDk.r*0.55f, cloakDk.g*0.55f, cloakDk.b*0.6f), 2f);
            P3DEllipseGlow(px, R, 80f*s, 71f*s, 3.4f*s, 3.0f*s, new Color(1f, 0.86f, 0.45f, 0.9f));
            // belt + strap
            DrawLineR(px, R, 58f*s, 54f*s, 76f*s, 58f*s, 2.4f*s, leather);

            // thrown blades
            if (Atk(q))
            {
                float th = q == 4 ? 26f : 8f;
                for (int i = 0; i < 2; i++)
                {
                    float bx2 = 88f + th + i * 13f, by2 = 62f - i * 7f;
                    P3DPlate(px, R, new[]{
                        new Vector2((bx2-6)*s,(by2)*s), new Vector2((bx2)*s,(by2+4)*s),
                        new Vector2((bx2+7)*s,(by2)*s), new Vector2((bx2)*s,(by2-4)*s)
                    }, steel, ArtSteelDk(p), 1.4f);
                }
            }
            else
            {
                P3DPlate(px, R, new[]{ V(62,44), V(66,46), V(70,40), V(66,38) }, steel, ArtSteelDk(p), 1.2f);
            }
        }

        // ── PIGEON — medieval Drone, authored art ──────────────────────────────
        // A messenger bird with a scroll, not a quadrotor: wings beat through the frames and
        // the tail fans. Plumage takes the livery so allegiance still reads in the air lane.
        static void P3DBuildPigeon(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color body   = ArtLivery(p);
            Color bodyDk = ArtLiveryDk(p);
            Color wingDk = new Color(bodyDk.r*0.7f, bodyDk.g*0.7f, bodyDk.b*0.78f);
            Color beak   = new Color(0.95f, 0.72f, 0.25f);

            // wing beat: up on 1, down on 2, swept on the dive
            float up = q == 1 ? 16f : (q == 2 ? -12f : (Atk(q) ? -18f : 2f));
            float dive = q == 4 ? -6f : 0f;

            // far wing
            P3DPlate(px, R, new[]{
                V(60, 64 + dive), V(34, 62 + up*0.7f), V(28, 54 + up*0.7f), V(48, 56 + dive)
            }, wingDk, new Color(wingDk.r*0.7f, wingDk.g*0.7f, wingDk.b*0.75f), 1.8f);

            // body
            P3DPlate(px, R, new[]{
                V(48, 56 + dive), V(66, 58 + dive), V(76, 66 + dive), V(70, 74 + dive), V(52, 70 + dive)
            }, body, bodyDk, 2.2f);
            // tail fan
            P3DPlate(px, R, new[]{
                V(50, 68 + dive), V(34, 74 + up*0.3f), V(30, 66 + up*0.3f), V(48, 58 + dive)
            }, bodyDk, wingDk, 1.8f);
            // head + beak
            FillCircleR(px, R, 76f*s, (72f + dive)*s, 7.4f*s, body);
            P3DPolyLine(px, R, new[]{ V(69,72+dive), V(83,72+dive), V(83,79+dive), V(69,79+dive) }, ArtOutline, 0f);
            P3DPlate(px, R, new[]{
                V(82, 73 + dive), V(90, 71 + dive), V(82, 69 + dive)
            }, beak, new Color(beak.r*0.6f, beak.g*0.5f, beak.b*0.2f), 1.4f);
            P3DEllipseGlow(px, R, 78f*s, (74f + dive)*s, 2.2f*s, 2.2f*s, new Color(0.1f,0.08f,0.06f,0.95f));

            // near wing over the body
            P3DPlate(px, R, new[]{
                V(62, 62 + dive), V(38, 70 + up), V(30, 60 + up), V(52, 54 + dive)
            }, body, bodyDk, 2.2f);
            DrawLineR(px, R, 60f*s, (62f+dive)*s, 38f*s, (69f+up)*s, 1.5f*s, bodyDk);
            DrawLineR(px, R, 60f*s, (62f+dive)*s, 33f*s, (62f+up)*s, 1.3f*s, bodyDk);

            // scroll tied to the leg
            DrawLineR(px, R, 66f*s, (58f+dive)*s, 66f*s, (50f+dive)*s, 1.6f*s, ArtLeather);
            P3DPlate(px, R, new[]{
                V(62, 50 + dive), V(72, 50 + dive), V(72, 45 + dive), V(62, 45 + dive)
            }, new Color(0.90f, 0.86f, 0.72f), new Color(0.62f, 0.57f, 0.44f), 1.4f);
            DrawLineR(px, R, 67f*s, (50f+dive)*s, 67f*s, (45f+dive)*s, 1.2f*s, ArtCloth);
        }

        // ── WIZARD — medieval Hacker, authored art ─────────────────────────────
        // Hovers on the hem of its robe (no legs by design), hooded cowl with lit eyes, staff
        // with a bound orb that flares on the cast. Robe carries the livery; gold and wood do not.
        static void P3DBuildWizard(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color robe   = ArtLivery(p);
            Color robeDk = ArtLiveryDk(p);
            Color robeSh = new Color(robeDk.r * 0.55f, robeDk.g * 0.55f, robeDk.b * 0.62f);
            Color arcane = new Color(0.66f, 0.42f, 1.00f);
            Color arcHi  = new Color(0.90f, 0.80f, 1.00f);

            bool cast  = Atk(q);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (cast ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);

            Vector2 shN = V(64 + sway, 84 + bob), shF = V(56 + sway, 84 + bob);
            Vector2 nElb = V(J(8) + sway, J(9) + bob),  nHand = V(J(10) + sway, J(11) + bob);
            Vector2 fElb = V(J(12) + sway, J(13) + bob), fHand = V(J(14) + sway, J(15) + bob);
            Vector2 head = V((q == 5 ? 60 : 65) + sway, 104 + bob);

            // hover glow under the hem
            P3DEllipseGlow(px, R, (64f + sway) * s, 16f * s, 26f * s, 7f * s,
                new Color(arcane.r, arcane.g, arcane.b, cast ? 0.55f : 0.34f));

            // far sleeve
            P3DLimb(px, R, shF.x, shF.y, fElb.x, fElb.y, 5.4f*s, robeSh, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px, R, fElb.x, fElb.y, fHand.x, fHand.y, 4.6f*s, robeSh, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);

            // robe: wide hem tapering to the shoulders, hanging free (no legs)
            P3DPlate(px, R, new[]{
                V(48 + sway*1.4f, 20), V(80 + sway*1.4f, 20), V(76 + sway, 56),
                V(72 + sway, 86), V(56 + sway, 86), V(52 + sway, 56)
            }, robe, robeDk, 2.4f);
            // fold shading
            DrawLineR(px, R, (58f+sway)*s, 84f*s, (55f+sway*1.3f)*s, 24f*s, 1.8f*s, robeSh);
            DrawLineR(px, R, (70f+sway)*s, 84f*s, (73f+sway*1.3f)*s, 24f*s, 1.8f*s, robeSh);
            // gold hem band
            DrawLineR(px, R, (48f+sway*1.4f)*s, 21f*s, (80f+sway*1.4f)*s, 21f*s, 2.6f*s, ArtGold);

            // near sleeve
            P3DLimb(px, R, shN.x, shN.y, nElb.x, nElb.y, 5.8f*s, robe, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px, R, nElb.x, nElb.y, nHand.x, nHand.y, 5.0f*s, robe, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);

            // hooded cowl
            P3DPlate(px, R, new[]{
                new Vector2(head.x-12f*s, head.y-12f*s), new Vector2(head.x+12f*s, head.y-12f*s),
                new Vector2(head.x+11f*s, head.y+9f*s),  new Vector2(head.x, head.y+15f*s),
                new Vector2(head.x-11f*s, head.y+9f*s)
            }, robe, robeDk, 2.2f);
            // cowl shadow + lit eyes
            P3DPlate(px, R, new[]{
                new Vector2(head.x-8f*s, head.y-9f*s), new Vector2(head.x+8f*s, head.y-9f*s),
                new Vector2(head.x+7f*s, head.y+5f*s), new Vector2(head.x-7f*s, head.y+5f*s)
            }, new Color(0.06f,0.05f,0.10f), new Color(0.02f,0.02f,0.04f), 1f);
            P3DEllipseGlow(px, R, head.x-3.6f*s, head.y-2f*s, 3f*s, 3f*s, new Color(arcane.r,arcane.g,arcane.b,0.95f));
            P3DEllipseGlow(px, R, head.x+3.6f*s, head.y-2f*s, 3f*s, 3f*s, new Color(arcane.r,arcane.g,arcane.b,0.95f));
            DrawLineR(px, R, head.x-11f*s, head.y+9f*s, head.x+11f*s, head.y+9f*s, 1.8f*s, ArtGold);

            // staff + bound orb
            {
                // A two-handed staff is CARRIED, not swung. Blending the animated hand toward a
                // stable grip stops the walk cycle from waving it around, while the cast still
                // lifts and tilts it. Pure nHand made the staff swing like a sword.
                Vector2 canon = V(64f + sway + 12f, 78f + bob);
                Vector2 grip  = Vector2.Lerp(nHand, canon, cast ? 0.45f : 0.68f);
                float lean = cast ? -30f : -6f;
                Vector2 d = RotP(new Vector2(0f, 1f), Vector2.zero, lean);
                Vector2 top  = new Vector2(grip.x + d.x*46f*s, grip.y + d.y*46f*s);
                Vector2 btm  = new Vector2(grip.x - d.x*24f*s, grip.y - d.y*24f*s);
                DrawLineR(px, R, btm.x, btm.y, top.x, top.y, 3.4f*s, ArtLeather);
                DrawLineR(px, R, btm.x, btm.y, top.x, top.y, 1.4f*s,
                          new Color(ArtLeather.r*1.5f, ArtLeather.g*1.4f, ArtLeather.b*1.3f));
                // claw setting
                P3DPlate(px, R, new[]{
                    new Vector2(top.x-7f*s, top.y-3f*s), new Vector2(top.x+7f*s, top.y-3f*s),
                    new Vector2(top.x+4f*s, top.y+7f*s), new Vector2(top.x-4f*s, top.y+7f*s)
                }, ArtGoldHi, ArtGold, 1.6f);
                float orbR = cast ? 9.5f : 8f;
                P3DEllipseGlow(px, R, top.x, top.y+8f*s, orbR*2.1f*s, orbR*2.1f*s,
                    new Color(arcane.r, arcane.g, arcane.b, cast ? 0.75f : 0.45f));
                FillCircleR(px, R, top.x, top.y+8f*s, orbR*s, arcane);
                FillCircleR(px, R, top.x-orbR*0.32f*s, top.y+8.8f*s, orbR*0.36f*s, arcHi);
                if (cast)
                    for (int i = 0; i < 3; i++)
                        P3DEllipseGlow(px, R, top.x + (10f + i*9f)*s, top.y + (4f - i*5f)*s,
                            (3.4f - i*0.7f)*s, (3.4f - i*0.7f)*s,
                            new Color(arcane.r, arcane.g, arcane.b, 0.7f - i*0.16f));
            }
        }

        // ── GOLEM — medieval Titan, authored art ───────────────────────────────
        // Carved stone slabs with chipped edges, moss, rune-lit eyes and boulder fists.
        // Stone stays stone for both armies; the runes and the sash carry the livery.
        static void P3DBuildGolem(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color stoneHi = new Color(0.66f, 0.63f, 0.57f);
            Color stone   = new Color(0.44f, 0.42f, 0.37f);
            Color stoneDk = new Color(0.22f, 0.21f, 0.18f);
            Color moss    = new Color(0.30f, 0.44f, 0.20f);
            Color rune    = ArtLivery(p);

            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy, sway;
            // Pose 1 is CONTACT (legs spread, both feet planted); pose 2 is PASSING (the rear
            // leg swings through with the knee raised and the foot lifted clear of the ground).
            // These two used to be the same positions with near/far swapped, which changed the
            // shading but not the outline -- so a walking heavy looked like it was flickering
            // rather than stepping.
            if (pose == 1)      { nKx =  20f; nKy = 26f; nFx =  32f; nFy =  8f;
                                  fKx = -18f; fKy = 24f; fFx = -28f; fFy =  8f; sway =  3f; }
            else if (pose == 2) { nKx =  -6f; nKy = 32f; nFx = -12f; nFy = 18f;
                                  fKx =  16f; fKy = 25f; fFx =  28f; fFy =  8f; sway = -1f; }
            else if (pose == 6) { nKx = -18f; nKy = 24f; nFx = -28f; nFy =  8f;
                                  fKx =  20f; fKy = 26f; fFx =  32f; fFy =  8f; sway = -3f; }
            else if (pose == 7) { nKx =  16f; nKy = 25f; nFx =  28f; nFy =  8f;
                                  fKx =  -6f; fKy = 32f; fFx = -12f; fFy = 18f; sway =  1f; }
            else if (pose == 3) { nKx =  20f; nKy = 24f; nFx =  30f; nFy =  8f;
                                  fKx = -20f; fKy = 24f; fFx = -30f; fFy =  8f; sway =  0f; }
            else if (pose == 4) { nKx =  22f; nKy = 23f; nFx =  32f; nFy =  8f;
                                  fKx = -22f; fKy = 23f; fFx = -32f; fFy =  8f; sway =  0f; }
            else if (pose == 5) { nKx = -14f; nKy = 25f; nFx = -22f; nFy =  8f;
                                  fKx =  18f; fKy = 25f; fFx =  26f; fFy =  8f; sway =  0f; }
            else                { nKx =  12f; nKy = 25f; nFx =  14f; nFy =  8f;
                                  fKx = -12f; fKy = 25f; fFx = -14f; fFy =  8f; sway =  0f; }

            float hullDx = q == 3 ? -12f : (q == 4 ? 10f : 0f);
            float hullDy = q == 3 ?  -4f : (q == 4 ? -2f : 0f);
            float hx = 64f + hullDx;

            P3DEllipseGlow(px, R, 64f*s, 16f*s, 34f*s, 8f*s, new Color(0f,0f,0f,0.5f));

            // far leg + far arm, darkened
            P3DLimb(px, R, (64-6)*s, 42f*s, (64+fKx)*s, fKy*s, 10.5f*s, stoneDk, 0.1f, 0.15f, L,H,fillL);
            P3DLimb(px, R, (64+fKx)*s, fKy*s, (64+fFx)*s, fFy*s, 9f*s, stoneDk, 0.1f, 0.15f, L,H,fillL);
            P3DPlate(px, R, new[]{ new Vector2((64+fFx-11)*s, fFy*s+7f*s), new Vector2((64+fFx+11)*s, fFy*s+7f*s),
                                   new Vector2((64+fFx+9)*s, fFy*s-6f*s), new Vector2((64+fFx-9)*s, fFy*s-6f*s) },
                     stoneDk, new Color(0.14f,0.13f,0.11f), 2f);
            // Far arm COUNTER-swings and has its own fist. It used to be a stub that stopped at
            // the elbow, so the golem only ever had one hand and the off side looked amputated.
            float fex = hx - (q == 4 ? 17f : (q == 3 ?  3f : 10f));
            float fey = 78f + (q == 4 ? 3f : 0f);
            P3DLimb(px, R, (hx-10)*s, 100f*s, fex*s, fey*s, 12f*s, stoneDk, 0.1f, 0.15f, L,H,fillL);
            float ffx = fex - (q == 4 ? 13f : (q == 3 ? -7f : 6f));
            float ffy = fey - 10f;
            P3DPlate(px, R, new[]{
                new Vector2((ffx-11)*s,(ffy-9)*s), new Vector2((ffx+11)*s,(ffy-11)*s),
                new Vector2((ffx+12)*s,(ffy+8)*s), new Vector2((ffx-2)*s,(ffy+12)*s), new Vector2((ffx-12)*s,(ffy+5)*s)
            }, stoneDk, new Color(0.15f, 0.14f, 0.12f), 2.2f);

            // near leg — chunky slabs
            P3DLimb(px, R, (64+6)*s, 42f*s, (64+nKx)*s, nKy*s, 12f*s, stone, 0.12f, 0.2f, L,H,fillL);
            P3DLimb(px, R, (64+nKx)*s, nKy*s, (64+nFx)*s, nFy*s, 10f*s, stone, 0.12f, 0.2f, L,H,fillL);
            P3DPlate(px, R, new[]{ new Vector2((64+nFx-13)*s, nFy*s+8f*s), new Vector2((64+nFx+13)*s, nFy*s+8f*s),
                                   new Vector2((64+nFx+10)*s, nFy*s-7f*s), new Vector2((64+nFx-10)*s, nFy*s-7f*s) },
                     stoneHi, stone, 2.2f);

            // torso: irregular carved slab
            P3DPlate(px, R, new[]{
                V(hx-21, 34), V(hx-24, 60), V(hx-16, 82), V(hx+2, 90),
                V(hx+20, 80), V(hx+24, 56), V(hx+18, 34)
            }, stoneHi, stone, 2.6f);
            // cracks + moss + livery rune seam
            DrawLineR(px, R, (hx-12)*s, 82f*s, (hx-4)*s, 64f*s, 2f*s, stoneDk);
            DrawLineR(px, R, (hx-4)*s, 64f*s, (hx-12)*s, 46f*s, 1.8f*s, stoneDk);
            DrawLineR(px, R, (hx+8)*s, 84f*s, (hx+14)*s, 62f*s, 1.7f*s, stoneDk);
            DrawLineR(px, R, (hx-18)*s, 52f*s, (hx+16)*s, 46f*s, 3.4f*s, moss);
            DrawLineR(px, R, (hx-14)*s, 70f*s, (hx+12)*s, 74f*s, 2.6f*s,
                      new Color(rune.r, rune.g, rune.b, 0.9f));

            // near arm + boulder fist
            float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 8f));
            float ey = 76f + (q == 4 ? -6f : 0f);
            P3DLimb(px, R, (hx+10)*s, 100f*s, ex*s, ey*s, 14f*s, stone, 0.12f, 0.2f, L,H,fillL);
            float fx = ex + (q == 4 ? 20f : (q == 3 ? -10f : 8f));
            float fy = ey + (q == 4 ? -8f : -12f);
            P3DPlate(px, R, new[]{
                new Vector2((fx-13)*s,(fy-11)*s), new Vector2((fx+13)*s,(fy-13)*s),
                new Vector2((fx+15)*s,(fy+9)*s),  new Vector2((fx-2)*s,(fy+15)*s), new Vector2((fx-14)*s,(fy+6)*s)
            }, stoneHi, stone, 2.4f);
            DrawLineR(px, R, (fx-8)*s, (fy+2)*s, (fx+4)*s, (fy-4)*s, 1.8f*s, stoneDk);
            if (q == 4)
                P3DEllipseGlow(px, R, fx*s, fy*s, 22f*s, 20f*s, new Color(rune.r, rune.g, rune.b, 0.42f));

            // head: angular crown of rock with lit eyes
            // sway rocks the skull counter to the stride so the walk reads as weight shift
            float hdx = hx + 4f + sway * 0.6f, hdy = 104f + hullDy;
            P3DPlate(px, R, new[]{
                new Vector2((hdx-15)*s,(hdy-10)*s), new Vector2((hdx-11)*s,(hdy+10)*s),
                new Vector2((hdx)*s,(hdy+17)*s),    new Vector2((hdx+12)*s,(hdy+9)*s),
                new Vector2((hdx+15)*s,(hdy-10)*s), new Vector2((hdx+7)*s,(hdy-15)*s),
                new Vector2((hdx-8)*s,(hdy-15)*s)
            }, stoneHi, stone, 2.4f);
            DrawLineR(px, R, (hdx-9)*s, (hdy+3)*s, (hdx-3)*s, (hdy-3)*s, 1.6f*s, stoneDk);
            DrawLineR(px, R, (hdx+9)*s, (hdy+3)*s, (hdx+3)*s, (hdy-3)*s, 1.6f*s, stoneDk);
            P3DEllipseGlow(px, R, (hdx-5.5f)*s, hdy*s, 3.4f*s, 3.4f*s, new Color(rune.r,rune.g,rune.b,0.95f));
            P3DEllipseGlow(px, R, (hdx+5.5f)*s, hdy*s, 3.4f*s, 3.4f*s, new Color(rune.r,rune.g,rune.b,0.95f));
            DrawLineR(px, R, (hdx-13)*s, (hdy+9)*s, (hdx+12)*s, (hdy+8)*s, 2.8f*s, moss);
        }

        // ══ SAKURA ═══════════════════════════════════════════════════════════════
        // Theme 5. Medieval is hand-forged, Solar cast, Biopunk grown, Industrial stamped,
        // Dawn woven -- SAKURA IS FOLDED AND LACQUERED. Crisp paper creases and hard glossy
        // lacquer, laced together with silk cord.
        //
        // The one rule that keeps this clear of Dawn, which is also soft materials and light:
        // DAWN DRAPES AND FADES, SAKURA FOLDS AND REFLECTS. Dawn's edges are catenary sags and
        // translucent hems; Sakura's are hard creases and specular streaks. If a Sakura piece
        // looks like it could hang, it is wrong.

        static readonly Color SakPaper   = new Color(0.96f, 0.94f, 0.90f);
        static readonly Color SakPaperDk = new Color(0.74f, 0.70f, 0.68f);
        static readonly Color SakLacq    = new Color(0.13f, 0.07f, 0.09f);

        static Color SakLac(P3DP p)   => Color.Lerp(SakLacq, p.body, 0.16f);
        static Color SakLacHi(P3DP p)
        {
            var b = SakLac(p);
            return new Color(Mathf.Clamp01(b.r*2.2f+0.10f), Mathf.Clamp01(b.g*2.2f+0.08f),
                             Mathf.Clamp01(b.b*2.2f+0.10f));
        }
        static Color SakLacDk(P3DP p) { var b = SakLac(p); return new Color(b.r*0.55f, b.g*0.5f, b.b*0.55f); }

        /// <summary>
        /// Folded panel: one hard crease, two faces taking different light. NEW, and it is the
        /// whole theme. A plate is a single flat face and a drape sags; paper does neither --
        /// it breaks along a line and the halves catch light differently on either side.
        /// </summary>
        static void SakFold(Color[] px, int R, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float creaseT, Color col, Color dark)
        {
            Vector2 Lp(Vector2 u, Vector2 v, float t) => new Vector2(u.x + (v.x-u.x)*t, u.y + (v.y-u.y)*t);
            Vector2 m1 = Lp(a, b, creaseT), m2 = Lp(d, c, creaseT);
            var hi = new Color(Mathf.Clamp01(col.r*1.12f), Mathf.Clamp01(col.g*1.12f), Mathf.Clamp01(col.b*1.12f));
            P3DPlate(px, R, new[]{ a, m1, m2, d }, hi, col, 1.7f);
            P3DPlate(px, R, new[]{ m1, b, c, m2 },
                     new Color(col.r*0.72f, col.g*0.72f, col.b*0.72f),
                     new Color(col.r*0.58f, col.g*0.58f, col.b*0.58f), 1.7f);
            DrawLineR(px, R, m1.x, m1.y, m2.x, m2.y, 1.6f, dark);
            DrawLineR(px, R, m1.x+0.8f, m1.y, m2.x+0.8f, m2.y, 0.7f, new Color(1f,1f,1f,0.4f));
        }

        /// <summary>Lacquer plate: a hard gloss streak, no soft falloff. NEW.</summary>
        static void SakLacquer(Color[] px, int R, Vector2[] pts, P3DP p, float ow)
        {
            P3DPlate(px, R, pts, SakLacHi(p), SakLac(p), ow);
            float mnx = float.MaxValue, mxx = float.MinValue, mny = float.MaxValue, mxy = float.MinValue;
            foreach (var q in pts)
            {
                mnx = Mathf.Min(mnx, q.x); mxx = Mathf.Max(mxx, q.x);
                mny = Mathf.Min(mny, q.y); mxy = Mathf.Max(mxy, q.y);
            }
            float w = mxx-mnx, h = mxy-mny;
            DrawLineR(px, R, mnx+w*0.22f, mny+h*0.86f, mnx+w*0.40f, mny+h*0.14f,
                      Mathf.Max(1.4f, w*0.09f), new Color(1f,1f,1f,0.30f));
            DrawLineR(px, R, mnx+w*0.34f, mny+h*0.86f, mnx+w*0.46f, mny+h*0.36f,
                      Mathf.Max(0.9f, w*0.045f), new Color(1f,1f,1f,0.42f));
        }

        /// <summary>Silk cord lacing between armour lames. NEW.</summary>
        static void SakLace(Color[] px, int R, float x0, float y0, float x1, float y1,
            int rows, Color col, float s)
        {
            for (int r = 0; r < rows; r++)
            {
                float t = (r + 0.5f) / rows;
                float x = x0 + (x1-x0)*t, y = y0 + (y1-y0)*t;
                DrawLineR(px,R, (x-3.4f)*s, (y+1.6f)*s, (x+3.4f)*s, (y-1.6f)*s, 1.5f*s, col);
                DrawLineR(px,R, (x-3.4f)*s, (y-1.6f)*s, (x+3.4f)*s, (y+1.6f)*s, 1.5f*s, col);
            }
        }

        /// <summary>Zigzag paper streamer (shide).</summary>
        static void SakShide(Color[] px, int R, float x, float y, float h, int steps, float s)
        {
            if (steps < 2) steps = 2;
            var fwd = new Vector2[steps+1];
            fwd[0] = new Vector2(x*s, y*s);
            for (int i = 0; i < steps; i++)
                fwd[i+1] = new Vector2((x + ((i%2==1) ? 5f : -5f))*s, (y - (i+1)*(h/steps))*s);
            var all = new Vector2[(steps+1)*2];
            for (int i = 0; i <= steps; i++)
            {
                all[i] = fwd[i];
                all[(steps+1)*2-1-i] = new Vector2(fwd[i].x + 4.4f*s, fwd[i].y);
            }
            P3DPlate(px, R, all, SakPaper, SakPaperDk, 1.2f);
        }

        /// <summary>Falling blossom — lands on every strike frame in the theme.</summary>
        static void SakBlossom(Color[] px, int R, float x, float y, float r, float a, float s)
        {
            for (int i = 0; i < 5; i++)
            {
                float ang = (i*72f + 18f) * Mathf.Deg2Rad;
                P3DPlate(px,R, new[]{
                    new Vector2(x*s, y*s),
                    new Vector2((x+Mathf.Cos(ang-0.35f)*r)*s, (y+Mathf.Sin(ang-0.35f)*r)*s),
                    new Vector2((x+Mathf.Cos(ang)*r*1.25f)*s, (y+Mathf.Sin(ang)*r*1.25f)*s),
                    new Vector2((x+Mathf.Cos(ang+0.35f)*r)*s, (y+Mathf.Sin(ang+0.35f)*r)*s) },
                    new Color(1f,0.82f,0.90f,a), new Color(0.90f,0.55f,0.70f,a*0.8f), 0f);
            }
            FillCircleR(px,R, x*s, y*s, r*0.28f*s, new Color(1f,0.95f,0.72f,a));
        }

        // ---- SHINOBI — Sakura trooper ---------------------------------------------
        static void P3DBuildShinobi(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Vector2 hip = V(63,56), shN = V(64,84), shF = V(56,84);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11)), fE = V(J(12),J(13)), fH = V(J(14),J(15));
            Vector2 head = V(q == 5 ? 60 : 66, 104);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            Color mf = ArtSteelFar(p), md = ArtSteelDk(p);

            P3DLimb(px,R, shF.x,shF.y, fE.x,fE.y, 4.6f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fE.x,fE.y, fH.x,fH.y, 4.0f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 6.4f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.4f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 7.0f*s, SakLacDk(p), p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 6.0f*s, SakLacDk(p), p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = i == 0 ? fF : nF;
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+8f*s,f.y-3f*s), new Vector2(f.x-6f*s,f.y-3f*s) },
                         i == 0 ? SakPaperDk : SakPaper, SakPaperDk, 1.5f);
                DrawLineR(px,R, f.x+3f*s, f.y+3f*s, f.x+4f*s, f.y-3f*s, 1.1f*s, ArtOutline);  // split toe
            }
            for (int i = 0; i < 4; i++)
            {
                float sx = 52f + i*6f;
                SakLacquer(px,R, new[]{ V(sx-3,56), V(sx+3,56), V(sx+3+sway*0.2f,42), V(sx-3+sway*0.2f,42) },
                           p, 1.4f);
            }
            SakLace(px,R, 52,50, 74,50, 4, ArtCloth, s);
            for (int r = 0; r < 3; r++)
            {
                float yy = 86f - r*10f;
                SakLacquer(px,R, new[]{ V(54,yy), V(74,yy), V(73,yy-9), V(55,yy-9) }, p, 1.8f);
                SakLace(px,R, 56,yy-4, 72,yy-4, 4, ArtCloth, s);
            }
            SakLacquer(px,R, new[]{ new Vector2(shN.x-12f*s,shN.y+7f*s), new Vector2(shN.x+9f*s,shN.y+9f*s),
                                    new Vector2(shN.x+8f*s,shN.y-8f*s), new Vector2(shN.x-10f*s,shN.y-9f*s) },
                       p, 2f);
            SakLace(px,R, shN.x/s-8f, shN.y/s+2f, shN.x/s+6f, shN.y/s+3f, 3, ArtCloth, s);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.0f*s, SakLac(p), p.metallic, p.smoothness, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.4f*s, SakLac(p), p.metallic, p.smoothness, L,H,fillL);
            SakLacquer(px,R, new[]{ new Vector2(head.x-10f*s,head.y-10f*s), new Vector2(head.x+10f*s,head.y-9f*s),
                                    new Vector2(head.x+9f*s,head.y+7f*s), new Vector2(head.x-9f*s,head.y+6f*s) },
                       p, 2.1f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s,head.y-9f*s), new Vector2(head.x+9f*s,head.y-8f*s),
                                  new Vector2(head.x+8f*s,head.y-2f*s), new Vector2(head.x-7f*s,head.y-3f*s) },
                     ArtCloth, ArtClothDk, 1.4f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y+1f*s, 3.0f*s, 2.2f*s, new Color(1f,0.95f,0.85f,0.9f));
            P3DPlate(px,R, new[]{ new Vector2(head.x-2f*s,head.y+7f*s), new Vector2(head.x-9f*s,head.y+22f*s),
                                  new Vector2(head.x+1f*s,head.y+20f*s), new Vector2(head.x+4f*s,head.y+8f*s) },
                     ArtGoldHi, ArtGold, 1.4f);
            {
                float swing = q == 4 ? 54f : (q == 3 ? -58f : -16f);
                Vector2 d = RotP(new Vector2(0f,1f), Vector2.zero, swing);
                Vector2 tip = new Vector2(nH.x + d.x*40f*s, nH.y + d.y*40f*s);
                Vector2 btm = new Vector2(nH.x - d.x*24f*s, nH.y - d.y*24f*s);
                DrawLineR(px,R, btm.x,btm.y, tip.x,tip.y, 3.0f*s, ArtLeather);
                DrawLineR(px,R, btm.x,btm.y, tip.x,tip.y, 1.2f*s,
                          new Color(ArtLeather.r*1.7f, ArtLeather.g*1.7f, ArtLeather.b*1.7f));
                Vector2 pp = new Vector2(-d.y, d.x);
                P3DPlate(px,R, new[]{
                    new Vector2(tip.x+pp.x*2.4f*s, tip.y+pp.y*2.4f*s),
                    new Vector2(tip.x+d.x*13f*s+pp.x*8f*s, tip.y+d.y*13f*s+pp.y*8f*s),
                    new Vector2(tip.x+d.x*24f*s+pp.x*1f*s, tip.y+d.y*24f*s+pp.y*1f*s),
                    new Vector2(tip.x+d.x*11f*s-pp.x*2f*s, tip.y+d.y*11f*s-pp.y*2f*s) },
                    new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.7f);
                DrawLineR(px,R, tip.x+pp.x*2f*s, tip.y+pp.y*2f*s, tip.x+d.x*22f*s, tip.y+d.y*22f*s,
                          1.0f*s, new Color(1f,1f,1f,0.55f));
                if (q == 4)
                    for (int b = 0; b < 3; b++)
                        SakBlossom(px,R, nH.x/s+26f+b*10f, nH.y/s+8f-b*6f, 3.4f-b*0.7f, 0.7f-b*0.16f, s);
            }
        }

        // ---- YUMI — Sakura sniper ---------------------------------------------------
        static void P3DBuildYumi(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Vector2 hip = V(63,56), shN = V(64,84), shF = V(56,84);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 61 : 66, 104);
            Color mf = ArtSteelFar(p), md = ArtSteelDk(p);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 4.2f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 6.0f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.2f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.6f*s, ArtClothDk, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.6f*s, ArtClothDk, 0.1f,0.2f, L,H,fillL);
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = i == 0 ? fF : nF;
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+8f*s,f.y-3f*s), new Vector2(f.x-6f*s,f.y-3f*s) },
                         i == 0 ? SakPaperDk : SakPaper, SakPaperDk, 1.5f);
            }
            // hakama: hard pleat creases, never a drape
            SakFold(px,R, V(52,60), V(74,60), V(76,36), V(50,36), 0.5f, ArtCloth, ArtClothDk);
            for (int c = 0; c < 3; c++)
                DrawLineR(px,R, (56f+c*7f)*s, 58f*s, (55f+c*7f)*s, 38f*s, 1.1f*s, ArtClothDk);
            P3DPlate(px,R, new[]{ V(52,88), V(74,88), V(76,70), V(70,58), V(58,58), V(50,70) },
                     SakPaper, SakPaperDk, 2.2f);
            P3DPlate(px,R, new[]{ V(62,88), V(76,88), V(78,70), V(64,66) }, ArtCloth, ArtClothDk, 2.0f);
            SakLace(px,R, 56,80, 72,82, 4, ArtLeather, s);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 4.8f*s, SakPaperDk, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.2f*s, SakPaperDk, 0.1f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-8f*s), new Vector2(head.x+9f*s,head.y-7f*s),
                                  new Vector2(head.x+8f*s,head.y+7f*s), new Vector2(head.x-8f*s,head.y+6f*s) },
                     new Color(0.72f,0.60f,0.52f), new Color(0.44f,0.34f,0.30f), 2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-10f*s,head.y+2f*s), new Vector2(head.x+10f*s,head.y+3f*s),
                                  new Vector2(head.x+10f*s,head.y+7f*s), new Vector2(head.x-10f*s,head.y+6f*s) },
                     ArtCloth, ArtClothDk, 1.3f);
            DrawLineR(px,R, head.x-10f*s, head.y+5f*s, head.x-20f*s, head.y+1f*s, 1.8f*s, ArtCloth);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y-1f*s, 2.6f*s, 2.0f*s, new Color(0.15f,0.08f,0.06f,0.95f));
            {
                // THE yumi: gripped a third from the bottom, so the limbs are wildly unequal.
                // Every other bow in the game is symmetric and held at its centre.
                float draw = q == 3 ? 12f : (q == 4 ? -2f : 5f);
                float gx = nH.x/s + 6f, gy = nH.y/s;
                DrawLineR(px,R, gx*s,gy*s, (gx-3f)*s,(gy-26f)*s, 2.6f*s, ArtLeather);
                DrawLineR(px,R, gx*s,gy*s, (gx-4f)*s,(gy+50f)*s, 2.6f*s, ArtLeather);
                DrawLineR(px,R, (gx-3f)*s,(gy-26f)*s, (gx+1f)*s,(gy-32f)*s, 2.2f*s, ArtLeather);
                DrawLineR(px,R, (gx-4f)*s,(gy+50f)*s, (gx+1f)*s,(gy+58f)*s, 2.2f*s, ArtLeather);
                float nx = gx - draw, ny = gy + 8f;
                var str = new Color(0.92f,0.90f,0.84f);
                DrawLineR(px,R, (gx+1f)*s,(gy-32f)*s, nx*s,ny*s, 1.2f*s, str);
                DrawLineR(px,R, (gx+1f)*s,(gy+58f)*s, nx*s,ny*s, 1.2f*s, str);
                if (q == 4)
                {
                    DrawLineR(px,R, (gx+14f)*s,(gy+8f)*s, (gx+46f)*s,(gy+8f)*s, 1.8f*s, ArtLeather);
                    P3DPlate(px,R, new[]{ V(gx+46,gy+11), V(gx+55,gy+8), V(gx+46,gy+5) },
                             new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.2f);
                    for (int b = 0; b < 2; b++)
                        SakBlossom(px,R, gx+30f+b*14f, gy+16f+b*5f, 3.0f-b*0.6f, 0.6f-b*0.2f, s);
                }
                else
                {
                    DrawLineR(px,R, nx*s,ny*s, (gx+16f)*s,(gy+8f)*s, 1.5f*s, ArtLeather);
                    P3DPlate(px,R, new[]{ V(gx+16,gy+11), V(gx+24,gy+8), V(gx+16,gy+5) },
                             new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.2f);
                }
            }
        }

        // ---- SHRINE — Sakura shield-bot ---------------------------------------------
        static void P3DBuildShrineGuard(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Vector2 hip = V(62,52), shN = V(63,80), shF = V(55,80);
            Vector2 nK = V(J(0)-2,J(1)), nF = V(J(2)-2,J(3));
            Vector2 fK = V(J(4)-2,J(5)), fF = V(J(6)-2,J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 58 : 63, 98);
            Color mf = ArtSteelFar(p);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 5.2f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 7.6f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 6.6f*s, mf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 8.6f*s, SakLacDk(p), p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 7.4f*s, SakLacDk(p), p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            foreach (var f in new[]{ nF, fF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-8f*s,f.y+4f*s), new Vector2(f.x+9f*s,f.y+4f*s),
                                      new Vector2(f.x+8f*s,f.y-4f*s), new Vector2(f.x-7f*s,f.y-4f*s) },
                         SakPaperDk, new Color(SakPaperDk.r*0.7f,SakPaperDk.g*0.7f,SakPaperDk.b*0.7f), 1.6f);
            for (int r = 0; r < 3; r++)
            {
                float yy = 84f - r*11f;
                SakLacquer(px,R, new[]{ V(50,yy), V(76,yy), V(75,yy-10), V(51,yy-10) }, p, 2.0f);
                SakLace(px,R, 53,yy-5, 73,yy-5, 5, ArtCloth, s);
            }
            SakLacquer(px,R, new[]{ new Vector2(head.x-11f*s,head.y-9f*s), new Vector2(head.x+11f*s,head.y-8f*s),
                                    new Vector2(head.x+10f*s,head.y+6f*s), new Vector2(head.x-10f*s,head.y+5f*s) },
                       p, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-8f*s), new Vector2(head.x+10f*s,head.y-7f*s),
                                  new Vector2(head.x+9f*s,head.y-2f*s), new Vector2(head.x-8f*s,head.y-3f*s) },
                     ArtCloth, ArtClothDk, 1.3f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-3f*s,head.y+6f*s), new Vector2(head.x-11f*s,head.y+20f*s),
                                  new Vector2(head.x-4f*s,head.y+18f*s), new Vector2(head.x+1f*s,head.y+7f*s) },
                     ArtGoldHi, ArtGold, 1.4f);
            P3DPlate(px,R, new[]{ new Vector2(head.x+3f*s,head.y+6f*s), new Vector2(head.x+11f*s,head.y+20f*s),
                                  new Vector2(head.x+4f*s,head.y+18f*s), new Vector2(head.x-1f*s,head.y+7f*s) },
                     ArtGoldHi, ArtGold, 1.4f);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.6f*s, SakLac(p), p.metallic, p.smoothness, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.8f*s, SakLac(p), p.metallic, p.smoothness, L,H,fillL);
            {
                float bx = 64f + (q == 4 ? 14f : (q == 3 ? 3f : 9f));
                SakLacquer(px,R, new[]{ V(bx-15,92), V(bx+15,92), V(bx+15,34), V(bx-15,34) }, p, 2.6f);
                P3DPlate(px,R, new[]{ V(bx-13,88), V(bx+13,88), V(bx+13,80), V(bx-13,80) },
                         ArtGoldHi, ArtGold, 1.4f);
                // two leaves meeting at a hard crease -- the door is folded, not flat
                DrawLineR(px,R, bx*s,92f*s, bx*s,34f*s, 2.0f*s, ArtOutline);
                DrawLineR(px,R, (bx+1.4f)*s,90f*s, (bx+1.4f)*s,36f*s, 0.9f*s, new Color(1f,1f,1f,0.30f));
                DrawRingR(px,R, (bx-7)*s,62f*s, 4.6f*s, 2.0f*s, ArtGold);
                DrawRingR(px,R, (bx+7)*s,62f*s, 4.6f*s, 2.0f*s, ArtGold);
                DrawLineR(px,R, (bx-15)*s,84f*s, (bx+15)*s,86f*s, 3.4f*s, new Color(0.86f,0.82f,0.70f));
                for (int k = 0; k < 3; k++) SakShide(px,R, bx-9f+k*9f, 84f, 16f, 4, s);
                if (q == 4)
                    for (int b = 0; b < 3; b++)
                        SakBlossom(px,R, bx+20f+b*9f, 70f-b*7f, 3.2f-b*0.6f, 0.6f-b*0.15f, s);
            }
        }

        // ---- TANUKI — Sakura mech ----------------------------------------------------
        static void P3DBuildTanuki(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float st = q == 1 ? 2f : (q == 2 ? -2f : 0f);
            float rec = q == 4 ? -6f : (q == 3 ? 3f : 0f);
            Color mf = ArtSteelFar(p), md = ArtSteelDk(p);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            float[,] legs = { {48,-1},{58,-1},{70,1},{80,1} };
            for (int i = 0; i < 4; i++)
            {
                float gx = legs[i,0], dir = legs[i,1];
                float ax = gx + dir*3f + st*dir*2f;
                P3DLimb(px,R, gx*s,42f*s, ax*s,22f*s, 5.2f*s, i < 2 ? mf : md, 0.2f,0.4f, L,H,fillL);
                P3DPlate(px,R, new[]{ V(ax-7,24), V(ax+7,24), V(ax+6,16), V(ax-6,16) },
                         SakPaperDk, new Color(SakPaperDk.r*0.7f,SakPaperDk.g*0.7f,SakPaperDk.b*0.7f), 1.6f);
            }
            SakLacquer(px,R, new[]{ V(42,42), V(86,42), V(84,58), V(44,58) }, p, 2.3f);
            SakLace(px,R, 46,50, 82,50, 8, ArtCloth, s);
            float bx = 64f + rec*0.3f;
            P3DPlate(px,R, new[]{ V(bx-18,74), V(bx-14,86), V(bx,90), V(bx+14,86), V(bx+18,74),
                                  V(bx+14,62), V(bx,58), V(bx-14,62) },
                     SakPaperDk, new Color(SakPaperDk.r*0.72f,SakPaperDk.g*0.72f,SakPaperDk.b*0.72f), 2.2f);
            FillCircleR(px,R, bx*s, 70f*s, 11f*s, new Color(0.94f,0.90f,0.84f));
            FillCircleR(px,R, (bx-6)*s, 82f*s, 2.6f*s, new Color(0.12f,0.09f,0.08f));
            FillCircleR(px,R, (bx+6)*s, 82f*s, 2.6f*s, new Color(0.12f,0.09f,0.08f));
            P3DPlate(px,R, new[]{ V(bx-2,78), V(bx+2,78), V(bx,74) },
                     new Color(0.20f,0.14f,0.12f), new Color(0.10f,0.07f,0.06f), 1f);
            SakFold(px,R, V(46,92), V(82,92), V(70,102), V(58,102), 0.5f, SakPaper, SakPaperDk);
            P3DPlate(px,R, new[]{ V(58,102), V(70,102), V(64,108) }, SakPaper, SakPaperDk, 1.4f);
            SakFold(px,R, V(38,58), V(90,58), V(78,72), V(50,72), 0.5f, ArtCloth, ArtClothDk);
            DrawLineR(px,R, 38f*s,58f*s, 90f*s,58f*s, 2.4f*s, ArtGold);
            if (q == 4)
            {
                P3DEllipseGlow(px,R, 92f*s, 72f*s, 20f*s, 18f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
                for (int k = 0; k < 4; k++)
                {
                    float a = (200f+k*25f)*Mathf.Deg2Rad;
                    DrawLineR(px,R, 88f*s,72f*s, (88f+Mathf.Cos(a)*15f)*s, (72f+Mathf.Sin(a)*15f)*s,
                              2.0f*s, new Color(1f,0.9f,0.95f,0.6f));
                }
                for (int b = 0; b < 3; b++) SakBlossom(px,R, 96f+b*8f, 78f-b*6f, 3.4f-b*0.7f, 0.7f-b*0.18f, s);
            }
        }

        // ---- KAMI — Sakura titan ------------------------------------------------------
        static void P3DBuildKami(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color mf = ArtSteelFar(p), mh = ArtSteelHi(p), md = ArtSteelDk(p);
            var skin = new Color(0.68f, 0.26f, 0.24f);
            var skinDk = new Color(0.42f, 0.14f, 0.14f);

            float nKx,nKy,nFx,nFy,fKx,fKy,fFx,fFy,rock;
            // 1 = contact (spread, both planted); 2 = passing (rear leg lifted, knee raised).
            // Previously these were the same positions with near/far traded -- shading changed,
            // outline did not, so the legs never read as moving.
            if (q == 1)      { nKx= 20; nKy=26; nFx= 32; nFy= 8; fKx=-18; fKy=24; fFx=-28; fFy=8; rock= 3f; }
            else if (q == 2) { nKx= -6; nKy=32; nFx=-12; nFy=18; fKx= 16; fKy=25; fFx= 28; fFy=8; rock=-1f; }
            else if (q == 6) { nKx=-18; nKy=24; nFx=-28; nFy= 8; fKx= 20; fKy=26; fFx= 32; fFy=8; rock=-3f; }
            else if (q == 7) { nKx= 16; nKy=25; nFx= 28; nFy= 8; fKx= -6; fKy=32; fFx=-12; fFy=18; rock= 1f; }
            else if (q == 3) { nKx= 20; nKy=24; nFx= 30; nFy=8; fKx=-20; fKy=24; fFx=-30; fFy=8; rock= 0f; }
            else if (q == 4) { nKx= 22; nKy=23; nFx= 32; nFy=8; fKx=-22; fKy=23; fFx=-32; fFy=8; rock= 0f; }
            else if (q == 5) { nKx=-14; nKy=25; nFx=-22; nFy=8; fKx= 18; fKy=25; fFx= 26; fFy=8; rock= 0f; }
            else             { nKx= 12; nKy=25; nFx= 14; nFy=8; fKx=-12; fKy=25; fFx=-14; fFy=8; rock= 0f; }
            float hdx = q == 3 ? -12f : (q == 4 ? 10f : 0f);
            float hx = 64f + hdx;

            P3DEllipseGlow(px,R, 64f*s, 16f*s, 34f*s, 8f*s, new Color(0f,0f,0f,0.5f));
            // Far leg was drawn in STEEL GREY while the near leg was red skin. The two legs
            // swap position between walk frames, so trading a grey limb for a red one read as
            // the colour flickering rather than the legs stepping. Every other unit in the game
            // uses one material at two brightnesses for near/far, and an oni has two skin legs.
            var skinFar = new Color(0.34f, 0.13f, 0.12f);
            // part 3 = torso only. The four limbs come from UnitLimb() and are rotated live
            // by the view, so the walk is continuous instead of four baked stills.
            bool rigged = part == 3;
            if (!rigged) {
            P3DLimb(px,R, 58f*s,44f*s, (64+fKx)*s,fKy*s, 9.5f*s, skinFar, 0.2f,0.3f, L,H,fillL);
            P3DLimb(px,R, (64+fKx)*s,fKy*s, (64+fFx)*s,fFy*s, 8f*s, skinFar, 0.2f,0.3f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+fFx-10, fFy+7), V(64+fFx+10, fFy+7),
                                  V(64+fFx+8, fFy-6), V(64+fFx-8, fFy-6) },
                     new Color(SakPaperDk.r*0.62f,SakPaperDk.g*0.62f,SakPaperDk.b*0.62f),
                     new Color(SakPaperDk.r*0.42f,SakPaperDk.g*0.42f,SakPaperDk.b*0.42f), 2f);
            }
            if (!rigged) {
            P3DLimb(px,R, 70f*s,44f*s, (64+nKx)*s,nKy*s, 11f*s, new Color(0.55f,0.22f,0.20f), 0.2f,0.3f, L,H,fillL);
            P3DLimb(px,R, (64+nKx)*s,nKy*s, (64+nFx)*s,nFy*s, 9.2f*s, new Color(0.55f,0.22f,0.20f), 0.2f,0.3f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+nFx-12, nFy+8), V(64+nFx+12, nFy+8),
                                  V(64+nFx+9, nFy-7), V(64+nFx-9, nFy-7) },
                     SakPaperDk, new Color(SakPaperDk.r*0.7f,SakPaperDk.g*0.7f,SakPaperDk.b*0.7f), 2.2f);
            }
            // bare skin -- the only apex unit in the game that is not armoured
            P3DPlate(px,R, new[]{ V(hx-19,44), V(hx+19,44), V(hx+22,74), V(hx+16,92), V(hx-16,92), V(hx-22,74) },
                     skin, skinDk, 2.5f);
            SakFold(px,R, V(hx-20,54), V(hx+20,54), V(hx+18,38), V(hx-18,38), 0.5f,
                    new Color(0.86f,0.70f,0.28f), new Color(0.50f,0.38f,0.10f));
            for (int t = 0; t < 4; t++)
                DrawLineR(px,R, (hx-14f+t*9f)*s, 52f*s, (hx-13f+t*9f)*s, 42f*s, 2.4f*s,
                          new Color(0.20f,0.14f,0.08f));
            P3DPlate(px,R, new[]{ V(hx-30,96), V(hx-8,102), V(hx-6,84), V(hx-24,80) },
                     new Color(0.72f,0.30f,0.26f), new Color(0.44f,0.16f,0.15f), 2.3f);
            P3DPlate(px,R, new[]{ V(hx+8,100), V(hx+26,94), V(hx+22,80), V(hx+6,84) }, skin, skinDk, 2.2f);
            float fex = hx - (q == 4 ? 18f : 11f);
            if (!rigged) {
            P3DLimb(px,R, (hx-14)*s,92f*s, fex*s,76f*s, 10f*s, new Color(0.50f,0.20f,0.18f), 0.2f,0.3f, L,H,fillL);
            FillCircleR(px,R, (fex-4)*s, 70f*s, 7.4f*s, new Color(0.55f,0.22f,0.20f));
            }
            SakLacquer(px,R, new[]{ V(hx-13+rock*0.4f,102), V(hx+13+rock*0.4f,102),
                                    V(hx+12,118), V(hx,124), V(hx-12,118) }, p, 2.4f);
            P3DPlate(px,R, new[]{ V(hx-10,106), V(hx+10,107), V(hx+9,113), V(hx-9,112) },
                     new Color(0.90f,0.86f,0.80f), new Color(0.62f,0.56f,0.52f), 1.3f);
            FillCircleR(px,R, (hx-5)*s,110f*s, 2.4f*s, new Color(0.86f,0.20f,0.18f));
            FillCircleR(px,R, (hx+5)*s,110f*s, 2.4f*s, new Color(0.86f,0.20f,0.18f));
            for (int m = 0; m < 4; m++)
                DrawLineR(px,R, (hx-7f+m*4.6f)*s,106f*s, (hx-7f+m*4.6f)*s,102f*s, 1.4f*s,
                          new Color(0.95f,0.93f,0.88f));
            P3DPlate(px,R, new[]{ V(hx-11,118), V(hx-19,132), V(hx-8,126) },
                     new Color(0.94f,0.90f,0.82f), new Color(0.62f,0.56f,0.50f), 1.6f);
            P3DPlate(px,R, new[]{ V(hx+11,118), V(hx+19,132), V(hx+8,126) },
                     new Color(0.94f,0.90f,0.82f), new Color(0.62f,0.56f,0.50f), 1.6f);
            {
                float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 9f));
                float ey = 76f + (q == 4 ? -6f : 0f);
                if (!rigged)
                P3DLimb(px,R, (hx+14)*s,92f*s, ex*s,ey*s, 11.5f*s, new Color(0.60f,0.24f,0.22f),
                        0.2f,0.3f, L,H,fillL);
                Vector2 d = RotP(new Vector2(0f,1f), Vector2.zero, q == 4 ? 58f : (q == 3 ? -62f : -14f));
                float tx = ex + d.x*30f, ty = ey + d.y*30f;
                DrawLineR(px,R, ex*s,ey*s, tx*s,ty*s, 7.0f*s, md);
                DrawLineR(px,R, ex*s,ey*s, tx*s,ty*s, 2.6f*s, mh);
                for (int s2 = 0; s2 < 5; s2++)
                {
                    float t2 = (s2+1)/6f;
                    float bx2 = ex + (tx-ex)*t2, by2 = ey + (ty-ey)*t2;
                    FillCircleR(px,R, (bx2-4)*s,(by2+2)*s, 2.0f*s, mh);
                    FillCircleR(px,R, (bx2+4)*s,(by2-2)*s, 2.0f*s, mh);
                }
                if (q == 4)
                {
                    P3DEllipseGlow(px,R, (tx+8f)*s, ty*s, 24f*s, 22f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
                    for (int b = 0; b < 4; b++)
                        SakBlossom(px,R, tx+12f+b*9f, ty+10f-b*7f, 3.6f-b*0.7f, 0.72f-b*0.15f, s);
                }
            }
        }

        // ---- KITE — Sakura interceptor ------------------------------------------------
        static void P3DBuildKite(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float beat = q == 1 ? 12f : (q == 2 ? -10f : (Atk(q) ? 16f : 0f));
            float cx = 58f, cy = 62f + (q == 4 ? -5f : 0f);
            float w = 26f, h = 34f;

            SakFold(px,R, V(cx-w,cy), V(cx,cy+h), V(cx+w,cy), V(cx,cy), 0.5f, ArtCloth, ArtClothDk);
            SakFold(px,R, V(cx-w,cy), V(cx,cy), V(cx+w,cy), V(cx,cy-h), 0.5f,
                    new Color(Mathf.Min(ArtCloth.r*1.1f,1f), Mathf.Min(ArtCloth.g*1.1f,1f),
                              Mathf.Min(ArtCloth.b*1.1f,1f)), ArtClothDk);
            DrawLineR(px,R, (cx-w)*s,cy*s, (cx+w)*s,cy*s, 1.6f*s, ArtLeather);
            DrawLineR(px,R, cx*s,(cy+h)*s, cx*s,(cy-h)*s, 1.6f*s, ArtLeather);
            FillCircleR(px,R, cx*s,cy*s, 7.4f*s, new Color(0.95f,0.93f,0.88f));
            DrawRingR  (px,R, cx*s,cy*s, 7.4f*s, 1.6f*s, ArtOutline);
            FillCircleR(px,R, cx*s,cy*s, 4.4f*s, new Color(0.86f,0.20f,0.26f));
            for (int i = 0; i < 4; i++)
            {
                float tx = cx - 14f - i*11f;
                float ty = cy - 16f - i*7f + ((i%2==1) ? beat*0.4f : -beat*0.4f);
                DrawLineR(px,R, (tx+11f)*s,(ty+7f)*s, tx*s,ty*s, 1.2f*s, ArtLeather);
                P3DPlate(px,R, new[]{ V(tx-5,ty+3), V(tx,ty+1), V(tx-5,ty-3) }, SakPaper, SakPaperDk, 1.0f);
                P3DPlate(px,R, new[]{ V(tx+5,ty+3), V(tx,ty+1), V(tx+5,ty-3) }, SakPaper, SakPaperDk, 1.0f);
            }
            P3DPlate(px,R, new[]{ V(cx+14,cy+4), V(cx+26,cy+8), V(cx+30,cy-2), V(cx+18,cy-8) },
                     SakLac(p), SakLacDk(p), 1.9f);
            FillCircleR(px,R, (cx+31)*s,(cy+2)*s, 5.0f*s, SakPaperDk);
            P3DEllipseGlow(px,R, (cx+33)*s,(cy+3)*s, 2.4f*s, 2.2f*s, new Color(0.15f,0.08f,0.06f,0.9f));
            DrawLineR(px,R, (cx+16)*s,(cy+2)*s, (cx+8)*s,(cy+6)*s, 1.6f*s, ArtLeather);
            if (Atk(q))
            {
                for (int k = 0; k < 3; k++)
                    P3DPlate(px,R, new[]{ V(cx+38+k*12, cy+6-k*4), V(cx+50+k*12, cy+2-k*4),
                                          V(cx+38+k*12, cy-2-k*4) },
                             new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.1f);
                for (int b = 0; b < 3; b++)
                    SakBlossom(px,R, cx+44f+b*10f, cy+12f-b*6f, 3.2f-b*0.6f, 0.62f-b*0.16f, s);
            }
        }

        // ---- ONMYOJI — Sakura hacker ----------------------------------------------------
        static void P3DBuildOnmyoji(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            bool cast = Atk(q);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (cast ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);
            Vector2 shN = V(64+sway, 84+bob), shF = V(56+sway, 84+bob);
            Vector2 nE = V(J(8)+sway, J(9)+bob), nH = V(J(10)+sway, J(11)+bob);
            Vector2 fE = V(J(12)+sway, J(13)+bob);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 head = V((q == 5 ? 60 : 65) + sway, 102 + bob);
            Color mf = ArtSteelFar(p);

            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 4.6f*s, mf, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.2f*s, ArtClothDk, 0.1f,0.2f, L,H,fillL);
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = i == 0 ? fF : nF;
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+8f*s,f.y-3f*s), new Vector2(f.x-6f*s,f.y-3f*s) },
                         i == 0 ? SakPaperDk : SakPaper, SakPaperDk, 1.5f);
            }
            P3DLimb(px,R, shF.x,shF.y, fE.x,fE.y, 5.4f*s,
                    new Color(ArtClothDk.r*0.8f,ArtClothDk.g*0.8f,ArtClothDk.b*0.8f), 0.1f,0.2f, L,H,fillL);
            // kariginu: stiff sleeves that hold their shape. Folded, never draped -- this is the
            // clearest place the theme separates from Dawn.
            SakFold(px,R, V(46+sway,86), V(64+sway,86), V(62+sway,58), V(44+sway,60), 0.55f,
                    ArtCloth, ArtClothDk);
            SakFold(px,R, V(64+sway,86), V(82+sway,86), V(84+sway,60), V(66+sway,58), 0.45f,
                    new Color(Mathf.Min(ArtCloth.r*1.08f,1f), Mathf.Min(ArtCloth.g*1.08f,1f),
                              Mathf.Min(ArtCloth.b*1.08f,1f)), ArtClothDk);
            SakFold(px,R, V(50+sway,58), V(78+sway,58), V(76+sway,30), V(52+sway,30), 0.5f,
                    SakPaper, SakPaperDk);
            for (int c = 0; c < 3; c++)
                DrawLineR(px,R, (56f+sway+c*8f)*s, 56f*s, (55f+sway+c*8f)*s, 32f*s, 1.1f*s, SakPaperDk);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.0f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.4f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s,head.y-8f*s), new Vector2(head.x+9f*s,head.y-7f*s),
                                  new Vector2(head.x+8f*s,head.y+6f*s), new Vector2(head.x-7f*s,head.y+5f*s) },
                     new Color(0.84f,0.72f,0.62f), new Color(0.54f,0.44f,0.38f), 2f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y-1f*s, 2.4f*s, 1.8f*s, new Color(0.14f,0.08f,0.06f,0.92f));
            // eboshi: a tall folded black cap -- the read
            SakFold(px,R, new Vector2(head.x-8f*s, head.y+6f*s), new Vector2(head.x+8f*s, head.y+6f*s),
                          new Vector2(head.x+5f*s, head.y+28f*s), new Vector2(head.x-3f*s, head.y+26f*s),
                    0.5f, SakLacq, new Color(0.05f,0.03f,0.04f));
            {
                float rr = cast ? 18f : 12f;
                float ox = nH.x/s + 10f, oy = nH.y/s + 4f;
                for (int i = 0; i < 5; i++)
                {
                    float a = (i*72f + q*36f) * Mathf.Deg2Rad;
                    float tx = ox + Mathf.Cos(a)*rr, ty = oy + Mathf.Sin(a)*rr*0.75f;
                    P3DPlate(px,R, new[]{ V(tx-3.4f,ty+6), V(tx+3.4f,ty+6), V(tx+3.4f,ty-6), V(tx-3.4f,ty-6) },
                             SakPaper, SakPaperDk, 1.0f);
                    DrawLineR(px,R, tx*s,(ty+4f)*s, tx*s,(ty-4f)*s, 1.1f*s, ArtCloth);
                    DrawLineR(px,R, (tx-2f)*s,(ty+1f)*s, (tx+2f)*s,(ty+1f)*s, 1.0f*s, ArtCloth);
                }
                if (cast)
                    P3DEllipseGlow(px,R, ox*s, oy*s, 26f*s, 22f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.35f));
                if (q == 4)
                    for (int b = 0; b < 3; b++)
                        SakBlossom(px,R, ox+24f+b*10f, oy+4f-b*7f, 3.2f-b*0.6f, 0.62f-b*0.16f, s);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SYNTHWAVE — extruded chrome, lit from the grid below
        //
        //  Every other theme has a MATERIAL: plate, cast bronze, chitin, iron,
        //  cloth, lacquered paper. Synthwave has chrome, which is a finish rather
        //  than a substance -- it looks like whatever it reflects, so on its own it
        //  cannot carry an identity. Three rules stand in for the missing material:
        //    1. every flat face carries a magenta-to-cyan horizon gradient,
        //    2. every silhouette is extruded from a primitive, never organic,
        //    3. every unit stands on a visible ground grid.
        //  That is also why several of these are geometric solids rather than
        //  figures -- it is the one theme where that reads as deliberate.
        // ══════════════════════════════════════════════════════════════════════

        static Color SynMag  => new Color(1.00f, 0.24f, 0.68f);
        static Color SynCya  => new Color(0.30f, 0.92f, 1.00f);
        static Color SynGrid => new Color(0.55f, 0.20f, 0.85f);
        static Color SynChr(P3DP p)   => Color.Lerp(new Color(0.66f,0.68f,0.74f), p.body, 0.20f);
        static Color SynChrHi(P3DP p) { var b = SynChr(p);
            return new Color(Mathf.Min(b.r*1.55f+0.16f,1f), Mathf.Min(b.g*1.55f+0.16f,1f),
                             Mathf.Min(b.b*1.55f+0.18f,1f)); }
        static Color SynChrDk(P3DP p) { var b = SynChr(p); return new Color(b.r*0.46f, b.g*0.48f, b.b*0.54f); }
        static Color SynChrFar(P3DP p){ var b = SynChr(p); return new Color(b.r*0.28f, b.g*0.30f, b.b*0.36f); }

        /// <summary>NEW: a flat face carrying the sunset. Magenta low, cyan high, with hard
        /// scan bands across the middle. This is the theme's stand-in for a material.</summary>
        static void SynHorizon(Color[] px, int R, Vector2[] pts, bool bands, float s)
        {
            float mn = float.MaxValue, mx = float.MinValue, xl = float.MaxValue, xr = float.MinValue;
            foreach (var v in pts) { mn = Mathf.Min(mn, v.y); mx = Mathf.Max(mx, v.y);
                                     xl = Mathf.Min(xl, v.x); xr = Mathf.Max(xr, v.x); }
            // three stacked bands approximate the gradient with the plate primitive we have
            P3DPlate(px, R, pts, SynCya, SynMag, 2f * s);
            if (!bands) return;
            float ym = (mn + mx) * 0.5f;
            for (int i = 0; i < 3; i++)
                DrawLineR(px, R, xl, ym + i * 3.2f * s, xr, ym + i * 3.2f * s,
                          Mathf.Max(0.8f, (1.6f - i * 0.35f)) * s, new Color(0.06f, 0.02f, 0.10f, 0.75f));
        }

        /// <summary>NEW: a solid you can see the edges through — rows and columns clipped to
        /// the outline. Nothing else in the game shows its own interior.</summary>
        static void SynWire(Color[] px, int R, float x0, float y0, float x1, float y1,
                            int rows, int cols, Color col, float s)
        {
            for (int r = 1; r < rows; r++)
            {
                float y = y0 + (y1 - y0) * r / rows;
                DrawLineR(px, R, x0, y, x1, y, 1.0f * s, col);
            }
            for (int c = 1; c < cols; c++)
            {
                float x = x0 + (x1 - x0) * c / cols;
                DrawLineR(px, R, x, y0, x, y1, 1.0f * s, col);
            }
            DrawLineR(px, R, x0, y0, x1, y0, 1.8f * s, col);
            DrawLineR(px, R, x0, y1, x1, y1, 1.8f * s, col);
            DrawLineR(px, R, x0, y0, x0, y1, 1.8f * s, col);
            DrawLineR(px, R, x1, y0, x1, y1, 1.8f * s, col);
        }

        /// <summary>NEW: the ground grid receding to a vanishing point. Every Synthwave unit
        /// stands on one — it is the theme's shadow.</summary>
        static void SynGridFloor(Color[] px, int R, float cx, float y, float w, int rows, float s)
        {
            for (int r = 0; r < rows; r++)
            {
                float t  = (float)r / rows;
                float ww = w * (1f - t * 0.55f);
                var c = new Color(SynGrid.r, SynGrid.g, SynGrid.b, 0.55f - t * 0.42f);
                DrawLineR(px, R, (cx-ww)*s, (y - r*3.4f)*s, (cx+ww)*s, (y - r*3.4f)*s, 1.0f*s, c);
            }
            for (int c2 = -3; c2 <= 3; c2++)
                DrawLineR(px, R, (cx + c2*w/3f)*s, y*s, (cx + c2*w/9f)*s, (y - rows*3.4f)*s,
                          1.0f*s, new Color(SynGrid.r, SynGrid.g, SynGrid.b, 0.40f));
            P3DEllipseGlow(px, R, cx*s, (y+2f)*s, w*1.1f*s, 7f*s,
                           new Color(SynMag.r, SynMag.g, SynMag.b, 0.28f));
        }

        /// <summary>NEW: a neon tube — dark halo, saturated core, white filament. Every
        /// Synthwave weapon IS one of these; the light is the object.</summary>
        static void SynTube(Color[] px, int R, float x0, float y0, float x1, float y1, Color col, float s)
        {
            DrawLineR(px, R, x0, y0, x1, y1, 7f*s, new Color(col.r, col.g, col.b, 0.30f));
            DrawLineR(px, R, x0, y0, x1, y1, 3f*s, col);
            DrawLineR(px, R, x0, y0, x1, y1, 1.1f*s, new Color(1f, 1f, 1f, 0.90f));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CYBER — cold composite, exposed mechanism, one hot accent
        //
        //  These are NEW art ids. The canonical trooper/sniper/mech/titan/turret/
        //  drone/interceptor/hacker/shield-bot builders are the fallback set that
        //  every unmapped id in every theme renders as, so they stay frozen --
        //  Cyber gets its own cosmetic on top exactly like the other seven themes.
        //
        //  The difficulty unique to this theme: Cyber must not look like the plain
        //  default set, or it fails as a theme even though the code is correct.
        //  Three rules separate it:
        //    1. joins are PANEL GAPS -- a dark void with a light strip inside --
        //       never a rivet, seam or stitch,
        //    2. exactly ONE saturated element per unit on an otherwise cold body,
        //    3. every joint shows an EXPOSED ACTUATOR, so limbs read as assembled.
        // ══════════════════════════════════════════════════════════════════════

        static Color CybHot  => new Color(0.10f, 0.95f, 0.80f);
        static Color CybHot2 => new Color(1.00f, 0.42f, 0.20f);
        static Color CybCold => new Color(0.42f, 0.72f, 0.85f);
        static Color CybChr(P3DP p)    => Color.Lerp(Art.Metal, p.body, 0.18f);
        static Color CybChrHi(P3DP p)  { var b = CybChr(p);
            return new Color(Mathf.Min(b.r*1.45f+0.14f,1f), Mathf.Min(b.g*1.45f+0.14f,1f),
                             Mathf.Min(b.b*1.45f+0.15f,1f)); }
        static Color CybChrDk(P3DP p)  { var b = CybChr(p); return new Color(b.r*0.44f, b.g*0.46f, b.b*0.50f); }
        static Color CybChrFar(P3DP p) { var b = CybChr(p); return new Color(b.r*0.26f, b.g*0.28f, b.b*0.32f); }

        /// <summary>NEW: the Cyber join — a dark void between two panels with a light strip
        /// running inside it. Medieval rivets plates together; this pulls them apart.</summary>
        static void CybPanelGap(Color[] px, int R, float x0, float y0, float x1, float y1,
                                bool hot, float s)
        {
            DrawLineR(px, R, x0, y0, x1, y1, 5.0f*s, new Color(0.03f, 0.05f, 0.06f));
            DrawLineR(px, R, x0, y0, x1, y1, 1.6f*s,
                      hot ? new Color(CybHot.r, CybHot.g, CybHot.b, 0.85f)
                          : new Color(0.30f, 0.36f, 0.40f, 0.90f));
        }

        /// <summary>NEW: an exposed actuator — hard cylinder with a piston rod through it.
        /// Every other theme hides its joints; Cyber shows how the limb bends.</summary>
        static void CybActuator(Color[] px, int R, float x, float y, float angDeg,
                                float len, float rr, P3DP p, float s)
        {
            float a = angDeg * Mathf.Deg2Rad;
            float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
            float ax = x - dx*len*0.5f*s, ay = y - dy*len*0.5f*s;
            float bx = x + dx*len*0.5f*s, by = y + dy*len*0.5f*s;
            DrawLineR(px, R, ax, ay, bx, by, rr*2f*s, CybChrDk(p));
            DrawLineR(px, R, ax, ay, bx, by, rr*1.1f*s, CybChrHi(p));
            FillCircleR(px, R, x, y, rr*0.95f*s, CybChrDk(p));
            DrawRingR  (px, R, x, y, rr*0.95f*s, 1.5f*s, ArtOutline);
            FillCircleR(px, R, x, y, rr*0.36f*s, new Color(CybHot.r, CybHot.g, CybHot.b, 0.9f));
        }

        /// <summary>NEW: a coolant line — the single saturated element, run diagonally ACROSS
        /// the panels so it ties an otherwise cold, segmented body together.</summary>
        static void CybCoolant(Color[] px, int R, Vector2[] pts, Color col, float s)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                DrawLineR(px, R, pts[i].x, pts[i].y, pts[i+1].x, pts[i+1].y, 5f*s,
                          new Color(col.r, col.g, col.b, 0.25f));
                DrawLineR(px, R, pts[i].x, pts[i].y, pts[i+1].x, pts[i+1].y, 1.8f*s, col);
            }
        }

        /// <summary>Stacked torso panels with a lit gap between them — the Cyber body.</summary>
        static void CybTorso(Color[] px, int R, float cx, float cy, float w, float h,
                             P3DP p, bool hot, float s)
        {
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            P3DPlate(px, R, new[]{ V(cx-w, cy+h), V(cx+w, cy+h),
                                   V(cx+w*0.86f, cy-h*0.55f), V(cx-w*0.86f, cy-h*0.55f) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            CybPanelGap(px,R, (cx-w*0.94f)*s, (cy+h*0.28f)*s, (cx+w*0.94f)*s, (cy+h*0.28f)*s, false, s);
            CybPanelGap(px,R, (cx-w*0.90f)*s, (cy-h*0.12f)*s, (cx+w*0.90f)*s, (cy-h*0.12f)*s, hot, s);
        }

        /// <summary>Sensor visor — one band of light, never a face.</summary>
        static void CybVisor(Color[] px, int R, float hx, float hy, P3DP p, Color col, float s)
        {
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            P3DPlate(px, R, new[]{ V(hx-10,hy-10), V(hx+10,hy-9), V(hx+9,hy+9), V(hx-9,hy+8) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            CybPanelGap(px,R, (hx-9)*s, (hy+3)*s, (hx+9)*s, (hy+4)*s, false, s);
            DrawLineR(px,R, (hx-8)*s, (hy-2)*s, (hx+8)*s, (hy-1)*s, 3.4f*s, col);
            P3DEllipseGlow(px,R, hx*s, (hy-1)*s, 15f*s, 6f*s, new Color(col.r, col.g, col.b, 0.45f));
        }

        // ── DRONE — a machine with visible rotors, not an organ or a mote ────────
        static void P3DBuildCybDrone(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float dv = q == 4 ? -6f : (q == 1 ? 3f : (q == 2 ? -3f : 0f));
            float spin = (q % 2 == 1) ? 45f : 0f;
            P3DPlate(px,R, new[]{ V(54,66+dv), V(74,68+dv), V(72,80+dv), V(56,78+dv) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            CybPanelGap(px,R, 56f*s,(74f+dv)*s, 72f*s,(76f+dv)*s, true, s);
            for (int i = 0; i < 2; i++)
            {
                float dir = i == 1 ? 1f : -1f;
                float ax = 64f + dir*20f, ay = 76f + dv + dir*2f;
                DrawLineR(px,R, 64f*s,(73f+dv)*s, ax*s,ay*s, 3.0f*s, CybChrDk(p));
                FillCircleR(px,R, ax*s, ay*s, 3.0f*s, CybChrDk(p));
                for (int b = 0; b < 2; b++)
                {
                    float a = (spin + b*90f) * Mathf.Deg2Rad;
                    DrawLineR(px,R, (ax - Mathf.Cos(a)*11f)*s, (ay - Mathf.Sin(a)*3.2f)*s,
                                    (ax + Mathf.Cos(a)*11f)*s, (ay + Mathf.Sin(a)*3.2f)*s,
                              1.6f*s, new Color(CybCold.r, CybCold.g, CybCold.b, 0.75f));
                }
            }
            FillCircleR(px,R, 64f*s, (70f+dv)*s, 3.2f*s, CybHot);
            P3DEllipseGlow(px,R, 64f*s, (70f+dv)*s, 13f*s, 11f*s,
                           new Color(CybHot.r, CybHot.g, CybHot.b, 0.45f));
            for (int k = 0; k < 3; k++)
                DrawLineR(px,R, (64f-7f+k*7f)*s, (64f+dv)*s, (64f-11f+k*11f)*s, (54f+dv)*s,
                          1.3f*s, new Color(CybHot.r, CybHot.g, CybHot.b, 0.45f-k*0.1f));
            if (q == 4)
                for (int e = 0; e < 3; e++)
                    P3DEllipseGlow(px,R, (78f+e*10f)*s, (70f+dv-e*3f)*s, (6f-e)*s, (5f-e)*s,
                                   new Color(CybHot.r, CybHot.g, CybHot.b, 0.65f-e*0.16f));
        }

        // ── TROOPER — the one that shows its joints ──────────────────────────────
        static void P3DBuildCybTrooper(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13], fHx = SideJoints[q,14], fHy = SideJoints[q,15];
            float hx = q == 5 ? 60f : 66f;
            var far = new P3DP { body = new Color(p.body.r*0.4f, p.body.g*0.4f, p.body.b*0.4f) };
            P3DLimb(px,R, 56f*s,84f*s, fEx*s,fEy*s, 4.8f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fEx*s,fEy*s, fHx*s,fHy*s, 4.2f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,56f*s, fKx*s,fKy*s, 6.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 5.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, fKx*s, fKy*s, 20f, 11f, 3.4f, far, s);
            P3DLimb(px,R, 63f*s,56f*s, nKx*s,nKy*s, 7.2f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 6.2f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, nKx*s, nKy*s, 20f, 13f, 4.0f, p, s);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+9,fFy+3), V(fFx+7,fFy-4), V(fFx-6,fFy-4) },
                     CybChrFar(p), CybChrDk(p), 1.8f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+9,nFy+3), V(nFx+7,nFy-4), V(nFx-6,nFy-4) },
                     CybChr(p), CybChrDk(p), 1.8f*s);
            CybTorso(px,R, 63f, 70f, 12f, 18f, p, true, s);
            CybCoolant(px,R, new[]{ V(56,52), V(58,66), V(68,70), V(70,84) }, CybHot, s);
            P3DLimb(px,R, 64f*s,84f*s, nEx*s,nEy*s, 5.2f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.6f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, nEx*s, nEy*s, 0f, 10f, 3.4f, p, s);
            CybVisor(px,R, hx, 104f, p, CybHot, s);
            float sw = q == 4 ? 52f : (q == 3 ? -56f : -16f);
            float rad = sw * Mathf.Deg2Rad;
            float dx = -Mathf.Sin(rad), dy = Mathf.Cos(rad);
            DrawLineR(px,R, nHx - dx*8f*s, nHy - dy*8f*s, nHx + dx*30f*s, nHy + dy*30f*s, 4.4f*s, CybChrDk(p));
            DrawLineR(px,R, nHx + dx*14f*s, nHy + dy*14f*s, nHx + dx*30f*s, nHy + dy*30f*s, 2.0f*s,
                      new Color(CybHot.r, CybHot.g, CybHot.b, 0.9f));
            if (q == 4)
                P3DEllipseGlow(px,R, nHx + dx*34f*s, nHy + dy*34f*s, 14f*s, 12f*s,
                               new Color(CybHot.r, CybHot.g, CybHot.b, 0.55f));
        }

        // ── SNIPER — charge state is drawn ON the weapon ─────────────────────────
        static void P3DBuildCybSniper(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13];
            float hx = q == 5 ? 61f : 66f;
            P3DLimb(px,R, 56f*s,82f*s, fEx*s,fEy*s, 4.2f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,54f*s, fKx*s,fKy*s, 5.8f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 5.0f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,54f*s, nKx*s,nKy*s, 6.4f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 5.6f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, nKx*s, nKy*s, 20f, 11f, 3.4f, p, s);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+8,fFy+3), V(fFx+6,fFy-4), V(fFx-6,fFy-4) },
                     CybChrDk(p), CybChrFar(p), 1.7f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+8,nFy+3), V(nFx+6,nFy-4), V(nFx-6,nFy-4) },
                     CybChrDk(p), CybChrFar(p), 1.7f*s);
            CybTorso(px,R, 63f, 68f, 11f, 16f, p, false, s);
            CybCoolant(px,R, new[]{ V(54,52), V(57,64), V(66,68), V(68,82) }, CybCold, s);
            P3DLimb(px,R, 64f*s,82f*s, nEx*s,nEy*s, 4.8f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.2f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            CybVisor(px,R, hx, 102f, p, CybCold, s);
            float gx = nHx, gy = nHy;
            bool chg = Atk(q);
            P3DPlate(px,R, new[]{ new Vector2(gx-6f*s, gy+4f*s), new Vector2(gx+34f*s, gy+3f*s),
                                  new Vector2(gx+34f*s, gy-3f*s), new Vector2(gx-6f*s, gy-4f*s) },
                     CybChrHi(p), CybChrDk(p), 1.8f*s);
            for (int k = 0; k < 4; k++)
                DrawLineR(px,R, gx+(4f+k*8f)*s, gy-3.4f*s, gx+(4f+k*8f)*s, gy+3.4f*s, 1.8f*s,
                          new Color(CybCold.r, CybCold.g, CybCold.b,
                                    chg ? (0.9f - k*0.12f) : (0.30f - k*0.05f)));
            FillCircleR(px,R, gx+36f*s, gy, (chg?3.6f:2.2f)*s, new Color(0.92f,0.99f,1f));
            P3DEllipseGlow(px,R, gx+36f*s, gy, (chg?14f:7f)*s, (chg?12f:6f)*s,
                           new Color(CybCold.r, CybCold.g, CybCold.b, chg?0.65f:0.30f));
            if (q == 4)
            {
                DrawLineR(px,R, gx+40f*s, gy, 124f*s, gy, 2.2f*s, new Color(0.95f,1f,1f,0.95f));
                DrawLineR(px,R, gx+40f*s, gy, 124f*s, gy, 6f*s,
                          new Color(CybCold.r, CybCold.g, CybCold.b, 0.30f));
            }
        }

        // ── MECH — four legs, every joint on show ────────────────────────────────
        static void P3DBuildCybMech(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                    int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float st  = q == 1 ? 2f : (q == 2 ? 0f : (q == 6 ? -2f : (q == 7 ? 0f : 0f)));
            float rec = q == 4 ? -6f : (q == 3 ? 3f : 0f);
            var far = new P3DP { body = new Color(p.body.r*0.4f, p.body.g*0.4f, p.body.b*0.4f) };
            for (int i = 0; i < 2; i++)
            {
                float bx = 48f + i*30f, dir = i == 1 ? 1f : -1f;
                float kx = bx + dir*12f + st, ky = 32f;
                P3DLimb(px,R, bx*s,52f*s, kx*s,ky*s, (i==1?7.0f:5.6f)*s,
                        i==1?CybChrDk(p):CybChrFar(p), 0.6f,0.5f, L,H,fillL);
                P3DLimb(px,R, kx*s,ky*s, (kx+dir*8f)*s,12f*s, (i==1?6.0f:4.8f)*s,
                        i==1?CybChrDk(p):CybChrFar(p), 0.6f,0.5f, L,H,fillL);
                CybActuator(px,R, kx*s, ky*s, dir*40f, 12f, i==1?4.0f:3.2f, i==1?p:far, s);
                P3DPlate(px,R, new[]{ V(kx+dir*8-8,15), V(kx+dir*8+8,15), V(kx+dir*8+6,8), V(kx+dir*8-6,8) },
                         CybChrDk(p), CybChrFar(p), 1.8f*s);
            }
            P3DPlate(px,R, new[]{ V(38,52), V(90,56), V(88,76), V(40,72) }, CybChrHi(p), CybChr(p), 2.4f*s);
            CybPanelGap(px,R, 40f*s,66f*s, 88f*s,69f*s, true, s);
            CybPanelGap(px,R, 41f*s,59f*s, 87f*s,62f*s, false, s);
            CybCoolant(px,R, new[]{ V(42,56), V(52,64), V(74,66), V(86,74) }, CybHot, s);
            P3DPlate(px,R, new[]{ V(56,76), V(76,79), V(74,92), V(58,90) }, CybChrHi(p), CybChr(p), 2.2f*s);
            DrawLineR(px,R, 60f*s,92f*s, 60f*s,102f*s, 3.0f*s, CybChrDk(p));
            FillCircleR(px,R, 60f*s, 104f*s, 3.4f*s, CybHot);
            P3DEllipseGlow(px,R, 60f*s, 104f*s, 11f*s, 10f*s, new Color(CybHot.r,CybHot.g,CybHot.b,0.5f));
            float ang = q == 3 ? 18f : (q == 4 ? -8f : 4f);
            float a2 = ang * Mathf.Deg2Rad;
            float dx = Mathf.Cos(a2), dy = Mathf.Sin(a2);
            float pvx = (74f + rec)*s, pvy = 84f*s;
            DrawLineR(px,R, pvx, pvy, pvx+dx*28f*s, pvy+dy*28f*s, 5.0f*s, CybChrDk(p));
            DrawLineR(px,R, pvx+dx*14f*s, pvy+dy*14f*s, pvx+dx*28f*s, pvy+dy*28f*s, 2.2f*s,
                      new Color(CybHot.r, CybHot.g, CybHot.b, 0.9f));
            CybActuator(px,R, pvx, pvy, ang, 10f, 3.6f, p, s);
            if (q == 4)
                for (int e = 0; e < 3; e++)
                    P3DEllipseGlow(px,R, pvx+(34f+e*11f)*s, pvy-e*3f*s, (7f-e*1.3f)*s, (6f-e*1.2f)*s,
                                   new Color(CybHot.r, CybHot.g, CybHot.b, 0.6f-e*0.15f));
        }

        // ── WARDEN — the shield physically SPREADS on the brace ──────────────────
        static void P3DBuildCybShield(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            // joints are raw 128-space values, so the nudge is raw too -- not scaled
            float nKx = SideJoints[q,0]-2f, nKy = SideJoints[q,1];
            float nFx = SideJoints[q,2]-2f, nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4]-2f, fKy = SideJoints[q,5];
            float fFx = SideJoints[q,6]-2f, fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13];
            float hx = q == 5 ? 58f : 63f;
            P3DLimb(px,R, 55f*s,80f*s, fEx*s,fEy*s, 5.4f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 62f*s,52f*s, fKx*s,fKy*s, 7.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 6.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 62f*s,52f*s, nKx*s,nKy*s, 8.6f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 7.4f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, nKx*s, nKy*s, 20f, 14f, 4.4f, p, s);
            P3DPlate(px,R, new[]{ V(nFx-8,nFy+4), V(nFx+9,nFy+4), V(nFx+8,nFy-5), V(nFx-7,nFy-5) },
                     CybChrDk(p), CybChrFar(p), 1.9f*s);
            P3DPlate(px,R, new[]{ V(fFx-8,fFy+4), V(fFx+9,fFy+4), V(fFx+8,fFy-5), V(fFx-7,fFy-5) },
                     CybChrDk(p), CybChrFar(p), 1.9f*s);
            CybTorso(px,R, 62f, 66f, 14f, 17f, p, true, s);
            CybVisor(px,R, hx, 98f, p, CybHot, s);
            P3DLimb(px,R, 63f*s,80f*s, nEx*s,nEy*s, 5.6f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.8f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            float bx = 64f + (q == 4 ? 14f : (q == 3 ? 3f : 9f));
            float spread = Atk(q) ? 3f : 0f;
            for (int i = 0; i < 3; i++)
            {
                float yy = 44f + i*18f, off = (i-1)*spread;
                P3DPlate(px,R, new[]{ V(bx-13+off,yy), V(bx+13+off,yy+1),
                                      V(bx+13+off,yy+16), V(bx-13+off,yy+15) },
                         CybChrHi(p), CybChr(p), 2.0f*s);
                CybPanelGap(px,R, (bx-12+off)*s,(yy+15.5f)*s, (bx+12+off)*s,(yy+16.5f)*s, i==1, s);
            }
            DrawLineR(px,R, (bx-11f)*s,60f*s, (bx-11f)*s,90f*s, 2.2f*s,
                      new Color(CybHot.r, CybHot.g, CybHot.b, 0.75f));
            P3DEllipseGlow(px,R, bx*s, 68f*s, 20f*s, 30f*s, new Color(CybHot.r,CybHot.g,CybHot.b,0.16f));
        }

        // ── INTERCEPTOR — a forward-swept aircraft, not a bird or an insect ──────
        static void P3DBuildCybInter(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float beat = q == 1 ? 4f : (q == 2 ? -4f : (Atk(q) ? 7f : 0f));
            float dv = q == 4 ? -4f : 0f;
            P3DPlate(px,R, new[]{ V(30,56+dv), V(84,62+dv), V(92,54+dv), V(36,46+dv) },
                     CybChrHi(p), CybChr(p), 2.4f*s);
            CybPanelGap(px,R, 38f*s,(52f+dv)*s, 86f*s,(58f+dv)*s, true, s);
            P3DPlate(px,R, new[]{ V(52,62+dv), V(70,66+dv), V(64,78+dv-beat*0.3f), V(50,72+dv) },
                     CybChrDk(p), CybChrFar(p), 2.0f*s);
            P3DPlate(px,R, new[]{ V(52,46+dv), V(70,50+dv), V(64,36+dv+beat*0.3f), V(50,42+dv) },
                     CybChrDk(p), CybChrFar(p), 2.0f*s);
            FillCircleR(px,R, 84f*s, (60f+dv)*s, 4.0f*s, CybCold);
            P3DEllipseGlow(px,R, 84f*s, (60f+dv)*s, 13f*s, 10f*s,
                           new Color(CybCold.r, CybCold.g, CybCold.b, 0.45f));
            CybCoolant(px,R, new[]{ V(40,52+dv), V(58,56+dv), V(80,60+dv) }, CybHot, s);
            for (int e = 0; e < 3; e++)
                P3DEllipseGlow(px,R, (26f-e*8f)*s, (54f+dv-e*2f)*s, (7f-e*1.4f)*s, (5f-e)*s,
                               new Color(CybHot.r, CybHot.g, CybHot.b, 0.6f-e*0.16f));
            if (q == 4)
                for (int b = 0; b < 3; b++)
                    P3DEllipseGlow(px,R, (98f+b*10f)*s, (58f+dv-b*3f)*s, (6f-b)*s, (5f-b)*s,
                                   new Color(CybCold.r, CybCold.g, CybCold.b, 0.7f-b*0.18f));
        }

        // ── RUNNER — the power source is CABLED, not held ────────────────────────
        static void P3DBuildCybHacker(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8]+sway, nEy = SideJoints[q,9]+bob;
            float nHx = SideJoints[q,10]+sway, nHy = SideJoints[q,11]+bob;
            float fEx = SideJoints[q,12]+sway, fEy = SideJoints[q,13]+bob;
            float hx = (q == 5 ? 60f : 65f) + sway;
            bool cast = Atk(q);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 4.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 5.2f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+8,fFy+3), V(fFx+6,fFy-4), V(fFx-6,fFy-4) },
                     CybChrDk(p), CybChrFar(p), 1.7f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+8,nFy+3), V(nFx+6,nFy-4), V(nFx-6,nFy-4) },
                     CybChrDk(p), CybChrFar(p), 1.7f*s);
            P3DLimb(px,R, (56f+sway)*s,(84f+bob)*s, fEx*s,fEy*s, 5.0f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            CybTorso(px,R, 63f+sway, 68f, 11f, 17f, p, true, s);
            P3DPlate(px,R, new[]{ V(52+sway,72), V(60+sway,74), V(58+sway,90), V(50+sway,88) },
                     CybChrDk(p), CybChrFar(p), 2.0f*s);
            P3DLimb(px,R, (64f+sway)*s,(84f+bob)*s, nEx*s,nEy*s, 5.0f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.4f*s, CybChr(p), 0.6f,0.5f, L,H,fillL);
            CybVisor(px,R, hx, 102f+bob, p, CybHot, s);
            float dkx = nHx + 14f*s, dky = nHy + (cast ? 8f : 2f)*s;
            CybCoolant(px,R, new[]{ V(54+sway,86), new Vector2(nHx-4f*s, nHy+10f*s),
                                    new Vector2(dkx-6f*s, dky+2f*s) }, CybHot, s);
            P3DPlate(px,R, new[]{ new Vector2(dkx-9f*s,dky+7f*s), new Vector2(dkx+9f*s,dky+8f*s),
                                  new Vector2(dkx+8f*s,dky-5f*s), new Vector2(dkx-8f*s,dky-6f*s) },
                     CybChrHi(p), CybChr(p), 2.0f*s);
            for (int k = 0; k < 3; k++)
                DrawLineR(px,R, dkx-6f*s, dky+(-2f+k*3.4f)*s, dkx+6f*s, dky+(-2f+k*3.4f)*s, 1.3f*s,
                          new Color(CybHot.r, CybHot.g, CybHot.b, cast ? (0.9f-k*0.16f) : 0.32f));
            if (cast)
            {
                P3DEllipseGlow(px,R, dkx, dky, 17f*s, 15f*s, new Color(CybHot.r,CybHot.g,CybHot.b,0.4f));
                for (int s2 = 0; s2 < 4; s2++)
                    DrawRingR(px,R, dkx+(10f+s2*10f)*s, dky, (3f+s2*2.2f)*s, 1.3f*s,
                              new Color(CybHot.r, CybHot.g, CybHot.b, 0.6f-s2*0.13f));
            }
        }

        // ── TITAN — visibly ASSEMBLED from three segments, sensor block for a head ─
        static void P3DBuildCybTitan(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                     int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy, rock;
            if (q == 1)      { nKx= 20; nKy=26; nFx= 32; nFy= 8; fKx=-18; fKy=24; fFx=-28; fFy=8; rock= 3f; }
            else if (q == 2) { nKx= -6; nKy=32; nFx=-12; nFy=18; fKx= 16; fKy=25; fFx= 28; fFy=8; rock=-1f; }
            else if (q == 6) { nKx=-18; nKy=24; nFx=-28; nFy= 8; fKx= 20; fKy=26; fFx= 32; fFy=8; rock=-3f; }
            else if (q == 7) { nKx= 16; nKy=25; nFx= 28; nFy= 8; fKx= -6; fKy=32; fFx=-12; fFy=18; rock= 1f; }
            else if (q == 3) { nKx= 20; nKy=24; nFx= 30; nFy= 8; fKx=-20; fKy=24; fFx=-30; fFy=8; rock= 0f; }
            else if (q == 4) { nKx= 22; nKy=23; nFx= 32; nFy= 8; fKx=-22; fKy=23; fFx=-32; fFy=8; rock= 0f; }
            else if (q == 5) { nKx=-14; nKy=25; nFx=-22; nFy= 8; fKx= 18; fKy=25; fFx= 26; fFy=8; rock= 0f; }
            else             { nKx= 12; nKy=25; nFx= 14; nFy= 8; fKx=-12; fKy=25; fFx=-14; fFy=8; rock= 0f; }
            float hdx = q == 3 ? -12f : (q == 4 ? 10f : 0f), hx = 64f + hdx;
            var far = new P3DP { body = new Color(p.body.r*0.4f, p.body.g*0.4f, p.body.b*0.4f) };
            P3DLimb(px,R, 58f*s,44f*s, (64f+fKx)*s,fKy*s, 9.0f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, (64f+fKx)*s,fKy*s, (64f+fFx)*s,fFy*s, 7.6f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, (64f+fKx)*s, fKy*s, 25f, 16f, 4.6f, far, s);
            P3DPlate(px,R, new[]{ V(64+fFx-10,fFy+7), V(64+fFx+10,fFy+7), V(64+fFx+8,fFy-6), V(64+fFx-8,fFy-6) },
                     CybChrFar(p), CybChrDk(p), 2f*s);
            P3DLimb(px,R, 70f*s,44f*s, (64f+nKx)*s,nKy*s, 10.5f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, (64f+nKx)*s,nKy*s, (64f+nFx)*s,nFy*s, 8.8f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, (64f+nKx)*s, nKy*s, 25f, 19f, 5.4f, p, s);
            P3DPlate(px,R, new[]{ V(64+nFx-12,nFy+8), V(64+nFx+12,nFy+8), V(64+nFx+9,nFy-7), V(64+nFx-9,nFy-7) },
                     CybChr(p), CybChrDk(p), 2.2f*s);
            P3DPlate(px,R, new[]{ V(hx-20,44), V(hx+20,44), V(hx+19,62), V(hx-19,62) },
                     CybChrHi(p), CybChr(p), 2.3f*s);
            CybPanelGap(px,R, (hx-19f)*s,63f*s, (hx+19f)*s,63f*s, true, s);
            P3DPlate(px,R, new[]{ V(hx-19,64), V(hx+19,64), V(hx+18,84), V(hx-18,84) },
                     CybChrHi(p), CybChr(p), 2.3f*s);
            CybPanelGap(px,R, (hx-18f)*s,85f*s, (hx+18f)*s,85f*s, true, s);
            P3DPlate(px,R, new[]{ V(hx-18,86), V(hx+18,86), V(hx+15,104), V(hx-15,104) },
                     CybChrHi(p), CybChr(p), 2.3f*s);
            CybCoolant(px,R, new[]{ V(hx-16,48), V(hx-10,70), V(hx+10,78), V(hx+15,100) }, CybHot, s);
            P3DPlate(px,R, new[]{ V(hx-30,98), V(hx-10,104), V(hx-8,86), V(hx-26,82) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            P3DPlate(px,R, new[]{ V(hx+10,104), V(hx+30,98), V(hx+26,82), V(hx+8,86) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            float fex = hx - (q == 4 ? 20f : 12f);
            P3DLimb(px,R, (hx-16f)*s,94f*s, fex*s,78f*s, 9.5f*s, CybChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fex-9,84), V(fex+7,86), V(fex+9,72), V(fex-7,70) },
                     CybChrFar(p), CybChrDk(p), 2f*s);
            float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 10f)), ey = 78f + (q == 4 ? -6f : 0f);
            P3DLimb(px,R, (hx+16f)*s,94f*s, ex*s,ey*s, 10.5f*s, CybChrDk(p), 0.6f,0.5f, L,H,fillL);
            CybActuator(px,R, (hx+16f)*s, 94f*s, 0f, 14f, 4.4f, p, s);
            P3DPlate(px,R, new[]{ V(ex-9,ey+7), V(ex+9,ey+9), V(ex+11,ey-6), V(ex-7,ey-8) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            DrawLineR(px,R, (ex-5f)*s,(ey+2f)*s, (ex+9f)*s,(ey+3f)*s, 2.6f*s,
                      new Color(CybHot2.r, CybHot2.g, CybHot2.b, 0.9f));
            if (q == 4)
            {
                P3DEllipseGlow(px,R, (ex+22f)*s, ey*s, 24f*s, 20f*s,
                               new Color(CybHot2.r, CybHot2.g, CybHot2.b, 0.5f));
                for (int k = 0; k < 3; k++)
                    DrawRingR(px,R, (ex+18f+k*13f)*s, (ey+1f)*s, (5f+k*2.6f)*s, 1.5f*s,
                              new Color(CybHot2.r, CybHot2.g, CybHot2.b, 0.55f-k*0.14f));
            }
            float sbx = hx + rock*0.4f;
            P3DPlate(px,R, new[]{ V(sbx-11,106), V(sbx+11,107), V(sbx+9,118), V(sbx-9,117) },
                     CybChrHi(p), CybChr(p), 2.2f*s);
            DrawLineR(px,R, (sbx-9f)*s,112f*s, (sbx+9f)*s,113f*s, 3.0f*s, CybHot);
            P3DEllipseGlow(px,R, sbx*s, 112f*s, 16f*s, 7f*s, new Color(CybHot.r,CybHot.g,CybHot.b,0.45f));
        }

        // ── TURRET — twin barrels that RECOIL ────────────────────────────────────
        static void P3DBuildCybTurret(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                      int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            bool chg = Atk(q);
            float rec = q == 4 ? -5f : 0f;
            P3DPlate(px,R, new[]{ V(44,10), V(84,10), V(80,24), V(48,24) }, CybChrDk(p), CybChrFar(p), 2.2f*s);
            CybPanelGap(px,R, 48f*s,22f*s, 80f*s,22f*s, false, s);
            P3DPlate(px,R, new[]{ V(52,24), V(76,24), V(74,44), V(54,44) }, CybChrHi(p), CybChr(p), 2.3f*s);
            CybPanelGap(px,R, 54f*s,38f*s, 74f*s,39f*s, true, s);
            CybActuator(px,R, 64f*s, 46f*s, 0f, 18f, 5.0f, p, s);
            float ang = q == 3 ? 12f : (q == 4 ? -6f : 2f);
            float a2 = ang * Mathf.Deg2Rad;
            float dx = Mathf.Cos(a2), dy = Mathf.Sin(a2);
            for (int i = 0; i < 2; i++)
            {
                float oy = 52f + i*10f;
                DrawLineR(px,R, (62f+rec)*s, oy*s, (62f+rec)*s+dx*30f*s, oy*s+dy*30f*s, 4.2f*s, CybChrDk(p));
                DrawLineR(px,R, (62f+rec)*s+dx*16f*s, oy*s+dy*16f*s,
                                (62f+rec)*s+dx*30f*s, oy*s+dy*30f*s, 1.8f*s,
                          new Color(CybHot.r, CybHot.g, CybHot.b, chg ? 0.95f : 0.40f));
            }
            P3DPlate(px,R, new[]{ V(54,46), V(74,48), V(72,62), V(56,60) }, CybChrHi(p), CybChr(p), 2.2f*s);
            FillCircleR(px,R, 60f*s, 54f*s, 3.0f*s, new Color(CybHot.r, CybHot.g, CybHot.b, chg?1f:0.6f));
            P3DEllipseGlow(px,R, 60f*s, 54f*s, (chg?15f:9f)*s, (chg?13f:8f)*s,
                           new Color(CybHot.r, CybHot.g, CybHot.b, chg?0.5f:0.28f));
            if (q == 4)
                for (int k = 0; k < 2; k++)
                    for (int e = 0; e < 3; e++)
                        P3DEllipseGlow(px,R, (96f+e*10f)*s, (52f+k*10f-e*2f)*s, (6f-e)*s, (5f-e)*s,
                                       new Color(CybHot.r, CybHot.g, CybHot.b, 0.6f-e*0.16f));
        }


        // ── HOLOBOT (drone) — three rings around a core, no solid body at all ───
        static void P3DBuildSynBot(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            float dv = q == 4 ? -6f : (q == 1 ? 3f : (q == 2 ? -3f : 0f));
            float spin = (q % 2 == 1) ? 45f : 0f;
            SynGridFloor(px, R, 64f, 14f, 18f, 3, s);
            for (int i = 0; i < 3; i++)
            {
                float a  = (i * 60f + spin) * Mathf.Deg2Rad;
                float rx = 17f * Mathf.Abs(Mathf.Cos(a)) + 5f;
                var col = i % 2 == 0 ? SynCya : SynMag;
                for (int k = 0; k < 26; k++)
                {
                    float t0 = k / 26f * Mathf.PI * 2f, t1 = (k + 1) / 26f * Mathf.PI * 2f;
                    DrawLineR(px, R, (64f + Mathf.Cos(t0)*rx)*s, ((70f+dv) + Mathf.Sin(t0)*17f)*s,
                                     (64f + Mathf.Cos(t1)*rx)*s, ((70f+dv) + Mathf.Sin(t1)*17f)*s, 2.6f*s, col);
                }
            }
            FillCircleR(px, R, 64f*s, (70f+dv)*s, 6.4f*s, new Color(1f, 0.98f, 1f));
            P3DEllipseGlow(px, R, 64f*s, (70f+dv)*s, 20f*s, 20f*s, new Color(SynMag.r,SynMag.g,SynMag.b,0.45f));
            P3DEllipseGlow(px, R, 64f*s, (70f+dv)*s, 12f*s, 12f*s, new Color(SynCya.r,SynCya.g,SynCya.b,0.55f));
            for (int k = 0; k < 3; k++)
                DrawLineR(px, R, (64f-9f+k*9f)*s, (54f+dv)*s, (64f-14f+k*14f)*s, (42f+dv)*s,
                          1.3f*s, new Color(SynCya.r,SynCya.g,SynCya.b, 0.5f-k*0.1f));
            if (q == 4)
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px, R, (78f+e*10f)*s, (68f+dv-e*3f)*s, (6f-e)*s, (5f-e)*s,
                                   new Color(SynMag.r,SynMag.g,SynMag.b, 0.7f-e*0.15f));
        }

        // ── RACER (trooper) — jacket as a flat slab with a sunset printed on it ──
        static void P3DBuildSynRacer(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13], fHx = SideJoints[q,14], fHy = SideJoints[q,15];
            float hx = q == 5 ? 60f : 66f;
            SynGridFloor(px, R, 64f, 14f, 26f, 4, s);
            P3DLimb(px,R, 56f*s,84f*s, fEx*s,fEy*s, 4.8f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fEx*s,fEy*s, fHx*s,fHy*s, 4.2f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,56f*s, fKx*s,fKy*s, 6.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 5.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,56f*s, nKx*s,nKy*s, 7.2f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 6.2f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+9,fFy+3), V(fFx+7,fFy-4), V(fFx-6,fFy-4) },
                     SynChrFar(p), SynChrDk(p), 1.7f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+9,nFy+3), V(nFx+7,nFy-4), V(nFx-6,nFy-4) },
                     SynChr(p), SynChrDk(p), 1.7f*s);
            SynTube(px,R, (nFx-6)*s,(nFy-2)*s, (nFx+8)*s,(nFy-2)*s, SynCya, s);
            SynHorizon(px,R, new[]{ V(52,88), V(74,88), V(76,68), V(70,52), V(56,52), V(50,68) }, true, s);
            SynTube(px,R, 52f*s,88f*s, 74f*s,88f*s, SynMag, s);
            SynTube(px,R, 50f*s,68f*s, 56f*s,52f*s, SynCya, s);
            SynTube(px,R, 70f*s,52f*s, 76f*s,68f*s, SynCya, s);
            P3DLimb(px,R, 64f*s,84f*s, nEx*s,nEy*s, 5.2f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.6f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(hx-10,114), V(hx+10,115), V(hx+9,133), V(hx-9,132) },
                     SynChrHi(p), SynChr(p), 2.2f*s);
            SynTube(px,R, (hx-9)*s,124f*s, (hx+9)*s,125f*s, SynCya, s);
            P3DEllipseGlow(px,R, hx*s,124f*s, 16f*s,6f*s, new Color(SynCya.r,SynCya.g,SynCya.b,0.40f));
            float sw = q == 4 ? 52f : (q == 3 ? -56f : -16f);
            float rad = sw * Mathf.Deg2Rad;
            float dx = -Mathf.Sin(rad), dy = Mathf.Cos(rad);
            SynTube(px,R, (nHx - dx*10f*s), (nHy - dy*10f*s), (nHx + dx*36f*s), (nHy + dy*36f*s), SynMag, s);
            if (q == 4)
                P3DEllipseGlow(px,R, nHx + dx*40f*s, nHy + dy*40f*s, 16f*s, 14f*s,
                               new Color(SynMag.r,SynMag.g,SynMag.b,0.55f));
        }

        // ── LASER (sniper) — the weapon is a beam in an open frame, no barrel ────
        static void P3DBuildSynLaser(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13];
            float hx = q == 5 ? 61f : 66f;
            SynGridFloor(px, R, 64f, 14f, 24f, 4, s);
            P3DLimb(px,R, 56f*s,82f*s, fEx*s,fEy*s, 4.2f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,54f*s, fKx*s,fKy*s, 5.8f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 5.0f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 63f*s,54f*s, nKx*s,nKy*s, 6.4f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 5.6f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+8,fFy+3), V(fFx+6,fFy-4), V(fFx-6,fFy-4) },
                     SynChrDk(p), SynChrFar(p), 1.6f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+8,nFy+3), V(nFx+6,nFy-4), V(nFx-6,nFy-4) },
                     SynChrDk(p), SynChrFar(p), 1.6f*s);
            SynHorizon(px,R, new[]{ V(54,84), V(74,84), V(76,66), V(69,52), V(57,52), V(52,66) }, true, s);
            SynTube(px,R, 52f*s,66f*s, 57f*s,52f*s, SynMag, s);
            SynTube(px,R, 69f*s,52f*s, 76f*s,66f*s, SynMag, s);
            P3DLimb(px,R, 64f*s,82f*s, nEx*s,nEy*s, 4.8f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.2f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(hx-9,113), V(hx+9,114), V(hx+8,130), V(hx-8,129) },
                     SynChrHi(p), SynChr(p), 2.1f*s);
            SynTube(px,R, (hx-8)*s,121f*s, (hx+8)*s,122f*s, SynMag, s);
            bool open = Atk(q);
            float gx = nHx + 8f*s, gy = nHy;
            SynTube(px,R, gx, gy+14f*s, gx+6f*s, gy, SynCya, s);
            SynTube(px,R, gx+6f*s, gy, gx, gy-14f*s, SynCya, s);
            FillCircleR(px,R, gx+8f*s, gy, (open?4.4f:2.6f)*s, new Color(1f,0.98f,1f));
            P3DEllipseGlow(px,R, gx+8f*s, gy, (open?16f:8f)*s, (open?14f:7f)*s,
                           new Color(SynCya.r,SynCya.g,SynCya.b, open?0.70f:0.35f));
            if (q == 4)
            {
                DrawLineR(px,R, gx+14f*s, gy, 124f*s, gy, 2.6f*s, new Color(1f,0.98f,1f,0.95f));
                DrawLineR(px,R, gx+14f*s, gy, 124f*s, gy, 7f*s, new Color(SynCya.r,SynCya.g,SynCya.b,0.35f));
                for (int k = 0; k < 3; k++)
                    DrawRingR(px,R, gx+(30f+k*24f)*s, gy, (4f+k*2.4f)*s, 1.4f*s,
                              new Color(SynCya.r,SynCya.g,SynCya.b, 0.5f-k*0.12f));
            }
        }

        // ── CRUISER (mech) — hover chassis; the GAP under it is the read ────────
        static void P3DBuildSynCruiser(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                       int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float st  = q == 1 ? 2f : (q == 2 ? 0f : (q == 6 ? -2f : (q == 7 ? 0f : 0f)));
            float rec = q == 4 ? -6f : (q == 3 ? 3f : 0f);
            SynGridFloor(px, R, 64f, 14f, 34f, 5, s);
            SynHorizon(px,R, new[]{ V(34,42), V(94,48), V(92,64), V(36,58) }, true, s);
            SynTube(px,R, 34f*s,42f*s, 94f*s,48f*s, SynCya, s);
            SynTube(px,R, 36f*s,58f*s, 92f*s,64f*s, SynCya, s);
            for (int i = 0; i < 3; i++)
            {
                float x = 44f + i*20f + st*2f;
                P3DPlate(px,R, new[]{ V(x-9,40), V(x+9,41), V(x+8,32), V(x-8,31) },
                         SynChrDk(p), SynChrFar(p), 1.7f*s);
                P3DEllipseGlow(px,R, x*s, (24f-st)*s, 15f*s, 8f*s,
                               new Color(SynMag.r,SynMag.g,SynMag.b,0.50f));
                for (int k = 0; k < 3; k++)
                    DrawLineR(px,R, (x-7f+k*7f)*s, 30f*s, (x-7f+k*7f)*s, (20f-st)*s, 1.4f*s,
                              new Color(SynMag.r,SynMag.g,SynMag.b, 0.55f-k*0.12f));
            }
            SynWire(px,R, 52f*s,64f*s, 80f*s,86f*s, 4, 4, new Color(SynCya.r,SynCya.g,SynCya.b,0.55f), s);
            float ang = (q == 3 ? 24f : (q == 4 ? -10f : 6f)) * Mathf.Deg2Rad;
            float dx = Mathf.Cos(ang), dy = Mathf.Sin(ang);
            float pvx = (76f + rec) * s, pvy = 76f * s;
            SynTube(px,R, pvx, pvy, pvx + dx*30f*s, pvy + dy*30f*s, SynMag, s);
            FillCircleR(px,R, pvx + dx*32f*s, pvy + dy*32f*s, (q>=3?5.4f:3.4f)*s, new Color(1f,0.94f,1f));
            P3DEllipseGlow(px,R, pvx + dx*32f*s, pvy + dy*32f*s, (q>=3?18f:10f)*s, (q>=3?16f:9f)*s,
                           new Color(SynMag.r,SynMag.g,SynMag.b, q>=3?0.70f:0.40f));
            if (q == 4)
                for (int e = 0; e < 3; e++)
                    P3DEllipseGlow(px,R, pvx + (44f+e*12f)*s, pvy - e*4f*s, (7f-e*1.3f)*s, (6f-e*1.2f)*s,
                                   new Color(SynMag.r,SynMag.g,SynMag.b, 0.65f-e*0.16f));
        }

        // ── BOUNCER (shield-bot) — a barrier you can SEE THROUGH, lit on the edge ──
        static void P3DBuildSynBouncer(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8], nEy = SideJoints[q,9], nHx = SideJoints[q,10], nHy = SideJoints[q,11];
            float fEx = SideJoints[q,12], fEy = SideJoints[q,13];
            float hx = q == 5 ? 58f : 63f;
            SynGridFloor(px, R, 64f, 14f, 28f, 4, s);
            P3DLimb(px,R, 55f*s,80f*s, fEx*s,fEy*s, 5.4f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 62f*s,52f*s, fKx*s,fKy*s, 7.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 6.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, 62f*s,52f*s, nKx*s,nKy*s, 8.6f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 7.4f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fFx-8,fFy+4), V(fFx+9,fFy+4), V(fFx+8,fFy-5), V(fFx-7,fFy-5) },
                     SynChrDk(p), SynChrFar(p), 1.8f*s);
            P3DPlate(px,R, new[]{ V(nFx-8,nFy+4), V(nFx+9,nFy+4), V(nFx+8,nFy-5), V(nFx-7,nFy-5) },
                     SynChrDk(p), SynChrFar(p), 1.8f*s);
            SynHorizon(px,R, new[]{ V(50,84), V(76,84), V(78,64), V(72,50), V(54,50), V(48,64) }, true, s);
            SynTube(px,R, 48f*s,64f*s, 54f*s,50f*s, SynCya, s);
            SynTube(px,R, 72f*s,50f*s, 78f*s,64f*s, SynCya, s);
            P3DPlate(px,R, new[]{ V(hx-11,109), V(hx+11,110), V(hx+10,125), V(hx-10,124) },
                     SynChrHi(p), SynChr(p), 2.2f*s);
            SynTube(px,R, (hx-9)*s,117f*s, (hx+9)*s,118f*s, SynMag, s);
            P3DLimb(px,R, 63f*s,80f*s, nEx*s,nEy*s, 5.6f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.8f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            float bx = 64f + (q == 4 ? 14f : (q == 3 ? 3f : 9f));
            SynWire(px,R, (bx-14f)*s,36f*s, (bx+14f)*s,92f*s, 7, 3,
                    new Color(SynCya.r,SynCya.g,SynCya.b,0.45f), s);
            SynTube(px,R, (bx-14f)*s,92f*s, (bx+14f)*s,92f*s, SynMag, s);
            SynTube(px,R, (bx+14f)*s,92f*s, (bx+14f)*s,36f*s, SynMag, s);
            SynTube(px,R, (bx+14f)*s,36f*s, (bx-14f)*s,36f*s, SynMag, s);
            SynTube(px,R, (bx-14f)*s,36f*s, (bx-14f)*s,92f*s, SynMag, s);
            P3DEllipseGlow(px,R, bx*s, 64f*s, 22f*s, 34f*s,
                           new Color(SynMag.r,SynMag.g,SynMag.b,0.22f));
        }

        // ── SPEEDER (interceptor) — a vehicle WITH A DRIVER; two masses, not one ──
        static void P3DBuildSynSpeeder(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float beat = q == 1 ? 4f : (q == 2 ? -4f : (Atk(q) ? 7f : 0f));
            float dv = q == 4 ? -4f : 0f;
            SynGridFloor(px, R, 64f, 16f, 30f, 5, s);
            SynHorizon(px,R, new[]{ V(26,58+dv), V(86,64+dv), V(92,54+dv), V(34,48+dv) }, true, s);
            SynTube(px,R, 26f*s,(58f+dv)*s, 86f*s,(64f+dv)*s, SynCya, s);
            SynTube(px,R, 92f*s,(54f+dv)*s, 34f*s,(48f+dv)*s, SynCya, s);
            for (int i = 0; i < 2; i++)
            {
                float x = 40f + i*36f;
                P3DPlate(px,R, new[]{ V(x-8,46+dv), V(x+8,47+dv), V(x+7,40+dv), V(x-7,39+dv) },
                         SynChrDk(p), SynChrFar(p), 1.7f*s);
                P3DEllipseGlow(px,R, x*s, (34f+dv-beat*0.3f)*s, 13f*s, 7f*s,
                               new Color(SynMag.r,SynMag.g,SynMag.b,0.50f));
                for (int k = 0; k < 3; k++)
                    DrawLineR(px,R, (x-7f+k*7f)*s, (38f+dv)*s, (x-7f+k*7f)*s, (30f+dv-beat*0.4f)*s,
                              1.4f*s, new Color(SynMag.r,SynMag.g,SynMag.b, 0.6f-k*0.12f));
            }
            P3DPlate(px,R, new[]{ V(52,60+dv), V(68,66+dv), V(74,78+dv), V(60,76+dv) },
                     SynChrHi(p), SynChr(p), 2.1f*s);
            FillCircleR(px,R, 76f*s, (80f+dv)*s, 6.4f*s, SynChr(p));
            DrawRingR  (px,R, 76f*s, (80f+dv)*s, 6.4f*s, 1.8f*s, ArtOutline);
            SynTube(px,R, 71f*s,(80f+dv)*s, 81f*s,(81f+dv)*s, SynCya, s);
            P3DLimb(px,R, 66f*s,(70f+dv)*s, 84f*s,(66f+dv)*s, 3.4f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            SynTube(px,R, 84f*s,(70f+dv)*s, 92f*s,(68f+dv)*s, SynMag, s);
            for (int e = 0; e < 3; e++)
                P3DEllipseGlow(px,R, (22f-e*8f)*s, (56f+dv-e*2f)*s, (7f-e*1.4f)*s, (5f-e)*s,
                               new Color(SynCya.r,SynCya.g,SynCya.b, 0.55f-e*0.15f));
            if (q == 4)
                for (int b = 0; b < 3; b++)
                    P3DEllipseGlow(px,R, (98f+b*10f)*s, (58f+dv-b*3f)*s, (6f-b)*s, (5f-b)*s,
                                   new Color(SynMag.r,SynMag.g,SynMag.b, 0.7f-b*0.18f));
        }

        // ── SYNTH (hacker) — keytar; the attack is SOUND DRAWN AS BARS ───────────
        static void P3DBuildSynKeytar(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);
            float nKx = SideJoints[q,0], nKy = SideJoints[q,1], nFx = SideJoints[q,2], nFy = SideJoints[q,3];
            float fKx = SideJoints[q,4], fKy = SideJoints[q,5], fFx = SideJoints[q,6], fFy = SideJoints[q,7];
            float nEx = SideJoints[q,8] + sway, nEy = SideJoints[q,9] + bob;
            float nHx = SideJoints[q,10] + sway, nHy = SideJoints[q,11] + bob;
            float fEx = SideJoints[q,12] + sway, fEy = SideJoints[q,13] + bob;
            float hx = (q == 5 ? 60f : 65f) + sway;
            bool cast = Atk(q);
            SynGridFloor(px, R, 64f, 14f, 24f, 4, s);
            P3DLimb(px,R, fKx*s,fKy*s, fFx*s,fFy*s, 4.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nKx*s,nKy*s, nFx*s,nFy*s, 5.2f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fFx-7,fFy+3), V(fFx+8,fFy+3), V(fFx+6,fFy-4), V(fFx-6,fFy-4) },
                     SynChrDk(p), SynChrFar(p), 1.6f*s);
            P3DPlate(px,R, new[]{ V(nFx-7,nFy+3), V(nFx+8,nFy+3), V(nFx+6,nFy-4), V(nFx-6,nFy-4) },
                     SynChrDk(p), SynChrFar(p), 1.6f*s);
            P3DLimb(px,R, (56f+sway)*s,(84f+bob)*s, fEx*s,fEy*s, 5.0f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            SynWire(px,R, (48f+sway)*s,30f*s, (80f+sway)*s,86f*s, 7, 4,
                    new Color(SynMag.r,SynMag.g,SynMag.b,0.42f), s);
            SynHorizon(px,R, new[]{ V(54+sway,86), V(74+sway,86), V(72+sway,56), V(56+sway,56) }, true, s);
            SynTube(px,R, (48f+sway)*s,86f*s, (52f+sway)*s,30f*s, SynCya, s);
            SynTube(px,R, (80f+sway)*s,86f*s, (76f+sway)*s,30f*s, SynCya, s);
            P3DLimb(px,R, (64f+sway)*s,(84f+bob)*s, nEx*s,nEy*s, 5.0f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, nEx*s,nEy*s, nHx*s,nHy*s, 4.4f*s, SynChr(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(hx-9,113), V(hx+9,114), V(hx+8,130), V(hx-8,129) },
                     SynChrHi(p), SynChr(p), 2.1f*s);
            SynTube(px,R, (hx-8)*s,121f*s, (hx+8)*s,122f*s, SynCya, s);
            float tilt = (cast ? -14f : -4f) * Mathf.Deg2Rad;
            float dx = Mathf.Cos(tilt), dy = Mathf.Sin(tilt);
            float ax = nHx - dx*8f*s,  ay = nHy - dy*8f*s;
            float bx = nHx + dx*34f*s, by = nHy + dy*34f*s;
            float pvx = -dy*7f*s, pvy = dx*7f*s;
            P3DPlate(px,R, new[]{ new Vector2(ax+pvx,ay+pvy), new Vector2(bx+pvx,by+pvy),
                                  new Vector2(bx-pvx,by-pvy), new Vector2(ax-pvx,ay-pvy) },
                     SynChrHi(p), SynChr(p), 2.0f*s);
            for (int k = 0; k < 7; k++)
            {
                float t = (k + 0.5f) / 7f;
                float mx = ax + (bx-ax)*t, my = ay + (by-ay)*t;
                DrawLineR(px,R, mx+pvx*0.8f, my+pvy*0.8f, mx-pvx*0.8f, my-pvy*0.8f, 1.4f*s,
                          (cast && k % 2 == 0) ? new Color(1f,0.98f,1f) : new Color(0.10f,0.06f,0.14f));
            }
            SynTube(px,R, ax+pvx*1.1f, ay+pvy*1.1f, bx+pvx*1.1f, by+pvy*1.1f, SynMag, s);
            if (cast)
                for (int b2 = 0; b2 < 4; b2++)
                {
                    float hgt = ((q == 4 ? 16f : 9f) - b2*2.4f) * s;
                    DrawLineR(px,R, bx+(8f+b2*9f)*s, nHy-hgt, bx+(8f+b2*9f)*s, nHy+hgt, 2.4f*s,
                              new Color(SynCya.r,SynCya.g,SynCya.b, 0.75f-b2*0.16f));
                }
        }

        // ── OBELISK (titan) — a geometric solid. No head, no hands, no anatomy ──
        static void P3DBuildSynObelisk(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                       int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy, rock;
            if (q == 1)      { nKx= 20; nKy=26; nFx= 32; nFy= 8; fKx=-18; fKy=24; fFx=-28; fFy=8; rock= 3f; }
            else if (q == 2) { nKx= -6; nKy=32; nFx=-12; nFy=18; fKx= 16; fKy=25; fFx= 28; fFy=8; rock=-1f; }
            else if (q == 6) { nKx=-18; nKy=24; nFx=-28; nFy= 8; fKx= 20; fKy=26; fFx= 32; fFy=8; rock=-3f; }
            else if (q == 7) { nKx= 16; nKy=25; nFx= 28; nFy= 8; fKx= -6; fKy=32; fFx=-12; fFy=18; rock= 1f; }
            else if (q == 3) { nKx= 20; nKy=24; nFx= 30; nFy= 8; fKx=-20; fKy=24; fFx=-30; fFy=8; rock= 0f; }
            else if (q == 4) { nKx= 22; nKy=23; nFx= 32; nFy= 8; fKx=-22; fKy=23; fFx=-32; fFy=8; rock= 0f; }
            else if (q == 5) { nKx=-14; nKy=25; nFx=-22; nFy= 8; fKx= 18; fKy=25; fFx= 26; fFy=8; rock= 0f; }
            else             { nKx= 12; nKy=25; nFx= 14; nFy= 8; fKx=-12; fKy=25; fFx=-14; fFy=8; rock= 0f; }
            float hdx = q == 3 ? -12f : (q == 4 ? 10f : 0f), hx = 64f + hdx;
            SynGridFloor(px, R, 64f, 14f, 36f, 5, s);
            P3DLimb(px,R, 58f*s,44f*s, (64f+fKx)*s,fKy*s, 9.0f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, (64f+fKx)*s,fKy*s, (64f+fFx)*s,fFy*s, 7.6f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+fFx-10,fFy+7), V(64+fFx+10,fFy+7), V(64+fFx+8,fFy-6), V(64+fFx-8,fFy-6) },
                     SynChrFar(p), SynChrDk(p), 2f*s);
            P3DLimb(px,R, 70f*s,44f*s, (64f+nKx)*s,nKy*s, 10.5f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DLimb(px,R, (64f+nKx)*s,nKy*s, (64f+nFx)*s,nFy*s, 8.8f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+nFx-12,nFy+8), V(64+nFx+12,nFy+8), V(64+nFx+9,nFy-7), V(64+nFx-9,nFy-7) },
                     SynChr(p), SynChrDk(p), 2.2f*s);
            SynHorizon(px,R, new[]{ V(hx-22,44), V(hx+22,44), V(hx+16,104), V(hx-16,104) }, true, s);
            SynWire(px,R, (hx-20f)*s,46f*s, (hx+20f)*s,102f*s, 6, 3, new Color(1f,1f,1f,0.28f), s);
            SynTube(px,R, (hx-22f)*s,44f*s, (hx+22f)*s,44f*s, SynMag, s);
            SynTube(px,R, (hx-16f)*s,104f*s, (hx+16f)*s,104f*s, SynCya, s);
            SynTube(px,R, (hx-22f)*s,44f*s, (hx-16f)*s,104f*s, SynCya, s);
            SynTube(px,R, (hx+22f)*s,44f*s, (hx+16f)*s,104f*s, SynCya, s);
            P3DPlate(px,R, new[]{ V(hx-30,98), V(hx-10,104), V(hx-8,86), V(hx-26,82) },
                     SynChrHi(p), SynChr(p), 2.2f*s);
            P3DPlate(px,R, new[]{ V(hx+10,104), V(hx+30,98), V(hx+26,82), V(hx+8,86) },
                     SynChrHi(p), SynChr(p), 2.2f*s);
            float fex = hx - (q == 4 ? 20f : 12f);
            P3DLimb(px,R, (hx-16f)*s,94f*s, fex*s,78f*s, 9.5f*s, SynChrFar(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(fex-9,84), V(fex+7,86), V(fex+9,72), V(fex-7,70) },
                     SynChrFar(p), SynChrDk(p), 2f*s);
            float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 10f)), ey = 78f + (q == 4 ? -6f : 0f);
            P3DLimb(px,R, (hx+16f)*s,94f*s, ex*s,ey*s, 10.5f*s, SynChrDk(p), 0.6f,0.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(ex-9,ey+7), V(ex+9,ey+9), V(ex+11,ey-6), V(ex-7,ey-8) },
                     SynChrHi(p), SynChr(p), 2.2f*s);
            SynTube(px,R, (ex-6f)*s,(ey+2f)*s, (ex+9f)*s,(ey+3f)*s, SynMag, s);
            if (q == 4)
            {
                P3DEllipseGlow(px,R, (ex+22f)*s, ey*s, 24f*s, 20f*s,
                               new Color(SynMag.r,SynMag.g,SynMag.b,0.50f));
                for (int k = 0; k < 3; k++)
                    DrawRingR(px,R, (ex+18f+k*13f)*s, (ey+1f)*s, (5f+k*2.6f)*s, 1.5f*s,
                              new Color(SynMag.r,SynMag.g,SynMag.b, 0.55f-k*0.14f));
            }
            DrawRingR(px,R, (hx + rock*0.4f)*s, 114f*s, 9f*s, 2.4f*s,
                      new Color(SynCya.r,SynCya.g,SynCya.b,0.90f));
            P3DEllipseGlow(px,R, (hx + rock*0.4f)*s, 114f*s, 16f*s, 12f*s,
                           new Color(SynCya.r,SynCya.g,SynCya.b,0.40f));
        }

        // ── PYLON (turret) — made almost entirely of light ───────────────────────
        static void P3DBuildSynPylon(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
                                     int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            bool chg = Atk(q);
            SynGridFloor(px, R, 64f, 14f, 30f, 5, s);
            SynTube(px,R, 46f*s,16f*s, 60f*s,60f*s, SynCya, s);
            SynTube(px,R, 82f*s,16f*s, 68f*s,60f*s, SynCya, s);
            SynTube(px,R, 64f*s,16f*s, 64f*s,58f*s, new Color(SynCya.r,SynCya.g,SynCya.b,0.60f), s);
            float[] feet = { 46f, 64f, 82f };
            for (int f = 0; f < 3; f++)
                P3DPlate(px,R, new[]{ V(feet[f]-7,13), V(feet[f]+7,13), V(feet[f]+5,19), V(feet[f]-5,19) },
                         SynChrDk(p), SynChrFar(p), 1.6f*s);
            P3DPlate(px,R, new[]{ V(52,58), V(76,58), V(74,68), V(54,68) }, SynChrHi(p), SynChr(p), 2.1f*s);
            for (int r = 0; r < 3; r++)
            {
                float rr = (9f + r*7f) * (chg ? 1.15f : 1f);
                var col = r % 2 == 1 ? SynMag : SynCya;
                DrawRingR(px,R, 64f*s, 84f*s, rr*s, 2.4f*s, new Color(col.r,col.g,col.b, 0.85f-r*0.18f));
            }
            FillCircleR(px,R, 64f*s, 84f*s, 5.4f*s, new Color(1f,0.96f,1f));
            P3DEllipseGlow(px,R, 64f*s, 84f*s, (chg?22f:14f)*s, (chg?20f:13f)*s,
                           new Color(SynMag.r,SynMag.g,SynMag.b, chg?0.60f:0.35f));
            if (q == 4)
            {
                DrawLineR(px,R, 76f*s,84f*s, 124f*s,84f*s, 2.6f*s, new Color(1f,0.98f,1f,0.95f));
                DrawLineR(px,R, 76f*s,84f*s, 124f*s,84f*s, 8f*s, new Color(SynCya.r,SynCya.g,SynCya.b,0.30f));
                for (int k = 0; k < 3; k++)
                    DrawRingR(px,R, (90f+k*14f)*s, 84f*s, (4f+k*2.4f)*s, 1.4f*s,
                              new Color(SynMag.r,SynMag.g,SynMag.b, 0.5f-k*0.12f));
            }
        }


        // ---- WISP — Sakura drone -----------------------------------------------------
        static void P3DBuildWisp(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            float dv = q == 4 ? -6f : (q == 1 ? 3f : (q == 2 ? -3f : 0f));
            float swing = q == 1 ? 4f : (q == 2 ? -4f : (q == 4 ? 9f : 0f));
            float cx = 64f + swing*0.4f, cy = 70f + dv;

            P3DPlate(px,R, new[]{ V(cx-16,cy), V(cx-13,cy+16), V(cx+13,cy+16),
                                  V(cx+16,cy), V(cx+13,cy-16), V(cx-13,cy-16) },
                     SakPaper, SakPaperDk, 2.2f);
            for (int i = 0; i < 5; i++)
            {
                float t = (i+1)/6f;
                float x = cx - 16f + 32f*t;
                float bulge = Mathf.Sin(Mathf.PI*t)*3f;
                DrawLineR(px,R, x*s,(cy-16f+bulge*0.3f)*s, x*s,(cy+16f-bulge*0.3f)*s, 1.3f*s, SakPaperDk);
            }
            DrawLineR(px,R, (cx-15)*s,cy*s, (cx+15)*s,cy*s, 1.4f*s, SakPaperDk);
            P3DPlate(px,R, new[]{ V(cx-9,cy+16), V(cx+9,cy+16), V(cx+7,cy+21), V(cx-7,cy+21) },
                     SakLacq, new Color(0.05f,0.03f,0.04f), 1.6f);
            P3DPlate(px,R, new[]{ V(cx-8,cy-16), V(cx+8,cy-16), V(cx+6,cy-21), V(cx-6,cy-21) },
                     SakLacq, new Color(0.05f,0.03f,0.04f), 1.6f);
            // the flame is seen THROUGH the paper, not on it
            P3DEllipseGlow(px,R, cx*s, cy*s, 18f*s, 18f*s, new Color(1f,0.72f,0.42f,0.55f));
            P3DPlate(px,R, new[]{ V(cx-3,cy-4), V(cx,cy+7), V(cx+3,cy-4) },
                     new Color(1f,0.92f,0.62f), new Color(0.98f,0.55f,0.22f), 0f);
            FillCircleR(px,R, cx*s,(cy+4f)*s, 4.2f*s, new Color(0.86f,0.20f,0.26f,0.55f));
            DrawLineR(px,R, cx*s,(cy-21f)*s, (cx+swing*0.3f)*s,(cy-27f)*s, 1.6f*s, ArtCloth);
            for (int k = 0; k < 4; k++)
                DrawLineR(px,R, (cx-3f+k*2f+swing*0.3f)*s,(cy-27f)*s,
                                (cx-4f+k*2f+swing*0.4f)*s,(cy-36f)*s, 1.2f*s, ArtCloth);
            if (q == 4)
            {
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px,R, (cx+12f+e*10f)*s, (cy-4f-e*4f)*s, (6f-e)*s, (5f-e)*s,
                        new Color(1f,0.8f,0.5f, 0.7f - e*0.15f));
                for (int b = 0; b < 3; b++)
                    SakBlossom(px,R, cx+18f+b*9f, cy+6f-b*7f, 3.0f-b*0.6f, 0.6f-b*0.16f, s);
            }
        }

        // ---- TORII — Sakura turret ------------------------------------------------------
        static void P3DBuildTorii(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            bool chg = Atk(q);
            var verm  = new Color(0.80f, 0.22f, 0.20f);
            var vermH = new Color(0.86f, 0.26f, 0.22f);
            var vermD = new Color(0.48f, 0.11f, 0.11f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 32f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            P3DPlate(px,R, new[]{ V(42,16), V(52,16), V(54,84), V(46,84) }, verm, vermD, 2.4f);
            P3DPlate(px,R, new[]{ V(76,16), V(86,16), V(82,84), V(74,84) }, vermH, new Color(0.52f,0.13f,0.12f), 2.4f);
            DrawLineR(px,R, 46f*s,30f*s, 52f*s,30f*s, 2.0f*s, new Color(0.30f,0.07f,0.07f));
            DrawLineR(px,R, 76f*s,30f*s, 82f*s,30f*s, 2.0f*s, new Color(0.30f,0.07f,0.07f));
            P3DPlate(px,R, new[]{ V(40,72), V(88,72), V(88,64), V(40,64) }, verm, vermD, 2.2f);
            SakFold(px,R, V(34,92), V(94,92), V(90,80), V(38,80), 0.5f, vermH, new Color(0.44f,0.10f,0.10f));
            P3DPlate(px,R, new[]{ V(46,80), V(82,80), V(82,74), V(46,74) },
                     SakLacq, new Color(0.05f,0.03f,0.04f), 1.8f);
            P3DPlate(px,R, new[]{ V(58,80), V(70,80), V(70,68), V(58,68) }, ArtGoldHi, ArtGold, 1.6f);
            DrawLineR(px,R, 61f*s,77f*s, 67f*s,77f*s, 1.4f*s, ArtOutline);
            DrawLineR(px,R, 61f*s,73f*s, 67f*s,73f*s, 1.4f*s, ArtOutline);
            DrawLineR(px,R, 46f*s,60f*s, 82f*s,62f*s, 3.6f*s, new Color(0.86f,0.82f,0.70f));
            for (int k = 0; k < 4; k++) SakShide(px,R, 52f+k*8f, 60f, 15f, 4, s);
            {
                // the gate IS the weapon mount -- the bow is drawn inside the doorway
                float draw = chg ? 11f : 4f;
                float bx = 64f + (q == 4 ? -5f : 0f);
                DrawLineR(px,R, (bx+4f)*s,52f*s, (bx+4f)*s,30f*s, 2.4f*s, ArtLeather);
                DrawLineR(px,R, (bx-draw)*s,41f*s, (bx+4f)*s,52f*s, 1.3f*s, new Color(0.92f,0.90f,0.84f));
                DrawLineR(px,R, (bx-draw)*s,41f*s, (bx+4f)*s,30f*s, 1.3f*s, new Color(0.92f,0.90f,0.84f));
                if (q == 4)
                {
                    DrawLineR(px,R, (bx+14f)*s,41f*s, (bx+52f)*s,41f*s, 2.0f*s, ArtLeather);
                    P3DPlate(px,R, new[]{ V(bx+52,44), V(bx+62,41), V(bx+52,38) },
                             new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.3f);
                    for (int b = 0; b < 3; b++)
                        SakBlossom(px,R, bx+30f+b*11f, 48f-b*5f, 3.2f-b*0.6f, 0.62f-b*0.16f, s);
                }
                else
                {
                    DrawLineR(px,R, (bx-draw)*s,41f*s, (bx+16f)*s,41f*s, 1.6f*s, ArtLeather);
                    P3DPlate(px,R, new[]{ V(bx+16,44), V(bx+24,41), V(bx+16,38) },
                             new Color(0.94f,0.94f,0.97f), ArtSteelDk(p), 1.3f);
                }
            }
        }

        // ══ DAWN ═════════════════════════════════════════════════════════════════
        // Theme 7. Medieval is hand-forged, Solar is cast, Biopunk is grown, Industrial is
        // stamped -- DAWN IS WOVEN AND LIT FROM WITHIN. Cloth, weathered wood, cord and paper.
        // Almost no metal, and nothing in the theme has a hard edge: every silhouette either
        // drapes or fades. Light is never a surface finish here, it is always *inside* something.

        /// <summary>
        /// Draped cloth with catenary sag between two anchors. NEW -- the theme is fabric and
        /// nothing in the primitive set can hang a span; plates are rigid polygons and limbs are
        /// capsules, so a canopy or a wing built from those reads as sheet metal.
        /// </summary>
        static void DawnDrape(Color[] px, int R, float x0, float y0, float x1, float y1,
            float sag, Color col, Color dk, int seg, float s)
        {
            if (seg < 3) seg = 3;
            var pts = new Vector2[(seg + 1) * 2];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                pts[i] = new Vector2(x * s, y * s);
                pts[(seg + 1) * 2 - 1 - i] = new Vector2(x * s, (y - Mathf.Sin(Mathf.PI * t) * sag) * s);
            }
            P3DPlate(px, R, pts, col, dk, 1.8f);
            for (int k = 1; k < seg; k++)
            {
                float t = k / (float)seg;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                DrawLineR(px, R, x * s, y * s, x * s, (y - Mathf.Sin(Mathf.PI * t) * sag * 0.92f) * s,
                          1.0f * s, dk);
            }
        }

        /// <summary>
        /// A hem that fades out instead of ending. NEW. Every other theme terminates cloth with
        /// an outline; Dawn's rule is that no edge is hard, so the hem is drawn as stacked bands
        /// of falling alpha with no contour at all.
        /// </summary>
        static void DawnHem(Color[] px, int R, Vector2[] pts, Color col, int steps, float s)
        {
            if (steps < 2) steps = 2;
            for (int i = 0; i < steps; i++)
            {
                float a = 0.85f - (i / (float)steps) * 0.78f;
                float off = i * 2.4f * s;
                var p2 = new Vector2[pts.Length];
                for (int k = 0; k < pts.Length; k++) p2[k] = new Vector2(pts[k].x, pts[k].y - off);
                P3DPlate(px, R, p2, new Color(col.r, col.g, col.b, a),
                                    new Color(col.r, col.g, col.b, a * 0.5f), 0f);
            }
        }

        /// <summary>Light held inside cloth — lantern, ward, sigil, core.</summary>
        static void DawnLight(Color[] px, int R, float x, float y, float r, float k, float s)
        {
            P3DEllipseGlow(px, R, x*s, y*s, r*2.4f*s, r*2.4f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.28f * k));
            P3DEllipseGlow(px, R, x*s, y*s, r*1.4f*s, r*1.4f*s, new Color(1f, 0.98f, 0.90f, 0.55f * k));
            FillCircleR(px, R, x*s, y*s, r*s, new Color(1f, 0.99f, 0.94f));
            FillCircleR(px, R, (x - r*0.3f)*s, (y + r*0.3f)*s, r*0.34f*s, Color.white);
        }

        /// <summary>Woven cord wrap — grips, bindings, posts.</summary>
        static void DawnCord(Color[] px, int R, float x0, float y0, float x1, float y1,
            int turns, Color col, float s)
        {
            float dx = x1-x0, dy = y1-y0, len = Mathf.Sqrt(dx*dx+dy*dy);
            if (len < 0.01f) return;
            float ux = dx/len, uy = dy/len, pvx = -uy, pvy = ux;
            DrawLineR(px,R, x0*s,y0*s, x1*s,y1*s, 3.0f*s, col);
            var hi = new Color(Mathf.Min(col.r*1.5f,1f), Mathf.Min(col.g*1.5f,1f), Mathf.Min(col.b*1.5f,1f));
            for (int i = 0; i < turns; i++)
            {
                float t = (i + 0.5f) / turns;
                float cx = x0 + dx*t, cy = y0 + dy*t;
                DrawLineR(px,R, (cx - pvx*2.6f - ux*1.4f)*s, (cy - pvy*2.6f - uy*1.4f)*s,
                                (cx + pvx*2.6f + ux*1.4f)*s, (cy + pvy*2.6f + uy*1.4f)*s, 1.3f*s, hi);
            }
        }

        /// <summary>Weathered standing stone — beacon base, sentinel footing.</summary>
        static void DawnStone(Color[] px, int R, float x, float y, float w, float h, P3DP p, float s)
        {
            P3DPlate(px,R, new[]{ new Vector2((x-w)*s, y*s), new Vector2((x+w*0.86f)*s, (y+h*0.1f)*s),
                                  new Vector2((x+w*0.7f)*s, (y+h)*s), new Vector2((x-w*0.8f)*s, (y+h*0.92f)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 2.3f);
            DrawLineR(px,R, (x-w*0.5f)*s,(y+h*0.2f)*s, (x-w*0.3f)*s,(y+h*0.7f)*s, 1.6f*s, ArtSteelDk(p));
            DrawLineR(px,R, (x+w*0.3f)*s,(y+h*0.25f)*s, (x+w*0.2f)*s,(y+h*0.8f)*s, 1.4f*s, ArtSteelDk(p));
        }

        // ---- SPRITE — Dawn drone -------------------------------------------------
        static void P3DBuildSprite(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Color ribbon = Color.Lerp(ArtCloth, p.body, 0.35f);
            float dv = q == 4 ? -6f : (q == 1 ? 3f : (q == 2 ? -3f : 0f));
            float flare = Atk(q) ? 1.6f : 1f;
            float wob = q == 1 ? 4f : (q == 2 ? -4f : 0f);

            for (int i = 0; i < 3; i++)
            {
                float t = i / 3f;
                P3DPlate(px,R, new[]{
                    new Vector2((58f-i*10f)*s, (70f+dv-i*4f)*s),
                    new Vector2((44f-i*10f)*s, (66f+dv-i*6f-wob)*s),
                    new Vector2((42f-i*10f)*s, (58f+dv-i*6f)*s),
                    new Vector2((56f-i*10f)*s, (62f+dv-i*4f)*s) },
                    new Color(ribbon.r, ribbon.g, ribbon.b, 0.8f - t*0.42f),
                    new Color(ArtClothDk.r, ArtClothDk.g, ArtClothDk.b, 0.7f - t*0.4f), 1.1f);
            }
            DawnLight(px,R, 66f, 68f+dv, 7.4f*flare, flare, s);
            for (int k = 0; k < 3; k++)
            {
                float a = (k*120f + q*57f) * Mathf.Deg2Rad;
                float sx = 66f + Mathf.Cos(a)*15f, sy = 68f + dv + Mathf.Sin(a)*11f;
                FillCircleR(px,R, sx*s, sy*s, 2.0f*s, new Color(1f,0.98f,0.88f,0.9f));
                P3DEllipseGlow(px,R, sx*s, sy*s, 6f*s, 6f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
            }
            if (q == 4)
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px,R, (80f+e*10f)*s, (66f+dv-e*3f)*s, (6f-e)*s, (5f-e)*s,
                        new Color(1f,0.96f,0.84f, 0.7f - e*0.15f));
        }

        // ---- SEEKER — Dawn sniper ------------------------------------------------
        static void P3DBuildSeeker(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.28f), linenDk = ArtClothDk, wood = ArtLeather;
            Color linenSh = new Color(linenDk.r*0.7f, linenDk.g*0.7f, linenDk.b*0.7f);
            Vector2 hip = V(63,52), shN = V(64,78), shF = V(56,78);
            Vector2 nK = V(J(0)-1,J(1)-6), nF = V(J(2),J(3));
            Vector2 fK = V(J(4)-1,J(5)-6), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)-6), nH = V(J(10),J(11)-6);
            Vector2 head = V(q == 5 ? 61 : 66, 96);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,(J(13)-6)*s, 4.2f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 5.6f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 4.8f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.2f*s, wood, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.4f*s, wood, 0.05f,0.2f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+8f*s,f.y+3f*s),
                                      new Vector2(f.x+6f*s,f.y-4f*s), new Vector2(f.x-6f*s,f.y-4f*s) },
                         wood, new Color(wood.r*0.45f,wood.g*0.45f,wood.b*0.45f), 1.6f);
            P3DPlate(px,R, new[]{ V(54,80), V(73,80), V(75,64), V(69,50), V(57,50), V(51,64) },
                     linen, linenDk, 2.2f);
            DawnHem(px,R, new[]{ V(54,51), V(73,51), V(72,46), V(55,46) }, linen, 3, s);
            DawnCord(px,R, 52,70, 74,74, 4, ArtLeather, s);
            // eyes bound in woven cord -- no lens, no visor, no mask
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-8f*s), new Vector2(head.x+9f*s,head.y-7f*s),
                                  new Vector2(head.x+8f*s,head.y+6f*s), new Vector2(head.x-8f*s,head.y+5f*s) },
                     linen, linenDk, 2f);
            for (int b = 0; b < 3; b++)
                DrawLineR(px,R, head.x-8f*s, (head.y/s-4f+b*3f)*s, head.x+8f*s, (head.y/s-3f+b*3f)*s,
                          1.3f*s, new Color(linenDk.r*0.8f, linenDk.g*0.8f, linenDk.b*0.8f));
            P3DEllipseGlow(px,R, head.x+3f*s, head.y+1f*s, 4.0f*s, 2.4f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, Atk(q) ? 0.7f : 0.3f));
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 4.6f*s, linen, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.0f*s, linen, 0.05f,0.2f, L,H,fillL);
            {
                float draw = q == 3 ? 13f : (q == 4 ? -3f : 6f);
                float gx = nH.x/s + 9f, gy = nH.y/s;
                DawnCord(px,R, gx+3f, gy-30f, gx+3f, gy+30f, 6, wood, s);
                float nx = gx - draw;
                var lightStr = new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f);
                DrawLineR(px,R, (gx+3f)*s, (gy+30f)*s, nx*s, gy*s, 1.4f*s, lightStr);
                DrawLineR(px,R, (gx+3f)*s, (gy-30f)*s, nx*s, gy*s, 1.4f*s, lightStr);
                P3DEllipseGlow(px,R, (gx+3f)*s, (gy+30f)*s, 7f*s, 7f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
                P3DEllipseGlow(px,R, (gx+3f)*s, (gy-30f)*s, 7f*s, 7f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
                if (q == 4)
                {
                    DrawLineR(px,R, (gx+14f)*s, gy*s, (gx+52f)*s, gy*s, 2.2f*s,
                        new Color(1f,0.98f,0.9f,0.95f));
                    P3DEllipseGlow(px,R, (gx+40f)*s, gy*s, 18f*s, 7f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f));
                }
                else
                {
                    DrawLineR(px,R, nx*s, gy*s, (gx+16f)*s, gy*s, 1.6f*s, new Color(1f,0.98f,0.9f,0.8f));
                    DawnLight(px,R, gx+18f, gy, 2.6f, 0.8f, s);
                }
            }
        }

        // ---- WARD — Dawn shield-bot ----------------------------------------------
        static void P3DBuildWard(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.28f), linenDk = ArtClothDk, wood = ArtLeather;
            Color linenSh = new Color(linenDk.r*0.7f, linenDk.g*0.7f, linenDk.b*0.7f);
            Vector2 hip = V(62,52), shN = V(63,80), shF = V(55,80);
            Vector2 nK = V(J(0)-2,J(1)), nF = V(J(2)-2,J(3));
            Vector2 fK = V(J(4)-2,J(5)), fF = V(J(6)-2,J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 58 : 63, 98);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 5.4f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 7.4f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 6.4f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 8.2f*s, wood, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 7.0f*s, wood, 0.05f,0.2f, L,H,fillL);
            foreach (var f in new[]{ nF, fF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-8f*s,f.y+4f*s), new Vector2(f.x+9f*s,f.y+4f*s),
                                      new Vector2(f.x+7f*s,f.y-5f*s), new Vector2(f.x-7f*s,f.y-5f*s) },
                         wood, new Color(wood.r*0.45f,wood.g*0.45f,wood.b*0.45f), 1.7f);
            for (int t = 0; t < 3; t++)
            {
                float w = 22f - t*4f, y = 82f - t*11f;
                P3DPlate(px,R, new[]{ V(62-w,y), V(62+w,y), V(62+w-2,y-13), V(62-w+2,y-13) },
                         t > 0 ? linen : new Color(Mathf.Min(linen.r*1.1f,1f), Mathf.Min(linen.g*1.1f,1f),
                                                   Mathf.Min(linen.b*1.1f,1f)), linenDk, 2.0f);
            }
            DawnHem(px,R, new[]{ V(42,50), V(82,50), V(80,44), V(44,44) }, linen, 4, s);
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y-10f*s), new Vector2(head.x+11f*s,head.y-9f*s),
                                  new Vector2(head.x+10f*s,head.y+7f*s), new Vector2(head.x,head.y+13f*s),
                                  new Vector2(head.x-11f*s,head.y+7f*s) }, linen, linenDk, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-7f*s,head.y-7f*s), new Vector2(head.x+8f*s,head.y-6f*s),
                                  new Vector2(head.x+7f*s,head.y+4f*s), new Vector2(head.x-6f*s,head.y+4f*s) },
                     new Color(0.07f,0.07f,0.11f), new Color(0.02f,0.02f,0.04f), 1f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y-1f*s, 2.8f*s, 2.6f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.6f*s, linen, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.8f*s, linen, 0.05f,0.2f, L,H,fillL);
            {
                // the barrier is LIGHT, not matter -- three translucent planes on a cord frame
                float bx = 64f + (q == 4 ? 14f : (q == 3 ? 3f : 9f));
                float br = q == 4 ? 1.15f : 1f;
                for (int r2 = 0; r2 < 3; r2++)
                {
                    float sc = (1f - r2*0.16f) * br;
                    P3DPlate(px,R, new[]{ V(bx-15*sc,90), V(bx+15*sc,90), V(bx+17*sc,60),
                                          V(bx+8*sc,34), V(bx-9*sc,34), V(bx-17*sc,60) },
                             new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.16f + r2*0.05f),
                             new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.05f + r2*0.03f), 0f);
                }
                DrawLineR(px,R, (bx-15)*s,90f*s, (bx-17)*s,60f*s, 2.2f*s, wood);
                DrawLineR(px,R, (bx-17)*s,60f*s, (bx-9)*s,34f*s, 2.2f*s, wood);
                DrawLineR(px,R, (bx+15)*s,90f*s, (bx+17)*s,60f*s, 2.2f*s, wood);
                DrawLineR(px,R, (bx+17)*s,60f*s, (bx+8)*s,34f*s, 2.2f*s, wood);
                DrawLineR(px,R, (bx-15)*s,90f*s, (bx+15)*s,90f*s, 2.4f*s, wood);
                DrawLineR(px,R, (bx-9)*s,34f*s, (bx+8)*s,34f*s, 2.0f*s, wood);
                for (int k = 0; k < 3; k++)
                    DrawLineR(px,R, (bx-16+k*2)*s, (78f-k*16f)*s, (bx+16-k*2)*s, (78f-k*16f)*s,
                              1.2f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
                DawnLight(px,R, bx, 62f, 5.0f*br, br, s);
                P3DEllipseGlow(px,R, bx*s, 62f*s, 26f*br*s, 30f*br*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.20f));
            }
        }

        // ---- CARAVAN — Dawn mech --------------------------------------------------
        static void P3DBuildCaravan(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.26f), linenDk = ArtClothDk, wood = ArtLeather;
            Color woodDk = new Color(wood.r*0.45f, wood.g*0.45f, wood.b*0.45f);
            float st = q == 1 ? 1f : (q == 2 ? -1f : 0f);
            float rec = q == 4 ? -6f : (q == 3 ? 3f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            float[,] legs = { {52,-1},{60,-1},{68,1},{76,1} };
            for (int i = 0; i < 4; i++)
            {
                float gx = legs[i,0], dir = legs[i,1];
                float kx = gx + dir*8f + st*dir*4f;
                Color tone = i < 2 ? new Color(wood.r*0.7f, wood.g*0.7f, wood.b*0.7f) : wood;
                P3DLimb(px,R, gx*s,52f*s, kx*s,32f*s, 3.6f*s, tone, 0.05f,0.2f, L,H,fillL);
                P3DLimb(px,R, kx*s,32f*s, (kx+dir*4f)*s,16f*s, 3.0f*s, tone, 0.05f,0.2f, L,H,fillL);
                P3DPlate(px,R, new[]{ V(kx+dir*4-6,18), V(kx+dir*4+6,18),
                                      V(kx+dir*4+5,12), V(kx+dir*4-5,12) }, wood, woodDk, 1.5f);
            }
            P3DPlate(px,R, new[]{ V(46,52), V(82,52), V(80,62), V(48,62) },
                     ArtSteelHi(p), ArtSteelBase(p), 2.0f);
            DrawLineR(px,R, 50f*s,62f*s, 50f*s,86f*s, 2.6f*s, wood);
            DrawLineR(px,R, 78f*s,62f*s, 78f*s,86f*s, 2.6f*s, wood);
            DrawLineR(px,R, 50f*s,86f*s, 78f*s,86f*s, 2.4f*s, wood);
            DawnDrape(px,R, 48,86, 80,86, 16f, linen, linenDk, 8, s);
            DawnDrape(px,R, 50,74, 78,74, 11f,
                new Color(Mathf.Min(linen.r*1.06f,1f), Mathf.Min(linen.g*1.06f,1f), Mathf.Min(linen.b*1.06f,1f)),
                linenDk, 7, s);
            DawnHem(px,R, new[]{ V(46,64), V(82,64), V(80,58), V(48,58) }, linen, 4, s);
            P3DPlate(px,R, new[]{ V(56+rec,60), V(74+rec,62), V(72+rec,48), V(58+rec,46) }, wood, woodDk, 1.9f);
            DawnCord(px,R, 57+rec,54, 73+rec,56, 3, linenDk, s);
            DawnLight(px,R, 64f, 68f, 4.4f, Atk(q) ? 1.4f : 1f, s);
            // it strikes by swinging its cargo lamp forward on a cord
            if (q == 4)
            {
                DrawLineR(px,R, 76f*s,60f*s, 96f*s,44f*s, 1.4f*s, linenDk);
                DawnLight(px,R, 97f, 42f, 6.4f, 1.6f, s);
                P3DEllipseGlow(px,R, 97f*s, 42f*s, 26f*s, 24f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.4f));
            }
            else if (q == 3)
            {
                DrawLineR(px,R, 74f*s,60f*s, 80f*s,52f*s, 1.4f*s, linenDk);
                DawnLight(px,R, 81f, 50f, 4.0f, 1.2f, s);
            }
        }

        // ---- GLIDER — Dawn interceptor --------------------------------------------
        static void P3DBuildGlider(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.30f), linenDk = ArtClothDk, wood = ArtLeather;
            float beat = q == 1 ? 13f : (q == 2 ? -11f : (Atk(q) ? 17f : 0f));

            DawnDrape(px,R, 60,62, 26,72+beat*0.6f, 9f,
                new Color(linenDk.r, linenDk.g, linenDk.b, 0.8f),
                new Color(linenDk.r*0.6f, linenDk.g*0.6f, linenDk.b*0.6f), 6, s);
            DrawLineR(px,R, 60f*s,62f*s, 26f*s,(72f+beat*0.6f)*s, 2.0f*s, wood);
            for (int i = 0; i < 3; i++)
                P3DPlate(px,R, new[]{ V(52,58-i*3), V(30-i*5, 50-i*5-beat*0.3f),
                                      V(28-i*5, 45-i*5-beat*0.3f), V(52,53-i*3) },
                         new Color(linen.r, linen.g, linen.b, 0.7f - i*0.16f),
                         new Color(linenDk.r, linenDk.g, linenDk.b, 0.6f - i*0.14f), 1.0f);
            P3DPlate(px,R, new[]{ V(58,48), V(74,52), V(76,64), V(68,70), V(56,64) }, linen, linenDk, 2.2f);
            DawnCord(px,R, 60,56, 74,60, 3, ArtLeather, s);
            P3DPlate(px,R, new[]{ V(72,66), V(84,70), V(86,62), V(74,58) }, linen, linenDk, 1.9f);
            P3DEllipseGlow(px,R, 80f*s, 65f*s, 2.6f*s, 2.4f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
            DrawLineR(px,R, 64f*s,60f*s, 30f*s,(70f-beat)*s, 2.4f*s, wood);
            DawnDrape(px,R, 64,60, 30,70-beat, 12f, linen, linenDk, 7, s);
            for (int r = 1; r < 4; r++)
            {
                float t = r/4f;
                float ax = 64f + (30f-64f)*t, ay = 60f + ((70f-beat)-60f)*t;
                DrawLineR(px,R, ax*s, ay*s, ax*s, (ay - 9f*Mathf.Sin(Mathf.PI*t))*s, 1.1f*s, wood);
            }
            DawnLight(px,R, 66f, 58f, 3.0f, Atk(q) ? 1.5f : 0.9f, s);
            if (Atk(q))
                for (int e = 0; e < 3; e++)
                    P3DEllipseGlow(px,R, (92f+e*11f)*s, (62f-e*5f)*s, (5f-e)*s, (5f-e)*s,
                        new Color(1f,0.97f,0.88f, 0.7f - e*0.17f));
        }

        // ---- ORACLE — Dawn hacker -------------------------------------------------
        static void P3DBuildOracle(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.30f), linenDk = ArtClothDk;
            Color linenSh = new Color(linenDk.r*0.7f, linenDk.g*0.7f, linenDk.b*0.7f);
            bool cast = Atk(q);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (cast ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);
            Vector2 shN = V(64+sway, 84+bob), shF = V(56+sway, 84+bob);
            Vector2 nE = V(J(8)+sway, J(9)+bob), nH = V(J(10)+sway, J(11)+bob);
            Vector2 fE = V(J(12)+sway, J(13)+bob);
            Vector2 head = V((q == 5 ? 60 : 65) + sway, 104 + bob);

            // hovers on visible rings of light, not on a robe hem
            P3DEllipseGlow(px,R, (64f+sway)*s, 18f*s, 28f*s, 9f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, cast ? 0.5f : 0.3f));
            for (int r = 0; r < 3; r++)
                DrawRingR(px,R, (64f+sway)*s, (20f+r*3f)*s, (20f-r*5f)*s, 1.4f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f - r*0.11f));
            P3DLimb(px,R, shF.x,shF.y, fE.x,fE.y, 5.0f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(48+sway*1.4f,26), V(80+sway*1.4f,26), V(76+sway,58),
                                  V(72+sway,86), V(56+sway,86), V(52+sway,58) }, linen, linenDk, 2.4f);
            DawnHem(px,R, new[]{ V(48+sway*1.4f,27), V(80+sway*1.4f,27), V(78+sway,20), V(50+sway,20) },
                    linen, 5, s);
            DrawLineR(px,R, (58f+sway)*s,84f*s, (55f+sway*1.3f)*s,30f*s, 1.8f*s, linenSh);
            DrawLineR(px,R, (70f+sway)*s,84f*s, (73f+sway*1.3f)*s,30f*s, 1.8f*s, linenSh);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.4f*s, linen, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.8f*s, linen, 0.05f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-12f*s,head.y-12f*s), new Vector2(head.x+12f*s,head.y-12f*s),
                                  new Vector2(head.x+11f*s,head.y+9f*s), new Vector2(head.x,head.y+15f*s),
                                  new Vector2(head.x-11f*s,head.y+9f*s) }, linen, linenDk, 2.2f);
            // a band of light where the eyes would be -- no visor, no socket, no face
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y-3f*s), new Vector2(head.x+11f*s,head.y-2f*s),
                                  new Vector2(head.x+11f*s,head.y+2f*s), new Vector2(head.x-11f*s,head.y+1f*s) },
                     new Color(1f,0.98f,0.9f,0.85f), new Color(ArtRune.r,ArtRune.g,ArtRune.b,0.6f), 0f);
            P3DEllipseGlow(px,R, head.x, head.y-1f*s, 15f*s, 6f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
            {
                float cx = nH.x/s + (cast ? 16f : 6f), cy = nH.y/s + (cast ? 6f : 0f);
                float rr = cast ? 16f : 11f;
                for (int i = 0; i < 6; i++)
                {
                    float a = (i*60f + q*40f) * Mathf.Deg2Rad;
                    float sx = cx + Mathf.Cos(a)*rr, sy = cy + Mathf.Sin(a)*rr*0.7f;
                    P3DPlate(px,R, new[]{ V(sx-3,sy), V(sx,sy+3.4f), V(sx+3,sy), V(sx,sy-3.4f) },
                             new Color(1f,0.98f,0.9f,0.9f),
                             new Color(ArtRune.r,ArtRune.g,ArtRune.b,0.6f), 0.8f);
                }
                DrawRingR(px,R, cx*s, cy*s, rr*s, 1.2f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
                DawnLight(px,R, cx, cy, cast ? 5.0f : 3.4f, cast ? 1.5f : 1f, s);
                if (q == 4)
                    for (int e = 0; e < 3; e++)
                        P3DEllipseGlow(px,R, (cx+18f+e*10f)*s, (cy-e*4f)*s, (5f-e)*s, (5f-e)*s,
                            new Color(1f,0.97f,0.88f, 0.65f - e*0.16f));
            }
        }

        // ---- SENTINEL — Dawn titan ------------------------------------------------
        static void P3DBuildSentinel(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.24f), linenDk = ArtClothDk, wood = ArtLeather;
            Color woodDk = new Color(wood.r*0.45f, wood.g*0.45f, wood.b*0.45f);
            Color linenSh = new Color(linenDk.r*0.7f, linenDk.g*0.7f, linenDk.b*0.7f);

            float nKx,nKy,nFx,nFy,fKx,fKy,fFx,fFy,rock;
            // 1 = contact (spread, both planted); 2 = passing (rear leg lifted, knee raised).
            // Previously these were the same positions with near/far traded -- shading changed,
            // outline did not, so the legs never read as moving.
            if (q == 1)      { nKx= 20; nKy=26; nFx= 32; nFy= 8; fKx=-18; fKy=24; fFx=-28; fFy=8; rock= 3f; }
            else if (q == 2) { nKx= -6; nKy=32; nFx=-12; nFy=18; fKx= 16; fKy=25; fFx= 28; fFy=8; rock=-1f; }
            else if (q == 6) { nKx=-18; nKy=24; nFx=-28; nFy= 8; fKx= 20; fKy=26; fFx= 32; fFy=8; rock=-3f; }
            else if (q == 7) { nKx= 16; nKy=25; nFx= 28; nFy= 8; fKx= -6; fKy=32; fFx=-12; fFy=18; rock= 1f; }
            else if (q == 3) { nKx= 20; nKy=24; nFx= 30; nFy=8; fKx=-20; fKy=24; fFx=-30; fFy=8; rock= 0f; }
            else if (q == 4) { nKx= 22; nKy=23; nFx= 32; nFy=8; fKx=-22; fKy=23; fFx=-32; fFy=8; rock= 0f; }
            else if (q == 5) { nKx=-14; nKy=25; nFx=-22; nFy=8; fKx= 18; fKy=25; fFx= 26; fFy=8; rock= 0f; }
            else             { nKx= 12; nKy=25; nFx= 14; nFy=8; fKx=-12; fKy=25; fFx=-14; fFy=8; rock= 0f; }
            float hdx = q == 3 ? -12f : (q == 4 ? 10f : 0f);
            float hx = 64f + hdx;

            P3DEllipseGlow(px,R, 64f*s, 16f*s, 34f*s, 8f*s, new Color(0f,0f,0f,0.45f));
            P3DLimb(px,R, 58f*s,44f*s, (64+fKx)*s,fKy*s, 9.0f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, (64+fKx)*s,fKy*s, (64+fFx)*s,fFy*s, 7.6f*s, linenSh, 0.05f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+fFx-10, fFy+7), V(64+fFx+10, fFy+7),
                                  V(64+fFx+8, fFy-6), V(64+fFx-8, fFy-6) },
                     new Color(wood.r*0.7f,wood.g*0.7f,wood.b*0.7f), woodDk, 2f);
            P3DLimb(px,R, 70f*s,44f*s, (64+nKx)*s,nKy*s, 10.5f*s, wood, 0.05f,0.2f, L,H,fillL);
            P3DLimb(px,R, (64+nKx)*s,nKy*s, (64+nFx)*s,nFy*s, 8.8f*s, wood, 0.05f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+nFx-12, nFy+8), V(64+nFx+12, nFy+8),
                                  V(64+nFx+9, nFy-7), V(64+nFx-9, nFy-7) }, wood, woodDk, 2.2f);

            // no armour at all: cloth over a core bright enough to show through it
            P3DEllipseGlow(px,R, hx*s, 66f*s, 20f*s, 26f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.42f));
            for (int t = 0; t < 4; t++)
            {
                float w = 26f - t*4f, y = 90f - t*15f;
                P3DPlate(px,R, new[]{ V(hx-w,y), V(hx+w,y), V(hx+w-3,y-18), V(hx-w+3,y-18) },
                         new Color(linen.r, linen.g, linen.b, 0.94f), linenDk, 2.3f);
            }
            DawnHem(px,R, new[]{ V(hx-26,34), V(hx+26,34), V(hx+23,26), V(hx-23,26) }, linen, 5, s);
            DawnLight(px,R, hx, 66f, 7.0f, Atk(q) ? 1.5f : 1f, s);

            // arms are draped cloth ending in light -- there are no hands
            float fex = hx - (q == 4 ? 18f : 11f), fey = 78f;
            DawnDrape(px,R, hx-14, 92, fex, fey, 10f,
                new Color(linenDk.r, linenDk.g, linenDk.b, 0.9f),
                new Color(linenDk.r*0.6f, linenDk.g*0.6f, linenDk.b*0.6f), 6, s);
            DawnLight(px,R, fex-4f, fey-6f, 3.4f, 0.7f, s);
            float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 9f));
            float ey = 76f + (q == 4 ? -6f : 0f);
            DawnDrape(px,R, hx+14, 92, ex, ey, 12f, linen, linenDk, 6, s);
            DawnLight(px,R, ex + (q == 4 ? 12f : 6f), ey - 8f,
                      q == 4 ? 7.0f : 4.4f, q == 4 ? 1.8f : 1f, s);
            if (q == 4) P3DEllipseGlow(px,R, (ex+16f)*s, (ey-8f)*s, 26f*s, 24f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.42f));

            P3DPlate(px,R, new[]{ V(hx-13+rock*0.5f,104), V(hx+13+rock*0.5f,104),
                                  V(hx+12,120), V(hx,126), V(hx-12,120) }, linen, linenDk, 2.3f);
            P3DPlate(px,R, new[]{ V(hx-8+rock*0.5f,108), V(hx+8+rock*0.5f,108),
                                  V(hx+7,118), V(hx-7,118) },
                     new Color(0.06f,0.06f,0.10f), new Color(0.02f,0.02f,0.04f), 1f);
            P3DEllipseGlow(px,R, (hx+rock*0.5f)*s, 113f*s, 7f*s, 5f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
        }

        // ---- BEACON — Dawn turret --------------------------------------------------
        static void P3DBuildBeacon(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color linen = Color.Lerp(ArtCloth, p.body, 0.26f), linenDk = ArtClothDk, wood = ArtLeather;
            float pulse = q == 3 ? 1.4f : (q == 4 ? 1.8f : 1f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 30f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            DawnStone(px,R, 64f, 18f, 17f, 30f, p, s);
            var dimP = new P3DP { body = new Color(p.body.r*0.7f, p.body.g*0.7f, p.body.b*0.7f) };
            DawnStone(px,R, 48f, 16f, 9f, 16f, dimP, s);
            DawnStone(px,R, 80f, 16f, 8f, 13f, dimP, s);
            DawnCord(px,R, 64,46, 66,80, 7, wood, s);
            // prayer flags strung off the post -- the only emplacement in the game that moves
            for (int i = 0; i < 3; i++)
            {
                float y = 78f - i*9f;
                DawnDrape(px,R, 40+i*3, y, 66, y+2, 7f,
                    new Color(linen.r, linen.g, linen.b, 0.85f - i*0.12f), linenDk, 5, s);
            }
            for (int r = 0; r < 3; r++)
                DrawRingR(px,R, 66f*s, 88f*s, (9f + r*5f*pulse)*s, 1.3f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f - r*0.13f));
            DawnLight(px,R, 66f, 88f, 6.4f*pulse, pulse, s);
            P3DPlate(px,R, new[]{ V(58,84), V(74,84), V(72,78), V(60,78) },
                     ArtSteelHi(p), ArtSteelBase(p), 1.8f);
            if (q == 4)
            {
                P3DPlate(px,R, new[]{ V(76,92), V(124,88), V(124,84), V(76,84) },
                         new Color(1f,0.98f,0.9f,0.42f),
                         new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.14f), 0f);
                DrawLineR(px,R, 76f*s,88f*s, 124f*s,86f*s, 2.4f*s, new Color(1f,0.99f,0.94f,0.9f));
                for (int k = 0; k < 3; k++)
                    DrawRingR(px,R, (88f+k*14f)*s, (87.5f-k*0.5f)*s, (4f+k*2.4f)*s, 1.3f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f - k*0.12f));
                P3DEllipseGlow(px,R, 80f*s, 88f*s, 16f*s, 12f*s, new Color(1f,0.97f,0.88f,0.7f));
            }
        }

        // ══ INDUSTRIAL ═══════════════════════════════════════════════════════════
        // Theme 4. Medieval is hand-forged for one owner, Solar is cast and fired, Biopunk is
        // grown -- Industrial is STAMPED OUT BY THE THOUSAND and repaired badly. Flat riveted
        // plate, weld beads, hazard chevrons, brass gauges, exposed pistons, soot over rust.
        // Solar is the theme this has to stay clear of, since both are metal: the separation is
        // cast versus stamped. Solar is smooth brass curves with clean molten seams; Industrial
        // is flat plate with rivet rows and a sooty firebox glow behind grate bars.

        static readonly Color IndHazA = new Color(0.95f, 0.74f, 0.14f);
        static readonly Color IndHazB = new Color(0.13f, 0.12f, 0.11f);
        static readonly Color IndSoot = new Color(0.13f, 0.13f, 0.12f);

        /// <summary>Rivet row along an edge — the theme's most repeated mark.</summary>
        static void IndRivets(Color[] px, int R, float x0, float y0, float x1, float y1,
            int cnt, Color c, float s)
        {
            for (int i = 0; i < cnt; i++)
            {
                float t = (i + 0.5f) / cnt;
                float rx = x0 + (x1 - x0) * t, ry = y0 + (y1 - y0) * t;
                FillCircleR(px, R, rx * s, ry * s, 1.3f * s, c);
                FillCircleR(px, R, (rx - 0.4f) * s, (ry + 0.4f) * s, 0.6f * s,
                    new Color(Mathf.Min(c.r*1.8f,1f), Mathf.Min(c.g*1.8f,1f), Mathf.Min(c.b*1.8f,1f)));
            }
        }

        /// <summary>
        /// Diagonal hazard chevrons clipped to a polygon. NEW — nothing in the primitive set can
        /// stripe a shape; P3DPolyGrad picks a colour per scanline, not per pixel. This is the
        /// theme's signature mark, so it needs its own fill.
        /// </summary>
        static void IndHazard(Color[] px, int R, Vector2[] pts, float period, float ow)
        {
            if (pts.Length < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var q in pts) { if (q.y < minY) minY = q.y; if (q.y > maxY) maxY = q.y; }
            int y0 = Mathf.Max(0, (int)minY), y1 = Mathf.Min(R - 1, (int)maxY + 1);
            var xs = new System.Collections.Generic.List<float>(8);
            for (int y = y0; y <= y1; y++)
            {
                xs.Clear();
                for (int i = 0, n = pts.Length; i < n; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % n];
                    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                        xs.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Mathf.Max(0, Mathf.CeilToInt(xs[k]));
                    int xb = Mathf.Min(R - 1, Mathf.FloorToInt(xs[k + 1]));
                    for (int x = xa; x <= xb; x++)
                    {
                        // 45-degree stripe: constant along x+y
                        float u = Mathf.Repeat((x + y) / period, 1f);
                        P3DSet(px, R, x, y, u < 0.5f ? IndHazA : IndHazB);
                    }
                }
            }
            if (ow > 0f) P3DPolyLine(px, R, pts, ArtOutline, ow);
        }

        /// <summary>Hydraulic ram: dark sleeve then a bright exposed rod. NEW.</summary>
        static void IndPiston(Color[] px, int R, float x0, float y0, float x1, float y1,
            float r, P3DP p, float ext, Vector3 L, Vector3 H, Vector3 fillL, float s)
        {
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx*dx + dy*dy);
            if (len < 0.01f) return;
            float ux = dx / len, uy = dy / len;
            float mid = 0.52f + ext * 0.16f;
            Color hi = ArtSteelHi(p), bo = ArtSteelBase(p), dk = ArtSteelDk(p);
            P3DLimb(px,R, x0*s, y0*s, (x0+ux*len*mid)*s, (y0+uy*len*mid)*s, r*s, dk,
                    p.metallic, p.smoothness, L,H,fillL);
            DrawLineR(px,R, (x0+ux*len*(mid-0.05f))*s, (y0+uy*len*(mid-0.05f))*s, x1*s, y1*s,
                      r*0.72f*s, new Color(Mathf.Min(bo.r*1.5f,1f), Mathf.Min(bo.g*1.5f,1f), Mathf.Min(bo.b*1.5f,1f)));
            DrawLineR(px,R, (x0+ux*len*(mid-0.05f))*s, (y0+uy*len*(mid-0.05f))*s, x1*s, y1*s,
                      r*0.26f*s, new Color(1f,1f,1f,0.5f));
            FillCircleR(px,R, (x0+ux*len*mid)*s, (y0+uy*len*mid)*s, r*0.9f*s, hi);
            FillCircleR(px,R, x1*s, y1*s, r*0.7f*s, dk);
        }

        /// <summary>Brass pressure gauge with a needle. NEW.</summary>
        static void IndGauge(Color[] px, int R, float x, float y, float r, float val, float s)
        {
            FillCircleR(px,R, x*s, y*s, r*s, ArtGoldDk);
            DrawRingR  (px,R, x*s, y*s, r*s, 1.6f*s, ArtGold);
            FillCircleR(px,R, x*s, y*s, r*0.78f*s, new Color(0.86f, 0.84f, 0.74f));
            for (int i = 0; i < 7; i++)
            {
                float a = (-210f + i*40f) * Mathf.Deg2Rad;
                DrawLineR(px,R, (x+Mathf.Cos(a)*r*0.5f)*s, (y+Mathf.Sin(a)*r*0.5f)*s,
                                (x+Mathf.Cos(a)*r*0.7f)*s, (y+Mathf.Sin(a)*r*0.7f)*s,
                          0.9f*s, new Color(0.2f,0.2f,0.18f));
            }
            float na = (-210f + val*240f) * Mathf.Deg2Rad;
            DrawLineR(px,R, x*s, y*s, (x+Mathf.Cos(na)*r*0.66f)*s, (y+Mathf.Sin(na)*r*0.66f)*s,
                      1.5f*s, new Color(0.72f,0.14f,0.10f));
            FillCircleR(px,R, x*s, y*s, r*0.16f*s, ArtGoldDk);
        }

        /// <summary>Lattice truss — the crane boom.</summary>
        static void IndLattice(Color[] px, int R, float x0, float y0, float x1, float y1,
            float w, int cells, P3DP p, float s)
        {
            float dx = x1-x0, dy = y1-y0, len = Mathf.Sqrt(dx*dx+dy*dy);
            float ux = dx/len, uy = dy/len, pvx = -uy*w, pvy = ux*w;
            DrawLineR(px,R,(x0+pvx)*s,(y0+pvy)*s,(x1+pvx)*s,(y1+pvy)*s,2.4f*s,ArtSteelBase(p));
            DrawLineR(px,R,(x0-pvx)*s,(y0-pvy)*s,(x1-pvx)*s,(y1-pvy)*s,2.4f*s,ArtSteelDk(p));
            for (int i = 0; i < cells; i++)
            {
                float t0 = i/(float)cells, t1 = (i+1)/(float)cells;
                float ax = x0+dx*t0, ay = y0+dy*t0, bx = x0+dx*t1, by = y0+dy*t1;
                DrawLineR(px,R,(ax+pvx)*s,(ay+pvy)*s,(bx-pvx)*s,(by-pvy)*s,1.5f*s,ArtSteelDk(p));
                DrawLineR(px,R,(ax-pvx)*s,(ay-pvy)*s,(bx+pvx)*s,(by+pvy)*s,1.5f*s,ArtSteelDk(p));
            }
        }

        /// <summary>Smokestack with a flared cap.</summary>
        static void IndStack(Color[] px, int R, float x, float y, float h, P3DP p, bool puff, float s)
        {
            P3DPlate(px,R, new[]{ new Vector2((x-4)*s,y*s), new Vector2((x+4)*s,y*s),
                                  new Vector2((x+3.4f)*s,(y+h)*s), new Vector2((x-3.4f)*s,(y+h)*s) },
                     ArtSteelBase(p), ArtSteelDk(p), 1.7f);
            P3DPlate(px,R, new[]{ new Vector2((x-6)*s,(y+h)*s), new Vector2((x+6)*s,(y+h)*s),
                                  new Vector2((x+5)*s,(y+h+4)*s), new Vector2((x-5)*s,(y+h+4)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 1.5f);
            IndRivets(px,R, x-3, y+3, x-3, y+h-3, 3, ArtSteelHi(p), s);
            if (puff)
                for (int i = 0; i < 3; i++)
                    P3DEllipseGlow(px,R, (x+i*2)*s, (y+h+8+i*7)*s, (5+i*2)*s, (4+i*2)*s,
                        new Color(0.35f,0.34f,0.32f, 0.5f - i*0.13f));
        }

        /// <summary>Ducted fan in a wire cage — the rivetbot rotor.</summary>
        static void IndDuct(Color[] px, int R, float x, float y, float r, P3DP p, float spin, float s)
        {
            DrawRingR(px,R, x*s, y*s, r*s, 2.6f*s, ArtSteelBase(p));
            DrawRingR(px,R, x*s, y*s, r*0.82f*s, 1.3f*s, ArtSteelDk(p));
            for (int i = 0; i < 4; i++)
            {
                float a = (i*90f + spin) * Mathf.Deg2Rad;
                P3DPlate(px,R, new[]{
                    new Vector2((x+Mathf.Cos(a)*2)*s, (y+Mathf.Sin(a)*2)*s),
                    new Vector2((x+Mathf.Cos(a+0.5f)*r*0.76f)*s, (y+Mathf.Sin(a+0.5f)*r*0.76f)*s),
                    new Vector2((x+Mathf.Cos(a-0.2f)*r*0.76f)*s, (y+Mathf.Sin(a-0.2f)*r*0.76f)*s) },
                    ArtSteelHi(p), ArtSteelBase(p), 1.0f);
            }
            FillCircleR(px,R, x*s, y*s, 3.0f*s, ArtGold);
            for (int g = 0; g < 3; g++)
                DrawLineR(px,R, (x-r)*s, (y-r+g*r)*s, (x+r)*s, (y-r+g*r)*s, 0.8f*s,
                    new Color(0.5f,0.5f,0.5f,0.4f));
        }

        // ---- RIVETBOT — Industrial drone ---------------------------------------
        static void P3DBuildRivetbot(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p);
            float dv = q == 4 ? -6f : (q == 1 ? 2f : (q == 2 ? -2f : 0f));
            // 4 vanes are 90 deg apart; half-pitch is 45. At 22 the fan barely moved, and 44
            // on the attack was almost a full half-turn back to where it started.
            float spin = q == 1 ? 45f : (q == 2 ? -45f : (Atk(q) ? 22f : 0f));

            P3DPlate(px,R, new[]{ V(52,58+dv), V(78,60+dv), V(80,74+dv), V(54,72+dv) }, ch, c, 2.3f);
            IndRivets(px,R, 55, 70+dv, 77, 72+dv, 6, cd, s);
            IndHazard(px,R, new[]{ V(52,62+dv), V(78,64+dv), V(78,60+dv), V(52,58+dv) }, 7f, 1.6f);
            IndGauge(px,R, 60, 66+dv, 5.0f, 0.5f, s);
            FillCircleR(px,R, 74f*s, (68f+dv)*s, 3.2f*s, new Color(1f,0.85f,0.5f));
            P3DEllipseGlow(px,R, 74f*s, (68f+dv)*s, 8f*s, 7f*s, new Color(1f,0.8f,0.4f,0.5f));
            IndDuct(px,R, 64, 84+dv, 13, p, spin, s);
            P3DPlate(px,R, new[]{ V(58,74+dv), V(70,74+dv), V(69,79+dv), V(59,79+dv) }, c, cd, 1.6f);
            DrawLineR(px,R, 56f*s,(58f+dv)*s, 54f*s,(50f+dv)*s, 2.2f*s, cd);
            DrawLineR(px,R, 74f*s,(60f+dv)*s, 76f*s,(52f+dv)*s, 2.2f*s, cd);
            DrawLineR(px,R, 50f*s,(50f+dv)*s, 60f*s,(50f+dv)*s, 2.4f*s, cd);
            DrawLineR(px,R, 72f*s,(52f+dv)*s, 82f*s,(52f+dv)*s, 2.4f*s, cd);
            for (int e = 0; e < 3; e++)
                P3DEllipseGlow(px,R, (48f-e*6f)*s, (64f+dv-e*3f)*s, (4f-e*0.7f)*s, (3.5f-e*0.6f)*s,
                    new Color(0.35f,0.34f,0.32f, 0.45f - e*0.12f));
            if (q == 4)
                for (int b = 0; b < 4; b++)
                    P3DEllipseGlow(px,R, (86f+b*9f)*s, (66f+dv-b*2f)*s, (5f-b*0.8f)*s, (4f-b*0.7f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f - b*0.15f));
        }

        // ---- GUNNER — Industrial sniper ----------------------------------------
        static void P3DBuildGunner(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int cc) => SideJoints[q, cc];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            Vector2 hip = V(63,56), shN = V(64,84), shF = V(56,84);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 61 : 66, 102);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 4.4f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 6.2f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.4f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.8f*s, ArtCloth, 0.1f, 0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.8f*s, ArtClothDk, 0.1f, 0.2f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+7f*s,f.y-5f*s), new Vector2(f.x-6f*s,f.y-5f*s) },
                         cd, cf, 1.7f);
            P3DPlate(px,R, new[]{ V(53,86), V(74,86), V(76,66), V(69,50), V(57,50), V(51,66) },
                     ArtCloth, ArtClothDk, 2.2f);
            P3DPlate(px,R, new[]{ V(46,72), V(58,74), V(60,58), V(48,56) },
                     ArtLeather, new Color(ArtLeather.r*0.5f,ArtLeather.g*0.5f,ArtLeather.b*0.5f), 1.9f);
            IndRivets(px,R, 48,70, 58,72, 3, ch, s);
            DrawLineR(px,R, 50f*s,80f*s, 72f*s,72f*s, 2.8f*s, ArtLeather);
            P3DPlate(px,R, new[]{ V(58,88), V(76,90), V(78,78), V(60,76) }, ch, c, 2.0f);
            IndRivets(px,R, 61,86, 75,88, 4, cd, s);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.0f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.4f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-10f*s), new Vector2(head.x+10f*s,head.y-9f*s),
                                  new Vector2(head.x+9f*s,head.y+7f*s), new Vector2(head.x-8f*s,head.y+6f*s) },
                     c, cd, 2.1f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-7f*s,head.y-2f*s), new Vector2(head.x+9f*s,head.y-1f*s),
                                  new Vector2(head.x+9f*s,head.y+2.6f*s), new Vector2(head.x-7f*s,head.y+1.6f*s) },
                     new Color(0.10f,0.16f,0.12f), new Color(0.04f,0.07f,0.05f), 1f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y+0.6f*s, 4.4f*s, 2.0f*s,
                new Color(0.3f,0.9f,0.6f, Atk(q) ? 0.6f : 0.25f));
            P3DPlate(px,R, new[]{ new Vector2(head.x-13f*s,head.y+1f*s), new Vector2(head.x-8f*s,head.y+3f*s),
                                  new Vector2(head.x-8f*s,head.y-5f*s), new Vector2(head.x-13f*s,head.y-3f*s) },
                     cd, cf, 1.4f);
            float rec = q == 4 ? -9f : (q == 3 ? 3f : 0f);
            DrawLineR(px,R, (nH.x/s-6f+rec)*s, nH.y, (nH.x/s+44f+rec)*s, (nH.y/s+2f)*s, 4.0f*s, cd);
            DrawLineR(px,R, (nH.x/s-6f+rec)*s, nH.y, (nH.x/s+44f+rec)*s, (nH.y/s+2f)*s, 1.5f*s, ch);
            IndRivets(px,R, nH.x/s+4f+rec, nH.y/s-2f, nH.x/s+34f+rec, nH.y/s, 4, ch, s);
            P3DPlate(px,R, new[]{ V(nH.x/s+34+rec, nH.y/s+6), V(nH.x/s+48+rec, nH.y/s+4),
                                  V(nH.x/s+48+rec, nH.y/s-2), V(nH.x/s+34+rec, nH.y/s-3) }, ch, c, 1.5f);
            P3DPlate(px,R, new[]{ V(nH.x/s-10+rec, nH.y/s+5), V(nH.x/s-2+rec, nH.y/s+5),
                                  V(nH.x/s-2+rec, nH.y/s-4), V(nH.x/s-10+rec, nH.y/s-4) },
                     ArtLeather, new Color(ArtLeather.r*0.5f,ArtLeather.g*0.5f,ArtLeather.b*0.5f), 1.4f);
            if (q == 4)
            {
                P3DEllipseGlow(px,R, (nH.x/s+56f)*s, (nH.y/s+3f)*s, 14f*s, 9f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.8f));
                for (int b = 0; b < 3; b++)
                    P3DEllipseGlow(px,R, (nH.x/s-14f-b*8f)*s, (nH.y/s-2f)*s, (6f-b)*s, (5f-b)*s,
                        new Color(0.4f,0.4f,0.4f, 0.4f - b*0.1f));
            }
        }

        // ---- BULKHEAD — Industrial shield-bot -----------------------------------
        static void P3DBuildBulkhead(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int cc) => SideJoints[q, cc];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            Vector2 hip = V(62,52), shN = V(63,80), shF = V(55,80);
            Vector2 nK = V(J(0)-2,J(1)), nF = V(J(2)-2,J(3));
            Vector2 fK = V(J(4)-2,J(5)), fF = V(J(6)-2,J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 58 : 63, 98);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 5.4f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 8.0f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 7.0f*s, cf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            IndPiston(px,R, hip.x/s,hip.y/s, nK.x/s,nK.y/s, 8.6f, p, q == 4 ? 1f : 0f, L,H,fillL, s);
            IndPiston(px,R, nK.x/s,nK.y/s, nF.x/s,nF.y/s, 7.4f, p, 0f, L,H,fillL, s);
            foreach (var f in new[]{ nF, fF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-9f*s,f.y+4f*s), new Vector2(f.x+10f*s,f.y+4f*s),
                                      new Vector2(f.x+8f*s,f.y-5f*s), new Vector2(f.x-8f*s,f.y-5f*s) },
                         cd, cf, 1.8f);
            P3DPlate(px,R, new[]{ V(50,86), V(76,86), V(78,66), V(72,50), V(54,50), V(48,66) }, ch, c, 2.4f);
            IndRivets(px,R, 52,83, 74,83, 6, cd, s);
            IndRivets(px,R, 52,54, 74,54, 6, cd, s);
            IndGauge(px,R, 58, 70, 5.4f, 0.8f, s);
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y-9f*s), new Vector2(head.x+11f*s,head.y-9f*s),
                                  new Vector2(head.x+10f*s,head.y+5f*s), new Vector2(head.x-10f*s,head.y+5f*s) },
                     ch, c, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s,head.y-2f*s), new Vector2(head.x+9f*s,head.y-2f*s),
                                  new Vector2(head.x+9f*s,head.y+1f*s), new Vector2(head.x-8f*s,head.y+1f*s) },
                     new Color(0.06f,0.06f,0.07f), new Color(0.02f,0.02f,0.03f), 1f);
            IndRivets(px,R, head.x/s-9, head.y/s+4, head.x/s+9, head.y/s+4, 4, cd, s);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.8f*s, c, p.metallic, p.smoothness, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 5.0f*s, c, p.metallic, p.smoothness, L,H,fillL);
            float sw = q == 4 ? 16f : (q == 3 ? -9f : 3f);
            DrawLineR(px,R, nH.x, nH.y, (nH.x/s+sw)*s, (nH.y/s+16f)*s, 3.4f*s, cd);
            P3DPlate(px,R, new[]{ V(nH.x/s+sw-4, nH.y/s+16), V(nH.x/s+sw+5, nH.y/s+16),
                                  V(nH.x/s+sw+4, nH.y/s+24), V(nH.x/s+sw-3, nH.y/s+24) }, ch, c, 1.5f);
            // THE blast door — a plain rectangle, the most boring shape available, on purpose
            float bx = 64f + (q == 4 ? 14f : (q == 3 ? 3f : 9f));
            P3DPlate(px,R, new[]{ V(bx-15,92), V(bx+15,92), V(bx+15,32), V(bx-15,32) }, c, cd, 2.6f);
            IndHazard(px,R, new[]{ V(bx-14,44), V(bx+14,44), V(bx+14,33), V(bx-14,33) }, 7f, 1.5f);
            IndHazard(px,R, new[]{ V(bx-14,91), V(bx+14,91), V(bx+14,80), V(bx-14,80) }, 7f, 1.5f);
            IndRivets(px,R, bx-12,46, bx-12,78, 6, ch, s);
            IndRivets(px,R, bx+12,46, bx+12,78, 6, ch, s);
            P3DPlate(px,R, new[]{ V(bx-7,68), V(bx+7,68), V(bx+7,58), V(bx-7,58) },
                     new Color(0.07f,0.09f,0.10f), new Color(0.03f,0.04f,0.05f), 1.6f);
            P3DEllipseGlow(px,R, bx*s, 63f*s, 7f*s, 4f*s, new Color(0.5f,0.7f,0.8f,0.28f));
            DrawRingR(px,R, bx*s, 50f*s, 5.4f*s, 2.0f*s, ArtGold);
            for (int i = 0; i < 4; i++)
            {
                float a = i*45f*Mathf.Deg2Rad;
                DrawLineR(px,R, (bx+Mathf.Cos(a)*3)*s, (50f+Mathf.Sin(a)*3)*s,
                                (bx+Mathf.Cos(a)*7)*s, (50f+Mathf.Sin(a)*7)*s, 1.6f*s, ArtGold);
            }
        }

        // ---- CRANE — Industrial mech --------------------------------------------
        static void P3DBuildCrane(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float roll = q == 1 ? 0f : (q == 2 ? 1f : (q == 6 ? 2f
                       : (q == 7 ? 3f : (q == 4 ? 1.5f : 0f))));
            float swing = q == 3 ? 36f : (q == 4 ? -24f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            P3DPlate(px,R, new[]{ V(38,16), V(92,16), V(94,30), V(36,30) }, cd, cf, 2.4f);

            // TRACKS, not legs. The cleats used to shift 4 units between walk frames and the road
            // wheels were plain filled circles -- a circle with no spokes cannot show that it is
            // turning, so the whole assembly read as a static box sliding along the ground.
            // Cleats now travel a full pitch and wrap around the belt, and every wheel is spoked
            // and rotates half a spoke-pitch per frame, which is the maximum perceived motion.
            const float PITCH = 8f, BELT = 56f;
            float travel = roll * (PITCH * 0.5f);
            for (int t = 0; t < 8; t++)
            {
                float cx = 38f + Mathf.Repeat(t * PITCH + travel, BELT);
                DrawLineR(px,R, cx*s, 17f*s, cx*s, 29f*s, 2.0f*s, ch);
                DrawLineR(px,R, cx*s, 17.5f*s, cx*s, 20f*s, 2.6f*s, cf);   // grouser bite
            }
            void RoadWheel(float wx, float rr, int spokes)
            {
                FillCircleR(px,R, wx*s, 23f*s, rr*s, c);
                DrawRingR  (px,R, wx*s, 23f*s, rr*s, 1.6f*s, cd);
                for (int i = 0; i < spokes; i++)
                {
                    float a = (i * (360f/spokes) + roll * (180f/spokes)) * Mathf.Deg2Rad;
                    DrawLineR(px,R, wx*s, 23f*s,
                              (wx + Mathf.Cos(a)*(rr-1.6f))*s, (23f + Mathf.Sin(a)*(rr-1.6f))*s,
                              1.5f*s, ch);
                }
                FillCircleR(px,R, wx*s, 23f*s, rr*0.30f*s, cd);
            }
            RoadWheel(46f, 6.4f, 5);          // drive sprocket
            RoadWheel(84f, 6.4f, 5);          // idler
            RoadWheel(60f, 4.0f, 4);          // road wheels between them
            RoadWheel(70f, 4.0f, 4);
            P3DPlate(px,R, new[]{ V(40,30), V(90,30), V(88,44), V(42,44) }, ch, c, 2.2f);
            IndRivets(px,R, 44,42, 86,42, 9, cd, s);
            P3DPlate(px,R, new[]{ V(44,44), V(68,44), V(70,70), V(46,70) }, c, cd, 2.2f);
            P3DPlate(px,R, new[]{ V(49,64), V(65,64), V(64,52), V(50,52) },
                     new Color(0.35f,0.45f,0.50f), new Color(0.14f,0.20f,0.24f), 1.6f);
            IndGauge(px,R, 60, 48, 4.4f, 0.4f, s);
            P3DPlate(px,R, new[]{ V(72,44), V(88,44), V(86,60), V(74,60) }, cd, cf, 2.0f);
            IndRivets(px,R, 75,58, 85,58, 3, ch, s);
            Vector2 piv = new Vector2(70f, 68f);
            Vector2 d = RotP(new Vector2(1f,0f), Vector2.zero, 52f + swing);
            Vector2 tip = new Vector2(piv.x + d.x*54f, piv.y + d.y*54f);
            IndLattice(px,R, piv.x, piv.y, tip.x, tip.y, 4.4f, 6, p, s);
            FillCircleR(px,R, piv.x*s, piv.y*s, 5.0f*s, ch);
            FillCircleR(px,R, piv.x*s, piv.y*s, 2.2f*s, cd);
            float drop = q == 4 ? 26f : (q == 3 ? 8f : 14f);
            DrawLineR(px,R, tip.x*s, tip.y*s, tip.x*s, (tip.y-drop)*s, 1.2f*s, new Color(0.55f,0.55f,0.55f));
            P3DPlate(px,R, new[]{ V(tip.x-6, tip.y-drop), V(tip.x+6, tip.y-drop),
                                  V(tip.x+5, tip.y-drop-9), V(tip.x-5, tip.y-drop-9) }, ch, c, 1.8f);
            DrawLineR(px,R, tip.x*s, (tip.y-drop-9)*s, (tip.x-4)*s, (tip.y-drop-16)*s, 2.4f*s, ch);
            if (q == 4)
            {
                P3DEllipseGlow(px,R, (tip.x-2)*s, (tip.y-drop-14)*s, 16f*s, 14f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
                for (int k = 0; k < 4; k++)
                {
                    float a = (200f+k*22f)*Mathf.Deg2Rad;
                    DrawLineR(px,R, tip.x*s, (tip.y-drop-12)*s,
                              (tip.x+Mathf.Cos(a)*13f)*s, (tip.y-drop-12+Mathf.Sin(a)*13f)*s,
                              1.8f*s, new Color(0.8f,0.8f,0.8f,0.6f));
                }
            }
        }

        // ---- FURNACE — Industrial titan ------------------------------------------
        static void P3DBuildFurnace(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float st = q == 1 ? 4f : (q == 2 ? -4f : 0f);
            float lean = q == 3 ? -8f : (q == 4 ? 8f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 16f*s, 36f*s, 8f*s, new Color(0f,0f,0f,0.5f));
            IndPiston(px,R, 58,46, 64-18-st, 26, 8.0f, p, q == 1 ? 1f : 0f, L,H,fillL, s);
            P3DLimb(px,R, (64-18-st)*s,26f*s, (64-26-st)*s,10f*s, 6.6f*s, cf, 0.3f,0.4f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64-34-st,14), V(64-16-st,14), V(64-18-st,6), V(64-32-st,6) }, cd, cf, 2.0f);
            IndPiston(px,R, 70,46, 64+18+st, 26, 9.0f, p, q == 2 ? 1f : 0f, L,H,fillL, s);
            P3DLimb(px,R, (64+18+st)*s,26f*s, (64+26+st)*s,10f*s, 7.2f*s, c, 0.3f,0.4f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+16+st,14), V(64+34+st,14), V(64+32+st,6), V(64+18+st,6) }, ch, c, 2.0f);
            float hx = 64f + lean;
            P3DPlate(px,R, new[]{ V(hx-20,34), V(hx+20,34), V(hx+22,52), V(hx-22,52) }, ch, c, 2.4f);
            P3DPlate(px,R, new[]{ V(hx-22,52), V(hx+22,52), V(hx+20,92), V(hx-20,92) }, ch, c, 2.6f);
            IndRivets(px,R, hx-18,56, hx+18,56, 9, cd, s);
            IndRivets(px,R, hx-18,88, hx+18,88, 9, cd, s);
            DrawLineR(px,R, (hx-20)*s,72f*s, (hx+20)*s,72f*s, 2.0f*s, cd);
            P3DPlate(px,R, new[]{ V(hx-12,80), V(hx+12,80), V(hx+12,60), V(hx-12,60) }, cd, cf, 2.2f);
            for (int g = 0; g < 4; g++)
                DrawLineR(px,R, (hx-10)*s, (63f+g*4.4f)*s, (hx+10)*s, (63f+g*4.4f)*s, 2.0f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
            P3DEllipseGlow(px,R, hx*s, 70f*s, 20f*s, 17f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, Atk(q) ? 0.65f : 0.45f));
            DrawRingR(px,R, (hx+9)*s, 70f*s, 3.0f*s, 1.4f*s, ArtGold);
            IndGauge(px,R, hx-15, 84, 5.0f, Atk(q) ? 0.9f : 0.5f, s);
            IndStack(px,R, hx-11, 92, 16, p, true, s);
            IndStack(px,R, hx+9, 92, 20, p, true, s);
            float ext = q == 4 ? 1f : 0f;
            IndPiston(px,R, hx-16,86, hx-(q == 4 ? 30f : 18f), 74, 7.0f, p, ext, L,H,fillL, s);
            P3DPlate(px,R, new[]{ V(hx-(q==4?40:28),80), V(hx-(q==4?22:10),80),
                                  V(hx-(q==4?24:12),66), V(hx-(q==4?38:26),66) }, cd, cf, 2.0f);
            IndPiston(px,R, hx+16,86, hx+(q == 4 ? 32f : 19f), 74, 7.6f, p, ext, L,H,fillL, s);
            P3DPlate(px,R, new[]{ V(hx+(q==4?23:11),80), V(hx+(q==4?43:29),80),
                                  V(hx+(q==4?41:27),66), V(hx+(q==4?25:13),66) }, ch, c, 2.0f);
            if (q == 4) P3DEllipseGlow(px,R, (hx+46f)*s, 73f*s, 20f*s, 17f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
            P3DPlate(px,R, new[]{ V(hx-9,92), V(hx+9,92), V(hx+7,104), V(hx-7,104) }, c, cd, 2.0f);
            FillCircleR(px,R, (hx+3)*s, 99f*s, 3.4f*s, new Color(1f,0.92f,0.6f));
            P3DEllipseGlow(px,R, (hx+3)*s, 99f*s, 9f*s, 8f*s, new Color(1f,0.86f,0.5f,0.55f));
        }

        // ---- ORNITHOPTER — Industrial interceptor --------------------------------
        static void P3DBuildOrnithopter(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float beat = q == 1 ? 15f : (q == 2 ? -13f : (Atk(q) ? 19f : 0f));

            void Wing(float ax, float ay, float dir, float len, Color tone, float al)
            {
                float d = dir * Mathf.Deg2Rad;
                float tx = ax + Mathf.Cos(d)*len, ty = ay + Mathf.Sin(d)*len;
                float pvx = -Mathf.Sin(d)*len*0.24f, pvy = Mathf.Cos(d)*len*0.24f;
                DrawLineR(px,R, ax*s,ay*s, tx*s,ty*s, 2.4f*s, tone);
                DrawLineR(px,R, ax*s,ay*s, (tx+pvx)*s,(ty+pvy)*s, 1.6f*s, tone);
                DrawLineR(px,R, ax*s,ay*s, (tx-pvx*0.6f)*s,(ty-pvy*0.6f)*s, 1.6f*s, tone);
                P3DPlate(px,R, new[]{ V(ax,ay), V(tx+pvx,ty+pvy), V(tx-pvx*0.6f,ty-pvy*0.6f) },
                         new Color(ArtCloth.r,ArtCloth.g,ArtCloth.b,al),
                         new Color(ArtClothDk.r,ArtClothDk.g,ArtClothDk.b,al), 1.2f);
                for (int r = 1; r < 3; r++)
                    DrawLineR(px,R, (ax+(tx-ax)*r/3f)*s, (ay+(ty-ay)*r/3f)*s,
                              (ax+(tx-ax)*r/3f+pvx*0.5f)*s, (ay+(ty-ay)*r/3f+pvy*0.5f)*s, 1.0f*s, tone);
            }
            Wing(58,58, 176f-beat*0.7f, 30f, cf, 0.5f);
            Wing(62,62, 170f+beat, 36f, c, 0.72f);
            P3DPlate(px,R, new[]{ V(54,50), V(76,54), V(82,64), V(74,70), V(56,64) }, ch, c, 2.2f);
            IndRivets(px,R, 58,56, 74,60, 5, cd, s);
            float ca = (q == 1 ? 30f : (q == 2 ? -30f : (Atk(q) ? 60f : 0f))) * Mathf.Deg2Rad;
            FillCircleR(px,R, 64f*s,62f*s, 6.4f*s, cd);
            DrawRingR  (px,R, 64f*s,62f*s, 6.4f*s, 1.6f*s, ch);
            DrawLineR(px,R, 64f*s,62f*s, (64f+Mathf.Cos(ca)*5f)*s, (62f+Mathf.Sin(ca)*5f)*s, 2.2f*s, ArtGold);
            DrawLineR(px,R, (64f+Mathf.Cos(ca)*5f)*s, (62f+Mathf.Sin(ca)*5f)*s, 60f*s, 62f*s, 1.8f*s, ch);
            P3DPlate(px,R, new[]{ V(70,68), V(82,70), V(84,60), V(72,58) },
                     new Color(0.30f,0.38f,0.42f), new Color(0.12f,0.16f,0.20f), 1.7f);
            for (int b = 0; b < 3; b++)
                DrawLineR(px,R, (72f+b*4f)*s, 69f*s, (72f+b*4f)*s, 59f*s, 1.0f*s, cd);
            FillCircleR(px,R, 88f*s, 64f*s, 3.0f*s, cd);
            // 2 blades sit 90 deg apart, so half-pitch is 45 and the blade pattern repeats
            // every 90. Alternating 0 / 45 is the maximum perceived spin for a two-blade prop.
            float pa = (q % 2 == 1) ? 45f : 0f;
            for (int i = 0; i < 2; i++)
            {
                float a = (pa+i*90f)*Mathf.Deg2Rad;
                P3DPlate(px,R, new[]{ V(88,64), V(88+Mathf.Cos(a)*11f, 64+Mathf.Sin(a)*4f),
                                      V(88+Mathf.Cos(a)*10f, 64+Mathf.Sin(a)*4f-2f) }, ch, c, 0.9f);
            }
            P3DPlate(px,R, new[]{ V(54,60), V(40,66), V(38,58), V(52,55) }, c, cd, 1.7f);
            if (q == 4)
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px,R, (96f+e*9f)*s, (64f-e*2f)*s, (5f-e*0.8f)*s, (4f-e*0.7f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f - e*0.15f));
        }

        // ---- ENGINEER — Industrial hacker ----------------------------------------
        static void P3DBuildEngineer(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int cc) => SideJoints[q, cc];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            Vector2 hip = V(63,54), shN = V(64,82), shF = V(56,82);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 60 : 65, 101);
            bool arc = Atk(q);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 4.6f*s, cf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 5.8f*s, cf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.0f*s, cf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.4f*s, ArtClothDk, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.6f*s, ArtClothDk, 0.1f,0.2f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+8f*s,f.y+3f*s),
                                      new Vector2(f.x+6f*s,f.y-5f*s), new Vector2(f.x-6f*s,f.y-5f*s) },
                         ArtLeather, new Color(ArtLeather.r*0.45f,ArtLeather.g*0.45f,ArtLeather.b*0.45f), 1.6f);
            P3DPlate(px,R, new[]{ V(44,84), V(58,86), V(60,58), V(46,56) }, cd, cf, 2.1f);
            IndRivets(px,R, 47,82, 57,84, 3, ch, s);
            for (int w = 0; w < 4; w++)
                DrawLineR(px,R, 46f*s, (62f+w*4f)*s, 58f*s, (63f+w*4f)*s, 1.6f*s, ArtGold);
            IndGauge(px,R, 52, 76, 4.6f, arc ? 0.9f : 0.35f, s);
            P3DPlate(px,R, new[]{ V(54,84), V(74,84), V(76,62), V(70,48), V(58,48), V(52,62) },
                     ArtCloth, ArtClothDk, 2.2f);
            DrawLineR(px,R, 56f*s,72f*s, 74f*s,74f*s, 2.2f*s, ArtLeather);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.0f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.4f*s, ArtCloth, 0.1f,0.2f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-10f*s,head.y-9f*s), new Vector2(head.x+10f*s,head.y-8f*s),
                                  new Vector2(head.x+9f*s,head.y+8f*s), new Vector2(head.x-9f*s,head.y+7f*s) },
                     ArtCloth, ArtClothDk, 2.1f);
            FillCircleR(px,R, head.x+4f*s, head.y-1f*s, 3.4f*s, new Color(0.12f,0.14f,0.10f));
            DrawRingR  (px,R, head.x+4f*s, head.y-1f*s, 4.4f*s, 1.3f*s, ArtGold);
            P3DEllipseGlow(px,R, head.x+4f*s, head.y-1f*s, 5f*s, 4f*s,
                new Color(0.5f,0.8f,1f, arc ? 0.6f : 0.25f));
            float lift = arc ? -18f : -4f;
            Vector2 d = RotP(new Vector2(0f,1f), Vector2.zero, lift);
            Vector2 top = new Vector2(nH.x/s + d.x*40f, nH.y/s + d.y*40f);
            DrawLineR(px,R, (nH.x/s-d.x*10f)*s, (nH.y/s-d.y*10f)*s, top.x*s, top.y*s, 3.0f*s, ArtLeather);
            for (int k = 0; k < 5; k++)
            {
                float t = k/4f;
                DrawRingR(px,R, (nH.x/s + (top.x-nH.x/s)*(0.55f+t*0.36f))*s,
                                (nH.y/s + (top.y-nH.y/s)*(0.55f+t*0.36f))*s,
                          (5.4f-k*0.7f)*s, 1.5f*s, ArtGold);
            }
            FillCircleR(px,R, top.x*s, top.y*s, 4.4f*s, ch);
            FillCircleR(px,R, top.x*s, top.y*s, 2.2f*s, new Color(0.7f,0.9f,1f));
            P3DEllipseGlow(px,R, top.x*s, top.y*s, (arc?15f:8f)*s, (arc?15f:8f)*s,
                new Color(0.5f,0.8f,1f, arc ? 0.7f : 0.3f));
            if (arc)
            {
                // forked lightning, not a soft glow — the attack has to have a shape
                float pxp = top.x, pyp = top.y;
                for (int i2 = 0; i2 < 5; i2++)
                {
                    float nx = top.x + (i2+1)*(q == 4 ? 9f : 5f);
                    float ny = top.y + ((i2 % 2 == 1) ? 5f : -5f) - i2*1.5f;
                    DrawLineR(px,R, pxp*s, pyp*s, nx*s, ny*s, 1.8f*s, new Color(0.75f,0.92f,1f,0.95f));
                    pxp = nx; pyp = ny;
                }
                P3DEllipseGlow(px,R, pxp*s, pyp*s, 10f*s, 9f*s, new Color(0.5f,0.8f,1f,0.6f));
            }
        }

        // ---- GATLING — Industrial turret ------------------------------------------
        static void P3DBuildGatling(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float spin = q == 4 ? 42f : (q == 3 ? 18f : 0f);
            float rec  = q == 4 ? -5f : (q == 3 ? 2f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 32f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            P3DPlate(px,R, new[]{ V(44,16), V(52,16), V(62,54), V(56,54) }, c, cd, 2.1f);
            P3DPlate(px,R, new[]{ V(76,16), V(84,16), V(72,54), V(66,54) }, ch, c, 2.1f);
            P3DPlate(px,R, new[]{ V(60,16), V(68,16), V(67,50), V(61,50) }, cf,
                     new Color(cf.r*0.7f, cf.g*0.7f, cf.b*0.7f), 1.9f);
            DrawLineR(px,R, 50f*s,30f*s, 78f*s,30f*s, 2.8f*s, cd);
            foreach (float fx in new[]{ 48f, 64f, 80f })
                P3DPlate(px,R, new[]{ new Vector2((fx-6)*s,13f*s), new Vector2((fx+6)*s,13f*s),
                                      new Vector2((fx+4)*s,18f*s), new Vector2((fx-4)*s,18f*s) }, ch, c, 1.5f);
            P3DPlate(px,R, new[]{ V(50,54), V(78,54), V(76,64), V(52,64) }, ch, c, 2.1f);
            P3DPlate(px,R, new[]{ V(46,64), V(60,66), V(60,80), V(46,78) }, cd, cf, 2.0f);
            for (int g = 0; g < 3; g++)
                DrawLineR(px,R, 48f*s, (69f+g*4f)*s, 58f*s, (70f+g*4f)*s, 1.8f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
            P3DEllipseGlow(px,R, 53f*s, 72f*s, 11f*s, 10f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, Atk(q) ? 0.6f : 0.4f));
            IndStack(px,R, 50, 80, 12, p, true, s);
            P3DPlate(px,R, new[]{ V(58,80), V(76,82), V(78,94), V(60,92) }, ch, c, 2.1f);
            IndRivets(px,R, 61,90, 76,92, 5, cd, s);
            for (int l = 0; l < 5; l++)
                DrawLineR(px,R, (64f+l*2f)*s, 80f*s, (64f+l*2f)*s, 74f*s, 1.4f*s, ArtGold);
            float bx = 68f + rec;
            FillCircleR(px,R, bx*s, 70f*s, 8.4f*s, cd);
            DrawRingR  (px,R, bx*s, 70f*s, 8.4f*s, 2.0f*s, ch);
            for (int i = 0; i < 6; i++)
            {
                float a = (i*60f + spin) * Mathf.Deg2Rad;
                float ox = bx + Mathf.Cos(a)*4.6f, oy = 70f + Mathf.Sin(a)*4.6f;
                DrawLineR(px,R, ox*s, oy*s, (ox+26f)*s, oy*s, 2.6f*s, i % 2 == 0 ? ch : c);
                FillCircleR(px,R, (ox+26f)*s, oy*s, 1.6f*s, cd);
            }
            FillCircleR(px,R, bx*s, 70f*s, 3.0f*s, ArtGold);
            P3DPlate(px,R, new[]{ V(bx+24,76), V(bx+34,74), V(bx+34,66), V(bx+24,64) }, cd, cf, 1.7f);
            if (q == 4)
            {
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px,R, (bx+40f+e*10f)*s, (70f+((e%2==1)?2f:-2f))*s,
                        (7f-e*1.2f)*s, (6f-e*1.1f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.75f - e*0.16f));
                for (int s2 = 0; s2 < 3; s2++)
                    P3DEllipseGlow(px,R, (bx-6f-s2*5f)*s, (62f-s2*4f)*s, 4f*s, 3.4f*s,
                        new Color(0.4f,0.4f,0.4f, 0.4f - s2*0.11f));
            }
        }

        // ══ BIOPUNK ══════════════════════════════════════════════════════════════
        // Theme 2. Medieval is hand-forged, Solar is cast and fired -- Biopunk is grown.
        // Nothing is manufactured: chitin, bone, sinew, fruiting bodies and translucent sacs
        // lit from inside. And unlike both finished themes this one is deliberately
        // ASYMMETRIC -- no two limbs match, and the mismatch is the silhouette.
        // Colours come from _artSets[2], so ArtGold reads as bone and ArtRune as acid green.

        // ---- new primitives the grown look needs ------------------------------

        /// <summary>
        /// Translucent sac: a membrane you see INTO. Nothing in the existing primitive set
        /// draws interior detail -- plates and limbs are opaque -- so bodies that are supposed
        /// to be organs read as solid blobs without this. Veins, a nucleus and a wet specular
        /// highlight on the lower-left, matching the light rig.
        /// </summary>
        static void P3DSac(Color[] px, int R, float cx, float cy, float rx, float ry,
            Color inner, float pulse)
        {
            rx *= 1f + pulse * 0.06f;
            ry *= 1f + pulse * 0.06f;
            int x0 = Mathf.Max(0, (int)(cx - rx - 2)), x1 = Mathf.Min(R - 1, (int)(cx + rx + 2));
            int y0 = Mathf.Max(0, (int)(cy - ry - 2)), y1 = Mathf.Min(R - 1, (int)(cy + ry + 2));
            var shell = new Color(0.10f, 0.20f, 0.10f);
            var mid   = new Color(0.30f, 0.46f, 0.24f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                float d2 = dx * dx + dy * dy;
                if (d2 > 1f) continue;
                float d = Mathf.Sqrt(d2);
                // interior gradient: hot core, cooling out to a dark rim
                Color c = d < 0.45f ? Color.Lerp(inner, mid, d / 0.45f)
                                    : Color.Lerp(mid, shell, (d - 0.45f) / 0.55f);
                c.a = 0.94f;
                P3DSet(px, R, x, y, c);
            }
            // veins radiating from the nucleus
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 18f) * Mathf.Deg2Rad;
                DrawLineR(px, R, cx + Mathf.Cos(a) * rx * 0.22f, cy + Mathf.Sin(a) * ry * 0.22f,
                                 cx + Mathf.Cos(a) * rx * 0.86f, cy + Mathf.Sin(a) * ry * 0.86f,
                          1.2f, new Color(0.16f, 0.30f, 0.14f, 0.85f));
            }
            FillCircleR(px, R, cx, cy, Mathf.Min(rx, ry) * 0.30f,
                new Color(inner.r, inner.g, inner.b, 0.9f));
            P3DEllipseGlow(px, R, cx, cy, rx * 1.15f, ry * 1.15f,
                new Color(inner.r, inner.g, inner.b, 0.35f));
            // wet specular, lower-left to match L = (-0.45, 0.70, 0.55)
            P3DEllipseGlow(px, R, cx - rx * 0.34f, cy + ry * 0.36f, rx * 0.24f, ry * 0.16f,
                new Color(1f, 1f, 1f, 0.38f));
        }

        /// <summary>
        /// Chitin plate: a normal plate with per-vertex jitter, so no two shells in the theme
        /// are identical. Deterministic on `seed` -- the same plate must not shimmer between
        /// pose frames.
        /// </summary>
        static Vector2[] P3DChitin(Color[] px, int R, Vector2[] pts, P3DP p, int seed, float ow)
        {
            var j = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.Sin((seed + i) * 12.9898f) * 43758.5453f;
                float b = Mathf.Sin((seed + i) * 78.2330f) * 43758.5453f;
                j[i] = new Vector2(pts[i].x + ((a - Mathf.Floor(a)) - 0.5f) * 2.6f,
                                   pts[i].y + ((b - Mathf.Floor(b)) - 0.5f) * 2.6f);
            }
            P3DPlate(px, R, j, ArtSteelHi(p), ArtSteelBase(p), ow);
            DrawLineR(px, R, j[0].x, j[0].y, j[1].x, j[1].y, 1.4f, new Color(1f, 1f, 1f, 0.28f));
            return j;
        }

        /// <summary>Trailing curled filaments — drone underside, caster hands.</summary>
        static void BioFilaments(Color[] px, int R, float x, float y, int cnt, float len,
            float curl, Color col, float s)
        {
            for (int i = 0; i < cnt; i++)
            {
                float sp = (i - (cnt - 1) * 0.5f) * 3.2f;
                float pxp = x + sp, pyp = y;
                for (int k = 0; k < 4; k++)
                {
                    float t = (k + 1) / 4f;
                    float nx = x + sp * (1f + t * 0.5f) + Mathf.Sin(t * 3f + i) * curl;
                    float ny = y - len * t;
                    DrawLineR(px, R, pxp * s, pyp * s, nx * s, ny * s, (2.0f - t * 1.1f) * s, col);
                    pxp = nx; pyp = ny;
                }
                FillCircleR(px, R, pxp * s, pyp * s, 1.3f * s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.8f));
            }
        }

        /// <summary>Bracket fungus — layered asymmetric fan. Replaces the shield.</summary>
        static void BioShelf(Color[] px, int R, float x, float y, P3DP p, float sc, float dir, float s)
        {
            for (int i = 0; i < 4; i++)
            {
                float w = (22f - i * 3.5f) * sc, h = (7f - i * 0.9f) * sc, yy = y + i * 6f * sc;
                P3DPlate(px, R, new[]{
                    new Vector2(x * s, yy * s),
                    new Vector2((x + dir * w) * s, (yy + h * 0.5f) * s),
                    new Vector2((x + dir * w * 0.92f) * s, (yy + h * 1.5f) * s),
                    new Vector2(x * s, (yy + h * 1.3f) * s) },
                    ArtSteelHi(p), ArtSteelBase(p), 1.7f);
                DrawLineR(px, R, (x + dir * 3f * sc) * s, (yy + h * 0.6f) * s,
                                 (x + dir * w * 0.86f) * s, (yy + h * 1.0f) * s, 1.1f * sc * s, ArtGoldDk);
            }
            P3DEllipseGlow(px, R, (x + dir * 10f * sc) * s, (y + 12f * sc) * s, 12f * sc * s, 14f * sc * s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.22f));
        }

        /// <summary>Segmented insect leg — thin, splayed, knuckled at each joint.</summary>
        static void BioSegLeg(Color[] px, int R, float x0, float y0, Vector2[] pts, float w,
            Color c, float s, Vector3 L, Vector3 H, Vector3 fillL)
        {
            float pxp = x0, pyp = y0;
            for (int i = 0; i < pts.Length; i++)
            {
                P3DLimb(px, R, pxp * s, pyp * s, pts[i].x * s, pts[i].y * s,
                        (w - i * 0.6f) * s, c, 0.15f, 0.35f, L, H, fillL);
                FillCircleR(px, R, pts[i].x * s, pts[i].y * s,
                            Mathf.Max(1.2f, w - i * 0.9f) * s,
                            new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f));
                pxp = pts[i].x; pyp = pts[i].y;
            }
        }

        /// <summary>Gilled fruiting body — the mycelium's head.</summary>
        static void BioCap(Color[] px, int R, float x, float y, P3DP p, float sc, float s)
        {
            P3DPlate(px, R, new[]{
                new Vector2((x - 18f * sc) * s, y * s), new Vector2((x - 13f * sc) * s, (y + 11f * sc) * s),
                new Vector2(x * s, (y + 15f * sc) * s), new Vector2((x + 13f * sc) * s, (y + 11f * sc) * s),
                new Vector2((x + 18f * sc) * s, y * s) },
                ArtSteelHi(p), ArtSteelBase(p), 2.2f);
            for (int i = 0; i < 7; i++)
            {
                float gx = x - 15f * sc + i * 5f * sc;
                DrawLineR(px, R, gx * s, y * s, (gx + (gx - x) * 0.06f) * s, (y - 6f * sc) * s,
                          1.6f * sc * s, ArtClothDk);
            }
            DrawLineR(px, R, (x - 18f * sc) * s, y * s, (x + 18f * sc) * s, y * s, 1.8f * sc * s, ArtGoldDk);
            P3DEllipseGlow(px, R, x * s, (y - 3f * sc) * s, 16f * sc * s, 7f * sc * s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.3f));
        }

        /// <summary>Bone club — knobbed and spined. Grown, not forged.</summary>
        static void BioBone(Color[] px, int R, Vector2 grip, float deg, float len, float s)
        {
            Vector2 d = RotP(new Vector2(0f, 1f), Vector2.zero, deg);
            Vector2 tip = new Vector2(grip.x + d.x * len * s, grip.y + d.y * len * s);
            DrawLineR(px, R, grip.x, grip.y, tip.x, tip.y, 3.6f * s, ArtGold);
            FillCircleR(px, R, tip.x, tip.y, 7.0f * s, ArtGoldHi);
            FillCircleR(px, R, tip.x - d.y * 4f * s, tip.y + d.x * 4f * s, 4.0f * s, ArtGold);
            FillCircleR(px, R, tip.x + d.y * 4f * s, tip.y - d.x * 4f * s, 4.0f * s, ArtGold);
            for (int i = 0; i < 3; i++)
            {
                Vector2 sd = RotP(new Vector2(0f, 1f), Vector2.zero, deg + 90f + i * 60f);
                P3DPlate(px, R, new[]{
                    new Vector2(tip.x + sd.x * 4f * s, tip.y + sd.y * 4f * s),
                    new Vector2(tip.x + sd.x * 12f * s, tip.y + sd.y * 12f * s),
                    new Vector2(tip.x + sd.x * 5f * s - sd.y * 3f * s,
                                tip.y + sd.y * 5f * s + sd.x * 3f * s) },
                    ArtGoldHi, ArtGold, 1.1f);
            }
        }

        // ---- MUTANT — Biopunk trooper ------------------------------------------
        static void P3DBuildMutant(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);

            Vector2 hip = V(63,56), shN = V(64,84), shF = V(56,84);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11)), fE = V(J(12),J(13)), fH = V(J(14),J(15));
            Vector2 head = V(q == 5 ? 60 : 67, 101);

            // withered far arm — thin and short
            P3DLimb(px,R, shF.x,shF.y, fE.x+3f*s,fE.y+2f*s, 2.6f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, fE.x+3f*s,fE.y+2f*s, fH.x+6f*s,fH.y+4f*s, 2.0f*s, cf, 0.1f,0.3f, L,H,fillL);
            FillCircleR(px,R, fH.x+6f*s, fH.y+4f*s, 2.4f*s, cf);
            // digitigrade legs — an extra ankle joint no other theme has
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 6.0f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x-3f*s,fF.y+7f*s, 4.8f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, fF.x-3f*s,fF.y+7f*s, fF.x+4f*s,fF.y, 3.6f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.8f*s, cd, 0.15f,0.35f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x-3f*s,nF.y+7f*s, 5.6f*s, cd, 0.15f,0.35f, L,H,fillL);
            P3DLimb(px,R, nF.x-3f*s,nF.y+7f*s, nF.x+5f*s,nF.y, 4.2f*s, cd, 0.15f,0.35f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(nF.x-3f*s,nF.y+2f*s), new Vector2(nF.x+10f*s,nF.y+2f*s),
                                  new Vector2(nF.x+8f*s,nF.y-3f*s), new Vector2(nF.x-3f*s,nF.y-3f*s) },
                     ArtGold, ArtGoldDk, 1.4f);

            P3DPlate(px,R, new[]{ V(54,86), V(73,88), V(76,68), V(68,52), V(57,52), V(51,68) },
                     ArtCloth, ArtClothDk, 2.2f);
            P3DChitin(px,R, new[]{ V(62,88), V(78,86), V(80,70), V(64,68) }, p, 3, 2.1f);
            P3DChitin(px,R, new[]{ V(60,68), V(76,70), V(74,56), V(60,55) }, p, 9, 1.9f);
            P3DSac(px,R, head.x-2f*s, head.y-9f*s, 8f*s, 6f*s, ArtRune, Atk(q) ? 1f : 0f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-10f*s,head.y-5f*s), new Vector2(head.x+11f*s,head.y-3f*s),
                                  new Vector2(head.x+9f*s,head.y+8f*s), new Vector2(head.x-8f*s,head.y+7f*s) },
                     ch, c, 2.1f);
            DrawLineR(px,R, head.x-6f*s, head.y+2f*s, head.x+8f*s, head.y+3f*s, 1.6f*s, ArtGoldDk);
            for (int t = 0; t < 3; t++)
                FillCircleR(px,R, head.x+(2f+t*3f)*s, head.y+5f*s, 1.4f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));

            // overgrown near arm — the read
            float sw = q == 4 ? 42f : (q == 3 ? -40f : -8f);
            P3DLimb(px,R, shN.x-1f*s,shN.y, nE.x+2f*s,nE.y-4f*s, 8.5f*s, c, 0.2f,0.4f, L,H,fillL);
            P3DLimb(px,R, nE.x+2f*s,nE.y-4f*s, nH.x+4f*s,nH.y-6f*s, 7.2f*s, ch, 0.2f,0.4f, L,H,fillL);
            P3DChitin(px,R, new[]{ new Vector2(shN.x-11f*s,shN.y+8f*s), new Vector2(shN.x+9f*s,shN.y+10f*s),
                                   new Vector2(shN.x+8f*s,shN.y-6f*s), new Vector2(shN.x-9f*s,shN.y-7f*s) },
                      p, 5, 2.2f);
            BioBone(px,R, new Vector2(nH.x+4f*s, nH.y-6f*s), sw, 22f, s);
            if (q == 4) P3DEllipseGlow(px,R, nH.x+26f*s, nH.y-2f*s, 15f*s, 13f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.4f));
        }

        // ---- STINGER — Biopunk sniper -------------------------------------------
        static void P3DBuildStinger(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            Vector2 shN = V(64,74), nH = V(J(10), J(11)-10);
            Vector2 head = V(q == 5 ? 62 : 69, 86);
            float pump = q == 3 ? 1.4f : (q == 4 ? 0.4f : 1f);

            BioSegLeg(px,R, 58f,52f, new[]{ new Vector2(46,40), new Vector2(40,20) }, 3.4f, cf, s, L,H,fillL);
            BioSegLeg(px,R, 60f,52f, new[]{ new Vector2(52,36), new Vector2(48,19) }, 3.8f, cf, s, L,H,fillL);
            BioSegLeg(px,R, 66f,52f, new[]{ new Vector2(76,38), new Vector2(82,20) }, 4.0f, cd, s, L,H,fillL);
            BioSegLeg(px,R, 68f,52f, new[]{ new Vector2(80,44), new Vector2(88,22) }, 3.6f, cd, s, L,H,fillL);

            P3DSac(px,R, 52f*s, 56f*s, 15f*pump*s, 12f*pump*s, ArtRune, Atk(q) ? 1f : 0f);
            P3DChitin(px,R, new[]{ V(44,62), V(62,64), V(64,50), V(46,48) }, p, 2, 1.9f);
            P3DPlate(px,R, new[]{ V(58,72), V(76,74), V(80,58), V(62,54) }, ch, c, 2.2f);
            DrawLineR(px,R, 62f*s, 66f*s, 78f*s, 68f*s, 1.5f*s, ArtGoldDk);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-7f*s), new Vector2(head.x+9f*s,head.y-5f*s),
                                  new Vector2(head.x+8f*s,head.y+6f*s), new Vector2(head.x-8f*s,head.y+5f*s) },
                     c, cd, 2f);
            for (int e = 0; e < 3; e++)
            for (int f = 0; f < 2; f++)
                FillCircleR(px,R, head.x+(-4f+e*4.4f)*s, head.y+(-2f+f*4f)*s, 1.8f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f - f*0.2f));
            P3DLimb(px,R, shN.x,shN.y, nH.x+6f*s,nH.y+2f*s, 3.4f*s, c, 0.2f,0.4f, L,H,fillL);

            float ext = q == 4 ? 30f : (q == 3 ? -6f : 0f);
            float px0 = head.x/s + 8f, py0 = head.y/s - 1f;
            for (int i = 0; i < 5; i++)
                P3DLimb(px,R, (px0 + i*(9f+ext*0.18f))*s, py0*s,
                              (px0 + (i+1)*(9f+ext*0.18f))*s, py0*s,
                        (2.8f - i*0.32f)*s, i % 2 == 0 ? ArtGoldHi : ArtGold, 0.2f,0.4f, L,H,fillL);
            if (q == 4)
            {
                DrawLineR(px,R, (px0+52f)*s, py0*s, (px0+70f)*s, py0*s, 1.8f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
                P3DEllipseGlow(px,R, (px0+66f)*s, py0*s, 10f*s, 5f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
            }
        }

        // ---- CARAPACE — Biopunk shield-bot --------------------------------------
        static void P3DBuildCarapace(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            Vector2 hip = V(60,50), shN = V(62,76);
            Vector2 nK = V(J(0)-3,J(1)-2), nF = V(J(2)-3,J(3));
            Vector2 fK = V(J(4)-3,J(5)-2), fF = V(J(6)-3,J(7));
            Vector2 nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 55 : 60, 88);

            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 7.4f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 6.4f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 8.6f*s, cd, 0.15f,0.35f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 7.4f*s, cd, 0.15f,0.35f, L,H,fillL);
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = i == 0 ? nF : fF;
                P3DPlate(px,R, new[]{ new Vector2(f.x-8f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+7f*s,f.y-4f*s), new Vector2(f.x-7f*s,f.y-4f*s) },
                         i == 0 ? ArtGold : ArtGoldDk, ArtGoldDk, 1.6f);
            }
            P3DPlate(px,R, new[]{ V(48,80), V(70,82), V(72,60), V(64,48), V(52,48), V(45,60) },
                     ArtCloth, ArtClothDk, 2.3f);
            P3DChitin(px,R, new[]{ V(47,80), V(69,82), V(70,64), V(48,62) }, p, 7, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s,head.y-6f*s), new Vector2(head.x+9f*s,head.y-4f*s),
                                  new Vector2(head.x+7f*s,head.y+5f*s), new Vector2(head.x-7f*s,head.y+4f*s) },
                     ch, c, 2f);
            for (int e = 0; e < 2; e++)
                FillCircleR(px,R, head.x+(2f+e*4f)*s, head.y, 1.6f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
            P3DLimb(px,R, shN.x,shN.y, nH.x-2f*s,nH.y-2f*s, 5.0f*s, c, 0.2f,0.4f, L,H,fillL);
            float sp = q == 4 ? 16f : (q == 3 ? -8f : 2f);
            BioBone(px,R, new Vector2(nH.x-2f*s, nH.y-2f*s), sp + 40f, 13f, s);
            BioShelf(px,R, 64f + (q == 4 ? 12f : (q == 3 ? 2f : 7f)), 50f, p, 1.0f, 1f, s);
        }

        // ---- CRAWLER — Biopunk mech ---------------------------------------------
        static void P3DBuildCrawler(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float t   = q == 1 ? 1f : (q == 2 ? -1f : 0f);
            float rec = q == 4 ? -6f : (q == 3 ? 3f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            for (int i = 0; i < 3; i++)
            {
                float bx = 48f + i * 13f;
                BioSegLeg(px,R, bx,44f, new[]{ new Vector2(bx-11f+t*5f,30f), new Vector2(bx-16f+t*7f,18f) },
                          3.2f, cf, s, L,H,fillL);
            }
            for (int j = 0; j < 3; j++)
            {
                float bx = 54f + j * 13f;
                BioSegLeg(px,R, bx,42f, new[]{ new Vector2(bx+12f-t*5f,30f), new Vector2(bx+17f-t*7f,18f) },
                          3.8f, cd, s, L,H,fillL);
            }
            P3DChitin(px,R, new[]{ V(42,44), V(86,46), V(90,64), V(80,74), V(48,72), V(38,60) }, p, 11, 2.6f);
            P3DChitin(px,R, new[]{ V(48,70), V(80,72), V(78,58), V(50,56) }, p, 13, 2.0f);
            for (int r = 0; r < 4; r++)
                DrawLineR(px,R, (50f+r*9f)*s, 46f*s, (50f+r*9f)*s, 70f*s, 1.4f*s, ArtGoldDk);

            float sx = 76f + rec;
            P3DSac(px,R, (sx-6f)*s, 78f*s, 13f*s, 10f*s, ArtRune, q == 3 ? 1f : 0f);
            P3DLimb(px,R, (sx+2f)*s,78f*s, (sx+18f)*s,76f*s, 4.6f*s, c, 0.2f,0.4f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(sx+16,82), V(sx+28,79), V(sx+16,72) }, ch, c, 1.8f);
            DrawRingR(px,R, (sx+22f)*s, 77.5f*s, 3.2f*s, 1.4f*s, ArtGoldDk);
            if (q == 4)
                for (int e = 0; e < 4; e++)
                    P3DEllipseGlow(px,R, (sx+34f+e*11f)*s, (77f-e*3f)*s, (6f-e*0.9f)*s, (6f-e*0.9f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f - e*0.15f));
            for (int k = 0; k < 3; k++)
                FillCircleR(px,R, (84f+k*2f)*s, (66f-k*3f)*s, 1.7f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
        }

        // ---- HIVE — Biopunk titan ------------------------------------------------
        static void P3DBuildHive(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float st   = q == 1 ? 4f : (q == 2 ? -4f : 0f);
            float lean = q == 3 ? -8f : (q == 4 ? 8f : 0f);

            P3DEllipseGlow(px,R, 64f*s, 16f*s, 38f*s, 9f*s, new Color(0f,0f,0f,0.5f));
            BioSegLeg(px,R, 52f,52f, new[]{ new Vector2(40-st,34), new Vector2(34-st,18) }, 6.0f, cf, s, L,H,fillL);
            BioSegLeg(px,R, 58f,50f, new[]{ new Vector2(46+st,32), new Vector2(42+st,18) }, 6.6f, cf, s, L,H,fillL);
            BioSegLeg(px,R, 70f,50f, new[]{ new Vector2(82-st,32), new Vector2(88-st,18) }, 7.0f, cd, s, L,H,fillL);
            BioSegLeg(px,R, 76f,52f, new[]{ new Vector2(88+st,34), new Vector2(94+st,18) }, 6.4f, cd, s, L,H,fillL);

            // the brood chamber — larvae visible inside the membrane
            P3DSac(px,R, (56f+lean*0.4f)*s, 62f*s, 26f*s, 22f*s, ArtRune, Atk(q) ? 1f : 0f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 28f) * Mathf.Deg2Rad;
                float lx = 56f + lean*0.4f + Mathf.Cos(a)*11f, ly = 62f + Mathf.Sin(a)*9f;
                P3DEllipseGlow(px,R, lx*s, ly*s, 4.6f*s, 3.0f*s, new Color(0.85f,0.95f,0.70f,0.60f));
            }
            for (int b = 0; b < 3; b++)
                P3DChitin(px,R, new[]{ V(38+b*2,74+b*4), V(76-b*2,76+b*4),
                                       V(74-b*2,66+b*4), V(40+b*2,64+b*4) }, p, 20+b, 1.9f);
            P3DPlate(px,R, new[]{ V(72+lean,86), V(92+lean,84), V(94+lean,66), V(74+lean,64) }, ch, c, 2.4f);
            float open = q == 3 ? -14f : (q == 4 ? 18f : 0f);
            P3DLimb(px,R, (88f+lean)*s,80f*s, (102f+lean+open*0.4f)*s,(74f+open*0.3f)*s, 6.0f*s, c, 0.2f,0.4f, L,H,fillL);
            P3DLimb(px,R, (88f+lean)*s,72f*s, (100f+lean+open*0.4f)*s,(62f-open*0.3f)*s, 5.4f*s, cd, 0.2f,0.4f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(100+lean+open*0.4f,77+open*0.3f), V(116+lean+open,72+open*0.5f),
                                  V(100+lean+open*0.4f,69+open*0.3f) }, ArtGoldHi, ArtGold, 1.6f);
            P3DPlate(px,R, new[]{ V(98+lean+open*0.4f,65-open*0.3f), V(113+lean+open,58-open*0.5f),
                                  V(98+lean+open*0.4f,58-open*0.3f) }, ArtGoldHi, ArtGold, 1.6f);
            FillCircleR(px,R, (92f+lean)*s, 90f*s, 7.4f*s, ch);
            for (int e = 0; e < 4; e++)
                FillCircleR(px,R, (90f+lean+(e%2)*4f)*s, (92f-(e/2)*4f)*s, 1.8f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
            if (q == 4) P3DEllipseGlow(px,R, (118f+lean)*s, 70f*s, 18f*s, 16f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
        }

        // ---- SWARM — Biopunk interceptor ----------------------------------------
        static void P3DBuildSwarm(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cf = ArtSteelFar(p);
            float b1 = q == 1 ? 14f : (q == 2 ? -12f : (Atk(q) ? 18f : 0f));
            float b2 = -b1 * 0.8f;

            void Wing(float ax, float ay, float dir, float len, Color tone, float al)
            {
                float d = dir * Mathf.Deg2Rad;
                float tx = ax + Mathf.Cos(d)*len, ty = ay + Mathf.Sin(d)*len;
                float pvx = -Mathf.Sin(d)*len*0.26f, pvy = Mathf.Cos(d)*len*0.26f;
                P3DPlate(px,R, new[]{ V(ax,ay), V(tx+pvx*0.5f, ty+pvy*0.5f), V(tx-pvx*0.4f, ty-pvy*0.4f) },
                         new Color(tone.r, tone.g, tone.b, al),
                         new Color(ArtRune.r, ArtRune.g, ArtRune.b, al*0.45f), 1.3f);
                for (int v = 1; v < 4; v++)
                    DrawLineR(px,R, ax*s, ay*s,
                              (ax+(tx-ax)+pvx*(v/4f-0.4f))*s, (ay+(ty-ay)+pvy*(v/4f-0.4f))*s,
                              0.8f*s, new Color(tone.r, tone.g, tone.b, 0.6f));
            }
            Wing(62,60, 168f+b2, 34f, cf, 0.5f);
            Wing(60,56, 196f+b2, 27f, cf, 0.45f);
            Wing(64,62, 172f+b1, 38f, ch, 0.62f);
            Wing(62,57, 200f+b1, 30f, ch, 0.55f);

            P3DSac(px,R, 48f*s, 54f*s, 11f*s, 8f*s, ArtRune, Atk(q) ? 1f : 0f);
            for (int i = 0; i < 3; i++)
                P3DPlate(px,R, new[]{ V(54+i*7,62), V(62+i*7,63), V(63+i*7,52), V(55+i*7,51) }, ch, c, 1.6f);
            P3DPlate(px,R, new[]{ V(72,64), V(84,66), V(86,56), V(74,54) }, ch, c, 2f);
            FillCircleR(px,R, 88f*s, 62f*s, 5.4f*s, c);
            for (int e = 0; e < 2; e++)
                FillCircleR(px,R, 90f*s, (63f-e*3f)*s, 1.8f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
            float curl = q == 4 ? -26f : (Atk(q) ? 10f : 0f);
            P3DLimb(px,R, 44f*s,50f*s, 34f*s,(44f+curl*0.4f)*s, 2.8f*s, ArtGold, 0.2f,0.4f, L,H,fillL);
            P3DLimb(px,R, 34f*s,(44f+curl*0.4f)*s, 28f*s,(36f+curl)*s, 2.0f*s, ArtGoldHi, 0.2f,0.4f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(28,39+curl), V(21,32+curl), V(30,34+curl) }, ArtGoldHi, ArtGold, 1.1f);
            if (q == 4) P3DEllipseGlow(px,R, 22f*s, (34f+curl)*s, 8f*s, 8f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f));
        }

        // ---- MYCELIUM — Biopunk hacker ------------------------------------------
        static void P3DBuildMycelium(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), cd = ArtSteelDk(p), cf = ArtSteelFar(p);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nH = V(J(10),J(11));

            P3DLimb(px,R, 62f*s,50f*s, fK.x,fK.y, 5.0f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 4.2f*s, cf, 0.1f,0.3f, L,H,fillL);
            P3DLimb(px,R, 64f*s,50f*s, nK.x,nK.y, 5.8f*s, cd, 0.15f,0.35f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.0f*s, cd, 0.15f,0.35f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                for (int r = 0; r < 3; r++)
                    DrawLineR(px,R, f.x, f.y+2f*s, f.x+(-6f+r*6f)*s, f.y-5f*s, 1.8f*s, ArtGoldDk);

            for (int i = 0; i < 5; i++)
            {
                float off = (i - 2) * 3.4f;
                DrawLineR(px,R, (63f+off*0.4f+sway*0.3f)*s, 50f*s, (64f+off+sway)*s, 88f*s,
                          (2.6f - Mathf.Abs(i-2)*0.4f)*s, i % 2 == 0 ? c : cd);
            }
            P3DPlate(px,R, new[]{ V(56+sway,88), V(72+sway,88), V(70+sway,60), V(58+sway,60) },
                     ArtCloth, ArtClothDk, 2.1f);
            P3DSac(px,R, (54f+sway)*s, 70f*s, 7f*s, 6f*s, ArtRune, Atk(q) ? 1f : 0f);
            P3DSac(px,R, (74f+sway)*s, 78f*s, 6f*s, 5f*s, ArtRune, Atk(q) ? 1f : 0f);
            BioCap(px,R, 64f+sway, 92f, p, 1.0f, s);
            P3DLimb(px,R, (60f+sway)*s,84f*s, nH.x-4f*s,nH.y+2f*s, 4.0f*s, c, 0.2f,0.4f, L,H,fillL);
            float reach = q == 4 ? 24f : (q == 3 ? -8f : 4f);
            BioFilaments(px,R, nH.x/s + reach, nH.y/s + 4f, 4, 16f, 4f, ArtCloth, s);
            if (Atk(q))
            {
                // this caster attacks along the FLOOR -- no other caster in the game does
                for (int t = 0; t < 3; t++)
                {
                    float len = q == 4 ? (26f + t*10f) : (10f + t*5f);
                    DrawLineR(px,R, (70f+sway)*s, (20f+t*3f)*s, (70f+sway+len)*s, (17f+t*4f)*s,
                              2.0f*s, ArtClothDk);
                    FillCircleR(px,R, (70f+sway+len)*s, (17f+t*4f)*s, 2.0f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.8f));
                }
                P3DEllipseGlow(px,R, (64f+sway)*s, 26f*s, 30f*s, 12f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, q == 4 ? 0.4f : 0.25f));
            }
        }

        // ---- SPORE — Biopunk drone ----------------------------------------------
        static void P3DBuildSpore(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p);
            float dv = q == 4 ? -7f : (q == 1 ? 3f : (q == 2 ? -3f : 0f));
            float puff = Atk(q) ? 1f : 0f;

            P3DSac(px,R, 64f*s, (74f+dv)*s, 17f*s, 15f*s, ArtRune, puff);
            P3DPlate(px,R, new[]{ V(50,80+dv), V(64,90+dv), V(78,80+dv), V(74,74+dv), V(54,74+dv) },
                     ArtSteelHi(p), c, 2.0f);
            for (int i = 0; i < 4; i++)
                DrawLineR(px,R, (56f+i*5f)*s, (80f+dv)*s, (58f+i*5f)*s, (88f+dv)*s, 1.3f*s, ArtGoldDk);
            float gw = 15f + puff * 3f;
            P3DPlate(px,R, new[]{ V(64-gw,62+dv), V(64+gw,62+dv), V(64+gw*0.6f,54+dv), V(64-gw*0.6f,54+dv) },
                     ArtCloth, ArtClothDk, 1.8f);
            for (int g = 0; g < 6; g++)
                DrawLineR(px,R, (64f-11f+g*4.4f)*s, (62f+dv)*s, (64f-8f+g*3.2f)*s, (55f+dv)*s, 1.2f*s, ArtGoldDk);
            BioFilaments(px,R, 64f, 52f+dv, 5, 16f, 3f, ArtClothDk, s);
            if (q == 4)
                for (int e = 0; e < 5; e++)
                {
                    float t = e / 4f;
                    P3DEllipseGlow(px,R, (64f+8f+t*30f)*s, (58f+dv-t*t*16f)*s,
                        (6f-t*3.4f)*s, (6f-t*3.4f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.65f - t*0.14f));
                }
            else P3DEllipseGlow(px,R, 64f*s, (66f+dv)*s, 20f*s, 12f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.22f));
        }

        // ---- POD — Biopunk turret ------------------------------------------------
        static void P3DBuildPod(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color c = ArtSteelBase(p), ch = ArtSteelHi(p), cd = ArtSteelDk(p);
            float swell = q == 3 ? 1.35f : (q == 4 ? 0.55f : 1f);

            P3DEllipseGlow(px,R, 64f*s, 15f*s, 34f*s, 7f*s, new Color(0f,0f,0f,0.45f));
            // root ball spreading wider than the head -- reads instantly as rooted
            for (int i = 0; i < 7; i++)
            {
                float a = (-14f + i * 28f) * Mathf.Deg2Rad;
                float len = 16f + ((i * 7) % 5) * 3f;
                float ex = 64f + Mathf.Cos(a + Mathf.PI) * len;
                float ey = 20f + Mathf.Abs(Mathf.Sin(a)) * 4f;
                DrawLineR(px,R, 64f*s, 26f*s, ex*s, ey*s, (3.4f - Mathf.Abs(i-3)*0.4f)*s, cd);
                DrawLineR(px,R, ex*s, ey*s, (ex+(ex-64f)*0.3f)*s, (ey-3f)*s, 1.8f*s, ArtGoldDk);
            }
            P3DPlate(px,R, new[]{ V(50,20), V(78,20), V(74,32), V(54,32) }, ch, c, 2.2f);
            P3DLimb(px,R, 64f*s,30f*s, 66f*s,58f*s, 6.4f*s, c, 0.15f,0.35f, L,H,fillL);
            for (int f = 0; f < 3; f++)
                DrawLineR(px,R, (61f+f*3f)*s, 32f*s, (63f+f*3f)*s, 56f*s, 1.2f*s, ArtGoldDk);

            float split = q == 4 ? 9f : (q == 3 ? -2f : 0f);
            P3DSac(px,R, 66f*s, 72f*s, 15f*swell*s, 17f*swell*s, ArtRune, q == 3 ? 1f : 0f);
            P3DPlate(px,R, new[]{ V(52,72), V(50-split,86), V(62,90), V(64,74) }, ch, c, 2.0f);
            P3DPlate(px,R, new[]{ V(80,72), V(82+split,86), V(70,90), V(68,74) }, ch, c, 2.0f);
            if (q == 4)
                for (int e = 0; e < 3; e++)
                {
                    P3DPlate(px,R, new[]{ V(84+e*13,74+e*2), V(96+e*13,71+e*2), V(84+e*13,68+e*2) },
                             ArtGoldHi, ArtGold, 1.2f);
                    P3DEllipseGlow(px,R, (90f+e*13f)*s, (71f+e*2f)*s, (7f-e*1.6f)*s, (6f-e*1.4f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f - e*0.15f));
                }
        }

        // ══ SOLAR FORGE ══════════════════════════════════════════════════════════
        // Theme 6 runs its own geometry, NOT the Medieval builders repainted. The design
        // premise: Medieval is hand-forged for war (beaten plate, leather, heraldry, timber,
        // carved stone); Solar is cast and fired (brass automata, molten cores, lenses and
        // mirrors, censers, radiate crowns). Every piece below is a deliberate divergence:
        // banded lorica not a one-piece cuirass, open seven-ray crown not a closed great-helm,
        // strap skirt not a cape, circular sun disc not a kite shield, crescent glaive not a
        // straight sword. Colours come from _artSets[6], so ArtGold reads as brass here.

        // ---- shared Solar pieces ------------------------------------------------

        /// <summary>Radiate crown: open face, seven sun rays. Replaces the great-helm.</summary>
        static void SolCrown(Color[] px, int R, float x, float y, P3DP p, float s)
        {
            P3DPlate(px, R, new[]{ new Vector2((x-9)*s,(y-10)*s), new Vector2((x+9)*s,(y-10)*s),
                                   new Vector2((x+10)*s,(y+5)*s), new Vector2(x*s,(y+10)*s),
                                   new Vector2((x-10)*s,(y+5)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 2.2f);
            P3DPlate(px, R, new[]{ new Vector2((x-7)*s,(y-2)*s), new Vector2((x+8)*s,(y-2)*s),
                                   new Vector2((x+8)*s,(y+1.6f)*s), new Vector2((x-7)*s,(y+1.6f)*s) },
                     new Color(0.06f,0.04f,0.03f), new Color(0.02f,0.01f,0.01f), 1f);
            P3DEllipseGlow(px, R, (x+3)*s, y*s, 3.2f*s, 2.2f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.9f));
            for (int i = 0; i < 7; i++)
            {
                float a = (-64f + i * 32f) * Mathf.Deg2Rad;
                float bx = x + Mathf.Sin(a) * 8f,  by = y + 7f + Mathf.Cos(a) * 3f;
                float tx = x + Mathf.Sin(a) * 17f, ty = y + 9f + Mathf.Cos(a) * 13f;
                P3DPlate(px, R, new[]{ new Vector2((bx-2.2f)*s, by*s), new Vector2((bx+2.2f)*s, by*s),
                                       new Vector2(tx*s, ty*s) }, ArtGoldHi, ArtGold, 1.1f);
            }
            DrawLineR(px, R, (x-9)*s, (y+5)*s, (x+9)*s, (y+5)*s, 1.8f*s, ArtGold);
        }

        /// <summary>Banded lorica: three overlapping cast bands + molten seam. Not a cuirass.</summary>
        static void SolLorica(Color[] px, int R, float x, float y, P3DP p, float s)
        {
            for (int i = 0; i < 3; i++)
            {
                float yy = y + 12f - i * 10f, w = 12f + i * 0.8f;
                P3DPlate(px, R, new[]{ new Vector2((x-w)*s, yy*s), new Vector2((x+w)*s, yy*s),
                                       new Vector2((x+w-1)*s,(yy-9)*s), new Vector2((x-w+1)*s,(yy-9)*s) },
                         ArtSteelHi(p), ArtSteelBase(p), 1.9f);
                DrawLineR(px, R, (x-w+2)*s, (yy-8.4f)*s, (x+w-2)*s, (yy-8.4f)*s, 1.2f*s, ArtSteelDk(p));
            }
            DrawLineR(px, R, (x+1)*s, (y+11)*s, (x+1)*s, (y-16)*s, 1.8f*s,
                      new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.8f));
            FillCircleR(px, R, (x+1)*s, (y-2)*s, 3.4f*s, ArtGold);
            DrawRingR  (px, R, (x+1)*s, (y-2)*s, 5.4f*s, 1.4f*s, ArtGoldHi);
        }

        /// <summary>Pteruges: hanging brass-tipped straps. Replaces the cape.</summary>
        static void SolPteruges(Color[] px, int R, float x, float y, float sway, float s)
        {
            for (int i = 0; i < 5; i++)
            {
                float sx = x - 12f + i * 6f;
                float dx = sx + sway * (0.3f + i * 0.12f);
                P3DPlate(px, R, new[]{ new Vector2((sx-2.6f)*s, y*s), new Vector2((sx+2.6f)*s, y*s),
                                       new Vector2((dx+2.2f)*s,(y-15)*s), new Vector2((dx-2.2f)*s,(y-15)*s) },
                         ArtCloth, ArtClothDk, 1.4f);
                FillCircleR(px, R, dx*s, (y-15.5f)*s, 1.5f*s, ArtGold);
            }
            DrawLineR(px, R, (x-14)*s, (y+0.5f)*s, (x+14)*s, (y+0.5f)*s, 2.6f*s, ArtGold);
        }

        /// <summary>Sun disc: concentric rings + 12 spokes. Circular, unlike every other shield.</summary>
        static void SolSunDisc(Color[] px, int R, float x, float y, P3DP p, float sc, float s)
        {
            FillCircleR(px, R, x*s, y*s, 19f*sc*s, ArtSteelBase(p));
            DrawRingR  (px, R, x*s, y*s, 19f*sc*s, 2.4f*sc*s, ArtOutline);
            DrawRingR  (px, R, x*s, y*s, 15.5f*sc*s, 2.0f*sc*s, ArtGold);
            DrawRingR  (px, R, x*s, y*s, 10.5f*sc*s, 1.6f*sc*s, ArtGoldDk);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                DrawLineR(px, R, (x + Mathf.Cos(a)*10.5f*sc)*s, (y + Mathf.Sin(a)*10.5f*sc)*s,
                                 (x + Mathf.Cos(a)*15f*sc)*s,   (y + Mathf.Sin(a)*15f*sc)*s,
                          1.5f*sc*s, ArtGoldHi);
            }
            FillCircleR(px, R, x*s, y*s, 5.4f*sc*s, ArtGoldHi);
            P3DEllipseGlow(px, R, x*s, y*s, 9f*sc*s, 9f*sc*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
        }

        /// <summary>Solar glaive: haft, counterweight, crescent lens blade. Replaces the sword.</summary>
        static void SolGlaive(Color[] px, int R, Vector2 grip, float deg, P3DP p, float s)
        {
            Vector2 d = RotP(new Vector2(0f,1f), Vector2.zero, deg);
            Vector2 tip = new Vector2(grip.x + d.x*44f*s, grip.y + d.y*44f*s);
            Vector2 btm = new Vector2(grip.x - d.x*20f*s, grip.y - d.y*20f*s);
            DrawLineR(px, R, btm.x, btm.y, tip.x, tip.y, 3.2f*s, ArtLeather);
            DrawLineR(px, R, btm.x, btm.y, tip.x, tip.y, 1.3f*s,
                      new Color(ArtLeather.r*1.7f, ArtLeather.g*1.6f, ArtLeather.b*1.5f));
            P3DPlate(px, R, new[]{ new Vector2(btm.x-4f*s, btm.y-4f*s), new Vector2(btm.x+4f*s, btm.y-4f*s),
                                   new Vector2(btm.x+3f*s, btm.y+4f*s), new Vector2(btm.x-3f*s, btm.y+4f*s) },
                     ArtGoldHi, ArtGold, 1.4f);
            Vector2 pp = new Vector2(-d.y, d.x);
            Vector2 c1 = new Vector2(tip.x + pp.x*3f*s, tip.y + pp.y*3f*s);
            Vector2 c2 = new Vector2(tip.x + d.x*15f*s + pp.x*11f*s, tip.y + d.y*15f*s + pp.y*11f*s);
            Vector2 c3 = new Vector2(tip.x + d.x*20f*s + pp.x*3f*s,  tip.y + d.y*20f*s + pp.y*3f*s);
            Vector2 c4 = new Vector2(tip.x + d.x*11f*s - pp.x*2f*s,  tip.y + d.y*11f*s - pp.y*2f*s);
            P3DPlate(px, R, new[]{ c1, c2, c3, c4 }, ArtGoldHi, ArtSteelBase(p), 1.8f);
            DrawLineR(px, R, c1.x, c1.y, c3.x, c3.y, 1.2f*s, new Color(1f,1f,1f,0.45f));
            FillCircleR   (px, R, tip.x + d.x*4f*s, tip.y + d.y*4f*s, 3.6f*s, ArtRune);
            P3DEllipseGlow(px, R, tip.x + d.x*4f*s, tip.y + d.y*4f*s, 8f*s, 8f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
        }

        /// <summary>Censer bowl on chains. Used by the pyromancer flail.</summary>
        static void SolCenser(Color[] px, int R, float x, float y, P3DP p, float swing, float s)
        {
            for (int i = -1; i <= 1; i++)
                DrawLineR(px, R, (x+i*6)*s, (y+16)*s, (x+swing+i*3)*s, (y+3)*s, 1.2f*s, ArtGold);
            P3DPlate(px, R, new[]{ new Vector2((x+swing-9)*s,(y+2)*s), new Vector2((x+swing+9)*s,(y+2)*s),
                                   new Vector2((x+swing+7)*s,(y-9)*s), new Vector2((x+swing-7)*s,(y-9)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 2f);
            P3DPlate(px, R, new[]{ new Vector2((x+swing-8)*s,(y+2)*s), new Vector2((x+swing+8)*s,(y+2)*s),
                                   new Vector2((x+swing+5)*s,(y+9)*s), new Vector2((x+swing-5)*s,(y+9)*s) },
                     ArtGoldHi, ArtGold, 1.8f);
            for (int k = 0; k < 3; k++)
                FillCircleR(px, R, (x+swing-4+k*4)*s, (y-3)*s, 1.5f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.95f));
            P3DEllipseGlow(px, R, (x+swing)*s, (y-3)*s, 12f*s, 10f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
            FillCircleR(px, R, (x+swing)*s, (y+11)*s, 2.2f*s, ArtGoldHi);
        }

        /// <summary>Focusing tube: stacked lens rings + aperture iris. Replaces the longbow.</summary>
        static void SolLensTube(Color[] px, int R, float x, float y, float len, P3DP p, bool open, float s)
        {
            DrawLineR(px, R, x*s, y*s, (x+len)*s, y*s, 5.2f*s, ArtSteelBase(p));
            DrawLineR(px, R, x*s, y*s, (x+len)*s, y*s, 2.0f*s, ArtSteelHi(p));
            for (int i = 0; i < 3; i++)
            {
                float rx = x + 9f + i * (len - 14f) / 3f;
                P3DPlate(px, R, new[]{ new Vector2((rx-1.6f)*s,(y-6)*s), new Vector2((rx+1.6f)*s,(y-6)*s),
                                       new Vector2((rx+1.6f)*s,(y+6)*s), new Vector2((rx-1.6f)*s,(y+6)*s) },
                         ArtGoldHi, ArtGold, 1.2f);
            }
            float ap = open ? 4.6f : 2.4f;
            FillCircleR(px, R, (x+len)*s, y*s, ap*s, ArtRune);
            DrawRingR  (px, R, (x+len)*s, y*s, (ap+1.6f)*s, 1.4f*s, ArtGold);
            if (open)
                P3DEllipseGlow(px, R, (x+len+7)*s, y*s, 11f*s, 7f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f));
        }

        /// <summary>Accordion bellows + exhaust stack. Forgewalker back unit.</summary>
        static void SolBellows(Color[] px, int R, float x, float y, P3DP p, float squash, float s)
        {
            for (int i = 0; i < 4; i++)
            {
                float h = 4.4f - squash * 0.5f;
                float yy = y + i * h * 1.7f;
                P3DPlate(px, R, new[]{ new Vector2((x-10)*s, yy*s), new Vector2((x+10)*s, yy*s),
                                       new Vector2((x+8)*s,(yy+h)*s), new Vector2((x-8)*s,(yy+h)*s) },
                         ArtLeather, new Color(ArtLeather.r*0.5f, ArtLeather.g*0.5f, ArtLeather.b*0.5f), 1.3f);
            }
            P3DPlate(px, R, new[]{ new Vector2((x-11)*s,(y-3)*s), new Vector2((x+11)*s,(y-3)*s),
                                   new Vector2((x+10)*s,(y+1)*s), new Vector2((x-10)*s,(y+1)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 1.6f);
            DrawLineR(px, R, (x+13)*s, (y+18)*s, (x+13)*s, (y+30)*s, 3.4f*s, ArtSteelDk(p));
            P3DEllipseGlow(px, R, (x+13)*s, (y+31)*s, 6f*s, 5f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
        }

        /// <summary>Barred face cage + high collar. Replaces the wizard hood.</summary>
        static void SolCage(Color[] px, int R, float x, float y, P3DP p, float s)
        {
            P3DPlate(px, R, new[]{ new Vector2((x-13)*s,(y-9)*s), new Vector2((x+13)*s,(y-9)*s),
                                   new Vector2((x+11)*s,(y-17)*s), new Vector2((x-11)*s,(y-17)*s) },
                     ArtCloth, ArtClothDk, 1.8f);
            P3DPlate(px, R, new[]{ new Vector2((x-9)*s,(y-9)*s), new Vector2((x+10)*s,(y-9)*s),
                                   new Vector2((x+10)*s,(y+8)*s), new Vector2((x-2)*s,(y+13)*s),
                                   new Vector2((x-9)*s,(y+7)*s) },
                     ArtSteelBase(p), ArtSteelDk(p), 2.1f);
            for (int i = 0; i < 4; i++)
                DrawLineR(px, R, (x-6+i*4.4f)*s, (y-8)*s, (x-6+i*4.4f)*s, (y+7)*s, 1.5f*s, ArtGoldDk);
            DrawLineR(px, R, (x-9)*s, (y-1)*s, (x+10)*s, (y-1)*s, 1.4f*s, ArtGold);
            P3DEllipseGlow(px, R, (x+3)*s, (y+1)*s, 4.6f*s, 3.4f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.75f));
        }

        /// <summary>One row of brass feather plates. Phoenix wings.</summary>
        static void SolFeatherRow(Color[] px, int R, float ax, float ay, float dir, float len,
            int cnt, Color tone, float s)
        {
            for (int i = 0; i < cnt; i++)
            {
                float t = i / (float)(cnt - 1);
                float a = (dir + (t - 0.5f) * 46f) * Mathf.Deg2Rad;
                float L = len * (0.65f + 0.35f * Mathf.Sin(Mathf.PI * t));
                float tx = ax + Mathf.Cos(a) * L, ty = ay + Mathf.Sin(a) * L;
                float pvx = -Mathf.Sin(a) * 2.8f, pvy = Mathf.Cos(a) * 2.8f;
                P3DPlate(px, R, new[]{
                    new Vector2((ax+pvx)*s,(ay+pvy)*s),
                    new Vector2((tx+pvx*0.5f)*s,(ty+pvy*0.5f)*s),
                    new Vector2((tx-pvx*0.5f)*s,(ty-pvy*0.5f)*s),
                    new Vector2((ax-pvx)*s,(ay-pvy)*s)
                }, tone, new Color(tone.r*0.55f, tone.g*0.55f, tone.b*0.55f), 1.2f);
                DrawLineR(px, R, ax*s, ay*s, tx*s, ty*s, 0.9f*s, ArtGold);
            }
        }

        /// <summary>Cast bronze hammer head. Replaces the golem's boulder fist.</summary>
        static void SolHammer(Color[] px, int R, float x, float y, P3DP p, float s)
        {
            P3DPlate(px,R, new[]{ new Vector2((x-10)*s,(y-9)*s), new Vector2((x+11)*s,(y-11)*s),
                                  new Vector2((x+12)*s,(y+9)*s), new Vector2((x-9)*s,(y+11)*s) },
                     ArtSteelHi(p), ArtSteelBase(p), 2.2f);
            DrawLineR(px,R, (x-6)*s,(y-7)*s, (x-6)*s,(y+8)*s, 1.6f*s, ArtSteelDk(p));
            DrawLineR(px,R, (x+6)*s,(y-8)*s, (x+6)*s,(y+8)*s, 1.6f*s, ArtSteelDk(p));
            FillCircleR(px,R, (x+2)*s, y*s, 3.2f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
        }

        /// <summary>
        /// Swept impact arc. A soft round glow at the fist read as a blob -- an impact needs
        /// direction. This draws nested crescents along the swing path plus radial shards at
        /// the contact point, so the eye gets the arc the hammer travelled.
        /// </summary>
        static void SolImpactArc(Color[] px, int R, float cx, float cy, float radius,
            float fromDeg, float toDeg, Color col, float s)
        {
            for (int band = 0; band < 3; band++)
            {
                float rr = radius - band * 4.5f;
                float a  = 0.55f - band * 0.15f;
                Vector2 prev = Vector2.zero; bool has = false;
                for (int k = 0; k <= 8; k++)
                {
                    float t  = k / 8f;
                    float ag = Mathf.Lerp(fromDeg, toDeg, t) * Mathf.Deg2Rad;
                    var pt = new Vector2((cx + Mathf.Cos(ag)*rr)*s, (cy + Mathf.Sin(ag)*rr)*s);
                    if (has) DrawLineR(px,R, prev.x, prev.y, pt.x, pt.y,
                                       (3.4f - band*0.8f)*s, new Color(col.r, col.g, col.b, a));
                    prev = pt; has = true;
                }
            }
            // contact shards, radiating from the end of the sweep
            float end = toDeg * Mathf.Deg2Rad;
            float ex = cx + Mathf.Cos(end)*radius, ey = cy + Mathf.Sin(end)*radius;
            for (int i = 0; i < 5; i++)
            {
                float sa = (toDeg - 52f + i * 26f) * Mathf.Deg2Rad;
                float len = 9f + (i % 2) * 6f;
                P3DPlate(px,R, new[]{
                    new Vector2((ex + Mathf.Cos(sa+0.16f)*3f)*s, (ey + Mathf.Sin(sa+0.16f)*3f)*s),
                    new Vector2((ex + Mathf.Cos(sa)*len)*s,      (ey + Mathf.Sin(sa)*len)*s),
                    new Vector2((ex + Mathf.Cos(sa-0.16f)*3f)*s, (ey + Mathf.Sin(sa-0.16f)*3f)*s)
                }, new Color(col.r, col.g, col.b, 0.9f), new Color(col.r*0.6f, col.g*0.4f, col.b*0.2f, 0.5f), 0f);
            }
            P3DEllipseGlow(px,R, ex*s, ey*s, 13f*s, 12f*s, new Color(col.r, col.g, col.b, 0.45f));
        }

        /// <summary>
        /// Focused light lance. A flat bar plus one glow read as a blob; a beam needs a hot
        /// core, a tapered envelope, a muzzle flare and shock rings that space out along it.
        /// </summary>
        static void SolBeam(Color[] px, int R, float x0, float y, float x1, Color col, float s)
        {
            float len = x1 - x0;
            // tapered envelope
            P3DPlate(px,R, new[]{ new Vector2(x0*s,(y-5f)*s), new Vector2(x1*s,(y-2.6f)*s),
                                  new Vector2(x1*s,(y+2.6f)*s), new Vector2(x0*s,(y+5f)*s) },
                     new Color(col.r, col.g, col.b, 0.42f),
                     new Color(col.r, col.g, col.b, 0.16f), 0f);
            DrawLineR(px,R, x0*s, y*s, x1*s, y*s, 2.6f*s, new Color(col.r, col.g, col.b, 0.85f));
            DrawLineR(px,R, x0*s, y*s, x1*s, y*s, 1.1f*s, new Color(1f, 0.97f, 0.88f, 0.95f));
            // shock rings, widening down the beam
            for (int i = 0; i < 3; i++)
            {
                float t = 0.22f + i * 0.28f;
                float rx = x0 + len * t;
                DrawRingR(px,R, rx*s, y*s, (4f + i*2.6f)*s, 1.4f*s,
                    new Color(col.r, col.g, col.b, 0.55f - i*0.13f));
            }
            // muzzle flare
            P3DEllipseGlow(px,R, x0*s, y*s, 15f*s, 11f*s, new Color(col.r, col.g, col.b, 0.75f));
            FillCircleR   (px,R, x0*s, y*s, 3.4f*s, new Color(1f, 0.96f, 0.85f));
        }

        // ---- COLOSSUS — Solar titan --------------------------------------------
        // Cast bronze automaton: smooth castings, an open ribcage over a molten core, a
        // clavicle yoke the arms actually bolt to, and a swept impact arc on the strike.
        static void P3DBuildColossus(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);

            float nKx, nKy, nFx, nFy, fKx, fKy, fFx, fFy, rock;
            // 1 = contact (spread, both planted); 2 = passing (rear leg lifted, knee raised).
            // Previously these were the same positions with near/far traded -- shading changed,
            // outline did not, so the legs never read as moving.
            if (q == 1)      { nKx= 20; nKy=26; nFx= 32; nFy= 8; fKx=-18; fKy=24; fFx=-28; fFy=8; rock= 3f; }
            else if (q == 2) { nKx= -6; nKy=32; nFx=-12; nFy=18; fKx= 16; fKy=25; fFx= 28; fFy=8; rock=-1f; }
            else if (q == 6) { nKx=-18; nKy=24; nFx=-28; nFy= 8; fKx= 20; fKy=26; fFx= 32; fFy=8; rock=-3f; }
            else if (q == 7) { nKx= 16; nKy=25; nFx= 28; nFy= 8; fKx= -6; fKy=32; fFx=-12; fFy=18; rock= 1f; }
            else if (q == 3) { nKx= 20; nKy=24; nFx= 30; nFy=8; fKx=-20; fKy=24; fFx=-30; fFy=8; rock= 0f; }
            else if (q == 4) { nKx= 22; nKy=23; nFx= 32; nFy=8; fKx=-22; fKy=23; fFx=-32; fFy=8; rock= 0f; }
            else if (q == 5) { nKx=-14; nKy=25; nFx=-22; nFy=8; fKx= 18; fKy=25; fFx= 26; fFy=8; rock= 0f; }
            else             { nKx= 12; nKy=25; nFx= 14; nFy=8; fKx=-12; fKy=25; fFx=-14; fFy=8; rock= 0f; }
            float hdx = q == 3 ? -12f : (q == 4 ? 10f : 0f);
            float hdy = q == 3 ?  -4f : (q == 4 ? -2f : 0f);
            float hx  = 64f + hdx;

            P3DEllipseGlow(px,R, 64f*s, 16f*s, 34f*s, 8f*s, new Color(0f,0f,0f,0.5f));

            P3DLimb(px,R, 58f*s,44f*s, (64+fKx)*s,fKy*s, 9.5f*s, bf, 0.2f, 0.3f, L,H,fillL);
            P3DLimb(px,R, (64+fKx)*s,fKy*s, (64+fFx)*s,fFy*s, 8f*s, bf, 0.2f, 0.3f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+fFx-10, fFy+7), V(64+fFx+10, fFy+7),
                                  V(64+fFx+8, fFy-6), V(64+fFx-8, fFy-6) },
                     bf, new Color(bf.r*0.7f, bf.g*0.7f, bf.b*0.7f), 2f);
            Vector2 fSock = V(hx-13, 92);
            float fex = hx - (q == 4 ? 18f : (q == 3 ? 4f : 11f));
            float fey = 78f + (q == 4 ? 3f : 0f);
            P3DLimb(px,R, fSock.x, fSock.y, fex*s, fey*s, 10.5f*s, bf, 0.2f, 0.3f, L,H,fillL);
            SolHammer(px, R, fex - (q == 4 ? 13f : (q == 3 ? -7f : 6f)), fey - 10f,
                      new P3DP { body = new Color(p.body.r*0.6f, p.body.g*0.6f, p.body.b*0.6f) }, s);

            P3DLimb(px,R, 70f*s,44f*s, (64+nKx)*s,nKy*s, 11f*s, b, 0.25f, 0.35f, L,H,fillL);
            P3DLimb(px,R, (64+nKx)*s,nKy*s, (64+nFx)*s,nFy*s, 9.2f*s, b, 0.25f, 0.35f, L,H,fillL);
            P3DPlate(px,R, new[]{ V(64+nFx-12, nFy+8), V(64+nFx+12, nFy+8),
                                  V(64+nFx+9, nFy-7), V(64+nFx-9, nFy-7) }, bh, b, 2.2f);

            // hip drum + waist collar (the old build had an open gap here)
            P3DPlate(px,R, new[]{ V(hx-16,34), V(hx+16,34), V(hx+18,50), V(hx-18,50) }, bh, b, 2.4f);
            P3DPlate(px,R, new[]{ V(hx-13,50), V(hx+13,50), V(hx+14,58), V(hx-14,58) }, b, bd, 2.0f);
            DrawLineR(px,R, (hx-11)*s, 54f*s, (hx+11)*s, 54f*s, 1.5f*s, ArtGoldDk);

            // ribcage with real end rings and side spars
            P3DEllipseGlow(px,R, (hx+1)*s, 74f*s, 17f*s, 20f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, q == 3 ? 0.8f : 0.6f));
            FillCircleR(px,R, (hx+1)*s, 74f*s, 7.5f*s, ArtGoldHi);
            P3DPlate(px,R, new[]{ V(hx-15,56), V(hx+15,56), V(hx+16,62), V(hx-16,62) }, bh, b, 2.1f);
            for (int i = 0; i < 4; i++)
            {
                float yy = 64f + i*6.5f, w = 15f - Mathf.Abs(i - 1.5f)*1.6f;
                DrawLineR(px,R, (hx-w)*s, yy*s, (hx+w)*s, (yy+1.2f)*s, 3.2f*s, b);
                DrawLineR(px,R, (hx-w)*s, yy*s, (hx+w)*s, (yy+1.2f)*s, 1.3f*s, bh);
            }
            P3DPlate(px,R, new[]{ V(hx-16,88), V(hx+16,88), V(hx+15,94), V(hx-15,94) }, bh, b, 2.1f);
            DrawLineR(px,R, (hx-13)*s, 58f*s, (hx-14)*s, 90f*s, 3.6f*s, bd);
            DrawLineR(px,R, (hx+14)*s, 58f*s, (hx+15)*s, 90f*s, 3.6f*s, bd);

            // clavicle yoke — the beam the arms and pauldrons bolt onto
            P3DPlate(px,R, new[]{ V(hx-22,90), V(hx+22,90), V(hx+20,100), V(hx-20,100) }, bh, b, 2.3f);
            DrawLineR(px,R, (hx-18)*s, 95f*s, (hx+18)*s, 95f*s, 1.6f*s, ArtGoldDk);
            FillCircleR(px,R, fSock.x, fSock.y, 4.4f*s, bd);
            FillCircleR(px,R, (hx+13)*s, 92f*s, 5.0f*s, bd);
            FillCircleR(px,R, (hx+13)*s, 92f*s, 2.4f*s, ArtGoldHi);

            P3DPlate(px,R, new[]{ V(hx-26,98), V(hx-8,104), V(hx-6,90), V(hx-22,86) }, bh, b, 2.2f);
            P3DPlate(px,R, new[]{ V(hx+8,104), V(hx+26,98), V(hx+22,86), V(hx+6,90) }, bh, b, 2.2f);

            float ex = hx + (q == 4 ? 26f : (q == 3 ? -14f : 9f));
            float ey = 76f + (q == 4 ? -6f : 0f);
            P3DLimb(px,R, (hx+13)*s, 92f*s, ex*s, ey*s, 12f*s, b, 0.25f, 0.35f, L,H,fillL);
            float fx = ex + (q == 4 ? 20f : (q == 3 ? -10f : 8f));
            float fy = ey + (q == 4 ? -8f : -12f);
            // The swing arc goes UNDER the fist so the hammer stays the brightest thing in it.
            if (q == 4)
                SolImpactArc(px, R, hx + 13f, 92f, 38f, 150f, -18f, ArtRune, s);
            SolHammer(px, R, fx, fy, p, s);

            P3DPlate(px,R, new[]{ V(hx-5,100), V(hx+7,100), V(hx+6,108), V(hx-4,108) }, b, bd, 1.8f);
            DrawLineR(px,R, (hx-2)*s, 101f*s, (hx-2)*s, 107f*s, 1.4f*s, ArtGoldDk);
            DrawLineR(px,R, (hx+4)*s, 101f*s, (hx+4)*s, 107f*s, 1.4f*s, ArtGoldDk);
            SolCrown(px, R, hx + 1f + rock*0.6f, 112f + hdy, p, s);
        }

        // ---- HELIOSTAT — Solar turret ------------------------------------------
        // Mirror array on a gimbal. Legs terminate inside the base plate, a solid column rises
        // from it, and a two-arm fork grips the ring at visible trunnions.
        static void P3DBuildHeliostat(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);

            float rec = q == 4 ? -4f : (q == 3 ? 2f : 0f);
            bool  chg = Atk(q);
            P3DEllipseGlow(px,R, 64f*s, 15f*s, 32f*s, 7f*s, new Color(0f,0f,0f,0.45f));

            P3DPlate(px,R, new[]{ V(43,16), V(51,16), V(61,58), V(55,58) }, b, bd, 2.1f);
            P3DPlate(px,R, new[]{ V(77,16), V(85,16), V(73,58), V(67,58) }, bh, b, 2.1f);
            P3DPlate(px,R, new[]{ V(60,16), V(68,16), V(67,58), V(61,58) }, bf,
                     new Color(bf.r*0.7f, bf.g*0.7f, bf.b*0.7f), 1.9f);
            DrawLineR(px,R, 49f*s,30f*s, 79f*s,30f*s, 3.0f*s, b);
            DrawLineR(px,R, 52f*s,42f*s, 76f*s,42f*s, 2.4f*s, bd);
            foreach (float fx in new[]{ 47f, 64f, 81f })
                P3DPlate(px,R, new[]{ new Vector2((fx-6)*s,13f*s), new Vector2((fx+6)*s,13f*s),
                                      new Vector2((fx+4)*s,18f*s), new Vector2((fx-4)*s,18f*s) },
                         bh, b, 1.5f);
            P3DPlate(px,R, new[]{ V(50,58), V(78,58), V(76,68), V(52,68) }, bh, b, 2.1f);
            DrawRingR(px,R, 64f*s, 63f*s, 4.6f*s, 1.3f*s, ArtGold);

            P3DPlate(px,R, new[]{ V(59,68), V(69,68), V(68,84), V(60,84) }, bh, b, 2.0f);
            DrawLineR(px,R, 64f*s, 70f*s, 64f*s, 82f*s, 1.6f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f));

            P3DPlate(px,R, new[]{ V(46,72), V(59,74), V(59,86), V(46,84) }, bd, bf, 1.8f);
            for (int k = 0; k < 4; k++)
                DrawLineR(px,R, 47f*s, (76f+k*3.2f)*s, 57f*s, (77f+k*3.2f)*s, 1.8f*s, ArtGoldDk);

            P3DPlate(px,R, new[]{ V(60,84), V(64,84), V(54,92), V(50,90) }, b, bd, 1.7f);
            P3DPlate(px,R, new[]{ V(64,84), V(68,84), V(78,90), V(74,92) }, b, bd, 1.7f);

            float mx = 64f + rec, my = 92f;
            DrawRingR(px,R, mx*s, my*s, 15f*s, 2.6f*s, b);
            for (int i = 0; i < 6; i++)
            {
                // panels rake inward as the array charges, so the windup reads before the shot
                float a = (i * 60f + 12f) * Mathf.Deg2Rad;
                float reach = chg ? 4.4f : 5.4f;
                float cx = mx + Mathf.Cos(a)*10f, cy = my + Mathf.Sin(a)*10f;
                float pvx = -Mathf.Sin(a)*5.8f, pvy = Mathf.Cos(a)*5.8f;
                P3DPlate(px,R, new[]{
                    new Vector2((cx+pvx)*s,(cy+pvy)*s),
                    new Vector2((cx+pvx+Mathf.Cos(a)*reach)*s,(cy+pvy+Mathf.Sin(a)*reach)*s),
                    new Vector2((cx-pvx+Mathf.Cos(a)*reach)*s,(cy-pvy+Mathf.Sin(a)*reach)*s),
                    new Vector2((cx-pvx)*s,(cy-pvy)*s) }, ArtGoldHi, ArtGold, 1.2f);
                if (chg)   // light converging on the lens
                    DrawLineR(px,R, cx*s, cy*s, mx*s, my*s, 1.2f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
            }
            FillCircleR(px,R, 52f*s, 90f*s, 2.6f*s, ArtGoldHi);
            FillCircleR(px,R, 76f*s, 90f*s, 2.6f*s, ArtGoldHi);
            FillCircleR(px,R, mx*s, my*s, 6.0f*s, chg ? ArtRune : bd);
            DrawRingR  (px,R, mx*s, my*s, 8.0f*s, 1.6f*s, ArtGold);
            if (chg) P3DEllipseGlow(px,R, mx*s, my*s, 17f*s, 13f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, q == 4 ? 0.85f : 0.6f));
            if (q == 4) SolBeam(px, R, mx + 12f, my, 126f, ArtRune, s);
        }

        // ---- EMBER — Solar drone ------------------------------------------------
        // A censer that flies. Everything bolts to a central mast: vane ring on a collar at the
        // top, bowl carried in a rigid gimbal yoke, finial as the mast's own bottom end.
        static void P3DBuildEmber(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p);

            float tilt = q == 4 ? -34f : (q == 3 ? 12f : (q == 1 ? 6f : (q == 2 ? -6f : 0f)));
            float dv   = q == 4 ? -5f  : (q == 1 ? 2f  : (q == 2 ? -2f : 0f));
            float spin = q == 1 ? 18f  : (q == 2 ? -18f : (Atk(q) ? 30f : 0f));

            DrawLineR(px,R, 64f*s, (46f+dv)*s, 64f*s, (96f+dv)*s, 4.2f*s, bd);
            DrawLineR(px,R, 64f*s, (46f+dv)*s, 64f*s, (96f+dv)*s, 1.8f*s, bh);

            P3DPlate(px,R, new[]{ V(57,92+dv), V(71,92+dv), V(70,97+dv), V(58,97+dv) }, bh, b, 1.7f);
            DrawRingR(px,R, 64f*s, (94f+dv)*s, 13f*s, 2.2f*s, b);
            for (int i = 0; i < 3; i++)
            {
                float a = (i*120f + spin) * Mathf.Deg2Rad;
                float cx = 64f + Mathf.Cos(a)*13f, cy = 94f + dv + Mathf.Sin(a)*4.5f;
                P3DPlate(px,R, new[]{
                    new Vector2((64f + Mathf.Cos(a)*5f)*s, (94f + dv + Mathf.Sin(a)*2f)*s),
                    new Vector2((cx+2.6f)*s, (cy-2.2f)*s),
                    new Vector2((cx+1.6f)*s, (cy+2.6f)*s) }, ArtGoldHi, ArtGold, 1.1f);
            }
            FillCircleR(px,R, 64f*s, (94f+dv)*s, 4.0f*s, b);
            DrawRingR  (px,R, 64f*s, (94f+dv)*s, 5.6f*s, 1.3f*s, ArtGoldHi);

            float by = 68f + dv;
            P3DPlate(px,R, new[]{ V(59,by+13), V(62,by+13), V(57,by+1), V(54,by+2) }, b, bd, 1.6f);
            P3DPlate(px,R, new[]{ V(69,by+13), V(72,by+13), V(74,by+2), V(71,by+1) }, b, bd, 1.6f);
            FillCircleR(px,R, 56f*s, (by+1)*s, 2.4f*s, ArtGoldHi);
            FillCircleR(px,R, 72f*s, (by+1)*s, 2.4f*s, ArtGoldHi);

            // bowl, tipping on its trunnions
            {
                Vector2 c0 = V(64, by);
                Vector2 RP(float x, float y) => RotP(new Vector2(x*s, y*s), c0, tilt);
                P3DPlate(px,R, new[]{ RP(55,by+2), RP(73,by+2), RP(71,by-9), RP(57,by-9) }, bh, b, 2f);
                P3DPlate(px,R, new[]{ RP(56,by+2), RP(72,by+2), RP(69,by+9), RP(59,by+9) },
                         ArtGoldHi, ArtGold, 1.8f);
                for (int k = 0; k < 3; k++)
                {
                    Vector2 g = RP(60f+k*4f, by-3f);
                    FillCircleR(px,R, g.x, g.y, 1.5f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.95f));
                }
            }

            P3DPlate(px,R, new[]{ V(57,54+dv), V(71,54+dv), V(68,48+dv), V(60,48+dv) }, bh, b, 1.6f);
            P3DPlate(px,R, new[]{ V(61,48+dv), V(67,48+dv), V(64,40+dv) }, ArtGoldHi, ArtGold, 1.3f);

            P3DEllipseGlow(px,R, 64f*s, (by-3f)*s, 15f*s, 13f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.45f));
            if (q == 4)
            {
                // spilled coals falling forward, sized down along the arc -- not one soft blob
                for (int e = 0; e < 5; e++)
                {
                    float t = e / 4f;
                    float cx = 64f + 10f + t*30f, cy = by - 6f - t*t*18f;
                    FillCircleR(px,R, cx*s, cy*s, (3.2f - t*2f)*s,
                        new Color(1f, 0.86f - t*0.2f, 0.5f - t*0.3f));
                    P3DEllipseGlow(px,R, cx*s, cy*s, (7f - t*3f)*s, (7f - t*3f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f - t*0.14f));
                }
            }
            else
                for (int e2 = 0; e2 < 3; e2++)
                    P3DEllipseGlow(px,R, (64f-3f-e2*4f)*s, (44f+dv-e2*5f)*s,
                        (3.0f-e2*0.6f)*s, (3.0f-e2*0.6f)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f - e2*0.14f));
        }

        // ---- GUARDIAN — Solar trooper ------------------------------------------
        static void P3DBuildGuardian(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));

            Vector2 hip = V(63,56), shN = V(64,84), shF = V(56,84);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11)), fE = V(J(12),J(13)), fH = V(J(14),J(15));
            Vector2 head = V(q == 5 ? 60 : 66, 104);

            P3DLimb(px,R, shF.x,shF.y, fE.x,fE.y, 4.8f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fE.x,fE.y, fH.x,fH.y, 4.2f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 6.6f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.6f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 7.2f*s, bd, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 6.2f*s, bd, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            {
                float gA = Mathf.Atan2(nF.y-nK.y, nF.x-nK.x) * Mathf.Rad2Deg + 90f;
                var gr = new[]{ V(-6,4), V(6,4), V(5,-15), V(-5,-15) };
                for (int i = 0; i < gr.Length; i++)
                    gr[i] = RotP(new Vector2(nK.x+gr[i].x, nK.y+gr[i].y), nK, gA);
                P3DPlate(px,R, gr, bh, b, 1.8f);
                DrawLineR(px,R, gr[0].x, gr[0].y, gr[3].x, gr[3].y, 1.4f*s,
                          new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f));
            }
            // open sandals, not solid sabatons
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = i == 0 ? fF : nF;
                Color sole = i == 0 ? new Color(ArtLeather.r*0.6f, ArtLeather.g*0.6f, ArtLeather.b*0.6f)
                                    : ArtLeather;
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+9f*s,f.y+3f*s),
                                      new Vector2(f.x+7f*s,f.y-3f*s), new Vector2(f.x-6f*s,f.y-3f*s) },
                         sole, new Color(ArtLeather.r*0.4f, ArtLeather.g*0.4f, ArtLeather.b*0.4f), 1.6f);
                DrawLineR(px,R, f.x-4f*s, f.y+4f*s, f.x+5f*s, f.y+7f*s, 1.3f*s, ArtGold);
            }
            SolPteruges(px, R, 63f, 56f, sway, s);
            SolLorica  (px, R, 63f, 72f, p, s);

            void Shoulder(Vector2 sh, Vector2 elb, Color hi, Color lo, float sc)
            {
                float a = Mathf.Atan2(elb.y-sh.y, elb.x-sh.x) * Mathf.Rad2Deg + 90f;
                var pl = new[]{ new Vector2(-10f*sc,5f*sc), new Vector2(10f*sc,5f*sc),
                                new Vector2(8f*sc,-8f*sc),  new Vector2(-8f*sc,-8f*sc) };
                for (int i = 0; i < pl.Length; i++)
                    pl[i] = RotP(new Vector2(sh.x+pl[i].x*s, sh.y+pl[i].y*s), sh, a);
                P3DPlate(px,R, pl, hi, lo, 1.9f);
                Vector2 c = RotP(new Vector2(sh.x, sh.y-1.5f*s), sh, a);
                FillCircleR(px,R, c.x, c.y, 3.0f*s, ArtGold);
                DrawRingR  (px,R, c.x, c.y, 4.6f*s, 1.2f*s, ArtGoldHi);
            }
            Shoulder(shF, fE, b, bf, 0.85f);
            SolSunDisc(px, R, (fH.x/s)+2f, fH.y/s, p, 0.52f, s);   // buckler
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.2f*s, b, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.6f*s, b, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            Shoulder(shN, nE, bh, b, 1.0f);
            SolCrown(px, R, head.x/s, head.y/s, p, s);
            SolGlaive(px, R, nH, q == 4 ? 52f : (q == 3 ? -58f : -14f), p, s);
        }

        // ---- RAYCASTER — Solar sniper ------------------------------------------
        static void P3DBuildRaycaster(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);
            Vector2 hip = V(63,52), shN = V(64,78), shF = V(56,78);
            Vector2 nK = V(J(0)-1, J(1)-6), nF = V(J(2), J(3));
            Vector2 fK = V(J(4)-1, J(5)-6), fF = V(J(6), J(7));
            Vector2 nE = V(J(8), J(9)-6), nH = V(J(10), J(11)-6);
            Vector2 head = V(q == 5 ? 61 : 66, 96);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,(J(13)-6)*s, 4.2f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 5.6f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 4.8f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.2f*s, ArtLeather, 0.15f, 0.25f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.4f*s, ArtLeather, 0.15f, 0.25f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+8f*s,f.y+3f*s),
                                      new Vector2(f.x+6f*s,f.y-4f*s), new Vector2(f.x-6f*s,f.y-4f*s) },
                         ArtLeather, new Color(ArtLeather.r*0.45f, ArtLeather.g*0.45f, ArtLeather.b*0.45f), 1.6f);
            P3DPlate(px,R, new[]{ new Vector2(nK.x-6f*s,nK.y+5f*s), new Vector2(nK.x+6f*s,nK.y+5f*s),
                                  new Vector2(nK.x+5f*s,nK.y-5f*s), new Vector2(nK.x-5f*s,nK.y-5f*s) },
                     bh, b, 1.5f);
            P3DPlate(px,R, new[]{ V(54,80), V(73,80), V(75,64), V(69,50), V(57,50), V(51,64) },
                     ArtCloth, ArtClothDk, 2.2f);
            DrawLineR(px,R, 52f*s, 70f*s, 74f*s, 74f*s, 2.8f*s, ArtLeather);
            for (int i = 0; i < 3; i++) FillCircleR(px,R, (56f+i*5f)*s, 60f*s, 2.0f*s, ArtGold);
            // shoulder heat vane
            P3DPlate(px,R, new[]{ V(52,82), V(62,84), V(58,98), V(47,94) }, ArtGoldHi, ArtGold, 1.8f);
            DrawLineR(px,R, 52f*s, 85f*s, 57f*s, 95f*s, 1.3f*s, ArtGoldDk);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 4.6f*s, ArtCloth, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.0f*s, ArtCloth, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(head.x-9f*s,head.y-8f*s), new Vector2(head.x+9f*s,head.y-8f*s),
                                  new Vector2(head.x+9f*s,head.y+6f*s), new Vector2(head.x-9f*s,head.y+6f*s) },
                     b, bd, 2f);
            FillCircleR(px,R, head.x+4f*s, head.y-1f*s, 3.6f*s, new Color(0.10f,0.06f,0.04f));
            DrawRingR  (px,R, head.x+4f*s, head.y-1f*s, 4.6f*s, 1.4f*s, ArtGold);
            P3DEllipseGlow(px,R, head.x+4f*s, head.y-1f*s, 5f*s, 4f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.55f));
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y+5f*s), new Vector2(head.x+10f*s,head.y+5f*s),
                                  new Vector2(head.x+7f*s,head.y+13f*s), new Vector2(head.x-9f*s,head.y+12f*s) },
                     ArtCloth, ArtClothDk, 1.7f);
            DrawLineR(px,R, nH.x+16f*s, nH.y-2f*s, nH.x+12f*s, nH.y-16f*s, 2.0f*s, bd);
            DrawLineR(px,R, nH.x+16f*s, nH.y-2f*s, nH.x+22f*s, nH.y-16f*s, 2.0f*s, bd);
            SolLensTube(px, R, (nH.x/s)-2f, nH.y/s, q == 4 ? 42f : 34f, p, q == 4, s);
        }

        // ---- AEGIS — Solar shield-bot ------------------------------------------
        static void P3DBuildAegis(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);
            Vector2 hip = V(62,52), shN = V(63,80), shF = V(55,80);
            Vector2 nK = V(J(0)-2,J(1)), nF = V(J(2)-2,J(3));
            Vector2 fK = V(J(4)-2,J(5)), fF = V(J(6)-2,J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V(q == 5 ? 58 : 63, 98);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 5.4f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 8.0f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 7.0f*s, bf, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 9.0f*s, bd, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 7.8f*s, bd, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            foreach (var f in new[]{ nF, fF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-9f*s,f.y+4f*s), new Vector2(f.x+10f*s,f.y+4f*s),
                                      new Vector2(f.x+8f*s,f.y-5f*s), new Vector2(f.x-8f*s,f.y-5f*s) },
                         bd, bf, 1.8f);
            SolLorica(px, R, 62f, 70f, p, s);
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y-9f*s), new Vector2(head.x+11f*s,head.y-9f*s),
                                  new Vector2(head.x+10f*s,head.y+4f*s), new Vector2(head.x,head.y+9f*s),
                                  new Vector2(head.x-10f*s,head.y+4f*s) }, bh, b, 2.2f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s,head.y-2f*s), new Vector2(head.x+9f*s,head.y-2f*s),
                                  new Vector2(head.x+9f*s,head.y+1f*s), new Vector2(head.x-8f*s,head.y+1f*s) },
                     new Color(0.06f,0.04f,0.03f), new Color(0.02f,0.01f,0.01f), 1f);
            P3DPlate(px,R, new[]{ new Vector2(head.x-11f*s,head.y-9f*s), new Vector2(head.x-6f*s,head.y-9f*s),
                                  new Vector2(head.x-7f*s,head.y-17f*s), new Vector2(head.x-12f*s,head.y-16f*s) },
                     b, bd, 1.4f);
            P3DPlate(px,R, new[]{ new Vector2(head.x+7f*s,head.y-9f*s), new Vector2(head.x+12f*s,head.y-9f*s),
                                  new Vector2(head.x+11f*s,head.y-16f*s), new Vector2(head.x+6f*s,head.y-17f*s) },
                     b, bd, 1.4f);
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.8f*s, b, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 5.0f*s, b, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            {
                float sw = q == 4 ? 16f : (q == 3 ? -9f : 3f);
                DrawLineR(px,R, nH.x, nH.y, nH.x+sw*s, nH.y+16f*s, 3.0f*s, ArtLeather);
                P3DPlate(px,R, new[]{ new Vector2(nH.x+(sw-3)*s, nH.y+16f*s), new Vector2(nH.x+(sw+3)*s, nH.y+16f*s),
                                      new Vector2(nH.x+(sw+7)*s, nH.y+23f*s), new Vector2(nH.x+(sw+1)*s, nH.y+22f*s) },
                         ArtGoldHi, ArtGold, 1.4f);
            }
            SolSunDisc(px, R, 64f + (q == 4 ? 16f : (q == 3 ? 4f : 10f)), 62f, p, 1.0f, s);
        }

        // ---- FORGEWALKER — Solar mech ------------------------------------------
        static void P3DBuildForgewalker(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL,
            int part = 0, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);
            float st  = q == 1 ? 1f : (q == 2 ? -1f : 0f);
            P3DEllipseGlow(px,R, 64f*s, 15f*s, 32f*s, 7f*s, new Color(0f,0f,0f,0.45f));

            void Leg(float sx, float dir, Color tone)
            {
                float kx = sx + dir*10f + st*dir*4f, ky = 40f;
                float ax = sx + dir*2f  - st*dir*6f, ay = 20f;
                P3DLimb(px,R, sx*s,58f*s, kx*s,ky*s, 7.0f*s, tone, 0.3f, 0.4f, L,H,fillL);
                P3DLimb(px,R, kx*s,ky*s, ax*s,ay*s, 5.8f*s, tone, 0.3f, 0.4f, L,H,fillL);
                P3DPlate(px,R, new[]{ new Vector2((ax-9)*s,(ay+3)*s), new Vector2((ax+10)*s,(ay+3)*s),
                                      new Vector2((ax+8)*s,(ay-5)*s), new Vector2((ax-8)*s,(ay-5)*s) },
                         tone, new Color(tone.r*0.55f, tone.g*0.55f, tone.b*0.55f), 1.8f);
                P3DPlate(px,R, new[]{ new Vector2((kx-6)*s,(ky+6)*s), new Vector2((kx+6)*s,(ky+6)*s),
                                      new Vector2((kx+5)*s,(ky-5)*s), new Vector2((kx-5)*s,(ky-5)*s) },
                         bh, b, 1.4f);
            }
            Leg(58f,-1f,bf); Leg(68f,1f,bd);
            P3DPlate(px,R, new[]{ V(52,54), V(76,54), V(74,64), V(54,64) }, bh, b, 2f);
            P3DPlate(px,R, new[]{ V(50,64), V(78,64), V(80,84), V(74,96), V(54,96), V(48,84) }, bh, b, 2.4f);
            P3DPlate(px,R, new[]{ V(56,70), V(72,70), V(72,86), V(56,86) }, bd, bf, 1.8f);
            for (int i = 0; i < 4; i++)
                DrawLineR(px,R, 57f*s, (72f+i*4f)*s, 71f*s, (72f+i*4f)*s, 1.6f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
            P3DEllipseGlow(px,R, 64f*s, 78f*s, 18f*s, 16f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.5f));
            SolBellows(px, R, 48f, 88f, p, q == 4 ? 1f : 0f, s);
            P3DPlate(px,R, new[]{ V(70,96), V(80,96), V(78,108), V(72,108) }, b, bd, 1.8f);
            P3DEllipseGlow(px,R, 75f*s, 110f*s, 7f*s, 6f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.4f));
            {
                float ang = q == 3 ? 128f : (q == 4 ? 24f : (q == 5 ? 96f : 70f));
                Vector2 piv = V(70,88);
                Vector2 d = RotP(new Vector2(1f,0f), Vector2.zero, ang);
                Vector2 elb = new Vector2(piv.x + d.x*20f*s, piv.y + d.y*20f*s);
                Vector2 lad = new Vector2(piv.x + d.x*36f*s, piv.y + d.y*36f*s);
                P3DLimb(px,R, piv.x,piv.y, elb.x,elb.y, 5.4f*s, b, p.metallic, p.smoothness, L,H,fillL);
                P3DLimb(px,R, elb.x,elb.y, lad.x,lad.y, 4.6f*s, bh, p.metallic, p.smoothness, L,H,fillL);
                P3DPlate(px,R, new[]{ new Vector2(lad.x-8f*s,lad.y+2f*s), new Vector2(lad.x+8f*s,lad.y+2f*s),
                                      new Vector2(lad.x+6f*s,lad.y-8f*s), new Vector2(lad.x-6f*s,lad.y-8f*s) },
                         bh, b, 1.9f);
                if (q != 4)
                {
                    FillCircleR   (px,R, lad.x, lad.y-2f*s, 4.4f*s, ArtRune);
                    P3DEllipseGlow(px,R, lad.x, lad.y-2f*s, 11f*s, 10f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.6f));
                }
                else P3DEllipseGlow(px,R, lad.x+18f*s, lad.y+4f*s, 13f*s, 11f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f));
                FillCircleR(px,R, piv.x, piv.y, 5.0f*s, bd);
                FillCircleR(px,R, piv.x, piv.y, 2.4f*s, ArtGoldHi);
            }
        }

        // ---- PHOENIX — Solar interceptor ---------------------------------------
        static void P3DBuildPhoenix(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose6(pose);
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bh = ArtSteelHi(p), bd = ArtSteelDk(p), bf = ArtSteelFar(p);
            float beat = q == 1 ? 12f : (q == 2 ? -10f : (Atk(q) ? 16f : 0f));

            SolFeatherRow(px, R, 60f, 62f, 178f - beat*0.6f, 30f, 5, bf, s);
            SolFeatherRow(px, R, 58f, 58f, 190f - beat*0.6f, 24f, 4,
                new Color(bf.r*0.85f, bf.g*0.85f, bf.b*0.85f), s);
            for (int i = 0; i < 3; i++)
                P3DPlate(px,R, new[]{ V(52, 60-i*3), V(26-i*4, 52-i*6-beat*0.3f),
                                      V(24-i*4, 46-i*6-beat*0.3f), V(52, 55-i*3) },
                         ArtCloth, ArtClothDk, 1.2f);
            P3DPlate(px,R, new[]{ V(54,48), V(74,52), V(80,64), V(72,72), V(56,68) }, bh, b, 2.2f);
            DrawLineR(px,R, 58f*s, 56f*s, 74f*s, 60f*s, 1.6f*s, ArtGold);
            P3DEllipseGlow(px,R, 66f*s, 60f*s, 12f*s, 10f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.42f));
            P3DLimb(px,R, 62f*s,50f*s, 64f*s,40f*s, 2.6f*s, bd, 0.3f, 0.4f, L,H,fillL);
            P3DLimb(px,R, 64f*s,40f*s, 70f*s,36f*s, 2.2f*s, bd, 0.3f, 0.4f, L,H,fillL);
            P3DLimb(px,R, 68f*s,50f*s, 72f*s,41f*s, 2.6f*s, bd, 0.3f, 0.4f, L,H,fillL);
            P3DLimb(px,R, 72f*s,41f*s, 78f*s,38f*s, 2.2f*s, bd, 0.3f, 0.4f, L,H,fillL);
            for (int k = 0; k < 2; k++)
                DrawLineR(px,R, (70f+k*8f)*s, 37f*s, (75f+k*8f)*s, 33f*s, 1.5f*s, ArtGoldHi);
            P3DPlate(px,R, new[]{ V(74,66), V(86,70), V(94,66), V(84,60), V(75,60) }, bh, b, 2f);
            P3DPlate(px,R, new[]{ V(86,70), V(97,67), V(86,63) }, ArtGoldHi, ArtGold, 1.4f);
            P3DEllipseGlow(px,R, 82f*s, 66f*s, 3.0f*s, 2.6f*s, new Color(1f,0.92f,0.6f,0.95f));
            for (int c = 0; c < 3; c++)
                P3DPlate(px,R, new[]{ V(76+c*2,72), V(72+c*2,84-c*3), V(78+c*2,80-c*3), V(80+c*2,72) },
                         ArtCloth, ArtClothDk, 1.1f);
            SolFeatherRow(px, R, 64f, 60f, 172f + beat, 34f, 6, b, s);
            SolFeatherRow(px, R, 62f, 56f, 186f + beat, 27f, 5, bh, s);
            if (Atk(q))
                for (int e = 0; e < 3; e++)
                    P3DEllipseGlow(px,R, (96f+e*10f)*s, (62f-e*5f)*s, (5f-e)*s, (5f-e)*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.7f - e*0.16f));
        }

        // ---- PYROMANCER — Solar hacker -----------------------------------------
        static void P3DBuildPyromancer(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color b = ArtSteelBase(p), bf = ArtSteelFar(p);
            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            Vector2 hip = V(63,54), shN = V(64,82), shF = V(56,82);
            Vector2 nK = V(J(0),J(1)), nF = V(J(2),J(3)), fK = V(J(4),J(5)), fF = V(J(6),J(7));
            Vector2 nE = V(J(8),J(9)), nH = V(J(10),J(11));
            Vector2 head = V((q == 5 ? 60 : 65) + sway, 102);

            P3DLimb(px,R, shF.x,shF.y, J(12)*s,J(13)*s, 4.6f*s, bf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, fK.x,fK.y, 5.8f*s, bf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, fK.x,fK.y, fF.x,fF.y, 5.0f*s, bf, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, hip.x,hip.y, nK.x,nK.y, 6.4f*s, ArtLeather, 0.15f, 0.25f, L,H,fillL);
            P3DLimb(px,R, nK.x,nK.y, nF.x,nF.y, 5.6f*s, ArtLeather, 0.15f, 0.25f, L,H,fillL);
            foreach (var f in new[]{ fF, nF })
                P3DPlate(px,R, new[]{ new Vector2(f.x-7f*s,f.y+3f*s), new Vector2(f.x+8f*s,f.y+3f*s),
                                      new Vector2(f.x+6f*s,f.y-4f*s), new Vector2(f.x-6f*s,f.y-4f*s) },
                         ArtLeather, new Color(ArtLeather.r*0.45f, ArtLeather.g*0.45f, ArtLeather.b*0.45f), 1.6f);
            for (int w = 0; w < 3; w++)
                DrawLineR(px,R, nK.x-5f*s, nK.y-(4f+w*4f)*s, nK.x+5f*s, nK.y-(3f+w*4f)*s, 1.4f*s, ArtGoldDk);
            P3DPlate(px,R, new[]{ V(55+sway,84), V(73+sway,84), V(74,64), V(68,50), V(58,50), V(52,64) },
                     ArtCloth, ArtClothDk, 2.2f);
            DrawLineR(px,R, 52f*s, 64f*s, 74f*s, 68f*s, 2.6f*s, ArtLeather);
            for (int v = 0; v < 4; v++) FillCircleR(px,R, (55f+v*5f)*s, 58f*s, 1.8f*s, ArtGold);
            for (int t = 0; t < 3; t++)
            {
                float yy = 84f - t*7f, wd = 20f - t*3.5f;
                P3DPlate(px,R, new[]{ V(64+sway-wd, yy), V(64+sway+wd, yy),
                                      V(64+sway+wd-2, yy-9), V(64+sway-wd+2, yy-9) },
                         t > 0 ? ArtCloth : ArtGold, ArtClothDk, 1.7f);
            }
            P3DPlate(px,R, new[]{ V(48+sway,88), V(62+sway,90), V(60+sway,80), V(47+sway,79) },
                     ArtSteelHi(p), b, 1.9f);
            for (int cc = 0; cc < 3; cc++)
                FillCircleR(px,R, (51f+sway+cc*4f)*s, 86f*s, 1.6f*s,
                    new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.95f));
            P3DEllipseGlow(px,R, (54f+sway)*s, 88f*s, 13f*s, 9f*s,
                new Color(ArtRune.r, ArtRune.g, ArtRune.b, Atk(q) ? 0.6f : 0.4f));
            P3DLimb(px,R, shN.x,shN.y, nE.x,nE.y, 5.0f*s, ArtCloth, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            P3DLimb(px,R, nE.x,nE.y, nH.x,nH.y, 4.4f*s, ArtCloth, p.metallic*.4f, p.smoothness*.5f, L,H,fillL);
            SolCage(px, R, head.x/s, head.y/s, p, s);
            {
                float arc = q == 4 ? 38f : (q == 3 ? -16f : 8f);
                float ax = (nH.x/s) + arc, ay = (nH.y/s) + (q == 4 ? 4f : -14f);
                for (int ch = 0; ch < 4; ch++)
                    FillCircleR(px,R, nH.x + (ax*s - nH.x)*(ch/4f), nH.y + (ay*s - nH.y)*(ch/4f),
                                1.3f*s, ArtGold);
                SolCenser(px, R, ax, ay + 8f, p, 0f, s);
            }
        }

        // ── KNIGHT — first unit built from AUTHORED ART rather than lit primitives ──────
        // Equipment is drawn as gradient plates with dark outlines and pinned to the rig's
        // joints, so it inherits the walk/attack/flinch animation with no extra frames.
        // Steel takes the team tint (allegiance must still read); gold, cloth and leather are
        // fixed so the medieval SET holds together across both armies.
        static void P3DBuildKnight(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            // Joints
            Vector2 hip = V(63, 56), shN = V(64, 84), shF = V(56, 84), chest = V(63, 72);
            Vector2 nKnee = V(J(0), J(1)),  nFoot = V(J(2), J(3));
            Vector2 fKnee = V(J(4), J(5)),  fFoot = V(J(6), J(7));
            Vector2 nElb  = V(J(8), J(9)),  nHand = V(J(10), J(11));
            Vector2 fElb  = V(J(12), J(13)), fHand = V(J(14), J(15));
            Vector2 head  = V(q == 5 ? 60 : 66, 104);

            // Armour is metal for BOTH armies; the team colour lives in the livery.
            Color steelHi = ArtSteelHi(p);
            Color steel   = ArtSteelBase(p);
            Color steelDk = ArtSteelDk(p);
            Color farDk   = ArtSteelFar(p);
            Color livery  = ArtLivery(p);      // cape, plume, heraldry
            Color liveryDk= ArtLiveryDk(p);

            // ── cape, behind everything ─────────────────────────────────────────
            float capeSway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            P3DPlate(px, R, new[]{
                V(56 - capeSway*0.2f, 88), V(72 - capeSway*0.2f, 88),
                V(78 - capeSway, 40), V(70 - capeSway, 24), V(56 - capeSway, 24), V(50 - capeSway, 42)
            }, livery, liveryDk, 2f);

            // ── far limbs ───────────────────────────────────────────────────────
            P3DLimb(px, R, shF.x, shF.y, fElb.x, fElb.y, 5.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px, R, fElb.x, fElb.y, fHand.x, fHand.y, 4.4f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px, R, hip.x, hip.y, fKnee.x, fKnee.y, 7.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px, R, fKnee.x, fKnee.y, fFoot.x, fFoot.y, 6.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);

            // ── near leg + greave ───────────────────────────────────────────────
            P3DLimb(px, R, hip.x, hip.y, nKnee.x, nKnee.y, 7.6f*s, steelDk, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            P3DLimb(px, R, nKnee.x, nKnee.y, nFoot.x, nFoot.y, 6.6f*s, steelDk, p.metallic*.8f, p.smoothness*.8f, L,H,fillL);
            {   // greave plate rides the shin, rotated onto the limb
                float gA = Mathf.Atan2(nFoot.y - nKnee.y, nFoot.x - nKnee.x) * Mathf.Rad2Deg + 90f;
                Vector2 c0 = nKnee;
                var gr = new[]{ V(-6,4), V(6,4), V(5,-16), V(-5,-16) };
                for (int i = 0; i < gr.Length; i++) gr[i] = RotP(new Vector2(c0.x + gr[i].x, c0.y + gr[i].y), c0, gA);
                P3DPlate(px, R, gr, steelHi, steel, 1.8f);
            }
            // boots
            P3DPlate(px, R, new[]{ new Vector2(nFoot.x-8f*s,nFoot.y+3f*s), new Vector2(nFoot.x+9f*s,nFoot.y+3f*s),
                                   new Vector2(nFoot.x+7f*s,nFoot.y-4f*s), new Vector2(nFoot.x-7f*s,nFoot.y-4f*s) },
                     ArtLeather, new Color(ArtLeather.r*0.45f, ArtLeather.g*0.45f, ArtLeather.b*0.45f), 1.8f);

            // ── cuirass ─────────────────────────────────────────────────────────
            P3DPlate(px, R, new[]{
                V(52, 88), V(74, 88), V(76, 70), V(70, 54), V(56, 54), V(50, 70)
            }, steelHi, steel, 2.2f);
            // gold bands + heraldic diamond
            DrawLineR(px, R, 52f*s, 80f*s, 74f*s, 80f*s, 2.4f*s, ArtGold);
            DrawLineR(px, R, 54f*s, 64f*s, 72f*s, 64f*s, 1.8f*s, ArtGoldDk);
            DrawLineR(px, R, 51f*s, 72f*s, 75f*s, 84f*s, 3.4f*s, livery);   // livery sash
            P3DPlate(px, R, new[]{ V(63,76), V(67,71), V(63,66), V(59,71) }, ArtGoldHi, ArtGold, 1.2f);

            // ── PAULDRONS: rotate with the arm, which is the shoulder fix ───────
            // These used to sit at a fixed point, so the shoulder stayed frozen while the arm
            // swung out from under it. Each pauldron now takes the angle of its own upper arm.
            void Pauldron(Vector2 sh, Vector2 elb, Color hi, Color lo, float sc)
            {
                float a = Mathf.Atan2(elb.y - sh.y, elb.x - sh.x) * Mathf.Rad2Deg + 90f;
                var pl = new[]{ new Vector2(-11f*sc, 5f*sc), new Vector2(11f*sc, 5f*sc),
                                new Vector2(9f*sc, -8f*sc),  new Vector2(-9f*sc, -8f*sc) };
                for (int i = 0; i < pl.Length; i++)
                    pl[i] = RotP(new Vector2(sh.x + pl[i].x*s, sh.y + pl[i].y*s), sh, a);
                P3DPlate(px, R, pl, hi, lo, 2f);
                DrawLineR(px, R, pl[2].x, pl[2].y, pl[3].x, pl[3].y, 1.8f*s, ArtGold);
            }
            Pauldron(shF, fElb, steel, farDk, 0.86f);

            // ── near arm ────────────────────────────────────────────────────────
            P3DLimb(px, R, shN.x, shN.y, nElb.x, nElb.y, 5.4f*s, steel, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            P3DLimb(px, R, nElb.x, nElb.y, nHand.x, nHand.y, 4.8f*s, steel, p.metallic*.85f, p.smoothness*.9f, L,H,fillL);
            Pauldron(shN, nElb, steelHi, steel, 1.0f);

            // ── kite shield on the off hand ─────────────────────────────────────
            {
                float a = Mathf.Atan2(fHand.y - fElb.y, fHand.x - fElb.x) * Mathf.Rad2Deg + 90f;
                var sk = new[]{ new Vector2(-11,10), new Vector2(11,10), new Vector2(9,-8),
                                new Vector2(0,-17), new Vector2(-9,-8) };
                for (int i = 0; i < sk.Length; i++)
                    sk[i] = RotP(new Vector2(fHand.x + sk[i].x*s, fHand.y + sk[i].y*s), fHand, a);
                P3DPlate(px, R, sk, steel, farDk, 2f);
                DrawLineR(px, R, sk[0].x, sk[0].y, sk[3].x, sk[3].y, 2.4f*s, livery);
                DrawLineR(px, R, sk[4].x, sk[4].y, sk[2].x, sk[2].y, 2.4f*s, livery);
            }

            // ── great-helm + plume ──────────────────────────────────────────────
            P3DPlate(px, R, new[]{                       // plume, behind the helm
                new Vector2(head.x-2f*s, head.y+8f*s),  new Vector2(head.x-13f*s, head.y+26f*s),
                new Vector2(head.x-6f*s, head.y+30f*s), new Vector2(head.x+3f*s, head.y+12f*s)
            }, livery, liveryDk, 1.6f);
            P3DPlate(px, R, new[]{
                new Vector2(head.x-11f*s, head.y-11f*s), new Vector2(head.x+11f*s, head.y-11f*s),
                new Vector2(head.x+12f*s, head.y+7f*s),  new Vector2(head.x, head.y+13f*s),
                new Vector2(head.x-12f*s, head.y+7f*s)
            }, steelHi, steelDk, 2.2f);
            // visor slit + crest
            P3DPlate(px, R, new[]{
                new Vector2(head.x-9f*s, head.y-1f*s), new Vector2(head.x+9f*s, head.y-1f*s),
                new Vector2(head.x+9f*s, head.y+2.6f*s), new Vector2(head.x-9f*s, head.y+2.6f*s)
            }, new Color(0.05f,0.05f,0.08f), new Color(0.02f,0.02f,0.04f), 1f);
            DrawLineR(px, R, head.x-8f*s, head.y+0.6f*s, head.x+8f*s, head.y+0.6f*s, 1.5f*s,
                      new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.85f));
            DrawLineR(px, R, head.x, head.y+13f*s, head.x, head.y-9f*s, 2.2f*s, ArtGold);

            // ── runed greatsword in the weapon hand ─────────────────────────────
            {
                float swing = q == 4 ? 58f : (q == 3 ? -62f : (q == 5 ? -28f : -18f));
                Vector2 dir = RotP(new Vector2(0f, 1f), Vector2.zero, swing);
                Vector2 grip = nHand;
                Vector2 tip  = new Vector2(grip.x + dir.x * 46f * s, grip.y + dir.y * 46f * s);
                Vector2 gEnd = new Vector2(grip.x - dir.x * 9f * s,  grip.y - dir.y * 9f * s);
                // grip + pommel
                DrawLineR(px, R, grip.x, grip.y, gEnd.x, gEnd.y, 4.4f*s, ArtLeather);
                FillCircleR(px, R, gEnd.x, gEnd.y, 3.4f*s, ArtGold);
                // crossguard
                Vector2 perp = new Vector2(-dir.y, dir.x);
                DrawLineR(px, R, grip.x - perp.x*11f*s, grip.y - perp.y*11f*s,
                                 grip.x + perp.x*11f*s, grip.y + perp.y*11f*s, 4.2f*s, ArtGold);
                // blade + runes
                P3DBlade(px, R, new Vector2(grip.x + dir.x*3f*s, grip.y + dir.y*3f*s), tip, 3.4f*s, steelHi, steel);
                for (int i = 1; i <= 3; i++)
                {
                    float f = 0.28f + i * 0.20f;
                    P3DEllipseGlow(px, R, grip.x + dir.x*46f*s*f, grip.y + dir.y*46f*s*f,
                        2.6f*s, 2.6f*s, new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.8f));
                }
            }
        }

        // Industrial worker — hard-hat, overalls, rivet-gun arm
        // Industrial worker — side rig, chunky build, hard hat.
        // WORKER - Industrial trooper, AUTHORED ART.
        // Was the stock trooper rig with a yellow sphere on its head, which is not a theme.
        // Rebuilt on plates to match the Medieval set: canvas overalls over a riveted iron
        // breastplate, hazard-striped hard hat, tool belt, and a rivet gun that kicks on the
        // strike. Iron takes the team bias; the hazard yellow and copper stay fixed so both
        // armies read as the same foundry.
        static void P3DBuildWorker(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color ironHi = ArtSteelHi(p), iron = ArtSteelBase(p);
            Color ironDk = ArtSteelDk(p), farDk = ArtSteelFar(p);
            Color canvas = ArtCloth, canvasDk = ArtClothDk;
            Color strap  = ArtLeather;
            Color livery = ArtLivery(p), liveryDk = ArtLiveryDk(p);
            Color hazard = new Color(0.95f, 0.74f, 0.14f);
            Color hazardDk = new Color(0.46f, 0.33f, 0.04f);
            Color soot   = new Color(0.13f, 0.12f, 0.11f);

            Vector2 hip = V(63, 56), shN = V(64, 84), shF = V(56, 84);
            Vector2 nKnee = V(J(0), J(1)),  nFoot = V(J(2), J(3));
            Vector2 fKnee = V(J(4), J(5)),  fFoot = V(J(6), J(7));
            Vector2 nElb  = V(J(8), J(9)),  nHand = V(J(10), J(11));
            Vector2 fElb  = V(J(12), J(13)), fHand = V(J(14), J(15));
            Vector2 head  = V(q == 5 ? 61 : 66, 103);

            // far limbs
            P3DLimb(px,R, shF.x, shF.y, fElb.x, fElb.y, 5.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fElb.x, fElb.y, fHand.x, fHand.y, 4.4f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, hip.x, hip.y, fKnee.x, fKnee.y, 7.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);
            P3DLimb(px,R, fKnee.x, fKnee.y, fFoot.x, fFoot.y, 6.0f*s, farDk, p.metallic*.5f, p.smoothness*.6f, L,H,fillL);

            // near leg: canvas trouser, heavy boot
            P3DLimb(px,R, hip.x, hip.y, nKnee.x, nKnee.y, 7.4f*s, canvas, 0.1f, 0.2f, L,H,fillL);
            P3DLimb(px,R, nKnee.x, nKnee.y, nFoot.x, nFoot.y, 6.4f*s, canvasDk, 0.1f, 0.2f, L,H,fillL);
            foreach (var f in new[]{ fFoot, nFoot })
                P3DPlate(px,R, new[]{ new Vector2(f.x-8f*s, f.y+4f*s), new Vector2(f.x+10f*s, f.y+4f*s),
                                      new Vector2(f.x+8f*s, f.y-5f*s), new Vector2(f.x-7f*s, f.y-5f*s) },
                         ironDk, farDk, 1.8f);

            // knee pad on the near leg, rotated onto the shin
            {
                float kA = Mathf.Atan2(nFoot.y - nKnee.y, nFoot.x - nKnee.x) * Mathf.Rad2Deg + 90f;
                var kp = new[]{ V(-6,3), V(6,3), V(5,-9), V(-5,-9) };
                for (int i = 0; i < kp.Length; i++)
                    kp[i] = RotP(new Vector2(nKnee.x + kp[i].x, nKnee.y + kp[i].y), nKnee, kA);
                P3DPlate(px,R, kp, iron, ironDk, 1.6f);
            }

            // overalls
            P3DPlate(px,R, new[]{ V(52,86), V(74,86), V(76,66), V(70,50), V(56,50), V(50,66) },
                     canvas, canvasDk, 2.2f);
            // riveted breastplate over the bib
            P3DPlate(px,R, new[]{ V(55,84), V(73,84), V(74,68), V(56,68) }, ironHi, iron, 2f);
            IndRivets(px,R, 58, 81, 71, 81, 4, ironDk, s);
            IndRivets(px,R, 58, 70, 71, 70, 4, ironDk, s);
            IndGauge (px,R, 66, 76, 5.0f, 0.55f, s);   // shares the set's brass dial
            // bib straps over the shoulders, and the livery armband
            DrawLineR(px,R, 57f*s, 86f*s, 60f*s, 66f*s, 2.6f*s, strap);
            DrawLineR(px,R, 70f*s, 86f*s, 68f*s, 66f*s, 2.6f*s, strap);
            DrawLineR(px,R, 50f*s, 62f*s, 76f*s, 62f*s, 3.4f*s, strap);         // tool belt
            for (int i = 0; i < 3; i++)                                          // hanging tools
                DrawLineR(px,R, (56f + i*7f)*s, 61f*s, (56f + i*7f)*s, 53f*s, 1.6f*s, ironDk);
            DrawLineR(px,R, 51f*s, 74f*s, 57f*s, 78f*s, 4f*s, livery);           // livery armband

            // shoulder pad follows the near arm, same trick as the knight pauldron
            void Pad(Vector2 sh, Vector2 elb, Color hi, Color lo, float sc)
            {
                float a = Mathf.Atan2(elb.y - sh.y, elb.x - sh.x) * Mathf.Rad2Deg + 90f;
                var pl = new[]{ new Vector2(-10f*sc, 4f*sc), new Vector2(10f*sc, 4f*sc),
                                new Vector2(8f*sc, -7f*sc),  new Vector2(-8f*sc, -7f*sc) };
                for (int i = 0; i < pl.Length; i++)
                    pl[i] = RotP(new Vector2(sh.x + pl[i].x*s, sh.y + pl[i].y*s), sh, a);
                P3DPlate(px,R, pl, hi, lo, 1.8f);
                DrawLineR(px,R, pl[0].x, pl[0].y, pl[1].x, pl[1].y, 1.6f*s, hazard);
            }
            Pad(shF, fElb, iron, farDk, 0.85f);

            // near arm
            P3DLimb(px,R, shN.x, shN.y, nElb.x, nElb.y, 5.4f*s, canvas, 0.1f, 0.2f, L,H,fillL);
            P3DLimb(px,R, nElb.x, nElb.y, nHand.x, nHand.y, 4.8f*s, canvas, 0.1f, 0.2f, L,H,fillL);
            Pad(shN, nElb, ironHi, iron, 1.0f);
            P3DSphere(px,R, nHand.x, nHand.y, 4.2f*s, strap, 0.1f, 0.2f, L,H,fillL);   // work glove

            // head + hazard-striped hard hat
            P3DPlate(px,R, new[]{ new Vector2(head.x-8f*s, head.y-9f*s), new Vector2(head.x+8f*s, head.y-9f*s),
                                  new Vector2(head.x+8f*s, head.y+5f*s), new Vector2(head.x-8f*s, head.y+5f*s) },
                     new Color(0.62f,0.47f,0.36f), new Color(0.38f,0.27f,0.20f), 1.6f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y-1f*s, 2.4f*s, 2.0f*s, new Color(0.06f,0.05f,0.05f,0.95f));
            DrawLineR(px,R, head.x-7f*s, head.y-7f*s, head.x+7f*s, head.y-7f*s, 2f*s, soot);  // grime
            P3DPlate(px,R, new[]{                                             // dome
                new Vector2(head.x-11f*s, head.y+5f*s),  new Vector2(head.x-8f*s, head.y+14f*s),
                new Vector2(head.x+6f*s, head.y+15f*s),  new Vector2(head.x+11f*s, head.y+6f*s)
            }, hazard, hazardDk, 2f);
            DrawLineR(px,R, head.x-14f*s, head.y+5f*s, head.x+15f*s, head.y+5f*s, 2.8f*s, hazard);  // brim
            DrawLineR(px,R, head.x-3f*s, head.y+14f*s, head.x-1f*s, head.y+6f*s, 1.8f*s, hazardDk); // rib

            // rivet gun: kicks back on the strike, lowered on the windup
            {
                float kick = q == 4 ? 10f : (q == 3 ? -5f : 2f);
                Vector2 muzzle = new Vector2(nHand.x + (16f + kick)*s, nHand.y + 2f*s);
                DrawLineR(px,R, nHand.x, nHand.y, muzzle.x, muzzle.y, 4.2f*s, iron);
                DrawLineR(px,R, nHand.x, nHand.y, muzzle.x, muzzle.y, 1.6f*s, ironHi);
                P3DPlate(px,R, new[]{                                          // hopper
                    new Vector2(nHand.x+5f*s, nHand.y+5f*s),  new Vector2(nHand.x+13f*s, nHand.y+5f*s),
                    new Vector2(nHand.x+12f*s, nHand.y+12f*s), new Vector2(nHand.x+6f*s, nHand.y+12f*s)
                }, ArtGoldHi, ArtGold, 1.4f);
                DrawLineR(px,R, nHand.x-2f*s, nHand.y, nHand.x-2f*s, nHand.y-8f*s, 3f*s, strap); // grip
                if (q == 4)
                    P3DEllipseGlow(px,R, muzzle.x + 6f*s, muzzle.y, 8f*s, 6f*s,
                        new Color(ArtRune.r, ArtRune.g, ArtRune.b, 0.75f));
            }
        }

        // Cloaked wanderer — Dawn theme: wide cloak, hood, glowing staff
        // Cloaked traveller: no legs to animate, so the drape sways instead.
        static void P3DBuildWanderer(Color[] px, int R, P3DP p, Vector3 L, Vector3 H, Vector3 fillL, int pose = 0)
        {
            float s = R / 128f;
            int q = Pose8(pose);
            float J(int c) => SideJoints[q, c];
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);

            Color linen   = ArtLivery(p);            // the travelling cloak carries allegiance
            Color linenDk = ArtLiveryDk(p);
            Color linenSh = new Color(linenDk.r * 0.58f, linenDk.g * 0.58f, linenDk.b * 0.66f);
            Color wood    = ArtLeather;
            Color pale    = ArtSteelHi(p);
            Color paleDk  = ArtSteelFar(p);
            Color lightC  = ArtRune;

            float sway = q == 1 ? 3f : (q == 2 ? -3f : (Atk(q) ? 5f : 0f));
            float bob  = q == 1 ? 2f : (q == 2 ? -2f : 0f);

            Vector2 shN = V(64 + sway, 84 + bob), shF = V(56 + sway, 84 + bob);
            Vector2 nElb = V(J(8) + sway, J(9) + bob),  nHand = V(J(10) + sway, J(11) + bob);
            Vector2 fElb = V(J(12) + sway, J(13) + bob), fHand = V(J(14) + sway, J(15) + bob);
            Vector2 nFoot = V(J(2), J(3)), fFoot = V(J(6), J(7));
            Vector2 nKnee = V(J(0), J(1)), fKnee = V(J(4), J(5));
            Vector2 head  = V((q == 5 ? 60 : 65) + sway, 104 + bob);

            // Boots below the hem. The old build was a front-facing cloak blob with no legs at
            // all, so it never read as walking -- the drape just slid sideways. Showing the
            // lower legs under a travelling cloak keeps the drape silhouette and gets a stride.
            P3DLimb(px,R, fKnee.x, fKnee.y, fFoot.x, fFoot.y, 4.8f*s, linenSh, 0.05f, 0.15f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(fFoot.x-7f*s, fFoot.y+3f*s), new Vector2(fFoot.x+8f*s, fFoot.y+3f*s),
                                  new Vector2(fFoot.x+6f*s, fFoot.y-4f*s), new Vector2(fFoot.x-6f*s, fFoot.y-4f*s) },
                     paleDk, new Color(0.10f,0.10f,0.13f), 1.6f);
            P3DLimb(px,R, nKnee.x, nKnee.y, nFoot.x, nFoot.y, 5.4f*s, wood, 0.05f, 0.15f, L,H,fillL);
            P3DPlate(px,R, new[]{ new Vector2(nFoot.x-8f*s, nFoot.y+3f*s), new Vector2(nFoot.x+9f*s, nFoot.y+3f*s),
                                  new Vector2(nFoot.x+7f*s, nFoot.y-4f*s), new Vector2(nFoot.x-7f*s, nFoot.y-4f*s) },
                     wood, new Color(wood.r*0.42f, wood.g*0.42f, wood.b*0.42f), 1.8f);

            // travelling pack, slung behind the shoulder
            P3DPlate(px,R, new[]{ V(44+sway,84), V(56+sway,86), V(58+sway,62), V(46+sway,60) },
                     wood, new Color(wood.r*0.45f, wood.g*0.45f, wood.b*0.45f), 2f);
            DrawLineR(px,R, (45f+sway)*s, 76f*s, (57f+sway)*s, 78f*s, 1.8f*s, linenDk);
            DrawLineR(px,R, (46f+sway)*s, 68f*s, (58f+sway)*s, 70f*s, 1.8f*s, linenDk);
            P3DPlate(px,R, new[]{ V(46+sway,62), V(56+sway,62), V(55+sway,54), V(47+sway,54) },
                     linenDk, linenSh, 1.6f);                                    // bedroll

            // far sleeve
            P3DLimb(px,R, shF.x, shF.y, fElb.x, fElb.y, 5.0f*s, linenSh, 0.05f, 0.2f, L,H,fillL);
            P3DLimb(px,R, fElb.x, fElb.y, fHand.x, fHand.y, 4.4f*s, linenSh, 0.05f, 0.2f, L,H,fillL);

            // the cloak: a wide drape open at the front, ending mid-shin
            P3DPlate(px,R, new[]{
                V(50 + sway*1.5f, 30), V(78 + sway*1.5f, 32), V(76 + sway, 58),
                V(72 + sway, 86), V(56 + sway, 86), V(52 + sway, 58)
            }, linen, linenDk, 2.4f);
            DrawLineR(px,R, (58f+sway)*s, 84f*s, (55f+sway*1.4f)*s, 33f*s, 1.8f*s, linenSh);
            DrawLineR(px,R, (70f+sway)*s, 84f*s, (73f+sway*1.4f)*s, 34f*s, 1.8f*s, linenSh);
            DrawLineR(px,R, (66f+sway)*s, 82f*s, (67f+sway*1.4f)*s, 36f*s, 1.4f*s, linenSh);
            DrawLineR(px,R, (50f+sway*1.5f)*s, 31f*s, (78f+sway*1.5f)*s, 33f*s, 2.4f*s, ArtGold); // hem band

            // near sleeve
            P3DLimb(px,R, shN.x, shN.y, nElb.x, nElb.y, 5.4f*s, linen, 0.05f, 0.2f, L,H,fillL);
            P3DLimb(px,R, nElb.x, nElb.y, nHand.x, nHand.y, 4.8f*s, linen, 0.05f, 0.2f, L,H,fillL);

            // hood: deeper and further forward than the wizard cowl, so they do not read alike
            P3DPlate(px,R, new[]{
                new Vector2(head.x-12f*s, head.y-11f*s), new Vector2(head.x+13f*s, head.y-9f*s),
                new Vector2(head.x+12f*s, head.y+8f*s),  new Vector2(head.x-1f*s, head.y+15f*s),
                new Vector2(head.x-12f*s, head.y+8f*s)
            }, linen, linenDk, 2.2f);
            P3DPlate(px,R, new[]{
                new Vector2(head.x-7f*s, head.y-8f*s), new Vector2(head.x+9f*s, head.y-7f*s),
                new Vector2(head.x+8f*s, head.y+4f*s), new Vector2(head.x-6f*s, head.y+4f*s)
            }, new Color(0.07f,0.07f,0.11f), new Color(0.02f,0.02f,0.04f), 1f);
            P3DEllipseGlow(px,R, head.x+3f*s, head.y-2f*s, 2.8f*s, 2.6f*s,
                new Color(lightC.r, lightC.g, lightC.b, 0.95f));
            // scarf trailing off the back of the hood
            P3DPlate(px,R, new[]{
                new Vector2(head.x-10f*s, head.y+2f*s),  new Vector2(head.x-22f*s, head.y-8f*s - sway*s),
                new Vector2(head.x-19f*s, head.y-15f*s - sway*s), new Vector2(head.x-8f*s, head.y-6f*s)
            }, linenDk, linenSh, 1.6f);

            // walking staff with a hung lantern - carried, so blend toward a stable grip
            {
                bool lift = Atk(q);
                Vector2 canon = V(64f + sway + 13f, 76f + bob);
                Vector2 grip  = Vector2.Lerp(nHand, canon, lift ? 0.5f : 0.72f);
                float lean = lift ? -22f : -5f;
                Vector2 d = RotP(new Vector2(0f, 1f), Vector2.zero, lean);
                Vector2 top = new Vector2(grip.x + d.x*42f*s, grip.y + d.y*42f*s);
                Vector2 btm = new Vector2(grip.x - d.x*30f*s, grip.y - d.y*30f*s);
                DrawLineR(px,R, btm.x, btm.y, top.x, top.y, 3.0f*s, wood);
                DrawLineR(px,R, btm.x, btm.y, top.x, top.y, 1.2f*s,
                          new Color(wood.r*1.45f, wood.g*1.4f, wood.b*1.3f));
                // lantern hook + lantern
                Vector2 hook = new Vector2(top.x + 7f*s, top.y - 3f*s);
                DrawLineR(px,R, top.x, top.y, hook.x, hook.y, 1.6f*s, ArtGold);
                P3DPlate(px,R, new[]{
                    new Vector2(hook.x-5f*s, hook.y-2f*s),  new Vector2(hook.x+5f*s, hook.y-2f*s),
                    new Vector2(hook.x+4f*s, hook.y-12f*s), new Vector2(hook.x-4f*s, hook.y-12f*s)
                }, ArtGoldHi, ArtGold, 1.4f);
                float glowR = lift ? 13f : 9f;
                P3DEllipseGlow(px,R, hook.x, hook.y-7f*s, glowR*s, glowR*s,
                    new Color(lightC.r, lightC.g, lightC.b, lift ? 0.85f : 0.55f));
                FillCircleR(px,R, hook.x, hook.y-7f*s, 3.2f*s, new Color(1f,0.96f,0.86f));
                if (lift)
                    for (int i = 0; i < 3; i++)
                        P3DEllipseGlow(px,R, hook.x + (9f + i*8f)*s, hook.y + (-5f + i*4f)*s,
                            (3.2f - i*0.7f)*s, (3.2f - i*0.7f)*s,
                            new Color(lightC.r, lightC.g, lightC.b, 0.7f - i*0.17f));
            }
        }

        // ── shape helpers for specific units ──────────────────────────────────

        // Knight's flat shield plate (roughly viewer-facing with slight left-tilt)
        static void P3DShieldPlate(Color[] px, int R,
            float x0, float y0, float x1, float y1,
            Color bodyCol, Color rimCol, float metallic, float smoothness, Vector3 H)
        {
            var N   = new Vector3(0.18f, 0.05f, 0.98f).normalized; // slight left-face tilt
            float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), Mathf.Lerp(10f, 120f, smoothness));
            Color face = bodyCol * 0.7f + Color.white * (spec * Mathf.Lerp(0.04f, 0.50f, metallic));
            Color rim  = rimCol * 1.3f;

            for (int y = (int)y0; y <= (int)y1; y++)
            for (int x = (int)x0; x <= (int)x1; x++)
            {
                float xf = (x - x0) / (x1 - x0);
                float yf = (y - y0) / (y1 - y0);
                // Smooth round-cornered fade
                float ex = Mathf.Min(xf, 1f - xf) * 5f, ey = Mathf.Min(yf, 1f - yf) * 5f;
                float a  = Mathf.Clamp01(Mathf.Min(ex, ey));
                Color c  = Color.Lerp(face, rim, (1f - xf) * 0.4f); // rim highlight on left edge
                c.a      = a * 0.88f;
                P3DSet(px, R, x, y, c);
            }
            // Bright top rim line
            for (int x = (int)x0; x <= (int)x1; x++)
            {
                float a = Mathf.Clamp01(((x - x0) / (x1 - x0)) * ((x1 - x) / (x1 - x0)) * 5f);
                var c = rim; c.r = Mathf.Min(c.r * 1.4f, 1f); c.g = Mathf.Min(c.g * 1.4f, 1f); c.b = Mathf.Min(c.b * 1.4f, 1f);
                c.a = a * 0.75f;
                P3DSet(px, R, x, (int)y1, c);
            }
        }

        // Knight's narrow visor slit — dark with faint skin-glow line
        static void P3DKnightVisor(Color[] px, int R, float cx, float cy, float r, Color visorCol, Color glowCol)
        {
            float r2   = r * r;
            float slitH = r * 0.09f; // much narrower than trooper
            float vy   = cy + r * 0.08f;
            Color dark = new Color(0.04f, 0.03f, 0.06f);

            for (int y = (int)(vy - slitH); y <= (int)(vy + slitH); y++)
            for (int x = (int)(cx - r * .85f); x <= (int)(cx + r * .85f); x++)
            {
                float dx = x + .5f - cx, dy = y + .5f - cy;
                if (dx * dx + dy * dy >= r2) continue;
                float xf = 1f - Mathf.Abs(dx) / (r * .82f);
                float yf = 1f - Mathf.Abs(y + .5f - vy) / slitH;
                Color c  = Color.Lerp(dark, visorCol * 0.25f, xf * yf * 0.5f);
                c.a = xf * yf * 0.94f;
                P3DSet(px, R, x, y, c);
            }
            // Thin emissive glow along centre of slit
            for (int x = (int)(cx - r * .72f); x <= (int)(cx + r * .72f); x++)
            {
                float xf = 1f - Mathf.Abs(x + .5f - cx) / (r * .72f);
                var c = glowCol; c.a = xf * xf * 0.42f;
                P3DSet(px, R, x, (int)vy, c);
            }
        }

        // Hard-hat brim — flat elliptical disc below the dome
        static void P3DHelmetBrim(Color[] px, int R, float cx, float cy, float rx, float ry, Color col)
        {
            Color top  = new Color(Mathf.Min(col.r * 1.3f, 1f), Mathf.Min(col.g * 1.3f, 1f), Mathf.Min(col.b * 1.3f, 1f));
            for (int y = (int)(cy - ry); y <= (int)(cy + ry); y++)
            for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
            {
                float ddx = (x + .5f - cx) / rx, ddy = (y + .5f - cy) / ry;
                float d   = ddx * ddx + ddy * ddy;
                if (d > 1f) continue;
                float edge = Mathf.Clamp01(1f - d) * Mathf.Clamp01((1f - Mathf.Abs(ddx)) * 5f);
                // Only the underside showing — darker inner, bright rim
                float bright = Mathf.Abs(ddx);
                Color c = Color.Lerp(col * 0.65f, top, bright); c.a = edge * 0.90f;
                P3DSet(px, R, x, y, c);
            }
        }

        // Worker goggles — two dark lenses with coloured rim
        static void P3DGoggles(Color[] px, int R, float cx, float cy, float headR, Color lensCol, Color rimCol)
        {
            float gR   = headR * 0.22f;
            float gOff = headR * 0.30f;
            float gY   = cy + headR * 0.09f;
            Color dark = new Color(0.04f, 0.04f, 0.07f, 1f);

            foreach (float gX in new[] { cx - gOff, cx + gOff })
            {
                // Dark lens (sphere-shaded so it has a specular pop)
                float r2 = gR * gR;
                for (int y = (int)(gY - gR - 1); y <= (int)(gY + gR + 1); y++)
                for (int x = (int)(gX - gR - 1); x <= (int)(gX + gR + 1); x++)
                {
                    float dx = x + .5f - gX, dy = y + .5f - gY;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= r2) continue;
                    float dz  = Mathf.Sqrt(r2 - d2);
                    float spec = dz / gR; spec = spec * spec * 0.55f; // viewer-facing highlight
                    Color c = Color.Lerp(dark, lensCol * 0.35f, spec);
                    c.a = Mathf.Clamp01((gR - Mathf.Sqrt(d2)) * 1.8f);
                    P3DSet(px, R, x, y, c);
                }
                // Coloured rim ring
                for (int y = (int)(gY - gR - 3); y <= (int)(gY + gR + 3); y++)
                for (int x = (int)(gX - gR - 3); x <= (int)(gX + gR + 3); x++)
                {
                    float dx   = x + .5f - gX, dy = y + .5f - gY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float aa   = Mathf.Clamp01(2f - Mathf.Abs(dist - (gR + 1.2f)) * 2.5f);
                    if (aa <= 0f) continue;
                    var c = rimCol; c.a = aa * 0.72f;
                    P3DSet(px, R, x, y, c);
                }
            }
            // Bridge
            int bridgeY = (int)gY;
            for (int x = (int)(cx - gOff + gR * .6f); x <= (int)(cx + gOff - gR * .6f); x++)
            {
                var c = dark; c.a = 0.65f;
                P3DSet(px, R, x, bridgeY, c);
                P3DSet(px, R, x, bridgeY + 1, new Color(dark.r, dark.g, dark.b, 0.35f));
            }
        }

        // Wanderer's wide draped cloak (scan-line fill, matte cloth look)
        static void P3DCloak(Color[] px, int R, float cx, Color col, float metallic, float smoothness)
        {
            float s = R / 128f;
            float yBot = 19f * s, yTop = 80f * s;
            for (int y = (int)yBot; y <= (int)yTop; y++)
            {
                float t      = (y - yBot) / (yTop - yBot); // 0=bottom, 1=top
                float halfW  = Mathf.Lerp(35f * s, 14f * s, t);
                for (int x = (int)(cx - halfW); x <= (int)(cx + halfW); x++)
                {
                    float xn     = (x + .5f - cx) / halfW; // -1..1
                    float edgeA  = Mathf.Clamp01((halfW - Mathf.Abs(x + .5f - cx)) * 1.4f);
                    // Cloth shading: spherical cross-section normal
                    float nz     = Mathf.Sqrt(Mathf.Max(0f, 1f - xn * xn));
                    float diff   = Mathf.Clamp01(0.25f + nz * 0.65f + 0.10f * t);
                    float rim    = Mathf.Pow(1f - nz, 3f) * 0.14f;
                    Color c = col * diff + new Color(0.3f, 0.4f, 0.6f, 0f) * rim;
                    c.a = edgeA * (0.80f + t * 0.20f);
                    P3DSet(px, R, x, y, c);
                }
            }
            // Fold crease lines for cloth depth
            float[] foldXs = { cx - 10f * s, cx + 6f * s };
            foreach (float fx in foldXs)
            {
                for (int y = (int)yBot; y <= (int)(yTop * .85f); y++)
                {
                    float t  = (y - yBot) / (yTop - yBot);
                    float wob = Mathf.Sin(y * .14f) * 2f * s;
                    Color c = col * 0.48f; c.a = 0.30f * (1f - t);
                    P3DSet(px, R, (int)(fx + wob), y, c);
                }
            }
        }

        // Wanderer's magic staff (right side)
        static void P3DStaff(Color[] px, int R, float sx, Color orbCol, Color shaftCol)
        {
            float s    = R / 128f;
            float orbR = 6f * s;
            float orbY = 30f * s;
            float or2  = orbR * orbR;
            // Shaft
            for (int y = (int)(24 * s); y <= (int)(90 * s); y++)
            {
                float wob = Mathf.Sin(y * .08f) * 0.8f * s;
                Color c = shaftCol * 0.8f; c.a = 0.82f;
                P3DSet(px, R, (int)(sx + wob),     y, c);
                P3DSet(px, R, (int)(sx + wob) + 1, y, new Color(c.r, c.g, c.b, 0.45f));
            }
            // Orb at top
            for (int y = (int)(orbY - orbR); y <= (int)(orbY + orbR); y++)
            for (int x = (int)(sx   - orbR); x <= (int)(sx   + orbR); x++)
            {
                float dx = x + .5f - sx, dy = y + .5f - orbY;
                float d2 = dx * dx + dy * dy;
                if (d2 >= or2) continue;
                float dz   = Mathf.Sqrt(or2 - d2);
                float spec = (dz / orbR) * (dz / orbR);
                Color c = Color.Lerp(orbCol, Color.white, spec * 0.75f);
                c.a = Mathf.Clamp01((orbR - Mathf.Sqrt(d2)) * 1.8f);
                P3DSet(px, R, x, y, c);
            }
            // Glow around orb
            P3DEllipseGlow(px, R, sx, orbY, 10f * s, 10f * s, new Color(orbCol.r, orbCol.g, orbCol.b, 0.45f));
        }

        // Wanderer's glowing eyes inside the hood
        static void P3DWandererEyes(Color[] px, int R, float cx, float cy, float headR, Color eyeCol, Color glowCol)
        {
            float eY   = cy + headR * 0.10f;
            float eOff = headR * 0.25f;
            float eR   = headR * 0.14f;
            float r2   = headR * headR;

            foreach (float eX in new[] { cx - eOff, cx + eOff })
            {
                for (int y = (int)(eY - eR - 1); y <= (int)(eY + eR + 1); y++)
                for (int x = (int)(eX - eR - 1); x <= (int)(eX + eR + 1); x++)
                {
                    float hdx = x + .5f - cx, hdy = y + .5f - cy;
                    if (hdx * hdx + hdy * hdy >= r2) continue; // must be inside hood
                    float dx = x + .5f - eX, dy = y + .5f - eY;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= eR * eR) continue;
                    float a = Mathf.Clamp01((eR - Mathf.Sqrt(d2)) * 2.5f);
                    Color c = Color.Lerp(eyeCol, Color.white, Mathf.Sqrt(1f - d2 / (eR * eR)) * 0.6f);
                    c.a = a * 0.92f;
                    P3DSet(px, R, x, y, c);
                }
                P3DEllipseGlow(px, R, eX, eY, eR * 2.5f, eR * 2.5f, new Color(glowCol.r, glowCol.g, glowCol.b, 0.30f));
            }
        }

        // — pixel writer (alpha-blend over existing) —
        static void P3DSet(Color[] px, int R, int x, int y, Color src)
        {
            if ((uint)x >= (uint)R || (uint)y >= (uint)R) return;
            int i   = y * R + x;
            var dst = px[i];
            float oa = src.a + dst.a * (1f - src.a);
            if (oa < 0.001f) return;
            px[i] = new Color(
                (src.r * src.a + dst.r * dst.a * (1f - src.a)) / oa,
                (src.g * src.a + dst.g * dst.a * (1f - src.a)) / oa,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) / oa, oa);
        }

        // — soft radial/elliptical glow —
        static void P3DEllipseGlow(Color[] px, int R, float cx, float cy, float rx, float ry, Color col)
        {
            int x0 = Mathf.Max(0, (int)(cx-rx-2)), x1 = Mathf.Min(R-1, (int)(cx+rx+2));
            int y0 = Mathf.Max(0, (int)(cy-ry-2)), y1 = Mathf.Min(R-1, (int)(cy+ry+2));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float ddx = (x+.5f-cx)/rx, ddy = (y+.5f-cy)/ry;
                float a = Mathf.Clamp01(1f - ddx*ddx - ddy*ddy);
                if (a <= 0f) continue;
                var c = col; c.a = col.a * a * a;
                P3DSet(px, R, x, y, c);
            }
        }

        // — sphere with Blinn-Phong shading —
        static void P3DSphere(Color[] px, int R, float cx, float cy, float r,
            Color col, float metallic, float smoothness, Vector3 L, Vector3 H, Vector3 fillL)
        {
            int x0 = Mathf.Max(0,(int)(cx-r-1)), x1 = Mathf.Min(R-1,(int)(cx+r+1));
            int y0 = Mathf.Max(0,(int)(cy-r-1)), y1 = Mathf.Min(R-1,(int)(cy+r+1));
            float r2 = r * r;
            float specPow = Mathf.Lerp(10f, 220f, smoothness);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x+.5f-cx, dy = y+.5f-cy;
                float d2 = dx*dx + dy*dy;
                if (d2 >= r2) continue;
                float dz = Mathf.Sqrt(r2 - d2);
                var N = new Vector3(dx/r, dy/r, dz/r);

                float diff  = Mathf.Max(0f, Vector3.Dot(N, L));
                float fill  = Mathf.Max(0f, Vector3.Dot(N, fillL)) * 0.22f;
                float spec  = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), specPow);
                float rim   = Mathf.Pow(Mathf.Max(0f, 1f - N.z), 3.5f) * 0.18f; // N.z == dot(N, forward)

                Color lit = col * (0.15f + diff * 0.85f + fill)
                          + Color.white * (spec * Mathf.Lerp(0.06f, 0.95f, metallic) * smoothness)
                          + new Color(0.35f, 0.45f, 0.65f, 0f) * rim;
                lit.a = col.a * Mathf.Clamp01((r - Mathf.Sqrt(d2)) * 1.6f);
                P3DSet(px, R, x, y, lit);
            }
        }

        // — vertical cylinder with Blinn-Phong shading —
        // ── authored-art primitives ─────────────────────────────────────────────
        // The P3D* set above lights PRIMITIVES (spheres, capsules). Authored equipment needs
        // flat shapes with a vertical gradient and a dark outline instead -- that combination
        // is what separates "painted asset" from "lit blob" at unit scale.

        /// <summary>Rotate a point about a pivot, degrees, screen-space (y-up canvas).</summary>
        static Vector2 RotP(Vector2 pt, Vector2 pivot, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
            float dx = pt.x - pivot.x, dy = pt.y - pivot.y;
            return new Vector2(pivot.x + dx * c - dy * sn, pivot.y + dx * sn + dy * c);
        }

        /// <summary>Scanline-fill a polygon with a vertical gradient (top colour → bottom colour).</summary>
        static void P3DPolyGrad(Color[] px, int R, Vector2[] pts, Color top, Color bot)
        {
            if (pts.Length < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var q in pts) { if (q.y < minY) minY = q.y; if (q.y > maxY) maxY = q.y; }
            float span = Mathf.Max(0.001f, maxY - minY);
            int y0 = Mathf.Max(0, (int)minY), y1 = Mathf.Min(R - 1, (int)maxY + 1);
            var xs = new System.Collections.Generic.List<float>(8);

            for (int y = y0; y <= y1; y++)
            {
                xs.Clear();
                for (int i = 0, n = pts.Length; i < n; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % n];
                    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                        xs.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                float t = (y - minY) / span;
                Color c = Color.Lerp(top, bot, t);
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Mathf.Max(0, Mathf.CeilToInt(xs[k]));
                    int xb = Mathf.Min(R - 1, Mathf.FloorToInt(xs[k + 1]));
                    for (int x = xa; x <= xb; x++) P3DSet(px, R, x, y, c);
                }
            }
        }

        /// <summary>Stroke a closed polygon outline.</summary>
        static void P3DPolyLine(Color[] px, int R, Vector2[] pts, Color col, float w)
        {
            for (int i = 0, n = pts.Length; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                DrawLineR(px, R, a.x, a.y, b.x, b.y, w, col);
            }
        }

        /// <summary>Filled + outlined plate: the workhorse for helms, cuirasses, shields, capes.</summary>
        static void P3DPlate(Color[] px, int R, Vector2[] pts, Color top, Color bot, float outlineW = 2f)
        {
            P3DPolyGrad(px, R, pts, top, bot);
            P3DPolyLine(px, R, pts, ArtOutline, outlineW);
        }

        /// <summary>Tapered blade with a bright fuller down the centre.</summary>
        static void P3DBlade(Color[] px, int R, Vector2 grip, Vector2 tip, float halfW,
            Color edge, Color core)
        {
            Vector2 d = (tip - grip); float len = d.magnitude;
            if (len < 0.01f) return;
            d /= len;
            Vector2 nrm = new Vector2(-d.y, d.x) * halfW;
            Vector2 nose = tip;
            Vector2 shoulderL = tip - d * (len * 0.16f) + nrm;
            Vector2 shoulderR = tip - d * (len * 0.16f) - nrm;
            var poly = new[] { grip + nrm, shoulderL, nose, shoulderR, grip - nrm };
            P3DPlate(px, R, poly, edge, core, 2f);
            // fuller highlight
            DrawLineR(px, R, grip.x + d.x * len * 0.10f, grip.y + d.y * len * 0.10f,
                             tip.x  - d.x * len * 0.22f, tip.y  - d.y * len * 0.22f,
                      Mathf.Max(1.4f, halfW * 0.55f),
                      new Color(1f, 1f, 1f, 0.42f));
        }

        // ── team identity: LIVERY, not a full-body dye ─────────────────────────
        // Tinting every surface made the enemy knight a solid red shape -- allegiance read
        // instantly but the unit stopped being recognisable, and both armies stopped looking
        // like the same art set. Real armies solve this with livery: the armour stays metal,
        // and a few loud pieces (cape, plume, heraldry, sash) carry the colour. Steel keeps
        // only a slight warm/cool bias so the two sides still feel different at a glance.
        static Color ArtSteelBase(P3DP p)
            => Color.Lerp(Art.Metal, p.body, 0.20f);
        static Color ArtSteelHi(P3DP p)
        {
            Color b = ArtSteelBase(p);
            return new Color(Mathf.Min(b.r * 1.55f + 0.16f, 1f),
                             Mathf.Min(b.g * 1.55f + 0.16f, 1f),
                             Mathf.Min(b.b * 1.55f + 0.18f, 1f));
        }
        static Color ArtSteelDk(P3DP p)
        {
            Color b = ArtSteelBase(p);
            return new Color(b.r * 0.46f, b.g * 0.48f, b.b * 0.54f);
        }
        static Color ArtSteelFar(P3DP p)
        {
            Color b = ArtSteelBase(p);
            return new Color(b.r * 0.28f, b.g * 0.30f, b.b * 0.36f);
        }
        /// <summary>Full-strength team colour — for cloth, heraldry and glows only.</summary>
        static Color ArtLivery(P3DP p)
            => new Color(Mathf.Min(p.body.r * 1.12f, 1f),
                         Mathf.Min(p.body.g * 1.02f, 1f),
                         Mathf.Min(p.body.b * 1.02f, 1f));
        static Color ArtLiveryDk(P3DP p)
        {
            Color l = ArtLivery(p);
            return new Color(l.r * 0.42f, l.g * 0.36f, l.b * 0.40f);
        }

        // ART SETS: the fixed, non-team colours, per theme.
        // These used to be `static readonly` constants tuned for Medieval, and that single fact
        // was what stopped any other theme reusing a Medieval builder: the geometry was fine,
        // the hardcoded gold and leather were not. They are a lookup now, so a theme can adopt
        // an existing silhouette and repaint it -- which is exactly what Solar Forge does.
        //
        //   Metal : base tone for ArtSteel* before the 20% team bias
        //   Trim  : the "gold" role -- banding, crests, claw settings
        //   Strap : the "leather" role -- grips, belts, bindings
        //   Cloth : fixed (non-livery) fabric
        //   Rune  : the emissive accent -- eyes, seams, enchantment
        struct ArtSet
        {
            public Color Metal, Trim, TrimHi, TrimDk, Strap, Cloth, ClothDk, Rune, Outline;
        }

        static readonly ArtSet[] _artSets =
        {
            // 0 CYBER - gunmetal, cyan emissive
            new ArtSet { Metal = new Color(0.545f,0.600f,0.680f),
                Trim = new Color(0.42f,0.78f,0.90f), TrimHi = new Color(0.72f,0.95f,1.00f), TrimDk = new Color(0.16f,0.38f,0.50f),
                Strap = new Color(0.20f,0.23f,0.28f), Cloth = new Color(0.16f,0.26f,0.38f), ClothDk = new Color(0.07f,0.12f,0.19f),
                Rune = new Color(0.40f,0.90f,1.00f), Outline = new Color(0.040f,0.055f,0.090f,1f) },
            // 1 SYNTHWAVE - chrome, magenta emissive
            new ArtSet { Metal = new Color(0.660f,0.680f,0.740f),
                Trim = new Color(0.86f,0.46f,0.95f), TrimHi = new Color(1.00f,0.80f,1.00f), TrimDk = new Color(0.40f,0.12f,0.50f),
                Strap = new Color(0.18f,0.14f,0.26f), Cloth = new Color(0.30f,0.12f,0.44f), ClothDk = new Color(0.14f,0.05f,0.22f),
                Rune = new Color(0.95f,0.40f,1.00f), Outline = new Color(0.050f,0.030f,0.080f,1f) },
            // 2 BIOPUNK - bone chitin, acid emissive
            new ArtSet { Metal = new Color(0.560f,0.560f,0.470f),
                Trim = new Color(0.78f,0.74f,0.48f), TrimHi = new Color(0.94f,0.92f,0.72f), TrimDk = new Color(0.36f,0.34f,0.16f),
                Strap = new Color(0.34f,0.26f,0.18f), Cloth = new Color(0.22f,0.34f,0.16f), ClothDk = new Color(0.09f,0.16f,0.07f),
                Rune = new Color(0.40f,1.00f,0.45f), Outline = new Color(0.030f,0.055f,0.035f,1f) },
            // 3 MEDIEVAL - the original values; do not drift
            new ArtSet { Metal = new Color(0.585f,0.625f,0.700f),
                Trim = new Color(0.91f,0.70f,0.30f), TrimHi = new Color(1.00f,0.89f,0.60f), TrimDk = new Color(0.54f,0.41f,0.09f),
                Strap = new Color(0.48f,0.32f,0.19f), Cloth = new Color(0.78f,0.20f,0.29f), ClothDk = new Color(0.42f,0.07f,0.15f),
                Rune = new Color(0.50f,0.85f,1.00f), Outline = new Color(0.055f,0.045f,0.085f,1f) },
            // 4 INDUSTRIAL - soot iron, copper trim, furnace emissive
            new ArtSet { Metal = new Color(0.470f,0.485f,0.500f),
                Trim = new Color(0.78f,0.52f,0.26f), TrimHi = new Color(0.96f,0.76f,0.48f), TrimDk = new Color(0.40f,0.24f,0.10f),
                Strap = new Color(0.32f,0.24f,0.16f), Cloth = new Color(0.34f,0.36f,0.34f), ClothDk = new Color(0.15f,0.16f,0.16f),
                Rune = new Color(1.00f,0.55f,0.16f), Outline = new Color(0.045f,0.048f,0.055f,1f) },
            // 5 SAKURA - lacquer and gold leaf
            new ArtSet { Metal = new Color(0.600f,0.560f,0.600f),
                Trim = new Color(0.93f,0.78f,0.40f), TrimHi = new Color(1.00f,0.94f,0.72f), TrimDk = new Color(0.52f,0.38f,0.14f),
                Strap = new Color(0.36f,0.16f,0.22f), Cloth = new Color(0.80f,0.24f,0.36f), ClothDk = new Color(0.36f,0.08f,0.16f),
                Rune = new Color(1.00f,0.62f,0.82f), Outline = new Color(0.060f,0.030f,0.055f,1f) },
            // 6 SOLAR - brass over blackened iron, molten emissive.
            //   This row IS the Solar art set: Medieval geometry repainted, no new builders.
            new ArtSet { Metal = new Color(0.680f,0.530f,0.240f),
                Trim = new Color(1.00f,0.82f,0.36f), TrimHi = new Color(1.00f,0.95f,0.74f), TrimDk = new Color(0.52f,0.32f,0.05f),
                Strap = new Color(0.26f,0.18f,0.12f), Cloth = new Color(0.86f,0.34f,0.08f), ClothDk = new Color(0.40f,0.12f,0.02f),
                Rune = new Color(1.00f,0.62f,0.18f), Outline = new Color(0.070f,0.040f,0.015f,1f) },
            // 7 DAWN - pale silver, linen, soft light
            new ArtSet { Metal = new Color(0.640f,0.670f,0.740f),
                Trim = new Color(0.82f,0.80f,0.66f), TrimHi = new Color(0.98f,0.97f,0.90f), TrimDk = new Color(0.44f,0.44f,0.38f),
                Strap = new Color(0.40f,0.34f,0.26f), Cloth = new Color(0.72f,0.74f,0.82f), ClothDk = new Color(0.34f,0.36f,0.44f),
                Rune = new Color(0.62f,0.76f,1.00f), Outline = new Color(0.035f,0.042f,0.070f,1f) },
        };

        static int ArtThemeIdx => Mathf.Clamp(GameSettings.ThemeIndex, 0, _artSets.Length - 1);
        static ArtSet Art => _artSets[ArtThemeIdx];

        // Call sites are unchanged: these names resolve through the active theme's set now.
        static Color ArtOutline => Art.Outline;
        static Color ArtGold    => Art.Trim;
        static Color ArtGoldHi  => Art.TrimHi;
        static Color ArtGoldDk  => Art.TrimDk;
        static Color ArtCloth   => Art.Cloth;
        static Color ArtClothDk => Art.ClothDk;
        static Color ArtLeather => Art.Strap;
        static Color ArtRune    => Art.Rune;

        // — shaded capsule along an ARBITRARY axis —
        // P3DCylinder is vertical-only, which is why limbs could never swing: a walk cycle in
        // side profile needs thighs and shins at real angles. Same Blinn-Phong model, with the
        // surface normal taken from the perpendicular offset to the segment.
        static void P3DLimb(Color[] px, int R, float x0, float y0, float x1, float y1, float r,
            Color col, float metallic, float smoothness, Vector3 L, Vector3 H, Vector3 fillL)
        {
            float ax = x1 - x0, ay = y1 - y0;
            float len = Mathf.Sqrt(ax * ax + ay * ay);
            if (len < 0.001f) return;
            float ux = ax / len, uy = ay / len;      // axis
            float pxn = -uy, pyn = ux;               // in-plane perpendicular
            int bx0 = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - r - 1));
            int bx1 = Mathf.Min(R - 1, (int)(Mathf.Max(x0, x1) + r + 1));
            int by0 = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - r - 1));
            int by1 = Mathf.Min(R - 1, (int)(Mathf.Max(y0, y1) + r + 1));
            float specPow = Mathf.Lerp(8f, 160f, smoothness);

            for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++)
            {
                float vx = x + .5f - x0, vy = y + .5f - y0;
                float t  = Mathf.Clamp(vx * ux + vy * uy, 0f, len);
                float ddx = vx - ux * t, ddy = vy - uy * t;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d >= r) continue;

                float off = (ddx * pxn + ddy * pyn) / r;          // -1..1 across the limb
                float dz  = Mathf.Sqrt(Mathf.Max(0f, 1f - off * off));
                var N = new Vector3(pxn * off, pyn * off, dz);

                float diff = Mathf.Max(0f, Vector3.Dot(N, L));
                float fill = Mathf.Max(0f, Vector3.Dot(N, fillL)) * 0.18f;
                float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), specPow);
                float rim  = Mathf.Pow(Mathf.Max(0f, 1f - N.z), 3f) * 0.12f;

                Color lit = col * (0.15f + diff * 0.85f + fill)
                          + Color.white * (spec * Mathf.Lerp(0.05f, 0.85f, metallic) * smoothness)
                          + new Color(0.35f, 0.45f, 0.65f, 0f) * rim;
                lit.a = col.a * Mathf.Clamp01((r - d) * 1.6f);
                P3DSet(px, R, x, y, lit);
            }
        }

        static void P3DCylinder(Color[] px, int R, float cx, float yBot, float yTop, float r,
            Color col, float metallic, float smoothness, Vector3 L, Vector3 H, Vector3 fillL)
        {
            int x0 = Mathf.Max(0,(int)(cx-r-1)), x1 = Mathf.Min(R-1,(int)(cx+r+1));
            int y0 = Mathf.Max(0,(int)yBot),     y1 = Mathf.Min(R-1,(int)yTop);
            float r2 = r * r;
            float specPow = Mathf.Lerp(8f, 160f, smoothness);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x+.5f-cx;
                if (dx*dx >= r2) continue;
                float dz = Mathf.Sqrt(r2 - dx*dx);
                var N = new Vector3(dx/r, 0f, dz/r); // cylinder normal (XZ only)

                float diff  = Mathf.Max(0f, Vector3.Dot(N, L));
                float fill  = Mathf.Max(0f, Vector3.Dot(N, fillL)) * 0.18f;
                float spec  = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), specPow);
                float rim   = Mathf.Pow(Mathf.Max(0f, 1f - N.z), 3f) * 0.12f;

                float edgeFade = Mathf.Min(
                    Mathf.Clamp01((yTop - (y+.5f)) * 1.2f),
                    Mathf.Clamp01(((y+.5f) - yBot) * 1.2f));

                Color lit = col * (0.15f + diff * 0.85f + fill)
                          + Color.white * (spec * Mathf.Lerp(0.05f, 0.85f, metallic) * smoothness)
                          + new Color(0.30f, 0.38f, 0.60f, 0f) * rim;
                lit.a = col.a * Mathf.Clamp01((r - Mathf.Abs(dx)) * 1.4f) * edgeFade;
                P3DSet(px, R, x, y, lit);
            }
        }

        // — flat chest plate overlay (nearly viewer-facing) —
        static void P3DChestOverlay(Color[] px, int R, float cx, float cy, float hw, float hh,
            Color col, float metallic, float smoothness, Vector3 H)
        {
            // Flat panel normal pointing slightly upper-left toward key light
            var N = new Vector3(-0.08f, 0.10f, 0.99f).normalized;
            float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(N, H)), Mathf.Lerp(12f, 140f, smoothness));
            Color lit = col * 0.9f + Color.white * (spec * Mathf.Lerp(0.02f, 0.45f, metallic));

            for (int y = (int)(cy-hh); y <= (int)(cy+hh); y++)
            for (int x = (int)(cx-hw); x <= (int)(cx+hw); x++)
            {
                float xf = 1f - Mathf.Abs(x+.5f-cx)/hw;
                float yf = 1f - Mathf.Abs(y+.5f-cy)/hh;
                float s  = xf * xf * yf;
                if (s < 0.01f) continue;
                var c = lit; c.a = s * 0.72f;
                P3DSet(px, R, x, y, c);
            }
        }

        // — visor band with emissive glow —
        // ── stride-aware primitives (Set/DrawLine/FillPoly hardcode S=64; these take R) ──

        /// <summary>Circle outline in R space. Concentric rings are the Solar set's core motif.</summary>
        static void DrawRingR(Color[] px, int R, float cx, float cy, float r, float w, Color c)
        {
            const int SEG = 28;
            float pxp = cx + r, pyp = cy;
            for (int i = 1; i <= SEG; i++)
            {
                float a = i * (Mathf.PI * 2f / SEG);
                float nx = cx + Mathf.Cos(a) * r, ny = cy + Mathf.Sin(a) * r;
                DrawLineR(px, R, pxp, pyp, nx, ny, w, c);
                pxp = nx; pyp = ny;
            }
        }

        static void FillCircleR(Color[] px, int R, float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(R - 1, (int)(cx + r));
            int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(R - 1, (int)(cy + r));
            float r2 = r * r;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) P3DSet(px, R, x, y, c);
                }
        }

        static void DrawLineR(Color[] px, int R, float x0, float y0, float x1, float y1, float w, Color c)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f) { FillCircleR(px, R, x0, y0, w * 0.5f, c); return; }
            int steps = Mathf.Max(2, (int)(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillCircleR(px, R, x0 + dx * t, y0 + dy * t, w * 0.5f, c);
            }
        }

        static void FillPolyR(Color[] px, int R, Vector2[] pts, Color c)
        {
            if (pts.Length < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; }
            int n = pts.Length;
            for (int scanY = Mathf.Max(0, (int)minY); scanY <= Mathf.Min(R - 1, (int)maxY + 1); scanY++)
            {
                float fy = scanY + 0.5f;
                var xs = new List<float>();
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    if ((pts[i].y <= fy && pts[j].y > fy) || (pts[j].y <= fy && pts[i].y > fy))
                        xs.Add(pts[i].x + (fy - pts[i].y) / (pts[j].y - pts[i].y) * (pts[j].x - pts[i].x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Mathf.Max(0, (int)xs[k]); x <= Mathf.Min(R - 1, (int)xs[k + 1]); x++)
                        P3DSet(px, R, x, scanY, c);
            }
        }

        static void P3DVisor(Color[] px, int R, float cx, float cy, float r, Color visorCol, Color glowCol)
        {
            float vy   = cy + r * 0.08f;  // slightly above head center
            float vHalf = r * 0.22f;
            float r2   = r * r;

            // Emissive visor band
            for (int y = (int)(vy-vHalf); y <= (int)(vy+vHalf); y++)
            for (int x = (int)(cx-r*.92f); x <= (int)(cx+r*.92f); x++)
            {
                float dx = x+.5f-cx, dy = y+.5f-cy;
                if (dx*dx + dy*dy >= r2) continue;
                float dz = Mathf.Sqrt(r2 - dx*dx - dy*dy);
                float xf = 1f - Mathf.Abs(dx)/(r*.88f);
                float yf = 1f - Mathf.Abs(y+.5f-vy)/vHalf;
                float s  = xf * yf;
                Color c  = Color.Lerp(visorCol * 0.55f, visorCol, s);
                c = Color.Lerp(c, Color.white, dz/r * dz/r * 0.55f * s); // specular centre
                c.a = Mathf.Clamp01(s * 0.96f);
                P3DSet(px, R, x, y, c);
            }

            // Soft glow halo around visor
            for (int y = (int)(vy-vHalf*3f); y <= (int)(vy+vHalf*3f); y++)
            for (int x = (int)(cx-r); x <= (int)(cx+r); x++)
            {
                float dx = x+.5f-cx, dy = y+.5f-cy;
                if (dx*dx + dy*dy >= r2) continue;
                float yDist = Mathf.Abs(y+.5f-vy);
                if (yDist <= vHalf * 1.1f) continue;
                float gs = Mathf.Max(0f, 1f - yDist/(vHalf*3f));
                float xs = Mathf.Max(0f, 1f - Mathf.Abs(dx)/(r*.88f));
                var c = glowCol; c.a = gs * gs * xs * 0.28f;
                P3DSet(px, R, x, y, c);
            }
        }

        // — platform: 3D-look pedestal —
        static void P3DPlatform(Color[] px, int R, float cx, float cy, Color col)
        {
            float tw = 30f, th = 5f;  // half-width, half-height of top face
            Color top  = new Color(Mathf.Min(col.r*1.4f,1f), Mathf.Min(col.g*1.4f,1f), Mathf.Min(col.b*1.4f,1f), 1f);
            Color side = new Color(col.r*0.45f, col.g*0.45f, col.b*0.45f, 1f);
            Color rim  = new Color(Mathf.Min(col.r*1.7f,1f), Mathf.Min(col.g*1.7f,1f), Mathf.Min(col.b*1.7f,1f), 1f);

            // Side face (below top edge)
            for (int y = (int)(cy - th); y < (int)cy; y++)
            for (int x = (int)(cx - tw + 2); x <= (int)(cx + tw - 2); x++)
            {
                float ef = Mathf.Clamp01(1f - Mathf.Abs(x+.5f-cx)/tw * 1.1f);
                var c = side; c.a = ef;
                P3DSet(px, R, x, y, c);
            }

            // Top face
            for (int y = (int)cy; y <= (int)(cy + th); y++)
            for (int x = (int)(cx - tw); x <= (int)(cx + tw); x++)
            {
                float ef = Mathf.Clamp01(1f - Mathf.Abs(x+.5f-cx)/tw);
                float yf = (y - cy) / th;
                var c = Color.Lerp(top, rim, yf); c.a = ef * ef * 0.5f + ef * 0.5f;
                P3DSet(px, R, x, y, c);
            }

            // Bright top rim line
            int ry = (int)(cy + th);
            for (int x = (int)(cx - tw + 1); x <= (int)(cx + tw - 1); x++)
            {
                float ef = Mathf.Clamp01(1f - Mathf.Abs(x+.5f-cx)/tw);
                var c = rim; c.a = ef * ef * 0.85f;
                P3DSet(px, R, x, ry, c);
            }
        }

        // — add emission colour to dark/shadow areas —
        static void P3DShadowTint(Color[] px, int R, Color tint, float str)
        {
            for (int i = 0; i < px.Length; i++)
            {
                float a = px[i].a;
                if (a < 0.05f) continue;
                float lum = px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f;
                float t   = (1f - lum) * str * a;
                px[i] = new Color(
                    Mathf.Min(px[i].r + tint.r * t, 1f),
                    Mathf.Min(px[i].g + tint.g * t, 1f),
                    Mathf.Min(px[i].b + tint.b * t, 1f), a);
            }
        }

        // — Inferno-specific: glowing lava crack lines on torso —
        static void P3DInfernoOverlay(Color[] px, int R, Color crackCol)
        {
            // Shadow tint first (warm red in dark areas)
            P3DShadowTint(px, R, crackCol, 0.40f);

            // Crack lines on torso region
            float[] xs = { 57f, 63f, 71f };
            foreach (float bcx in xs)
            {
                for (int y = 64; y <= 88; y++)
                {
                    float wobble = Mathf.Sin(y * 0.42f + bcx * 0.18f) * 1.8f;
                    int ix = (int)(bcx + wobble);
                    float fade = Mathf.Sin((y - 64f) / 24f * Mathf.PI);
                    // Core crack pixel
                    var core = crackCol; core.a = fade * fade * 0.95f;
                    P3DSet(px, R, ix, y, core);
                    // Soft glow around crack
                    for (int ddx = -3; ddx <= 3; ddx++)
                    {
                        if (ddx == 0) continue;
                        var gc = crackCol; gc.a = core.a * (1f - Mathf.Abs(ddx)/4f) * 0.38f;
                        P3DSet(px, R, ix + ddx, y, gc);
                    }
                }
            }
        }

        // ── VFX sprite textures (white alpha-only shapes, tinted at runtime) ──

        /// Soft flame tongue: bright full-width base tapering to transparent tip.
        public static Sprite FlameLick()   => MkSprite("flame_lick", BuildFlameLick,   new Vector2(0.5f, 0f));
        /// Soft radial glow dot: white center fading to transparent edge.
        public static Sprite SparkDot()    => MkSprite("spark_dot",  BuildSparkDot,    new Vector2(0.5f, 0.5f));
        /// Horizontal sweep stripe: bright at centre, transparent at top and bottom.
        public static Sprite ChromeSweep() => MkSprite("chrome_sw",  BuildChromeSweep, new Vector2(0.5f, 0.5f));
        /// Full-area soft radial glow, white tinted.
        public static Sprite GlowCircle()  => MkSprite("glow_wht",   BuildGlowWhite,   new Vector2(0.5f, 0.5f));

        /// <summary>
        /// White annulus for the radial cooldown meter. Image.Type.Filled needs a real Sprite --
        /// a RawImage cannot do radial fill -- so the ring is built once and tinted per card.
        /// </summary>
        public static Sprite CooldownRing() => MkSprite("cd_ring", BuildCooldownRing, new Vector2(0.5f, 0.5f));

        static Texture2D BuildCooldownRing()
        {
            const int RS = 128;
            var px = new Color[RS * RS];
            float c = RS * 0.5f - 0.5f;
            float rOut = RS * 0.47f, rIn = RS * 0.36f;
            for (int y = 0; y < RS; y++)
            for (int x = 0; x < RS; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                // 1px soft edge on both rims so the ring does not alias into a cog
                float a = Mathf.Clamp01(rOut - d) * Mathf.Clamp01(d - rIn + 1f);
                px[y * RS + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            var tex = new Texture2D(RS, RS, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Sprite MkSprite(string texKey, System.Func<Texture2D> build, Vector2 pivot)
        {
            var t = Get(texKey, build);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, 100f);
        }

        static Texture2D BuildFlameLick()
        {
            const int FW = 32, FH = 64;
            var px = new Color[FW * FH];
            for (int y = 0; y < FH; y++)
            {
                float vy        = (float)y / (FH - 1);                              // 0 = bottom, 1 = top
                float heightA   = (1f - vy) * (1f - vy);                            // strong at base, fades up
                float halfWidth = Mathf.Lerp(0.88f, 0.06f, Mathf.Pow(vy, 0.65f));  // wide base → narrow tip
                for (int x = 0; x < FW; x++)
                {
                    float vx     = Mathf.Abs((x + 0.5f) / FW - 0.5f);              // 0=centre, 0.5=edge
                    float edgeA  = halfWidth > 0.001f
                                   ? Mathf.Clamp01(1f - (vx / halfWidth) * (vx / halfWidth))
                                   : 0f;
                    float core   = Mathf.Clamp01(1f - vx / Mathf.Max(0.001f, halfWidth * 0.35f))
                                   * (1f - vy * 0.8f);                              // bright column down center
                    float a      = Mathf.Clamp01(heightA * edgeA + core * 0.25f);
                    px[y * FW + x] = new Color(1f, 1f, 1f, a);
                }
            }
            var t = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D BuildSparkDot()
        {
            const int SD = 24;
            var px = new Color[SD * SD];
            float m = SD / 2f;
            for (int y = 0; y < SD; y++)
                for (int x = 0; x < SD; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(m, m)) / m;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;  // cubic falloff — sharp luminous centre
                    px[y * SD + x] = new Color(1f, 1f, 1f, a);
                }
            var t = new Texture2D(SD, SD, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D BuildChromeSweep()
        {
            const int CW = 64, CH = 12;
            var px = new Color[CW * CH];
            for (int y = 0; y < CH; y++)
            {
                float vy = (y + 0.5f) / CH;
                float a  = 4f * vy * (1f - vy);   // bell curve, peak at vertical centre
                a = a * a;
                for (int x = 0; x < CW; x++)
                    px[y * CW + x] = new Color(1f, 1f, 1f, a);
            }
            var t = new Texture2D(CW, CH, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D BuildGlowWhite()
        {
            var px = new Color[S * S];
            float m = S / 2f;
            for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(m, m)) / m;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * S + x] = new Color(1f, 1f, 1f, a * a);
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── glow map ─────────────────────────────────────────────────────────

        static Texture2D BuildGlow(Color c)
        {
            var px = new Color[S * S];
            float m = S / 2f;
            for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(m, m));
                    float a    = Mathf.Clamp01(1f - dist / m);
                    px[y * S + x] = new Color(c.r, c.g, c.b, a * a);
                }
            return Make(px);
        }

        // ── drawing primitives ───────────────────────────────────────────────

        static Color[] Blank() => new Color[S * S]; // all transparent

        static Texture2D Make(Color[] px)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode   = TextureWrapMode.Clamp;
            return t;
        }

        static void Set(Color[] px, int x, int y, Color src)
        {
            if (x < 0 || x >= S || y < 0 || y >= S) return;
            int i   = y * S + x;
            var dst = px[i];
            float oa = src.a + dst.a * (1f - src.a);
            if (oa < 0.001f) { px[i] = new Color(0, 0, 0, 0); return; }
            px[i] = new Color(
                (src.r * src.a + dst.r * dst.a * (1f - src.a)) / oa,
                (src.g * src.a + dst.g * dst.a * (1f - src.a)) / oa,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) / oa,
                oa);
        }

        static void FillCircle(Color[] px, float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(S - 1, (int)(cx + r + 1));
            int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(S - 1, (int)(cy + r + 1));
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    float aa   = Mathf.Clamp01(r - dist + 0.7f);
                    if (aa > 0f) Set(px, x, y, A(c, c.a * aa));
                }
        }

        static void DrawRing(Color[] px, float cx, float cy, float r, float w, Color c)
        {
            float outer = r + w * 0.5f + 1f;
            int x0 = Mathf.Max(0, (int)(cx - outer)), x1 = Mathf.Min(S - 1, (int)(cx + outer));
            int y0 = Mathf.Max(0, (int)(cy - outer)), y1 = Mathf.Min(S - 1, (int)(cy + outer));
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    float aa   = Mathf.Clamp01(w * 0.5f - Mathf.Abs(dist - r) + 0.7f);
                    if (aa > 0f) Set(px, x, y, A(c, c.a * aa));
                }
        }

        static void Glow2D(Color[] px, float cx, float cy, float r, Color c)
        {
            float rad = r * 1.6f;
            int x0 = Mathf.Max(0, (int)(cx - rad)), x1 = Mathf.Min(S - 1, (int)(cx + rad));
            int y0 = Mathf.Max(0, (int)(cy - rad)), y1 = Mathf.Min(S - 1, (int)(cy + rad));
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    float t    = Mathf.Max(0f, 1f - dist / r);
                    if (t > 0f) Set(px, x, y, A(c, c.a * t * t));
                }
        }

        static void FillRect(Color[] px, float lx, float ly, float w, float h, Color c)
        {
            if (w < 0) { lx += w; w = -w; }
            if (h < 0) { ly += h; h = -h; }
            int x0 = Mathf.Max(0, (int)lx),       x1 = Mathf.Min(S - 1, (int)(lx + w - 0.01f));
            int y0 = Mathf.Max(0, (int)ly),       y1 = Mathf.Min(S - 1, (int)(ly + h - 0.01f));
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    Set(px, x, y, c);
        }

        static void DrawLine(Color[] px, float x0, float y0, float x1, float y1, float w, Color c)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f) { FillCircle(px, x0, y0, w * 0.5f, c); return; }
            int steps = Mathf.Max(2, (int)(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillCircle(px, x0 + dx * t, y0 + dy * t, w * 0.5f, c);
            }
        }

        static void FillPoly(Color[] px, Vector2[] pts, Color c)
        {
            if (pts.Length < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; }
            int n = pts.Length;
            for (int scanY = Mathf.Max(0, (int)minY); scanY <= Mathf.Min(S - 1, (int)maxY + 1); scanY++)
            {
                float fy = scanY + 0.5f;
                var xs = new List<float>();
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    if ((pts[i].y <= fy && pts[j].y > fy) || (pts[j].y <= fy && pts[i].y > fy))
                        xs.Add(pts[i].x + (fy - pts[i].y) / (pts[j].y - pts[i].y) * (pts[j].x - pts[i].x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Mathf.Max(0, (int)xs[k]); x <= Mathf.Min(S - 1, (int)xs[k + 1]); x++)
                        Set(px, x, scanY, c);
            }
        }

        static void FillStar(Color[] px, float cx, float cy, float r1, float r2, int n, Color c)
        {
            var pts = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                float ang = i * Mathf.PI / n - Mathf.PI / 2f;
                float r   = (i % 2 == 0) ? r1 : r2;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            FillPoly(px, pts, c);
        }

        // ── polygon helpers ──────────────────────────────────────────────────

        static Vector2[] HexRotated(float cx, float cy, float r, float rotDeg)
        {
            float rotRad = rotDeg * Mathf.Deg2Rad;
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + rotRad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Vector2[] Hex(float cx, float cy, float r)
        {
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Vector2[] Tri(float cx, float cy, float r)
        {
            var pts = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * Mathf.PI / 3f - Mathf.PI / 2f;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        // ── cache + utilities ────────────────────────────────────────────────

        static Texture2D Get(string key, System.Func<Texture2D> build)
        {
            if (!_cache.TryGetValue(key, out var t) || t == null)
                _cache[key] = t = build();
            return t;
        }

        static Color  A(Color c, float a)        => new Color(c.r, c.g, c.b, a);
        static Color  Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
        static string CK(Color c)                => $"{c.r:F2}{c.g:F2}{c.b:F2}";
    }
}
