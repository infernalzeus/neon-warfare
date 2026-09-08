# NEON WARFARE — Liberation Campaign v2 (Phase 6)
**Document 14 · 2026-06-12 · 20 territories · 5 regions · 3 factions · supersedes doc 05 campaign structure**

---

## 1. The map

Neo-Arcadia rendered as a glowing city-grid viewed from orbit. Owned territory glows cyan; HELIOS territory pulses red-orange; the contested border crackles. **The campaign screen is Pillar 6 at macro scale** — progress is visible light.

```
                    ┌─────────────┐
                    │  R5 THE CORE │  HELIOS-PRIME
                    │ 17 18 19 [20]│
                    └──┬───────┬──┘
            ┌──────────┴─┐   ┌─┴──────────┐
            │ R3 FOUNDRY  │   │ R4 SKYLINE │
            │ 9 10 11 [12]│   │13 14 15[16]│   WARDEN / MIRROR
            └──────┬──────┘   └──────┬─────┘
            ┌──────┴──────┐   ┌──────┴─────┐
            │ R1 SHALLOWS │   │ R2 NEON DST│
            │ 1  2  3 [4] │   │ 5  6  7 [8]│   WARDEN / MIRROR
            └──────┬──────┘   └──────┬─────┘
                   └───── START ─────┘
```

- **Adjacency, not a line:** after T1 the player always has 2–3 legal targets. R1/R2 can interleave; R3 requires R1 clear, R4 requires R2 clear, R5 requires both. `[n]` = region boss territory, gated on its region's other 3.
- A territory = one bespoke battle: map skin, modifier set, enemy comp weights, director curve, optional bonus objective. Replayable any time at any difficulty for stars.

## 2. Factions (doctrine = how it warps OUR systems)

| Faction | Sub-mind | Doctrine (felt every battle) | Boss rule-break |
|---|---|---|---|
| **WARDEN** (R1, R3) | the fortifier | Starts owning 1–2 pylons; enemy turrets pre-built; slow, heavy comps | Periodically **firewalls a lane** (impassable 20 s, telegraphed) — you must split-push around it |
| **MIRROR** (R2, R4) | the copyist | 25% of director spend mirrors **your** last 3 deploys (same units, same lanes) | Copy-rate doubles; copies spawn with your skins glitched red — fighting your own army |
| **HELIOS-PRIME** (R5) | the corruptor | Injects locked/virus tiles into your board (doc 12 §7); both fronts attacked | Final boss corrupts a full board *column* + rotates WARDEN and MIRROR behaviors per act |

Doctrine teaching arc: WARDEN punishes single-lane tunneling → MIRROR punishes mindless comp-spam → PRIME punishes board neglect. The campaign *is* the tutorial for mastery.

## 3. Territory table (all 20)

Format: **Name** — twist · bonus objective · guaranteed reward. (E=Energy start mod, numbers are design intent, tuned later.)

### R1 — THE SHALLOWS (WARDEN, tutorial region)
| # | Territory | Twist | Bonus objective | Reward |
|---|---|---|---|---|
| 1 | **Dockside Relay** | FTUE: scripted first 4-match laser; only L1–L2 active | finish ≥75% core HP | unit: **Shock Trooper** |
| 2 | **Saltglass Market** | all 4 lanes open; first pylons | hold both P1s at once | unit: **Interceptor** |
| 3 | **Drowned Archive** | enemy starts owning both P2s | flip any enemy P2 | building: **Turret (base)** |
| 4 | **[BOSS] Warden's Gate** | WARDEN: lane firewalls | win with ≥2 lanes controlled | unit: **Mech Walker** + banner *Shallows Liberator* |
**Region keystone:** start every battle owning your choice of one P1 · battlefield skin *Shallows Dawn* at 100%.

### R2 — NEON DISTRICT (MIRROR)
| # | Territory | Twist | Bonus objective | Reward |
|---|---|---|---|---|
| 5 | **Glow Bazaar** | mirror-lite (copies 1 deploy) | win using ≥4 different units | unit: **Hacker** |
| 6 | **Pachinko Spire** | board: +1 gem type spawn weight shifts every 60 s | reach combo ×5 | tile skin set: **Glass** |
| 7 | **Red Lantern Yards** | air-heavy enemy comp | hold air superiority 60 s total | unit: **Bomber** |
| 8 | **[BOSS] Hall of Mirrors** | MIRROR full doctrine | win deploying ≤3 unit types (starve the mirror) | unit: **Siege Tank** + banner *Mirrorbreaker* |
**Keystone:** your first deploy each battle is not mirrored — ever · unit skin (Drone *Chrome*) at 100%.

