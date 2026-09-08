# NEON WARFARE — Vision v2 (Phase 2)
**Document 10 · 2026-06-12 · expands doc 09 Phase 2 · supersedes GDD Part I where they conflict**

---

## 1. High concept (v2)

**NEON WARFARE** is a premium real-time strategy game where a match-3 reactor board fuels a five-lane war for territory. Every match you make compiles resources; every resource deploys a unit into a lane you choose; every lane you hold pushes a visible frontline toward the enemy core. Conquer the 20 territories of Neo-Arcadia from three rogue AI factions, earning units, tech, and cosmetics — never buying them.

**One-line pitch:** *Puzzle Quest's matching, Clash Royale's lanes, Kingdom Rush's territory, Into the Breach's legibility — in Tron.*

**Elevator demo:** 90 seconds of play must show: a cascade detonating a line laser through a battlefield lane → a pylon flipping cyan → the frontline surging → the enemy core cracking a panel. If a bystander watching over a shoulder doesn't ask "what game is that?", the demo failed.

## 2. Pillars (final, with kill-tests)

Every feature must pass at least one pillar's test or it is cut. Every pillar has a *violation alarm* — the symptom that tells us we've drifted.

| # | Pillar | Test | Violation alarm |
|---|---|---|---|
| 1 | **TWO BRAINS, ONE CLOCK** | Does it force a glance at the other screen? | Player can stare at one half for 60s without losing |
| 2 | **EVERY MATCH IS AN EVENT** | Muted + blurred, would a spectator still feel the moment? | Any interaction with zero light/sound/motion |
| 3 | **THE GRID IS ALIVE** | Screenshot any frame — key art? | A static idle state anywhere |
| 4 | **PERMANENT FORWARD MOTION** | Can a player quit after a loss feeling richer? | Any session that pays nothing |
| 5 | **PREMIUM MEANS RESPECT** | Would we be proud of this at $14.99 with no DLC? | Any timer, currency pack, or grind wall |
| 6 | **THE MAP TELLS THE SCORE** | Freeze frame, hide the HUD — who's winning? | Player asks "wait, am I winning?" |

## 3. The fantasy

You are a **Netrunner Commander** jacked into the Grid of Neo-Arcadia, 2097, reclaiming the city from HELIOS's three sub-minds district by district. The board is your *Compiler* (refining raw data-noise into munitions); units are war-programs; lanes are data conduits; pylons are routing nodes you subvert; territory on the world map is liberated city-grid, lit cyan block by block.

The fantasy ladder — what the player *feels* at each zoom level:
- **Second-to-second:** "I am fast and precise" (matching under pressure).
- **Minute-to-minute:** "I am a general" (lane reads, counter-picks, pylon plays).
- **Match-to-match:** "I am a liberator" (a flag turns cyan on the map).
- **Week-to-week:** "I am a collector-veteran" (skins, banners, mastery, Blackwall grades).

## 4. Audience & positioning (v2 refresh)

- **Primary:** Steam, 18–40, players of *Hades*, *Kingdom Rush*, *Slay the Spire*, *Puzzle Quest*, auto-battlers. Sessions 15–45 min. They own 200+ games; they buy on a GIF.
- **The GIF test (marketing-as-design constraint):** the combo-×8 EMP, the Singularity board-wipe, and a lane-capture surge must each be a self-explanatory 6-second GIF. These three moments are built *to be clipped*.
- **Comp set deltas:** Puzzle Quest is turn-based (we're real-time); Clash Royale is PvP/MTX (we're solo/premium); Kingdom Rush is fixed-path TD (our lanes are contested both ways); auto-battlers have no second input game (our board is one).
- **Differentiator in one line:** *the only game where puzzle skill is artillery.*

## 5. Experience targets

| Metric | Target |
|---|---|
| Match length | 6–9 min (Story may end at 5, Blackwall may reach 11) |
| Decisions per minute | ≥8 (swaps count; lane choices count double) |
| Time to first "wow" in a fresh install | <90 s (first 4-match laser is scripted into FTUE territory 1) |
| Campaign length | 12–18 h first clear (Standard), 30+ h to 100% |
| Session shape | 1–3 territories, clean exit points after every battle |
| Felt difficulty | losses attributable to a decision ≥80% of the time (defeat analytics must show the lane/minute the game was lost) |

## 6. Emotional arc of one match

```
 act 1 SKIRMISH      act 2 ESCALATION        act 3 OVERRUN
 curiosity ──► confidence ──► alarm ──► desperation/triumph
 "what's their comp?"  "my lanes hold"  "elite wave!"  "ONE PYLON FROM SURGE"
```
Design levers per beat: act-1 director is quiet enough to let the player take a pylon and feel ownership *before* the first real threat; act-2 introduces exactly one elite per wave (a face to fear); act-3 music layer 3 + base damage states make the climax legible from across a room.

## 7. Scope guardrails (anti-creep contract)

- **No PvP at 1.0.** The sim is deterministic and tape-based — PvP-ready by architecture — but balancing for it is a different game. Revisit post-1.0.
- **No 3D.** 2.5D lane presentation with parallax; the budget goes to shaders and VFX, not meshes.
- **Heroes/Protocols are P3** behind a fun-gate (doc 09 §9.2). The MVP must be 8/10 *without* them; they are multipliers, not crutches.
- **Three GIF moments are sacred:** any scope cut that touches EMP, Singularity, or lane-surge presentation needs director sign-off.

*Related: battlefield 11 · board 12 · juice 13 · campaign 14 · metagame 15 · architecture 16.*
