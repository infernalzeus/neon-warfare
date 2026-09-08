# NEON WARFARE

Premium futuristic strategy: a **match-3 reactor board** powers a **real-time auto-battler**. Unity 6 (6000.4.11f1) · URP · Steam premium. Full design suite: `../civibattle-remake/docs/design/` (docs 00–08).

## Status — M1 scaffold (backlog A1–A6 + B1/C2 domain cores)

| Layer | State |
|---|---|
| Unity project skeleton (packages, settings) | ✅ offline-resolvable manifest, URP 17.4.0 |
| `NW.Domain` — seeded RNG | ✅ |
| `NW.Board.Domain` — full match-3 model: swap/cascade/refill, line lasers, cross, singularity, special-chain recipes (S+S = EMP), combo multipliers, deadlock reshuffle, event tape | ✅ + edit-mode tests |
| `NW.Combat.Domain` — fixed-tick lane sim: 3 ground rows + air, class triangle, AoE, budget-driven enemy director, deterministic | ✅ + edit-mode tests |
| `NW.Core` — SO event channels, state machine, save service (versioned/atomic/backups), pooling, service registry | ✅ |
| `NW.Data` — UnitDef/HeroDef/ProtocolDef/MissionDef SOs + GameDatabase; balance seed (12 GDD units) + editor generator | ✅ |
| `NW.App` — Bootstrap composition root | ✅ |
| Views (board, battlefield, HUD), VFX, audio | ⬜ next |

## Open the project

1. Unity Hub → Add → this folder → open with **6000.4.11f1**. First import pulls packages (all pinned to versions bundled with the editor — no network surprises).
2. Run tests: **Window → General → Test Runner → EditMode → Run All** (board + combat domain suites must be green).
3. Generate content: **NW → Generate Database From Seed** creates the 12 `UnitDef` assets + `GameDatabase` from `Assets/_Project/Data/balance-seed.json`.

## Next steps (in order)

1. **DOTween Pro** — import from Asset Store (not on UPM); then `MotionConfig` SO + tween helpers.
2. Boot scene: empty scene + `Bootstrap` component wired to `GameDatabase`.
3. URP 2D renderer asset + global Volume (bloom) per doc 01 Part V.
4. `BoardView` consuming the event tape (gems = pooled sprites; feel-ladder doc 01 §4.1).
5. `BattlefieldView` interpolating `CombatSim.Units` + Cinemachine rig.
6. Battle HUD (command strip) + first playable loop.

## Architecture rules (doc 07)

- `NW.*.Domain` assemblies are **engine-free** (`noEngineReferences`) — they power the headless balance simulator and tests. Never add UnityEngine references to them.
- Balance lives in ScriptableObjects generated from `balance-seed.json`; defs are immutable at runtime.
- Cross-system communication via SO event channels only; `FeelSystem` (to be built) is the single arbiter of shake/hitstop/flash.
