# NEON WARFARE — Compiler Board v2 (Phase 4)
**Document 12 · 2026-06-12 · full board spec: rules, specials, crossover, animation timings · supersedes GDD §3.3**

The board is not an economy widget. It is **the player's weapon**. Every spec below serves one sentence: *good puzzle play visibly bombards the enemy.*

---

## 1. Core rules (ratified from domain code, re-tuned)

- 8×8 grid, 5 gem types, **drag-swap or click-click** adjacent (both inputs ship; current build is click-click only).
- 3+ in a line clears. Cascades chain. **Deadlock auto-reshuffle** with "RESHUFFLING SECTOR" hologram (0.5 s dissolve/re-deal).
- Atomic resolve via `BoardModel.TrySwap()` event tape — presentation replays the tape; sim never waits for animation. (Architecture detail that makes everything below cheap: doc 16 §3.)
- **Combo multiplier** on payouts within one cascade chain: ×1 → ×1.5 → ×2 → ×3 (cap). Resets when the chain settles.
- No move limit, no turn structure — real-time. The constraint is *attention* (Pillar 1).

## 2. Resources (identity locked)

| Resource | Color | Silhouette | Hex | Fuels | Scarcity profile |
|---|---|---|---|---|---|
| Energy | electric cyan | hex-bolt | #00E5FF | every deploy (universal) | most common (24% spawn) |
| Plasma | magenta | flame-orb | #FF2E97 | air, offense | 19% |
| Nano | neon green | tri-swarm | #00FF9C | heavies, turrets, repairs | 19% |
| Quantum | violet | spinning cube | #B026FF | specials, ultimates | 19% |
| Data | amber | glyph-shard | #FFB300 | hackers; surplus→meta currency post-battle | 19% |

- Gems: faceted holographic crystals, refraction shader, **unique silhouette** (colorblind contract — never color-only), idle bob ±2 px per-gem phase offset.
- Resource caps: 99 each. Overflow Energy auto-converts to Data at 3:1 (waste hurts but pays the meta — soft pressure to spend, no hard punishment).

## 3. Special tiles

| Created by | Tile | On detonation (board) | On detonation (battlefield) |
|---|---|---|---|
| 4 in a row | **Line Laser** (H/V per match orientation) | clears full row/col | visible laser sweeps one battlefield lane: **15 dmg** to all enemies in it |
| L / T shape | **Cross Laser** | clears row + column | strikes **2 lanes** (X flash) |
| 5 in a row | **Singularity** | swap with any gem → clears all of that color (black-hole suck-in VFX) | screen shockwave, all enemies **slowed 30%, 1.5 s** |
| Laser + Laser swap | double cross | clears 2 rows + 2 cols | 2-lane strike ×2 |
| Singularity + Laser | color-lasers | every gem of chosen color becomes a laser | multi-lane barrage |
| Singularity + Singularity | **BOARD WIPE** | full clear | **EMP: all enemies stunned 3 s** + full-screen chromatic pulse |

**Lane targeting rule (the new glue):** a Line Laser fires into battlefield lane `matchRow % 4` (H-matches) or the lane with the most enemies (V-matches). The H-rule means board *position* has battlefield meaning — skilled players will set up 4-matches in the row that maps to the lane under pressure. This is the deepest skill-expression hook in the game; it must be taught by FTUE territory 2 ("match in the bottom rows to strike the bottom lanes").

## 4. Crossover contract (every match touches the war)

| Trigger | Payout | Battlefield | Notes |
|---|---|---|---|
| 3-match | +1/gem ×combo | nearest friendly units sheen 0.5 s (morale ping, cosmetic) | resource trail arcs board→meter |
| 4-match | +ress ×1.5 + Laser tile | (tile effect when detonated) | |
| 5-match | Singularity tile | (tile effect) | |
| Combo ×3 | — | next deploy's materialize is instant (0 s) | "compiler hot" card glow |
| Combo ×5 | — | **Orbital Strike**: 40 dmg, strongest enemy, 1 s telegraph ring | auto, no aim — keep hands on board |
| Combo ×8 | — | **EMP**: all enemies stunned 2 s | the GIF moment; rare (~1/match good play) |

Tuning guard: total crossover damage ≤ ~20% of a match's damage output at equal skill — the board *supports* the army, never replaces it. (Anti-degeneracy: a board-only strategy must lose to a deployed army; verified by sim harness, doc 16 §7.)

## 5. The supply line (signature presentation)

One unbroken visual circuit, every link animated:

```
match clears → voxel shards fly UP into resource meter → meter liquid-fills →
player taps card → meter drains INTO card → card flashes → spawn pad surge →
unit materializes in lane
```

The player should be able to follow a single match's energy from gem to soldier. This is the highest-value presentation item in the project (doc 09 ranked it inside P0 juice) — it makes the economy *physical* and welds the two screens into one machine (Pillar 1 + 2).

## 6. Animation timings (contract)

| Event | Duration | Curve | Detail |
|---|---|---|---|
| Hover | 80 ms in | out-quad | 1.06×, rim glow, soft tick |
| Select | 120 ms | out-back | lift 4 px, pulse ring, neighbors dim 10% |
| Swap | **0.14 s** | out-cubic | squash 0.9→1.05→1.0, motion trail |
| Invalid swap | 0.14 s + 0.18 s back | out-cubic / out-elastic | horizontal shake 3 px ×2, denied blip, "NO MATCH" microcopy |
| Clear | 0.16 s | — | 12 voxels/gem burst → trail to meter; +N popup at centroid; pitch-ladder pop (+1 semitone per combo step, resets on settle) |
| Fall | **0.08 s/cell** | in-quad, 30 ms column stagger | 1-frame land squash |
| Refill | as fall | — | new gems drop pre-lit from above frame |
| Special create | 0.25 s | out-back | gem transforms with flash + persistent aura |
| Special detonate | 0.3 s board + lane effect | — | see §3 |
| Reshuffle | 0.5 s | — | hologram sweep, dissolve/re-deal |
| Hint (idle 6 s) | 1.2 s loop | sine | one valid move shimmers; OFF at Veteran+ |

Total swap-to-playable for a simple 3-match: **≤0.40 s**. Board downtime is dead air in a real-time game; if a cascade is long, input is *not* locked — the player may queue the next swap on settled regions (current build locks all input during `_animating`; v2 locks only moving cells).

## 7. Board pressure (faction interactions)

The board is also a battlefield *receiving* attacks (HELIOS-PRIME doctrine, doc 14):
- **Locked tile:** chained gem, unmatched until an adjacent match breaks the chain (1 match).
- **Virus tile:** spreads to 1 neighbor per 8 s until cleared (match it or laser it); pays nothing.
- Injection events telegraph: red glitch sweep crosses the board 1 s before tiles corrupt.
These arrive only in R5 territories + Blackwall modifiers — the base game keeps the board a sanctuary.

## 8. UX rules
- Affordability is **card state**, not number-reading: card lit = affordable, dim+cost-chip-red = missing X (the missing resource's chip pulses).
- Meters show **income direction**: a subtle up-tick arrow when a type was matched in the last 3 s — glanceable "what am I earning".
- Every denied action says why, in ≤2 words, at the point of denial.
- Colorblind mode is the *default art* (silhouettes), not a toggle.

*Domain model already supports: swap tape, specials, payouts. New domain work: combo-clock, crossover events on tape, locked/virus tiles, partial input locking. → doc 16.*
