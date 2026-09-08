# NEON WARFARE — Battlefield v2 (Phase 3)
**Document 11 · 2026-06-12 · full spec for lanes, ownership, frontline, spawning, formations, structures · supersedes GDD §3.4**

All numbers herein are **tuning baselines** — they live in ScriptableObjects (`BattleConfig`, `LaneConfig`) and will move in playtests. The *structures* are the contract.

---

## 1. Field geometry

```
                          ◄──────────── LaneLength = 100 sim units ────────────►
            ┌─────────────────────────────────────────────────────────────────┐
  AIR  L4   │  ░░░░░░░░░░░░░░░░░░  (no pylons — superiority meter instead)    │
            ├─────────────────────────────────────────────────────────────────┤
  GND  L3   │ N ▣━━━━━━━━ P1 ━━━━━━╋━━━━━━━ P2 ━━━━━━━━▣ H                    │
  GND  L2   │ N ▣━━━━━━━━ P1 ━━━━━━╋━━━━━━━ P2 ━━━━━━━━▣ H   N = player Nexus │
  GND  L1   │ N ▣━━━━━━━━ P1 ━━━━━━╋━━━━━━━ P2 ━━━━━━━━▣ H   H = HELIOS core  │
  GND  L0   │ N ▣━━━━━━━━ P1 ━━━━━━╋━━━━━━━ P2 ━━━━━━━━▣ H   ╋ = frontline    │
            └─────────────────────────────────────────────────────────────────┘
              x=0        x=33        x=50       x=66        x=100
```

- **4 ground lanes (L0–L3) + 1 air lane (L4).** Player travels left→right.
- Pylons at **x=33 (P1)** and **x=66 (P2)** per ground lane. 8 pylons total.
- Both cores are shared across lanes (one Nexus, one HELIOS core — units exiting a lane at the enemy end attack the shared core). Core HP **500** (revert debug 99999).
- On screen, lanes stack bottom-to-top: L0 nearest (largest), L3 far (−6% scale, +parallax), L4 air floats above with altitude shadow blobs cast on its “home” presentation row.

**Why 4 ground lanes (not 3 or 5):** 3 makes Surge (majority control) trivial — hold 2; 5 exceeds the attention budget of a player who is also playing a match-3. 4 ground + air = Surge needs 3, which is a real campaign.

## 2. Lane ownership system

### 2.1 Pylon capture
- A pylon has a **capture zone** (±6 units). State: `Enemy / Neutral→ / Player`, progress −100..+100.
- Uncontested friendly presence in zone: progress ±20/s → **5 s flip**. Contested (both teams in zone): progress frozen, pylon strobes white — a visible standoff.
- Capture does **not** interrupt unit behavior — units capture by *being there*, fighting included. No channeling, no babysitting clicks.
- On flip: pylon flares, lane floor tint floods from the pylon at 25 units/s, banner "LANE 2 — NODE TAKEN", capture stinger.
- Battle start: P1s neutral-leaning-player? No — **all pylons start neutral**, except territory modifiers (WARDEN maps start some enemy-owned).

### 2.2 Frontline
- Per ground lane: `frontline = lerp(frontline, furthest sustained friendly X vs furthest enemy X midpoint, 0.5/s)`, clamped to ±5 beyond the outermost owned pylon.
- Pure presentation + score (Pillar 6): a crackling vertical seam where tints meet. Units cross it freely.
- Combat win at the seam (last unit of an engagement dies) snaps it 5 units toward the loser — fights visibly *move the war*.

### 2.3 Ownership payoffs
| State | Mechanical effect | Visual |
|---|---|---|
| Own P1 (lane) | **Forward spawn** at P1 for that lane | spawn pad ring materializes at pylon |
| Own P1+P2 (lane controlled) | +10% move speed (lane, yours); forward spawn at P2 | floor pulse 2 s loop, lane edge lights solid |
| Control 3+ ground lanes | **SURGE:** enemy core shield −15% (damage taken ×1.15) while held | map-wide tint shift, klaxon, core shield visibly thins |
| Air superiority (see §5) | +10% ranged damage, all ground lanes | targeting reticles appear over enemies |
| **Enemy** mirror | identical effects for HELIOS | hot-orange equivalents |

Symmetry is non-negotiable: the player must fear losing lanes exactly as much as they crave taking them.

### 2.4 Decay
Pylons do not decay to neutral on abandonment — flips require enemy presence. (Decay punishes map play; we want held ground to *stay won* until contested.)

## 3. Deployment

### 3.1 Input flow (3 paths, all ship)
1. **Drag:** drag card from command strip onto a lane → lane highlights while hovering → release. Primary mouse path.
2. **Click-click:** click card (card lifts, all legal lanes glow) → click lane. Primary Deck/touch path.
3. **Hotkeys:** `1–6` selects card, `Q/W/E/R` = L0–L3, `A` = air lane, `Space` = repeat last deploy. Speedrunner path.

