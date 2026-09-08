# NEON WARFARE — Rescue Redesign (v2.0)
**Document 09 · 2026-06-12 · status: CONTROLLING DOCUMENT — supersedes v1.0 suite where they conflict**

> Engagement framing: a studio inherited a Unity prototype and a v1.0 design suite. The suite (docs 00–08) is largely sound. The prototype ignored it. This document is the post-mortem of the build, the amendments to the vision, and the plan to ship.

Three systems in your brief do **not** exist anywhere in the v1.0 suite and are designed fresh here: **lane ownership / dynamic frontline** (Phase 3), **territory-conquest campaign** (Phase 6), and the **cosmetic metagame** (Phase 7).

---

# PHASE 1 — CRITIQUE OF THE CURRENT BUILD

The game has exactly one screen (Battle, procedurally built in `BattleScene.BuildUI()`). Review of that screen and the code beneath it.

## 1.1 The Battle screen

### Visual problems
- **It is a debug screen.** Flat untextured `Image` rects, one built-in font (LegacyRuntime.ttf), no sprites, no shaders, no particles, no lighting, no post-processing. The GDD's art bible (Part V — "darkness is the canvas, light is the paint") is 0% implemented.
- **Units are 11–22 px colored rectangles** with a 3 px HP strip. No silhouette, no animation, no facing, no walk cycle, no attack telegraph, no death state — units blink out of existence.
- **Gems are flat colored squares.** No icon, no silhouette differentiation (colorblind players are locked out), no idle motion, no depth.
- **No bases.** The "cores" are HP bars in the HUD plus `◄ ►` text glyphs in the lanes. The thing you're defending is invisible.
- **Side-by-side layout wastes the screen.** The board occupies a 500 px column; the battlefield is a letterboxed strip of four gray bands. Neither half looks designed; both look like editor gizmos.
- **Zero brand.** Screenshot this and nobody could name the game, the genre, or the decade.

**Visual score: 1.5/10** (the half point is for the consistent dark palette and the per-type color coding, which show intent).

### Gameplay problems
- **No lane decision.** Player deploys to a random/auto row (`Combat.Spawn` picks placement); enemy spreads across 3 ground rows. The player literally cannot answer the game's only strategic question — *which lane?* This is the single biggest design hole.
- **No frontline, no territory.** Units pass through each other's positions until ranges overlap; the battlefield state is invisible between engagements.
- **No formations, no counters in practice.** The class triangle exists in `UnitSpec` but with 5 fallback units and no anti-class bonuses surfaced, fights resolve as HP-pile arithmetic.
- **Board and battlefield are strangers.** A match pays +N resources. That's the entire crossover. No 4-match laser, no combo strikes, nothing the GDD §3.3 promised. Matching feels like data entry.
- **No progression of any kind.** One endless skirmish, no waves, no escalation announcement, no protocols, no campaign, no meta. Win/lose → "CLICK TO RESTART".
- **Economy is opaque.** Five resources, but the player can't tell what's scarce, what each unit drains, or why a match of green mattered. (Current testing values — 99999 core HP, 30/20/20 start — are debug scaffolding and must be reverted.)