### R3 — THE FOUNDRY (WARDEN, hard)
| # | Territory | Twist | Bonus objective | Reward |
|---|---|---|---|---|
| 9 | **Slag Channels** | only L0/L1/L3 (L2 is molten — impassable) | win without losing a pylon | unit: **Teleporter** |
| 10 | **Assembly Cathedral** | enemy turrets on every pylon | destroy 4 turrets | building: **Tesla Turret** |
| 11 | **Crucible Line** | heavy-only enemy comp (counter lesson) | 20 counter-kills | unit: **Titan** |
| 12 | **[BOSS] The Anvil** | WARDEN+: firewalls 2 lanes alternating | trigger Surge | building: **Bastion Turret** + banner *Forgebane* |
**Keystone:** turrets +25% HP · battlefield skin *Foundry Ember* at 100%.

### R4 — SKYLINE (MIRROR, hard)
| # | Territory | Twist | Bonus objective | Reward |
|---|---|---|---|---|
| 13 | **Cloud Tramway** | wind: all air units +20% speed both sides | win air superiority for final 90 s | unit: **Gunship** |
| 14 | **Helix Towers** | vertical map skin; pylons at 25/75 instead | flip 4 pylons in one match | building: **Flak Turret** |
| 15 | **Signal Gardens** | MIRROR copies your *board specials* (their lasers strike you) | detonate 6 specials | unit: **Chronomancer** |
| 16 | **[BOSS] The Penthouse** | MIRROR prime doctrine | win in under 8 min | unit: **Juggernaut** + banner *Skyfall* |
**Keystone:** +1 starting Singularity on the board each battle · unit skin (Interceptor *Stratos*) at 100%.

### R5 — THE CORE (HELIOS-PRIME, finale)
| # | Territory | Twist | Bonus objective | Reward |
|---|---|---|---|---|
| 17 | **Blackwall Approach** | virus tiles every 45 s | never let virus spread past 3 tiles | tile skin set: **Corrupted Salvage** |
| 18 | **Memory Vaults** | locked tiles spawn in your highest-income gem color | reach combo ×8 (EMP) | banner *Vaultbreaker* |
| 19 | **Throne Conduit** | both prior doctrines, halved intensity | win with all 4 ground lanes controlled | unit skin (Titan *Aegis Gold*) |
| 20 | **[FINAL] HELIOS-PRIME** | 3 acts = 3 doctrines; board column corruption | flawless: no core tier lost | battlefield skin *Core Meltdown* + title **LIBERATOR** + credits |
**Keystone (campaign clear):** **Blackwall difficulty + The Breach endless unlock** · NG+ modifiers.

## 4. Counterattacks (the map fights back)

- After every **2nd conquest**, HELIOS raids one owned **border** territory (telegraphed on map: flag flickers red, one full session of grace).
- **Defense battle variant** (shorter, 4–5 min): you start owning all pylons + pre-built turrets; survive 3 acts of pure director assault. Loss ≠ losing the territory — its keystone/bonus goes **offline** until you replay and win (no progress destruction, Pillar 5; just pressure).
- Declining to defend is legal. The map shows the dark flag — guilt is the mechanic.
- Frequency cap: max 1 active raid; raids never target the territory you just took.

## 5. Difficulty & grading

- Per-territory selectable: **Story** (−30% director) / **Standard** / **Veteran** (+25%, hints off) / **Blackwall** (+50%, corrupted-board modifiers, post-campaign).
- **Stars (replay engine):** ★ win · ★★ win + bonus objective · ★★★ win + bonus + grade A (time/core HP/combo peak composite). Stars gate only cosmetics, never progress.
- Story events: 2-line briefing in, commander taunt mid-battle (text bark), 2-line debrief out. Region finales add a 15 s in-engine cutscene-lite (camera pass over the liberated district lighting up). **No lore dumps** — tone per GDD §2.2.

## 6. Reward economics (summary; full faucets in doc 04 refresh)
- Every territory pays: guaranteed reward (table above) + Data meta-currency (scaled by difficulty + stars) + commander XP.
- Tech tree (doc 05's 40 nodes) is funded by Data; conquest is the only gate on *content*, tech is the player-directed power curve. Replays pay reduced Data (anti-grind: 25% after first clear) — forward motion beats farming.
- Defeat pays 40% Data + full XP (Pillar 4: losses teach and pay).

## 7. FTUE contract (first 20 minutes)
1. **T1 Dockside Relay** (4 min): two lanes, three units, scripted 4-match → laser kill moment inside 90 s. Teach: match → deploy → laser.
2. **Interstitial:** map zoom-out reveals the whole dark city. "All of this is theirs. Take it back." Single line.
3. **T2 Saltglass Market** (6 min): four lanes, pylons introduced by a single tooltip + announcer line. Teach: forward spawns. The win lights the first city block cyan.
No walls of text. Every tutorial beat is a thing that happens *in play*, skippable for veterans via "I've played before" map toggle.
