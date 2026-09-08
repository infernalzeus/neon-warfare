# NEON WARFARE — Juice Bible v2 (Phase 5)
**Document 13 · 2026-06-12 · the feel contract · supersedes GDD Part IV**

**The rule:** a feature without its juice row is not done. VFX/audio/UI treat every row below as acceptance criteria. Target felt-satisfaction **≥8/10** per action (measured: playtest exit survey, per-action Likert).

---

## 1. Global motion law

1. **Nothing teleports.** In: slide+fade 150–250 ms ease-out-cubic. Out: 100–150 ms ease-in.
2. **Idle ≠ static.** Panels scanline-shimmer (8 s loop), gems bob, cores breathe, lanes flow.
3. **Anticipation → action → settle.** Every animation ≥2 of the three phases; big moments get all three.
4. **One trauma knob.** All camera shake through the trauma system (doc 11 §7); designers add trauma, never raw shake.
5. **Pitch ladders.** Repeating events within a window climb +1 semitone per step (combos, kill streaks); reset on window end. This is the cheapest dopamine in audio.
6. **Sidechain hierarchy.** Ultimate/EMP/victory duck music −6 dB; core-critical klaxon ducks SFX −3 dB.
7. **Every timing in `MotionConfig` / every trauma value in `JuiceConfig`** (SOs, hot-tweakable in play mode).

## 2. Master feel table

Channels: **A**nimation · **S**ound · **V**FX · **U**I · **C**amera. Trauma values in (parens).

### Board
| Action | A | S | V | U | C |
|---|---|---|---|---|---|
| Tile select | lift 4 px, ring pulse 0.12 s | crisp tick | rim glow, neighbors dim | — | — |
| Tile swap | 0.14 s slide, squash, trail | whoosh | streak particles | — | — |
| Match (3) | voxel shatter 12/gem | crystalline pop, pitch ladder | **resource trail → meter** | +N popup, meter flash+fill | (0.05) |
| Special create | transform 0.25 s out-back | charge-up shimmer | persistent aura | tile glows in card-strip color | — |
| Laser detonate | row/col sweep 0.3 s | laser charge+fire | **beam crosses BOTH screens** into lane | lane flash | (0.12), 30 ms hitstop |
| Singularity | implosion→explosion 0.5 s | bass drop + riser | black-hole suck, shockwave ring | — | (0.2), 80 ms white flash* |
| Combo step | counter punch 1.2× | riser layer +semitone | board edge-light steps up | ×N badge slams | (+0.03/step) |
| Combo ×5 | orbital telegraph 1 s → impact | klaxon + boom | strike column, ground scorch fade 3 s | "ORBITAL STRIKE" banner | (0.25) |
| Combo ×8 EMP | radial chromatic pulse | sub-bass EMP, music ducks | full-screen aberration 0.4 s, all enemies stun-sparks | "EMP" mega-banner | (0.5), rumble burst |
| Resource gain | meter liquid-fill | soft chime per type | trail impact sparkle | number ticks (never jumps) | — |
| Invalid swap | bounce-back, 3 px shake ×2 | dull denied blip | — | "NO MATCH" microcopy 0.6 s | — |
| Reshuffle | dissolve/re-deal 0.5 s | data-static sweep | hologram sweep | "RESHUFFLING SECTOR" | — |

*All full-screen flashes capped by photosensitivity setting (§4).