- Air units only target lane L4 (auto-routed; lane pick irrelevant — selecting any lane deploys air).
- Illegal deploy (can't afford / cooldown): card shakes 3 px, cost chip flashes red, denied blip. Never silent.

### 3.2 Spawn rules
- Spawn at **rearmost→forwardmost owned point** in the chosen lane: Nexus pad → P1 pad → P2 pad (player picks pad by dropping the card on it; default = most forward).
- Materialize 0.3 s (wireframe→solid→flash). Unit is **invulnerable + inert** during materialize (no spawn-sniping).
- **Compile cooldown per card** (not global): Drone 2 s · Trooper 3 s · Mech 5 s · heavies 8–10 s · specials 5–9 s. Radial sweep on card.
- Deploy queue: 1 buffered deploy per card permitted (card shows a stacked chip).

### 3.3 Enemy director (lane-aware)
Extends the existing budget-curve director:
- Spend trigger: bank ≥ cheapest spawnable, roll vs composition weights (per territory).
- **Lane choice:** 60% weakest-own-frontline (defend), 40% strongest-own-push (snowball). Boss doctrines override (WARDEN: rotates a fortified lane; MIRROR: mirrors your last deploy's lane).
- **Wave telegraphs:** when director banks ≥3× normal spend (a wave), chevrons pulse at that lane's enemy edge 2 s before release + klaxon tick. Into the Breach rule: threats announce.
- Acts (doc 09 §3.7): budget/s ×1.0 / ×1.6 / ×2.2 at act 1/2/3. Elites enter act 2; boss patterns act 3.

## 4. Combat model

### 4.1 Per-lane 1D flow with formation slots
Each ground lane has **3 file slots** per team at the engagement front (front/mid/back, 4-unit spacing):

```
  player side                          enemy side
  ... [back][mid][front]  ◄─gap─►  [front][mid][back] ...
        ▲ ranged/support    ▲ melee row
```

- State machine per unit: `Materialize → Advance → SlotIn → Fight → Push` (and `Retarget` on kill).
- **Advance:** move at `Speed` until an enemy is within `AggroRange = Range + 8`.
- **SlotIn:** claim the most-forward open slot matching role (melee→front, ranged→mid, support→back). Overflow units stack behind back slot in march order — a visible *column* (the "building an army" read).
- **Fight:** attack nearest valid target by priority: countered class > closest > lowest HP. Cooldown per `AttackCooldown`.
- **Push:** when no enemies within AggroRange, resume Advance. Reaching x=100 → attack core (cores have 0 armor; DPS races end matches).
- Air (L4): ignores ground slots; engages air first, then strafes ground if `TargetsGround`.

### 4.2 Counters
- Class triangle: **ASSAULT ► SPECIAL ► HEAVY ► ASSAULT**; AIR outside (hit only by ranged/air/anti-air).
- Counter hit: **+35% damage**, "COUNTER" popup in attacker class color, distinct *crunch* layer. The triangle must be learnable from watching, not from a manual.
- Anti-air gap is a real threat: a pure ground comp loses L4 → loses spotting → loses ranged duels. Every comp needs an air answer.

### 4.3 Engagement readability
- Contact point spawns a persistent **clash zone VFX** (sparks, crossing tracers) while a fight is live — from across the room you can see *where* the war burns.
- Damage numbers: white, crit ×1.4 orange, counter adds class-color ring. Numbers pool, cap 30 visible (oldest culled).
- Hit-stop 40 ms on hits ≥50 dmg. Elite kill: 0.3 s slow-mo at 0.4× (max once/5 s).

## 5. Air lane (L4)
- No pylons. Instead an **air superiority meter**: net alive air DPS share, smoothed 5 s. >65% share held 5 s = superiority (spotting buff §2.3).
- Air units render with altitude bob + shadow blob; bombers telegraph their carpet line on the target ground lane before the run.

## 6. Structures

### 6.1 Turrets (placed, not marched)
- Turret card → legal sockets glow (each **owned pylon** has 1 socket; Nexus has 2 base sockets).
- Build: 0.6 s 3-part assembly. Stats baseline: 180 HP · 14 dmg · 1.4 s CD · range 20 · targets air+ground. Cost 2E+6N.
- Pylon lost → its turret **de-rezzes** (no refund). Turrets are bets on territory.
- Variants by campaign unlock: *Tesla* (chain 3), *Bastion* (taunt aura), *Flak* (anti-air ×2, can't hit ground).

### 6.2 Cores as buildings (3 damage tiers)
| HP | Player Nexus | HELIOS core |
|---|---|---|
| 100–66% | antennas rotate, shield shimmer, drones orbit | smooth obsidian monolith, red scanline eye |
| 65–33% | arc-sparks, cracked panel, shield flickers | panels split, internals glow, eye flickers |
| <33% | fire glow, alarm strobe, klaxon heartbeat | containment failure arcs, eye erratic |

- Each **Surge** the player triggers adds a visible armor ring to the Nexus (win-state = construction, Pillar 4 made visual).
- Core hits: screen-edge red pulse (own core) / forward punch-in (enemy core).

## 7. Camera (final spec)
- Orthographic, fixed framing — whole battlefield always visible. No player camera control.
- Idle drift ±8 px sine, 12 s. Parallax: 3 background layers (city grid −30%, data-stream haze −60%, skyline −85%).
- **Trauma system:** events add trauma (match 0.05, big hit 0.1, core hit 0.3, EMP 0.5); shake amplitude = trauma², decay 1.5/s, max amplitude 12 px. One knob, no soup.
- Punch-ins 3–5% (pylon flip, elite kill, core tier change), letterbox 8% only for victory/defeat sequences.

## 8. What this kills from the current build
- Random-row spawning → lane-targeted deploys (the game's missing verb).
- 3 anonymous ground rows → 4 owned lanes + air with pylons/frontline.
- Pass-through blob fights → slotted formations with clash zones.
- HP-bars-as-bases → 3-tier evolving structures.
- Silent metronome director → telegraphed, act-structured, lane-aware director.

*Numbers tuned in `BattleConfig`/`LaneConfig` SOs (doc 16). Juice contract for every event here: doc 13.*
