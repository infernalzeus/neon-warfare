# NEON WARFARE — Progression Design
**Document 05 of 09 · v1.0 · 2026-06-12**

Four interlocking progression tracks, each on a different clock so *something* advances every session:
| Track | Clock | Emotion |
|---|---|---|
| Campaign (missions/sectors) | per session | forward motion |
| Tech tree | every 2–3 missions | strategic identity |
| Heroes (XP + skill trees + ascension) | continuous drip | ownership/attachment |
| Base (buildings) | every 3–5 missions | visible empire |

Roguelite Protocols (per-battle drafts, GDD §3.6) sit *inside* battles and are deliberately impermanent — the permanent tracks below exist so protocols can stay wild without breaking the long game.

---

# 1. CAMPAIGN STRUCTURE

3 sectors × 13 missions = 39. Per sector: 10 standard, 2 elite (gold-skull node, +modifiers, blueprint targets), 1 boss.

**Unlock cadence (the "new toy" drumbeat)** — never more than 3 missions without a new *thing*:
```
1-01  tutorial: board + Drone/Shock Trooper          2-04  Gunship
1-02  tutorial: protocols + Mech Walker              2-06  HERO: The Architect (story)
1-03  tutorial: hero ult (NOVA) + Interceptor        2-08  Chronomancer
1-05  Titan + base unlock (Reactor, Drone Factory)   2-13  BOSS Mirror → Q-Cores, Quantum Forge
1-07  Bomber + tech tree unlock (AI Lab)             3-02  Juggernaut
1-09  Hacker + HERO: Cyber Monk (story beat)         3-05  HERO: Valkyrie-X (rescue mission)
1-11  Siege Tank                                     3-07  exotic tech keystones
1-13  BOSS Warden → Defense Grid bldg, elite intel   3-13  BOSS HELIOS-PRIME → The Breach + NG+ modifiers
1-13  BOSS Warden → sector 2                         
1-04/06/08… Teleporter, elites, etc. fill remaining slots
```
Star grades (★–★★★: time / core HP / peak combo) are replay hooks; sector gates need stars only on Blackwall (optional prestige), never on Standard — completion is skill-friendly, mastery is optional.

---

# 2. TECH TREE — 40 nodes, 5 branches

Constellation layout (doc 03 §3.5). Each branch: 7 nodes in 4 tiers + 1 **Exotic Keystone** (build-warping, pick-few). Tiers gate by AI Lab building level (1/2/3/4 → keystones need AI Lab 5). Costs in doc 04 §2.3.

**⚔ WARFARE (offense)**
T1 Sharpened Code (+5% unit dmg) · T1 Rapid Compile (−10% compile CDs) · T2 Crit Subroutine (units 5% crit ×2) · T2 Ordnance (+20% AoE radius) · T3 Focus Fire (units in same row +8% dmg) · T3 Alpha Strike (units deal +25% for 3s after deploy) · T4 Executioner (+30% dmg to targets <25% HP) · **KEYSTONE: TOTAL WAR** — your 4-match lasers deal 3× battlefield damage but board pays −15%.

**🛡 AEGIS (defense)**
T1 Hardened Cores (+10% unit HP) · T1 Reinforced Nexus (+15% core HP) · T2 Auto-Repair (units regen 1%/s out of combat) · T2 Shield Capacitor (deployed units spawn with 10% shield) · T3 Bulwark (Heavies +15% HP, taunt +2s) · T3 Emergency Vent (core <30%: all units +20% dmg — comeback valve) · T4 Last Firewall (core survives lethal once/mission at 1 HP, 5s invuln) · **KEYSTONE: FORTRESS PROTOCOL** — Defense Grid turrets ×2, but max active units −6.

**⚡ FLUX (economy/board)**
T1 Refinery (+1 payout per 4-match) · T1 Deep Cache (stockpile cap +100) · T2 Cascade Logic (combo mult cap ×3→×3.5) · T2 Gem Synthesis (start each battle with 8 of each resource ×AI Lab level) · T3 Special Engineering (+15% chance L/T-shapes upgrade to Singularity) · T3 Data Siphon (Data overflow converts 8:1 instead of 10:1) · T4 Overclocked Compiler (board refill +20% faster) · **KEYSTONE: SINGULARITY ENGINE** — 5-matches pull a true black hole on the battlefield (1s mass displacement), but Singularities pay 0 resources.

**◉ ASCENDANCY (heroes)**
T1 Sync Training (+20% hero XP) · T1 Quick Charge (+10% ult charge rate) · T2 Twin Actives (hero active gains 2nd charge) · T2 Battle Meditation (ult charge persists 50% between missions) · T3 Resonance (hero active also triggers a free 3-match payout) · T3 Veteran Instinct (heroes start battles level-buffed +1 ring) · T4 Avatar State (ult duration/impact +25%) · **KEYSTONE: DUAL DIVE** — equip a 2nd hero's *passive* in loadout.

