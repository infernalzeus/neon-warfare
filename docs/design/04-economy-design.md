# NEON WARFARE — Game Economy Design
**Document 04 of 09 · v1.0 · 2026-06-12**

Two economies, firewalled from each other:
- **Tactical economy** (inside a battle): board resources → units/abilities. Resets every mission (except Data overflow, below).
- **Meta economy** (between battles): mission rewards → permanent progression sinks. No real-money currency exists anywhere (premium product).

Design intent: the tactical economy creates *tempo decisions* (spend now vs bank for Titan); the meta economy creates *identity decisions* (which branch/hero/building expresses my playstyle). Neither is a grind-gate: at intended pace, a player clearing the campaign earns ~85% of all meta content; completionists use The Breach.

---

# 1. TACTICAL ECONOMY (in-battle)

## 1.1 Income model
8×8 board, 5 gem types ⇒ expected ~12.8 tiles of each type on board. Baseline skilled play ≈ 10 matches/min including cascades.

**Payout formula per cleared run:**
`payout = (3 base + 2 × tiles_beyond_3) × combo_mult` , combo_mult ∈ {1, 1.5, 2, 3 cap}
- 3-match: 3 · 4-match: 5 (+Line Laser) · 5-match: 7 (+Singularity)
- Expected income ≈ **42 resource units/min** total across the 5 types (≈8.4/type/min), heavily steerable by which colors the player hunts.

Specials are economy accelerants: a Singularity detonation pays every cleared tile at current combo mult — typical burst 25–40 of one resource. This makes special-crafting the *skilled income line* and keeps experts ~1.8× richer than button-mashers (target skill-income ratio; tune in beta).

## 1.2 Cost philosophy & curves
Unit costs (GDD §3.4) follow: `cost_total ≈ 18 × power_rating^1.15`, where Energy is always ≥60% of cost and the class-flavored secondary resource the rest. Consequences:
- **Drone (20E)** = ~30s of casual Energy income shared with other wants → constant small decisions.
- **Juggernaut (150E+60N)** = ~2 min of *deliberate* Nano-hunting → big payoffs are planned, felt, and celebrated.
- Compile cooldowns (2–10s) are the anti-spam valve so cost alone never has to do rate-limiting (this is what makes cheap-unit flooding a strategy rather than a degenerate dump).

**Stockpile cap:** 300/resource (UI warns at 90%; overflow converts to combo-meter charge at 5:1 — banking forever is never optimal; spending is the game).

## 1.3 Resource identities (steering decisions)
| Resource | Sinks | Decision it creates |
|---|---|---|
| Energy ⚡ | every unit | the metronome — always wanted, never hoarded |
| Plasma ◎ | air units, hero actives | "do I fund the sky or the hero?" |
| Nano ▲ | heavies, Cyber Monk heals | tempo vs insurance |
| Quantum ◆ | specials, 25% of ult charge | the rare gem — hunting it costs board tempo |
| Data ❖ | Hacker; **unspent Data converts 10:1 → meta Data Fragments post-battle** | the "savings account" gem; matching it is investing |

The Data overflow valve is the deliberate bridge between economies: it gives every board state value and lets economically-minded players express that identity, but at 10:1 it never beats playing the objective.

## 1.4 Enemy director budget
Enemy spawns are credit-driven: `budget_rate(t) = base × (1 + t/90s) × difficulty_mult`, spent on weighted comps with burst-saving (banks up to 3× rate for wave spikes → readable wave rhythm with breathers). Mission length target: standard 6–8 min, elite 9–11, boss 10–13. Difficulty mults: Story 0.7 / Standard 1.0 / Veteran 1.35 / Blackwall 1.7 + modifiers.

---

# 2. META ECONOMY

## 2.1 Currencies
| Currency | Earned from | Spent on | Tier |
|---|---|---|---|
| **Credits ⚡** | every mission (base 100–400 by sector ×grade mult), Breach | building levels, blueprint ranks | common workhorse |
| **Data Fragments ❖** | first-clears, elite/boss bonuses, Data overflow, achievements | tech tree nodes | strategic |
| **Quantum Cores ◆** | boss kills, sector completion, Breach milestones (scarce: ~30 earnable to 1.0 content end) | hero ascension tiers, exotic tech keystones | prestige |
| **Blueprint Shards ▤** | mission first-clears (targeted drops), elite repeats | unlock units 7–12, unit ranks | collection |

Grade multiplier: ★ 1.0 / ★★ 1.25 / ★★★ 1.5. Defeat pays 30% of base (Pillar 4). Replays pay ~40% of first-clear (anti-farm without punishing practice).

## 2.2 Faucet/sink balance (campaign totals, Standard difficulty, ★★ average)
| | Credits | Data Frags | Q-Cores | Shards |
|---|---|---|---|---|
| **Faucets (39 missions + firsts)** | ~9,000 | ~1,150 | 24 | ~210 |
| **Sinks (everything maxed)** | 11,500 | 1,400 | 30 | 260 |
| **Campaign coverage** | ~78% | ~82% | 80% | ~81% |

Intentional ~20% gap = The Breach's reason to exist (its faucets close the gap in ~4–6 endless hours). Nothing is timed, rotated, or random-rolled: every sink has a deterministic path.

## 2.3 Sink pricing
- **Buildings** (Credits): L1 free (story) → L2 250 → L3 500 → L4 900 → L5 1,500 per building (×5 buildings = 11,500 total infrastructure ceiling; effects in doc 05 §4).
- **Tech nodes** (Data): tier 1 15 → tier 2 25 → tier 3 40 → tier 4 60; exotic keystones 80 + 2 Q-Cores. Respec: free first time, then 50 Credits (cheap on purpose — experimentation is content).
- **Hero ascension** (Q-Cores): tier I 2 → II 4 → III 6 per hero (unlocks skill-tree rings; hero *levels* are XP-only, no currency).
- **Unit ranks** (Shards+Credits): rank 2 = 10▤+100⚡, rank 3 = 25▤+250⚡. Rank = +10% HP/dmg + 1 ability tweak at rank 3 (e.g., Titan taunt radius +30%).

## 2.4 Reward presentation (economy IS dopamine)
Currencies never "appear" — they shower, fly, and count up (doc 03 §3.7). First-clear bonuses are chest-burst moments. Pity-free, gambling-free: every reward is visible on the mission node before you dive ("informed greed" — the Slay the Spire map principle).

## 2.5 Anti-degeneracy audits (test plan)
1. **Floor farm:** replaying mission 1-01 must never beat progressing (40% replay rate + flat base by sector guarantees it). ✔ by construction; verify in telemetry.
2. **Data-gem tunnel:** ignoring battle to farm Data overflow must lose missions (enemy budget doesn't care). ✔
3. **One-unit spam:** compile cooldowns + class triangle; sim harness must show no single-unit comp >55% win vs mission comps at equal skill.
4. **Hoard-and-turtle:** stockpile cap + overflow-to-combo conversion + director ramp make late-game stalls strictly worse.

Balance tooling: all numbers live in ScriptableObjects exported to a balance sheet; headless simulator (doc 07 §8) runs 1,000-battle sweeps per tuning PR.