**Gameplay score: 2.5/10** (the deterministic sim and the atomic swap-tape are genuinely good bones; what's built on them is a tech demo).

### UX problems
- **Click-click swap with no drag**, no hover state, no invalid-swap explanation (it silently snaps back), no hint system, no reshuffle messaging.
- **Deploy buttons are text labels** ("TROOPER\n0+5P"). Cost legibility requires parsing a string. No cooldown, no queue feedback, no "can't afford" animation — just a gray-out.
- **No pause, no settings, no volume, no quit.** `R` to restart is undocumented on screen.
- **Defeat screen teaches nothing** (GDD VII.3 requires cause-of-death analytics).
- **Nothing is glanceable.** Resource boxes are 13 px text; core bars are unlabeled until read.

**UX score: 2/10**.

### Technical problems
- **All UI is hand-built in code** — ~700 lines of `new GameObject()` + anchor math across 4 view classes. Unmaintainable past prototype scale; every visual tweak is a recompile.
- **`_views` dictionary keyed by unit list index** in `BattlefieldView` — if `CombatSim.Units` ever compacts or reorders, views attach to the wrong units. Fragile contract.
- **uGUI legacy `Text`** throughout (no TextMeshPro), no sprite atlas, no object pooling for units (only floaters are pooled).
- **`DontDestroyOnLoad` + RuntimeInitialize bootstrap** is a clever testing hack but the wrong production lifecycle (no scene flow, no loading, no state machine).
- **What is genuinely good and must be kept:** `NW.Board.Domain` and `NW.Combat.Domain` are engine-free (`noEngineReferences: true`), deterministic, tick-based, and event-taped. `BoardModel.TrySwap()` returning an ordered event tape is exactly the right architecture for driving animation. The fixed-tick accumulator with deltaTime cap is correct. ~30% of the codebase is keeper; it's the 30% nobody can see.

**Technical score: 5/10** (domain layer 8/10, presentation layer 2/10).

| Area | Score | One-line verdict |
|---|---|---|
| Visual identity | 1.5/10 | Debug gizmos, not a game |
| Game feel / juice | 1/10 | Swap-slide and a floater; everything else is instant |
| Battlefield gameplay | 2/10 | No lane choice = no strategy |
| Board gameplay | 4/10 | Solid match-3 core, zero spectacle, zero crossover |
| UX | 2/10 | Unlabeled, unexplained, unpausable |
| Code architecture | 5/10 | Excellent domain, disposable presentation |
| Content / progression | 0.5/10 | One infinite skirmish |
| **Overall** | **2/10** | **Correct skeleton, no flesh — rebuild presentation, keep simulation** |

---

# PHASE 2 — GAME VISION (v2 amendments)

The v1.0 pillars (GDD §1.2) survive review. Two amendments:

## 2.1 Pillars (amended)
1. **TWO BRAINS, ONE CLOCK** — unchanged.
2. **EVERY MATCH IS AN EVENT** — unchanged.
3. **THE GRID IS ALIVE** — unchanged.
4. **PERMANENT FORWARD MOTION** — unchanged.
5. **PREMIUM MEANS RESPECT** — unchanged. **Zero MTX, permanently. $14.99.**
6. **NEW — THE MAP TELLS THE SCORE.** At any frozen frame, the battlefield itself must show who is winning: lane tint, frontline position, base damage state. If you need to read the HUD to know the score, the battlefield failed. *(Source of the Kingdom Rush / Into the Breach feel: state lives in the world, not the chrome.)*

## 2.2 The feel, by reference
- **Civibattle** — the soul: match-3 feeds a live war.
- **Clash Royale** — lane readability, deploy cards, tug-of-war tension.
- **Kingdom Rush** — visible territory, structures, charm under pressure.
- **Into the Breach** — legible cause and effect; you always know *why* you lost.
- **Puzzle Quest** — matching with consequence beyond income.

**Match length target: 6–9 minutes.** Three escalation acts per match (see 3.7). A session = 1–3 territories on the conquest map.

## 2.3 What v2 changes vs v1.0 suite (summary of deltas)
| Topic | v1.0 said | v2 says |
|---|---|---|
| Battlefield rows | 3 depth rows + air, no ownership | **5 discrete lanes with ownership + dynamic frontline** (Phase 3) |
| Campaign | 39 linear missions, 3 sectors | **20-territory conquest map, 5 regions, 3 enemy factions** (Phase 6) |
| Cosmetics | skins "via achievements", one line | **Full earnable cosmetic system: banners, unit/tile/battlefield skins** (Phase 7) |
| Layout | 55/45 vertical split | Ratified and detailed (3.1) — the current side-by-side build is the deviation |
| Heroes/Protocols | full systems at 1.0 | Kept, but pushed post-MVP (Phase 9 ranking) — lanes and juice come first |

---

# PHASE 3 — THE NEW BATTLEFIELD

## 3.1 Screen layout (ratified: vertical stack)

Your instinct matches GDD §3.2 — the side-by-side build is the mistake. Battlefield over board:

```
┌──────────────────────────────────────────────────────────────┐
│ TOP BAR · resources (5 meters) · combo · wave/act indicator   │  6%
├──────────────────────────────────────────────────────────────┤
│                                                              │
│   ENEMY BASE ▓▓                                  LANE 5 AIR  │
│  ┌────────┐ ═════════◄═══frontline═══════════ ┌────────┐    │
│  │ HELIOS │ ═════════════◄═════════════════── │ PLAYER │    │  49%
│  │  CORE  │ ════════◄════════════════════════ │ NEXUS  │    │
│  │ (right)│ ══════════════════◄══════════════ │ (left) │    │
│  └────────┘ ═══════◄═════════════════════════ └────────┘    │
│              LANES 1–4 GROUND  (tinted by ownership)         │
├──────────────────────────────────────────────────────────────┤
│ COMMAND STRIP · 6 unit cards w/ cost+cooldown · hero · pause  │  9%
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                 COMPILER BOARD  8×8                          │  36%
│            (centered, holographic frame)                     │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

- **Battlefield top, board bottom** — eyes flick vertically (cheap), thumb/mouse lives on the board, deploys via the strip between them. The strip is the hinge of the whole game: cards sit *between* income and spending.
- Player base **left**, enemy **right** (keeps current sim convention, `X` 0→100).
- 16:9 desktop: board is centered with side gutters used for combat log (left) and protocol/buff icons (right). The layout is natively portrait-friendly for a future mobile port.

## 3.2 Camera
- Single orthographic camera, gentle perspective cheat via lane Y-offset + scale (far lanes 6% smaller) — 2.5D parallax without 3D cost.
- **Idle drift:** ±8 px slow sine, 12 s period. The frame never sits still (Pillar 3).
- **Punch-ins:** 4% zoom toward big events (elite kill, base hit, lane capture), 0.25 s out, 0.6 s return.
- **Shake budget:** trauma-based (shake = trauma², decays 1.5/s) so stacked events don't become soup. Caps per accessibility sliders.
- No player camera control in battle. The whole war is always visible — Into the Breach rule.

## 3.3 Lanes & ownership (NEW SYSTEM)

**5 lanes: 4 ground + 1 air.** Each ground lane is a discrete horizontal track with three visible layers:

1. **Floor tint** — lane floor is a circuit-board strip that glows **cyan from the player edge** and **red-orange from the enemy edge**, meeting at the frontline. Ownership is literally painted on the ground.
2. **Frontline marker** — a vertical energy seam where the tints meet, crackling. It *moves*.
3. **Control pylons** — each ground lane has 2 capture pylons at 33% and 66%. A pylon converts after a team holds its zone uncontested for 5 s (visible capture ring fills).

### Frontline math
Per lane, `frontline = smoothed average X of the furthest-forward sustained presence`, clamped between owned pylons. Holding ground advances it ~2 px/s; winning a fight there snaps it forward 5%. It is a **score display**, not a wall — units pass it freely. Its job is Pillar 6: glance = score.

### What ownership pays
| Held | Effect |
|---|---|
| Pylon (each) | Forward **spawn point** unlocks in that lane (deploy at the pylon instead of base) + lane vision flare |
| 2 pylons (lane controlled) | +10% move speed for your units in that lane; lane floor pulses your color |
| 3+ lanes controlled | **Surge:** enemy core shield drops 15%; klaxon + map-wide tint shift announces it |
| Air lane | No pylons — air superiority instead grants spotting (+10% ranged damage all ground lanes) |

This converts "deploy units, wait" into **map play**: split-push to steal pylons, stack a lane for Surge, defend your forward spawns.

## 3.4 Deployment & spawn logic
- **Cards spawn into a chosen lane:** click card → lane highlights → click lane (or drag card onto lane; hotkeys 1–4 then Q/W/E/R for lane). One extra click vs today, infinite strategy gained.
- Spawn at your **rearmost owned point** in that lane (base, or forward pylon if held) with the materialize VFX (wireframe → solid → flash, 0.3 s).
- Cards have **resource cost + compile cooldown** (radial sweep on card, 2–10 s per unit class) — no dump-spam.
- Enemy director (budget-curve, already implemented) gains **lane intelligence**: weights spawns toward its weakest frontline 60% / strongest push 40%, so it both defends and snowballs. Telegraph: incoming wave shows lane-edge warning chevrons 2 s before spawn.

## 3.5 Formations & combat flow
- Each lane has **3 file slots** (front/mid/back). Melee units fill front first; ranged hold mid; support back. Arriving units slide into the open slot rather than stacking on one pixel — instantly reads as a *squad*, costs only a slot-assignment pass, no real pathfinding.
- **Pathfinding is 1D per lane** (it's a lane game — keep it that way): advance → engage at range → fight → push. Air lane ignores ground engagements except vs anti-air.
- Class triangle gets teeth: +35% damage vs countered class, floating **"COUNTER!"** popup in class color so the rule teaches itself.
- Engagements produce a brief **clash VFX zone** (sparks, tracers) at the contact point — fights look like fights even with simple units.

## 3.6 Structures & base evolution
- **Turrets are placed, not spawned:** turret card → click an owned pylon socket. Turrets defend the pylon; lose the pylon, lose the turret (it de-rezzes). This makes turrets territorial, not lane-cloggers.
- **Bases are buildings, not HP bars.** The Nexus is a 3-tier structure occupying the lane terminus:
  - **100–66% HP:** pristine — antennas rotate, shield shimmer, idle drones orbit
  - **65–33% HP:** damaged — arc-sparks, a cracked panel, shield flickers
  - **<33% HP:** critical — fire glow, alarm strobe, audible klaxon heartbeat
- Mid-match base **evolution upward** too: each Surge you trigger adds a visible armor ring to your Nexus. Winning *looks like building*.

## 3.7 Match flow (3 acts)
| Act | Time | Director budget | Beat |
|---|---|---|---|
| 1 · Skirmish | 0–2 min | low ramp | learn enemy comp, take first pylons |
| 2 · Escalation | 2–5 min | +60%, elites appear | lane trades, Surge race |
| 3 · Overrun | 5–9 min | +120%, boss-pattern waves | someone's core cracks |

Act transitions are announced (full-width banner sweep + music layer change). No more invisible metronome.

---

# PHASE 4 — THE NEW MATCH BOARD

## 4.1 Resource identity (decision)
Keeping the established cyberpunk five — your Biomass/Psionics example reads bio-fantasy, and the HELIOS fiction is already load-bearing across all docs:

| Resource | Color | Silhouette (colorblind-safe) | Fuels |
|---|---|---|---|
| **Energy** | Electric cyan | hex-bolt | universal — every deploy |
| **Plasma** | Magenta | flame-orb | air units, offensive abilities |
| **Nano** | Neon green | tri-swarm | heavies, turrets, repairs |
| **Quantum** | Violet | spinning cube | specials, ultimates |
| **Data** | Amber | glyph-shard | hackers; surplus → meta currency |

Every gem is a faceted holographic crystal with a unique silhouette, ±2 px idle bob with per-gem phase offset, refraction shader. **Never color-only.**

## 4.2 Matching feeds the war (the crossover contract)
Every match does **two** things — pays resources AND touches the battlefield:

| Match | Resource payout | Battlefield effect |
|---|---|---|
| 3-match | +N, particle stream arcs from cleared gems **up the screen into the resource meter** | meter flash; nearest friendly units get a 0.5 s sheen (cosmetic morale ping) |
| 4-match | +N ×1.5 + **Line Laser** special tile | on detonation, a visible laser fires **across a battlefield lane** — 15 dmg to enemies in the matching lane (H-match → lane = match row mod 4) |
| L/T-match | Cross Laser tile | detonation clears row+col AND strikes 2 lanes |
| 5-match | **Singularity** | swap-detonate: clears color; battlefield shockwave slows all enemies 1.5 s |
| Combo ×5 | — | **Orbital Strike** auto-targets strongest enemy (telegraph ring → impact 1 s later) |
| Combo ×8 | — | **EMP** — all enemies stunned 2 s, full-screen chromatic pulse |

The particle stream from board → meter → (on spend) meter → card → spawn is **one continuous visual supply line**. The player watches their economy physically flow. This is the single highest-value juice item in the game.

## 4.3 Board animations (full list)
- **Hover:** 1.06× scale, rim glow, soft tick (80 ms in).
- **Select:** gem lifts 4 px, pulse ring, neighbors dim 10%.
- **Swap:** 0.14 s slide with squash 0.9→1.05→1.0, motion trail streak.
- **Invalid swap:** slide + bounce-back with horizontal shake (3 px, 2 cycles) + dull "denied" blip + the *reason* if relevant ("NO MATCH").
- **Clear:** gem shatters into 12 voxels that fly toward the resource meter as the trail; +N popup at the centroid, pitch-laddered pop (+1 semitone per combo step).
- **Fall:** 0.08 s/cell, tiles land with 1-frame squash; column staggers 30 ms.
- **Refill:** new gems drop from above the frame, already lit.
- **Cascade:** combo counter punches up at screen edge (×2! ×3!), board edge-light brightens per step.
- **Deadlock:** "RESHUFFLING SECTOR" hologram sweep, gems dissolve and re-deal in 0.5 s.
- **Idle 6 s:** one valid move shimmers faintly (hint, off in Veteran+).

Board downtime is dead air — total swap-to-playable is ≤0.4 s for a simple match.

---

# PHASE 5 — GAME JUICE PASS

Contract format: every action ships with all five channels or it isn't done. Target ≥8/10 felt satisfaction; the listed spec is the minimum.

| Action | Animation | Sound | VFX | UI feedback | Camera |
|---|---|---|---|---|---|
| Tile select | lift 4 px + pulse ring 0.12 s | crisp tick | rim glow | neighbors dim | — |
| Tile swap | 0.14 s slide, squash & trail | whoosh | streak | — | — |
| Match | voxel shatter 12/gem | crystalline pop, pitch ladder | resource trail to meter | +N popup, meter flash | 2 px board shake |
| Combo (per step) | combo counter punch 1.2× | riser layer + semitone up | board edge-light step | ×N badge slams in | shake +1 px/step (cap 6) |
| Resource gain | meter liquid-fills | soft chime per type | trail impact sparkle | number ticks up (not jumps) | — |
| Unit spawn | wireframe→solid 0.3 s, ground ring | compile chirp (per class) | materialize particles | card punch + cooldown radial starts | — |
| Unit attack | weapon anim + recoil 2 px | per-weapon layer | muzzle flash / tracer / beam | dmg number (crit 1.4×, orange) | 40 ms hitstop if dmg ≥50 |
| Unit death | voxel de-rez burst + negative ghost 0.1 s | glitch crunch | dissolve shader | kill-feed tick (side gutter) | elite: 0.3 s slow-mo 0.4× + 5% punch-in (max 1/5 s) |
| Building construction | turret assembles in 3 parts, bottom-up 0.6 s | servo + clunk ×3 | weld sparks | pylon socket ring completes | 3% punch-in |
| Lane capture (pylon) | capture ring fills → pylon color-flips with flare | rising hum → claim stinger | floor tint floods 0.8 s from pylon | lane banner "LANE 2 SECURED" | 4% punch toward pylon |
| Base damage | tier-appropriate damage state (3.6) | impact thud; <33%: klaxon heartbeat | arc sparks, smoke | screen-edge red pulse, HP bar segment shatters | trauma +0.3 |
| Victory | time 0.3× → enemy core overload → voxel supernova → grade stamp (★★★ slams in one by one) | riser → detonation → fanfare | screen-wide shockwave, confetti voxels | results panel slides up with count-up rewards | dolly toward core, letterbox |
| Defeat | connection-glitch shader sweep, screen "de-rezzes" | signal-loss crunch, music cuts to one synth note | scanline collapse | **cause-of-death analytics** (damage by lane/type graph) | freeze + desaturate |

**Global motion rules** (GDD §4.3 ratified): nothing teleports; everything eases (150–250 ms out-cubic in, 100–150 ms in); idle states breathe; all timings live in one `MotionConfig` ScriptableObject, hot-tweakable.

---

# PHASE 6 — CAMPAIGN: THE LIBERATION MAP

Replaces v1.0's 39 linear missions. **Conquest map of Neo-Arcadia: 20 territories, 5 regions, 3 enemy factions.**

## 6.1 Structure
```
        NEO-ARCADIA GRID MAP
┌───────────────────────────────────┐
│  R5 THE CORE (4) ── HELIOS-PRIME  │   Region (territories) — Faction
│        ▲                          │
│  R3 FOUNDRY (4)   R4 SKYLINE (4)  │   R3: WARDEN  R4: MIRROR
│        ▲               ▲          │
│  R1 SHALLOWS (4)  R2 NEON DST (4) │   R1: WARDEN  R2: MIRROR
│        └──── START ────┘          │
└───────────────────────────────────┘
```
- **5 regions × 4 territories = 20 flags.** Adjacency graph, not a line: usually 2–3 legal targets, player picks their front.
- **3 factions** = the HELIOS sub-minds, each with a doctrine you feel in every battle of their territory:
  - **WARDEN** (R1, R3) — fortifier: extra enemy turrets/pylons start enemy-owned; boss firewalls a lane. Teaches: split-push.
  - **MIRROR** (R2, R4) — copyist: periodically deploys copies of *your* last units; boss copy-rate doubles. Teaches: comp discipline.
  - **HELIOS-PRIME** (R5) — corruptor: injects locked/virus tiles into your board mid-fight. Teaches: board hygiene under pressure. Final boss.
- Each territory = one bespoke battle: fixed map skin, modifier set, enemy comp, optional bonus objective ("win holding 3 lanes", "no air units").
- **Counterattacks:** after every 2 conquests, one owned border territory gets raided — defend (shorter, defensive setup) or lose its bonus until retaken. The map breathes; conquest isn't a checklist.

## 6.2 Conquest rewards (every flag pays)
| Reward type | Examples (one guaranteed per territory, fixed & previewable) |
|---|---|
| Unit unlock | new deployable (12 units across the map; ~1 per 1–2 territories early) |
| Building | turret variants, pylon upgrades, Nexus armor tiers |
| Resources | meta-currency (Data surplus) for the tech tree |
| Cosmetic | region-themed: banner, tile skin, unit skin, battlefield skin (Phase 7) |
| Story event | 2-line briefing + faction commander taunt/banter; region finale = boss cutscene-lite (in-engine, 15 s) |

Region clear = **region keystone**: a permanent account perk (e.g. R1: +1 starting pylon owned) + the region's showcase cosmetic. Map shows owned territory glowing cyan — the campaign screen *is* Pillar 6 at macro scale.

**3-star grading per territory** (time / core HP / combo peak) for replay; stars gate nothing required, only bonus cosmetics. Difficulty selectable per territory (Story → Blackwall).

---

# PHASE 7 — STEAM METAGAME (100% EARNABLE, ZERO MTX)

Everything below is earned by play. Nothing is purchasable. Permanently.

## 7.1 Commander profile
- **Commander Level** (account XP from any battle, win or lose — Pillar 4). Level milestones pay cosmetics, never power.
- **Profile card:** banner + title + 3 showcased achievements + favorite unit skin. Shown on victory/defeat screens and the campaign map.

## 7.2 Cosmetic tracks
| Track | Count at 1.0 | Sources |
|---|---|---|
| **Banners** (profile/Nexus flag) | 25 | region clears, achievements, level milestones |
| **Unit skins** (palette+trim variants, later bespoke) | 24 (2/unit) | territory 3-stars, faction mastery |
| **Tile skins** (gem sets: e.g. *Retro Arcade*, *Glass*, *Faction salvage*) | 6 sets | region keystones, seasonal challenges |
| **Battlefield skins** (map themes: *Shallows dawn*, *Core meltdown*) | 5 | region 100% completion |
| **Titles** | 30 | achievements |

Rule: cosmetics must never reduce readability (skins keep silhouette + class trim color).

## 7.3 Achievements
~40 total, mirrored to Steam. Three families:
- **Progression** (clear regions, beat factions on Veteran+) — the spine.
- **Mastery** ("EMP 3 times in one match", "win without losing a pylon", "win a match using only Assault") — build experiments.
- **Delight** ("watch your Nexus survive at <5% HP", "de-rez 10,000 units lifetime") — long-tail.

No achievement requires grinding losses or playing badly.

## 7.4 Seasonal challenges (free, light-touch)
- **Weekly Breach seed:** endless mode with a fixed seed + 2 modifiers; friend/global leaderboard. Top-percentile = seasonal banner variant.
- **Monthly gauntlet:** 3 curated territory remixes. Completing pays one exclusive tile skin per season — *exclusive in art, never in function*.
- All seasonal content rotates back into an earnable archive after 6 months (no FOMO economy — premium means respect).

---

# PHASE 8 — TECHNICAL ARCHITECTURE (v2)

## 8.1 Keep / kill from the current build
| Verdict | Code |
|---|---|
| **KEEP** | `NW.Board.Domain` (BoardModel, event tape, specials), `NW.Combat.Domain` (CombatSim, fixed tick, director), `Rng`, tests, `noEngineReferences` discipline, tick accumulator + deltaTime cap |
| **EXTEND** | `CombatSim`: lanes 4+1, pylon/ownership state, slot formations, counter bonuses — all inside the engine-free domain, all event-taped |
| **KILL** | All four `*View` procedural-UI classes, `BattleScene.BuildUI()`, legacy `Text`, index-keyed view dictionary, RuntimeInitialize bootstrap-as-lifecycle |

## 8.2 Assemblies
```
NW.Board.Domain      (no engine)  ← exists
NW.Combat.Domain     (no engine)  ← exists, extend: LaneState, PylonState, FormationSlots
NW.Meta.Domain       (no engine)  NEW: campaign graph, conquest state, cosmetic inventory, save model
NW.Data              ScriptableObjects: UnitDef, FactionDef, TerritoryDef, CosmeticDef,
                                        MotionConfig, JuiceConfig, SoundTheme, DirectorCurve
NW.Presentation      battle views, VFX, audio routing — consumes domain EVENTS only
NW.UI                screens, HUD, cards (UITK or uGUI+TMP prefabs — never code-built layout)
NW.App               composition root, GameStateMachine, SaveService, SceneFlow
```

## 8.3 The contract that keeps it clean
**Domains emit event tapes; presentation consumes them.** `TrySwap` already does this. `CombatSim.Tick()` gets the same treatment: `UnitSpawned`, `UnitMoved`, `AttackLanded(dmg, isCrit, isCounter)`, `UnitDied(isElite)`, `PylonProgress`, `PylonCaptured`, `LaneSurge`, `CoreDamaged(tier)`, `ActChanged`, `BattleEnded`. Every juice item in Phase 5 binds to exactly one event. No view ever polls domain state except for interpolation (positions). This makes the entire feel layer testable, replayable (record the tape = replay system for free), and rewrites-proof.

Per-unit views become **pooled prefabs keyed by stable `UnitId`** (domain assigns ids; never list indices).

## 8.4 Other decisions
- **uGUI + TextMeshPro prefabs** for battle HUD (designers tweak in-editor); **UI Toolkit acceptable** for meta screens (map, profile). No more code-built layout, period.
- **DOTween** (or PrimeTween) for all motion, durations sourced from `MotionConfig` SO.
- **Addressables** for skins/themes (cosmetics are data, hot-swappable — the `SoundTheme` pattern from GDD VI generalized to `VisualTheme`).
- **Save:** versioned JSON (conquest graph state, unlocks, cosmetics, settings) via `SaveService` with migration hooks; Steam Cloud.
- **GameStateMachine:** Boot → MainMenu → CampaignMap → Loadout → Battle → Results → CampaignMap. The DontDestroyOnLoad hack dies here.
- **Performance budget:** 60 fps min-spec with 48 active units + full board cascade + 200 particles; pooling mandatory for units, projectiles, floaters, voxels.

---

# PHASE 9 — IMPLEMENTATION PLAN

## 9.1 Releases
| Release | Definition of done | Content |
|---|---|---|
| **MVP** (prove fun) | One battle that strangers replay voluntarily 3+ times | New layout, 5 lanes + pylons + frontline, lane-targeted deploy, 6 units, counter triangle, full board juice (Phase 4), spawn/attack/death feedback, 3-act director, win/lose sequences, pause/settings |
| **Vertical Slice** (prove the game) | One region (4 territories) indistinguishable from shipped quality | Campaign map (R1 only), WARDEN faction doctrine + boss, conquest rewards incl. first cosmetics, unit roster 9, base evolution, full juice contract, save system, FTUE territory |
| **Early Access** | Worth $14.99 today | 3 regions / 12 territories, 2 factions complete, 12 units, turrets/structures, profile + achievements (Steam), Breach endless, tile/unit skins live, Deck verified |
| **1.0 Steam** | The boxed promise | All 20 territories, HELIOS-PRIME, heroes (4) + protocols (60) if fun-gates pass, seasonal challenge pipeline, full cosmetic catalog, 25 min OST, localization EFIGS+zh |

## 9.2 Feature ranking (Impact / Complexity / Priority)
| # | Feature | Impact | Complexity | Priority |
|---|---|---|---|---|
| 1 | Vertical layout rebuild (battlefield/strip/board) | 10 | 4 | **P0** |
| 2 | Lane ownership + pylons + frontline | 10 | 6 | **P0** |
| 3 | Lane-targeted deployment + cards w/ cooldown | 9 | 4 | **P0** |
| 4 | Board juice pass (shatter, trails, combo ladder) | 9 | 5 | **P0** |
| 5 | Combat event tape + presentation rebuild (prefabs, pooling) | 8 | 6 | **P0** (enables 4,6,7) |
| 6 | Unit spawn/attack/death feedback (materialize, hitstop, de-rez) | 9 | 5 | **P0** |
| 7 | Board→battlefield crossover (lasers, orbital, EMP) | 9 | 5 | **P1** |
| 8 | 3-act director + wave telegraphs | 7 | 3 | **P1** |
| 9 | Bases as evolving structures | 7 | 4 | **P1** |
| 10 | Win/defeat sequences + defeat analytics | 7 | 3 | **P1** |
| 11 | Formations (3-slot files) + counter popups | 6 | 3 | **P1** |
| 12 | Campaign map + conquest state (R1) | 8 | 6 | **P1** |
| 13 | Turret/pylon structures | 6 | 4 | **P2** |
| 14 | Save system + state machine + scene flow | 7 | 4 | **P2** (needed by 12) |
| 15 | Cosmetic system + first skins | 6 | 5 | **P2** |
| 16 | Achievements + profile | 5 | 3 | **P2** |
| 17 | Counterattack raids | 5 | 4 | **P3** |
| 18 | Breach endless + weekly seed | 6 | 4 | **P3** |
| 19 | Heroes | 7 | 8 | **P3** (fun-gate: only if MVP loop is already 8/10 without them) |
| 20 | Protocols (roguelite drafts) | 7 | 7 | **P3** (same gate) |
| 21 | Seasonal challenge pipeline | 4 | 4 | **P4** |
| 22 | Mobile-portrait layout variant | 5 | 5 | **post-1.0** |

**The rescue rule:** nothing from P1+ starts until P0 is felt-complete. P0 is one sentence: *make matching feel incredible and make lanes mean something.* That is the entire difference between the current build and a game.

---
*Supersedes conflicting sections of docs 01–08 (notably 01 §3.2/3.4, 05 campaign structure, 08 metagame). Next step: greenlight P0, then backlog grooming in doc 06.*