### Battlefield
| Action | A | S | V | U | C |
|---|---|---|---|---|---|
| Unit spawn | wireframe→solid 0.3 s | compile chirp per class | ground ring, materialize particles | card punch, cooldown radial starts, **meter→card drain** | — |
| Unit advance | walk cycle, slight bob; column march for overflow | low movement layer per class (mixed quiet) | engine glow / footstep dust | — | — |
| Unit attack | weapon anim + 2 px recoil | per-weapon layer | muzzle flash / tracer / beam | dmg number (crit ×1.4 orange; counter ring) | 40 ms hitstop if ≥50 dmg (0.1) |
| Counter hit | — | distinct crunch | class-color impact flare | "COUNTER" popup | — |
| Unit death | de-rez voxel burst + negative ghost 0.1 s | glitch crunch | dissolve shader, voxels obey lane gravity | kill-feed tick (gutter) | — |
| Elite kill | slow-mo 0.3 s @0.4× | choir-stab | shock ring | elite frame shatters | 5% punch-in (0.15), max 1/5 s |
| Pylon capturing | ring fills around pylon | rising hum | standoff strobe if contested | lane-edge progress glint | — |
| **Pylon flip** | pylon color-flips with flare | claim stinger | **floor tint floods 25 u/s from pylon** | "LANE n — NODE TAKEN" banner | 4% punch (0.2) |
| Lane controlled | floor pulse loop | low power-up hum | edge lights solid | lane tag lights | — |
| **SURGE** | enemy core shield visibly thins | escalating klaxon + music layer bump | map-wide tint shift wave | "SURGE — CORE EXPOSED" | (0.3) |
| Turret build | 3-part assembly 0.6 s bottom-up | servo ×3 + clunk | weld sparks | socket ring completes | 3% punch |
| Wave telegraph | lane-edge chevrons pulse 2 s | klaxon tick ×2 | — | wave icon on lane tag | — |
| Act change | full-width banner sweep | music layer change + stinger | background weather shifts | "ACT II — ESCALATION" | — |
| Core hit (yours) | tier-state per doc 11 §6.2 | impact thud; <33% klaxon heartbeat | arc sparks, smoke | screen-edge red pulse, HP segment shatters | (0.3) |
| Core hit (enemy) | same tiers | crunch + brittle crack | panel debris | HP segment shatters | forward 3% punch |

### Meta / flow
| Action | A | S | V | U | C |
|---|---|---|---|---|---|
| **Victory** | time 0.3× → core overload → voxel supernova → ★ stamps slam one-by-one | riser → detonation → fanfare | screen shockwave, voxel confetti | results slide up, rewards **count up** | dolly to core, letterbox 8% |
| **Defeat** | glitch shader sweep, screen de-rezzes | signal-loss crunch; music collapses to one synth note | scanline collapse | **cause-of-death analytics**: damage by lane × minute graph, "lost at L2, 4:10" | freeze + desaturate |
| Territory conquered (map) | flag voxel-builds, district lights cyan block-by-block 1.5 s | liberation motif (3 notes, region-variant) | grid-light propagation | reward cards flip | map camera ease to flag |
| Unlock (any) | item card flips + shine sweep | unlock chime | — | "NEW" badge propagates to its menu | — |
| Button hover/press | 1.04× glow 80 ms / 0.96× squash | tick / thock | press ring-burst | — | — |
| Screen transition | panels slide+fade per motion law | whoosh layer | data-stream wipe | — | — |

## 3. Audio architecture
- **Music:** darksynth, vertical layers: L0 menu/map · L1 act-1 · L2 act-2 · L3 act-3/boss. Crossfade 2 s on act change. Victory/defeat stingers override.
- **SFX buses:** Board / Battlefield / UI / Voice — independent mixer sliders. Pitch ladders computed per-bus.
- **Announcer (mission control):** "Node taken." "Wave inbound — lane three." "Core integrity critical." "Surge active." ≤4 words, cooldown 6 s per line class, priority queue (core > surge > wave > node).
- **SoundTheme SO** keeps everything hot-swappable (*Neon* default, *Retro Arcade* unlockable — doc 15).

## 4. Accessibility (feel without harm)
- Sliders: screen shake 0–100%, flash intensity 0–100%, hitstop on/off, announcer on/off.
- **Photosensitivity mode:** caps full-screen flashes to 20% alpha, disables chromatic pulses (EMP becomes a ring sweep).
- Slow-mo events never alter sim outcome (presentation-only time scale).
- All juice driven from the event tape ⇒ reduced-motion mode is a *filter*, not a fork.

## 5. Budget & discipline
- Particle budget: ≤200 alive @1080p min-spec; pooled emitters; voxel bursts LOD to 6/gem under load.
- Banner cooldown 1.5 s — banners queue, never stack.
- Trauma cap 1.0; popup cap 30; slow-mo max 1/5 s; full-screen effects max 1/3 s. Juice that overlaps becomes noise — the caps *are* the taste.

*Acceptance: a muted, blurred playtest video of any 30 s of battle must still communicate match quality, lane state, and who is winning (Pillar 2 + 6 joint test).*