**▣ INFRASTRUCTURE (base/protocols)**
T1 Efficient Construction (building upgrades −10% Credits) · T1 Field Scanner (mission intel free) · T2 Protocol Cache (draft offers 4 choices, pick 1) · T2 Stable Slot I (keep 1 protocol permanently) · T3 Reroll Subroutine (1 draft reroll/battle) · T3 Salvage Logic (+20% Credits from missions) · T4 Stable Slot II · **KEYSTONE: EXOTIC AFFINITY** — exotic protocols 2× draft weight, commons removed from pool.

Design rules: every node states exact numbers in-tooltip; respec is cheap (experimentation is content); keystones are mutually compatible but Q-Core-gated so a full build is an end-game statement.

---

# 3. HEROES

## 3.1 XP & levels (1–30)
Hero XP per battle = mission base × grade mult (hero must be fielded; bench earns 25% via "sync link" so alts never feel dead). Curve: `xp_to_level(n) = 80 × n^1.35` → reaching 30 ≈ full campaign + some Breach with one main. Levels grant flat stat ribbons (+1% hero dmg/HP per level) + **1 skill point**.

## 3.2 Skill trees — 18 nodes per hero, 3 rings
Rings gate by **Ascension** (Q-Cores, doc 04): Ring 1 (6 nodes) open at Ascension I, Ring 2 at II, Ring 3 at III. Three branches per hero shaping *how* their kit warps the game. Example — **NOVA**:
- **Gunline** (damage): Volley +2 shots → volley crits → ult +30% dmg → *R3: Supernova leaves a burning lane 6s*
- **Vanguard** (deploy synergy): passive speed buff +5s → buffed units +10% dmg → deploys near Nova gain shield → *R3: passive applies to whole row*
- **Reactor** (board): Plasma matches +1 payout → active costs −10P → ult converts 8 tiles not 5 → *R3: combo ≥6 grants 10% ult charge*

Same pattern for Cyber Monk (Mend/Bastion/Tempo), Architect (Specials/Refactor/Blueprint), Valkyrie-X (Ace/Armada/Bombardment). Full node tables to be authored in M3 as balance SOs; rule: **Ring-3 capstones must be visible on screen** (new VFX), never a hidden +%.

## 3.3 Acquisition
Story-gifted (no gacha): NOVA at start, Cyber Monk 1-09, Architect 2-06, Valkyrie-X 3-05 (rescue mission where she fights beside you NPC-style first — try-before-you-own).

---

# 4. BASE — 5 buildings × 5 levels

Visible on the hub (doc 03 §3.2); each level adds geometry/lights/drone traffic (visual evolution mandatory — progression you can *see*). Credits costs in doc 04.

| Building | Effect per level (L1 → L5) |
|---|---|
| **Reactor Core** | start battles with +10/20/35/50/75 Energy; L5: +5% all board payouts |
| **AI Lab** | gates tech tiers 1–4; L5 unlocks keystones; each level +5% Data Fragment earnings |
| **Drone Factory** | +1 loadout slot at L2/L4 (6→8 units); unit ranks craftable at L3; L5: −5% all compile CDs |
| **Quantum Forge** | special-tile potency +5%/level (laser dmg, singularity radius); L5 unlocks Laser+Singularity recipe preview UI |
| **Defense Grid** | Nexus turret: dmg/range per level; L3 second turret (air-capable); L5 shield: ignores first 200 dmg per wave |

Build/upgrade is instant (no timers — premium respect), costed by Credits and gated lightly by sector progress so the hub grows in step with the campaign.

---

# 5. POST-CAMPAIGN

- **The Breach** (endless): infinite director ramp, drafts every 3 waves, leaderboard by wave reached; faucets close the remaining ~20% economy gap (doc 04 §2.2).
- **NG+ ("Blackwall Dive")**: replay campaign with stacking modifiers (Hades Heat-style: pick pain, gain Q-Core drip), grades tracked separately.
- **Daily Seed**: fixed board RNG + fixed drafts, one attempt, global board — cheap retention, no FOMO rewards (cosmetic flair only).

# 6. THE FIRST-HOUR CONTRACT (FTUE pacing)
Minute 0–4: mission 1-01, first match, first deploy, first win. 4–10: protocols + hero ult tasted. 10–15: hub reveal, first building bought (gifted Credits cover it — first purchase is free dopamine). 15–60: missions 1-04→1-08, tech tree opens, ~6 "new thing" events. **Target: by minute 60 the player has made ≥3 permanent choices that are *theirs*** (a tech path, a building order, a unit rank) — ownership is retention.
