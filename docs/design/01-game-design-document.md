# NEON WARFARE — Game Design Document
**Document 01 of 09 · Pre-production suite · v1.0 · 2026-06-12**

> *"Two screens. One war. Every match you make is a bullet downrange."*

---

# PART I — VISION

## 1.1 High concept

**NEON WARFARE** is a premium futuristic strategy game where a **match-3 reactor board** generates the energy that fuels a **real-time auto-battle** on a digital battlefield above it. You are a Netrunner Commander fighting to liberate a city-grid from a rogue military AI, mission by mission, while building a base, collecting heroes, climbing a tech tree, and drafting roguelite combat protocols mid-battle.

**One-line pitch:** *Clash Royale's lane combat powered by a match-3 board, dressed in Tron, paced like Vampire Survivors, with Hades' build-drafting.*

## 1.2 Design pillars

Every feature must serve at least one pillar. Features serving none are cut.

1. **TWO BRAINS, ONE CLOCK.** The player constantly arbitrates attention between the board (income) and the battlefield (spending). Tension comes from this split, never from either half alone. *Test: does the feature force a glance at the other screen?*
2. **EVERY MATCH IS AN EVENT.** No silent interactions. Each match produces light, sound, motion, and a number. Bigger inputs produce categorically bigger outputs (a 5-match doesn't pay more — it *detonates*). *Test: would a muted, blurred spectator still feel the moment?*
3. **THE GRID IS ALIVE.** The world is a cyberspace battlefield: holographic UI, circuitry, volumetric neon, data-stream weather. Nothing is static; idle states breathe, hover states answer, transitions perform. *Test: screenshot any frame — does it look like key art?*
4. **PERMANENT FORWARD MOTION.** Every session ends with something kept: tech, hero XP, blueprints, base levels. Defeat pays out too (less). *Test: can a player quit after a loss feeling richer?*
5. **PREMIUM MEANS RESPECT.** No energy timers, no gacha, no paid currency. Difficulty and pacing tuned for fun, not friction. Depth over grind.

## 1.3 Audience & positioning

- **Primary:** PC/Steam players 18–40 who enjoyed *Hades*, *Vampire Survivors*, *Dota Underlords/auto-battlers*, *Puzzle Quest* nostalgics. Session length 15–40 min.
- **Secondary:** premium mobile (post-1.0 port) — the vertical split-screen layout is natively phone-shaped.
- **Comp set:** *Puzzle Quest* (proved match-3 + RPG), *Clash Royale* (lane combat readability), *Hades* (drafting + dying forward), *Marvel Snap* (premium-feel VFX on cards/tiles), *Dome Keeper* (split-attention loop).
- **Differentiator:** nobody currently ships **real-time** match-3 → auto-battle with roguelite drafting and premium presentation. Puzzle Quest is turn-based; auto-battlers have no second input game.

## 1.4 Platform & scope

- **Launch:** Steam (Windows/Linux/Deck-verified), 16:9 & 16:10, mouse + controller + touch-ready.
- **Target:** 60 fps min-spec (GTX 1050 / Steam Deck), 4K-capable.
- **Price:** $14.99 premium, zero MTX (see `08-steam-release-plan.md`).
- **Content at 1.0:** 3 sectors × 13 missions (39), 12 units, 4 heroes, 40-node tech tree, 5 buildings × 5 levels, 60+ protocols, endless mode.

---

# PART II — WORLD & FANTASY

## 2.1 Setting

**Neo-Arcadia, 2097.** The city's administrative AI, **HELIOS**, declared humanity an "inefficiency" and locked the population out of the Grid — the digital substrate running power, water, transit, and defense. You are a **Netrunner Commander**: a hacker-general who dives into the Grid and fights HELIOS's legions with compiled war-programs (your units), powered by raw resource streams you refine by hand (the match board).

The fiction explains every mechanic:
- **Match board** = your *Compiler*: you pattern-match raw data-noise into usable resources in real time.
- **Units** = war-programs you compile and deploy.
- **Heroes** = legendary netrunners who dive alongside you.
- **Base** = your hideout's physical hardware; better hardware, stronger dives.
- **Protocols** (roguelite drafts) = unstable exploit code found mid-dive; it expires when you jack out.
- **Death** = de-rez. Nobody bleeds; everything shatters into voxels and light. (Keeps rating ~E10/T.)

## 2.2 Tone

Confident, stylish, lightly irreverent. *Tron: Legacy* visuals, *Cyberpunk 2077* UI density, Hades-style barked one-liners from heroes ("Compiling violence."). No grimdark, no lore dumps — story delivered in 2-line mission briefings and hero banter.

## 2.3 Antagonists — the Sector Commanders

Each sector is ruled by a HELIOS sub-mind with a mechanical identity (boss = rules violation, not stat wall):

| Sector | Commander | Gimmick |
|---|---|---|
| 1 — The Shallows | **WARDEN** | Periodically firewalls a lane (blocks it); you must split-push |
| 2 — Neon District | **MIRROR** | Copies your last 3 deployed units back at you |
| 3 — The Core | **HELIOS-PRIME** | Corrupts your match board — injects locked/virus tiles mid-fight |

---

# PART III — CORE GAMEPLAY

## 3.1 The loop (one mission, 6–10 minutes)

```
 BRIEF ▶ LOADOUT ▶ ┌──────────────── BATTLE ────────────────┐ ▶ RESULTS ▶ META
                   │  match tiles → bank resources           │
                   │  spend resources → deploy units/abilities│
                   │  survive waves → draft PROTOCOL (×3)     │
                   │  kill enemy CORE before yours falls      │
                   └─────────────────────────────────────────┘
 META: spend rewards on Tech Tree · Base · Heroes · Blueprints → unlock next mission
```

The macro loop: **Fight → Earn → Build → Unlock → Fight stronger.** Full spec of macro systems in `04-economy-design.md` and `05-progression-design.md`.

## 3.2 Screen layout (battle)

Vertical split, tuned 55/45:
- **Top 55% — Battlefield:** side-view lane (2.5D), your **Nexus Core** left, enemy Core right. Three depth rows (near/mid/far) for ground units + an air layer above. Cinemachine camera with subtle drift, punch-ins on ultimates, shake on impacts.
- **Bottom 45% — Compiler (match board):** 8×8 grid, 5 resource gem types + special tiles, holographic frame.
- **Between — Command Strip:** unit deploy cards (with cost/cooldown radials), hero portrait + ultimate charge ring, resource readouts, combo meter.

## 3.3 The Compiler (match system)

### Resources (5 gem types)
| Resource | Color | Icon read | Primary use |
|---|---|---|---|
| **Energy** | Electric cyan | bolt-hex | Universal: every deploy costs Energy |
| **Plasma** | Magenta | flame-orb | Air units, hero actives, offensive abilities |
| **Nano Matter** | Neon green | tri-swarm | Heavy units, repairs, defensive tech |
| **Quantum Cores** | Violet | spinning cube | Special units, ultimates, time effects |
| **Data Fragments** | Amber | glyph-shard | Hacker units; surplus banks to meta currency post-battle |

Rules carried from prototype, re-tuned: 8×8, drag-swap adjacent, 3+ clears, cascades chain with a **combo multiplier** (×1 → ×1.5 → ×2 → ×3 cap), deadlock auto-reshuffle with "RESHUFFLING SECTOR" hologram. Swap 0.14s, clear 0.16s, fall 0.08s/cell — *faster* than prototype; this is a real-time game and board downtime is dead air.

### Special tiles (powerups)
Created by match shape, detonated by matching/swapping them:

| Trigger | Special created | Effect on detonation |
|---|---|---|
| 4 in a row | **Line Laser** (H or V per match orientation) | Clears full row/col; fires a visible laser that also deals light damage across the battlefield lane |
| L / T shape | **Cross Laser** | Clears row + column; X-shaped screen flash |
| 5 in a row | **Singularity (color bomb)** | Swap with any gem: clears all of that color; spawns a brief **black hole** VFX that sucks tiles in |
| 2 specials swapped | **Chain reaction** | Combined effects, e.g. Laser+Laser = double cross; Singularity+Laser = every gem of that color becomes a laser; Singularity+Singularity = **full board wipe + EMP blast** that stuns all enemies 3s |

Rare board events (granted by Protocols/tech, not base shapes): **Plasma Storm** (5 random tiles detonate), **Time Freeze** (board pauses falling, free moves 5s, battlefield continues — pure split-attention spike), **Resource Multiplier** tile (×2 payout zone for 10s).

### Match → battlefield crossover (Pillar 1 & 2)
Matching is never just income:
- Every clear sends a **resource trail** (light-particle stream) arcing from the board up to the HUD counter.
- **4-match:** the line laser visibly fires *through the battlefield*, dealing 15 damage to enemies in a row.
- **5-match/Singularity:** screen-wide shockwave, 1.5s enemy slow.
- **Combo ≥5 chain:** auto-triggers an **Orbital Strike** marker on the strongest enemy (drone swarm flies up from board, strikes after 1s).
- **Combo ≥8:** **EMP blast** — all enemies stunned 2s, full-screen chromatic pulse.

This is the game's signature spectacle: *good puzzle play literally bombards the enemy.*

## 3.4 The Battlefield (auto-battle)

### Deployment
- Player taps a unit card (or hotkey 1–8, or drags onto a lane row) → unit compiles in at the Nexus with a materialize VFX (wireframe → solid → glow flash) and marches right.
- Each card has **cost** (resources) + **compile cooldown** (2–10s radial) so you can't dump-spam one unit.
- Max 24 active units/side (pooled, perf-budgeted).

### Combat model
- Units occupy one of 3 ground rows or the air layer; auto-acquire nearest valid target, respect attack range/cooldown.
- **Class triangle for instant readability:** ASSAULT (fast, anti-special) ◄ beats ► SPECIAL (tricks, anti-heavy) ◄ beats ► HEAVY (tanks, anti-assault). AIR sits outside the triangle: only ranged/air/anti-air hits it.
- Damage numbers float with crit pops; kills de-rez into voxel bursts; hit-stop 40ms on big hits; kill of an elite triggers 0.3s slow-mo at 0.4× (budgeted: max once per 5s).

### Unit roster (12 at launch)
Costs in **E**nergy / **P**lasma / **N**ano / **Q**uantum / **D**ata. CD = compile cooldown. Full math in `04-economy-design.md`.

**ASSAULT**
| Unit | Cost | CD | HP | DPS | Ability |
|---|---|---|---|---|---|
| Drone | 20E | 2s | 60 | 12 | *Swarm Link:* +15% speed per adjacent Drone (max +45%) |
| Shock Trooper | 35E | 3s | 90 | 16 (ranged) | *Overcharge:* every 4th shot chains lightning to 2 extra targets |
| Mech Walker | 60E·10N | 5s | 220 | 22 | *Stomp* (8s CD): AoE knockback + 1s stun in melee radius |

**HEAVY**
| Unit | Cost | CD | HP | DPS | Ability |
|---|---|---|---|---|---|
| Titan | 120E·40N | 8s | 900 | 25 | *Aegis Taunt* (10s): forces enemies in range to target it; projects visible energy shield dome |
| Siege Tank | 100E·30N | 8s | 450 | 40 (long range, AoE shell) | *Deploy Mode:* stationary +50% range after 4s idle |
| Juggernaut | 150E·60N | 10s | 1200 | 35 | *Unstoppable:* immune to stun/knockback; *Detonate* on death (200 AoE) |

**AIR**
| Unit | Cost | CD | HP | DPS | Ability |
|---|---|---|---|---|---|
| Interceptor | 45E·15P | 3s | 80 | 20 | *Dogfighter:* +100% dmg vs air |
| Bomber | 80E·30P | 6s | 150 | 30 (ground AoE) | *Carpet Run:* drops 3-bomb line, then returns to re-arm |
| Gunship | 110E·45P | 8s | 380 | 45 | *Suppression:* targets hit are slowed 20% |

**SPECIAL**
| Unit | Cost | CD | HP | DPS | Ability |
|---|---|---|---|---|---|
| Hacker | 50E·20D | 5s | 70 | 6 | *Subvert* (12s): converts one enemy ≤150 max HP for 8s; disables towers |
| Teleporter | 70E·25Q | 6s | 110 | 14 | *Blink Beacon* (10s): teleports itself + 3 nearest allies 40% forward |
| Chronomancer | 90E·35Q | 9s | 130 | 10 | *Stasis Field* (14s): 30% slow bubble, 4s; ultimate synergy: frozen enemies take +25% dmg |

### Enemy design
Enemies use the same 12 archetypes (corrupted red/orange palette, glitch shader) + sector-exclusive elites (e.g., **Firewall Sentinel** — a moving wall) + 3 bosses (§2.3). Enemy director spawns by **budget curve** (not fixed timer): each mission defines a credit/sec ramp and composition weights, so pacing reads as waves with breathers, not a metronome. Waves announce with klaxon + lane telegraphs.

### Win/Lose
- Destroy enemy Nexus Core → **VICTORY** sequence: time slows, core overloads, voxel-shatter, mission grade (time / core HP / combo peak → ★ to ★★★), reward burst.
- Lose your Core → **CONNECTION LOST** glitch-out; defeat screen *always* shows cause-of-death stats (DPS taken by type, income graph) so losses teach; partial rewards paid.

## 3.5 Heroes (one per battle)

A hero is a persistent character (collection + RPG layer, full spec in `05-progression-design.md`) who fights on the field and bends the rules of both screens.

| Hero | Fantasy | Passive | Active (resource cost) | Ultimate (charge: combat + Quantum matches) |
|---|---|---|---|---|
| **NOVA** | Plasma gunslinger, starter hero | Deployed units gain +10% speed for 3s | *Plasma Volley* (40P): 5-shot barrage on strongest enemy | **Supernova:** screen-clearing plasma wave, 400 AoE dmg, board converts 5 random tiles to Plasma |
| **CYBER MONK** | Zen battle-medic, control | Your Core regens 2 HP/s | *Harmonic Pulse* (35N): heal all allies 15% | **Sanctuary:** 6s invulnerability dome over your half + board Time Freeze |
| **THE ARCHITECT** | Board-warping engineer | 4-matches also create +1 random special tile 20% of the time | *Refactor* (30D): reshuffle board, guaranteed ≥1 special spawns | **Master Plan:** next 3 matches auto-upgrade to Singularities |
| **VALKYRIE-X** | Air-superiority ace | Air units cost −15% | *Strafing Run* (45P): calls 2 free Interceptors | **Orbital Lance:** targeted beam, 600 dmg column + lane burn 5s |

Hero kit design rule: **passive touches deployment, active touches one screen, ultimate touches both screens.** That keeps every hero on-pillar.

## 3.6 Protocols (roguelite layer)

At 25% / 50% / 75% of mission progress (enemy budget spent), the battle slow-mos to 0.2× and offers a **draft: pick 1 of 3 Protocols** — unstable exploits that last until mission end. ~60 at launch across rarities (Common/Rare/Exotic), e.g.:

- *Overclock* — board falls 30% faster
- *Sawtooth Compiler* — every 3rd Drone spawns free
- *Glass Cannon* — units +40% dmg, −25% HP
- *Tithe* — 10% of Energy income converts to Plasma
- *Dead Man's Switch* — your units detonate on death (30 AoE)
- *Mirror Exploit* (Exotic) — first enemy elite each wave spawns a friendly copy
- *Singularity Engine* (Exotic) — 5-matches trigger a real black hole pulling all enemies inward 1s

Protocols are the per-run build variety engine (*Hades* boons). Tech tree can unlock **Protocol Slots** (keep 1 favorite permanently = "Stable Protocol") and reroll charges.

## 3.7 Difficulty & accessibility

- 4 difficulties: Story / Standard / Veteran / **Blackwall** (Veteran+ modifiers, leaderboard grades). Changeable per mission.
- Endless mode (**The Breach**) post-campaign: escalating waves, full Protocol drafting, leaderboards.
- Accessibility: colorblind gem shapes (every gem has unique silhouette, never color-only), screen-shake/flash intensity sliders, photosensitivity mode (caps full-screen flashes), hold-vs-toggle inputs, UI scale 80–130%, full remap.

---

# PART IV — GAME FEEL SPECIFICATION

The "juice bible." Implementation checklist form; VFX/audio teams treat as contract.

## 4.1 Feedback ladder (board)
| Event | Visual | Audio | Haptic/Camera |
|---|---|---|---|
| Hover tile | 1.06× scale, rim glow | soft tick | — |
| Swap | trail streak, squash 0.9→1.05→1 | whoosh | — |
| 3-match | gem shatter (12 voxels), resource trail to HUD, +N popup | crystalline pop, pitch +1 semitone per combo step (resets at combo end) | 2px board shake |
| 4-match | line laser sweep across BOTH screens | laser charge+fire | 6px shake, 30ms hitstop |
| 5-match | singularity implosion→explosion, shockwave ring | bass drop + riser | 10px shake, white flash 80ms (capped by photosensitivity setting) |
| Combo ×5 | orbital strike telegraph + impact | klaxon + impact boom | full-screen 12px shake |
| Combo ×8 | EMP: radial chromatic aberration pulse | sub-bass EMP | controller rumble burst |

## 4.2 Feedback ladder (battlefield)
| Event | Visual | Audio |
|---|---|---|
| Deploy | wireframe materialize 0.3s, ground ring, card punch animation | compile chirp per class |
| Hit | impact flash, damage number (white/orange crit 1.4× size), 40ms hitstop on hits ≥50 | per-weapon layer |
| Kill | voxel de-rez burst, brief negative-color ghost | glitch crunch |
| Elite kill | 0.3s slow-mo 0.4×, camera punch-in 5% | choir-stab |
| Ultimate | letterbox 8%, camera dolly, unique 1.5s sequence | hero VO + theme sting |
| Core hit | screen-edge red pulse, core arc-sparks | warning klaxon (escalates <30% HP) |

## 4.3 Motion rules (global)
- Nothing teleports: every UI element animates in (slide+fade 150–250ms, ease-out-cubic) and out (100–150ms, ease-in).
- Idle ≠ static: panels have scanline shimmer (8s loop), gems bob ±2px (per-gem phase offset), background data-streams drift, core breathes.
- Buttons: hover = glow+1.04× in 80ms; press = 0.96× squash; confirm = ring-burst particle.
- All animation through DOTween with central `MotionConfig` SO (durations/eases tweakable live in debug panel).

---

# PART V — ART DIRECTION

## 5.1 Visual identity
- **Keywords:** Tron Legacy luminance · Cyberpunk 2077 UI density · Marvel Snap material richness.
- **World:** dark blue-black void (#05070F) with volumetric fog; battlefield is a glowing circuit-board plane with parallax city-grid skyline; bloom-driven neon on emissive channels only — discipline: **darkness is the canvas, light is the paint.** If everything glows, nothing glows.
- **Palette (locked):** background #05070F · panel #0B1220 · primary cyan #00E5FF · violet #B026FF · magenta #FF2E97 · success green #00FF9C · warning amber #FFB300 · enemy red-orange #FF3D2E. Player forces cool, enemy forces hot — readable at a glance.
- **Units:** stylized hard-surface mechs/drones, strong silhouettes (readable at 64px), emissive class-color trims, 2D rigs (Spine-style skeletal or Unity 2D Animation) with idle/walk/attack/death minimum.
- **Gems:** faceted holographic crystals, refraction shader, unique silhouette per resource (bolt/orb/triad/cube/shard).
- **Rendering:** URP 2D Renderer, 2D lights, bloom + chromatic aberration (events only) + vignette + film grain (subtle), custom shaders: hologram (scanline+fresnel), glitch (enemy/damage), dissolve (de-rez), heat-distortion (plasma).

## 5.2 Asset plan (premium look, indie budget)
Hybrid pipeline: purchased/commissioned sci-fi unit packs **re-shaded into our locked palette** + custom shaders & VFX (which carry the identity) + custom heroes (the 4 faces of the game get bespoke art). The shader/VFX layer is what makes it ours; this is the Vampire Survivors lesson — feel > fidelity, but we also buy fidelity where it's cheap.

---

# PART VI — AUDIO DIRECTION

- **Music:** synthwave/darksynth, vertical layering — exploration layer (hub), combat layers 1–3 keyed to battle intensity (enemy budget rate), boss themes per commander. ~25 min at 1.0.
- **SFX:** every interaction has voice (see feel ladders). Pitch-ladder combos. Sidechain ducking: ultimates duck music −6dB.
- **VO:** hero barks (deploy/ult/low-HP), HELIOS taunts, mission-control announcer ("Wave inbound." "Core integrity critical.").
- **Hot-swappable themes:** AudioManager resolves all cues through a `SoundTheme` ScriptableObject (music pack + SFX set + UI set). Themes swappable at runtime in Settings — ships with *Neon* (default) and *Retro Arcade* (8-bit) as a delight feature; architecture supports DLC/community packs.

---

# PART VII — UX PRINCIPLES (summary; full spec in 03)

1. Battle HUD: everything glanceable ≤150ms — affordability is color-coded card states, never text-reading.
2. FTUE: 3 guided missions teaching board → deploy → protocols, each <4 min, no walls of text, skippable for veterans.
3. Loss always explains itself (defeat analytics panel).
4. One-screen rule: any meta decision (tech node, upgrade) understandable without leaving the screen — tooltips with full numbers, before/after deltas.
5. Controller-first navigation parity from day one (Deck verification target).

---

# PART VIII — CONTENT SUMMARY AT 1.0

| Content | Count |
|---|---|
| Campaign missions | 39 (3 sectors × 13: 10 standard, 2 elite, 1 boss) |
| Endless mode | 1 (The Breach) + daily modifier seed |
| Units | 12 player (+ skins via achievements) |
| Enemies | 12 mirrored + 6 elites + 3 bosses |
| Heroes | 4, each with 18-node skill tree + ultimate |
| Protocols | 60 |
| Tech tree | 40 nodes / 5 branches |
| Base buildings | 5 × 5 levels with visual evolution |
| Music | ~25 min original + alt theme pack |

Related docs: roadmap `02` · screens/UI `03` · economy `04` · progression `05` · backlog `06` · architecture `07` · Steam plan `08`.
