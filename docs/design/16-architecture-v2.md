# NEON WARFARE — Technical Architecture v2 (Phase 8)
**Document 16 · 2026-06-12 · supersedes doc 07 where they conflict · grounded in the actual Unity 6 codebase**

Stack: Unity 6 (6000.4.11f1) · URP 17 (2D Renderer) · C# 12 · TextMeshPro · DOTween/PrimeTween · Addressables · NUnit (domain tests exist, extend).

---

## 1. Prime directive

**Deterministic, engine-free domains emit event tapes; presentation consumes tapes and never writes back.**

This already exists for the board (`BoardModel.TrySwap()` → `BoardEvent[]`) and it is the best code in the project. v2 extends the same pattern to combat and meta. Consequences we get for free: headless balance simulation, replay system (record tapes), reduced-motion accessibility (filter tapes), PvP-readiness (sync inputs, not state), and a presentation layer that can be rewritten without touching game logic — *which is exactly the rescue we are performing now; never again.*

## 2. Assembly map

```
┌────────────────────────────────────────────────────────────┐
│ NW.App            composition root, GameStateMachine,      │
│                   SceneFlow, SaveService, Services          │
├──────────────┬─────────────────────────┬───────────────────┤
│ NW.UI        │ NW.Presentation         │ NW.Data            │
│ screens,     │ battle views, VFX,      │ ScriptableObjects  │
│ HUD, cards,  │ audio router, juice     │ (defs + configs)   │
│ map          │ binders                 │                    │
├──────────────┴─────────────────────────┴───────────────────┤
│ NW.Board.Domain   NW.Combat.Domain   NW.Meta.Domain         │
│ (no engine refs — pure C#, deterministic, unit-tested)      │
└────────────────────────────────────────────────────────────┘
```
Dependency rule: arrows point down only. Domains reference nothing. `NW.Data` references domains (defs produce specs). Presentation/UI reference domains + data. App references everything. **CI check:** `noEngineReferences: true` stays on all three domain asmdefs.

| Assembly | Status | Work |
|---|---|---|
| `NW.Board.Domain` | exists, good | add: combo-clock, crossover events on tape, locked/virus tiles, partial cell locking |
| `NW.Combat.Domain` | exists, good core | add: `LaneState[4]`, `PylonState[8]`, formation slots, counter bonus, air superiority, acts, full event tape |
| `NW.Meta.Domain` | **new** | campaign graph, conquest/raid state, unlock inventory, grading, save model (pure data) |
| `NW.Data` | minimal (`GameDatabase`, `UnitDef`) | grow per §5 |
| `NW.Presentation` | **replaces** the 4 procedural View classes | tape consumers, pooled prefab views, juice binders |
| `NW.UI` | **new** | prefab/TMP screens; zero code-built layout |
| `NW.App` | replace bootstrap hack | state machine + scene flow + save |

## 3. The combat event tape (the contract everything binds to)

`CombatSim.Tick()` returns (or appends to a frame buffer) ordered events:

```csharp
// NW.Combat.Domain — all structs, no engine types
UnitSpawned(unitId, specId, team, lane, x)        // unitId = stable ulong, never list index
UnitMoved(unitId, x, slot)                        // slot: None/Front/Mid/Back
AttackLanded(attackerId, targetId, dmg, isCrit, isCounter)
UnitDied(unitId, isElite, killerTeam)
PylonProgress(pylonId, value)                     // −100..100
PylonCaptured(pylonId, team)
LaneControlChanged(lane, team)                    // both pylons
SurgeChanged(team, active)
AirSuperiorityChanged(team, active)
CoreDamaged(team, hp, tier)                       // tier: Pristine/Damaged/Critical
WaveTelegraphed(lane, eta)                        // director pre-announce
ActChanged(act)
BattleEnded(winner, stats)                        // stats: per-lane dmg × minute (defeat analytics)
TurretBuilt/TurretDestroyed(socketId, ...)
```

Binding law: **every row of the juice bible (doc 13) names exactly one event.** A juice item with no event is a spec bug; an event with no juice row ships silent and fails review. Board tape gains: `ComboStepped(n)`, `SpecialCreated/Detonated(kind, row, col)`, `CrossoverFired(kind, lane, dmg)`, `TileLocked/Infected/Cleansed`.

Current-build fix folded in: per-unit views keyed by **stable `unitId`** (sim-assigned counter), killing the index-keyed dictionary bug in `BattlefieldView`.

## 4. Systems design (replacing monoliths)

```
BattleScope (one battle's object graph, built by BattleInstaller)
 ├─ BattleSession           orchestrates: owns BoardModel + CombatSim + economy   (exists, keep)
 ├─ TapePump                drains board+combat tapes each frame → typed C# events
 ├─ Presentation binders    (each ≤150 lines, one concern)
 │   ├─ UnitViewSystem      pooled prefab per spec, follows UnitMoved w/ interpolation
 │   ├─ LaneViewSystem      floor tint mesh, frontline seam, pylon views
 │   ├─ CrossoverVfxSystem  lasers/orbital/EMP across both screens
 │   ├─ BoardViewSystem     gem prefabs, partial locking, specials
 │   ├─ JuiceRouter         event → MotionConfig/JuiceConfig lookups → tween/trauma/SFX
 │   └─ AudioRouter         event → SoundTheme cue, buses, pitch ladders, announcer queue
 ├─ HUDController           meters, cards, banners (UI prefabs, TMP)
 └─ InputController         board drag/click + card deploy paths + hotkeys
```

- No `GameManager`. `NW.App.GameStateMachine` is flow-only: `Boot → MainMenu → CampaignMap → Loadout → Battle → Results → CampaignMap`, each state = scene (Addressable) + installer. The `RuntimeInitializeOnLoadMethod` bootstrap survives **only** as an editor play-from-Battle convenience, compiled `#if UNITY_EDITOR`.
- DI: constructor injection via installers (manual or VContainer — decide at P0 start; manual is fine at this scale). The existing `Services` locator is retired outside `NW.App`.
- **Sim cadence (keep):** fixed `TickDelta = 0.05`, accumulator, deltaTime cap 0.05. Presentation interpolates between tick states for smooth motion at any fps.

## 5. Data (ScriptableObjects)

| SO | Contents |
|---|---|
| `UnitDef` (exists) | spec fields + **new:** prefab ref, class trim color, SFX set key, skin list |
| `FactionDef` | doctrine params (pylon prestarts, mirror rate, corruption cadence), boss script id, palette |
| `TerritoryDef` | map node: adjacency, faction, twist flags, director curve, comp weights, bonus objective id, reward ids, battlefield skin |
| `RegionDef` | 4 territories, keystone effect id, 100% cosmetic |
| `CosmeticDef` | track, unlock condition id, addressable key, preview |
| `BattleConfig` / `LaneConfig` | every number in docs 11–12 (pylon timing, surge %, counter %, caps) |
| `MotionConfig` / `JuiceConfig` | every duration/ease/trauma in doc 13, live-tweakable |
| `SoundTheme` / `VisualTheme` | hot-swappable cue/skin tables |
| `DirectorCurve` | budget/s per act, wave thresholds, telegraph lead |

Authoring rule: designers tune SOs in play mode; **no balance number lives in C#**. (Current debug constants in `BattleSession.cs` — 99999 HP, 30/20/20 start, 1.5 budget — migrate into `BattleConfig` and revert to design values as the first refactor commit.)

## 6. Meta & persistence

- `NW.Meta.Domain`: pure-data `CampaignState` (territory ownership/stars/raid flags), `Inventory` (unlocks, cosmetics, equips), `ProfileState` (XP, stats vault), all mutated through methods that return… events (`TerritoryConquered`, `CosmeticUnlocked`) — the map screen juices off a tape too.
- **Save:** versioned JSON, `SaveModelV1` with explicit migration chain, atomic write (temp+rename), Steam Cloud. Autosave on every Results screen + map mutation. Settings saved separately (instant, no migration needed).
- Unlock conditions = data: `ConditionDef` (event type + comparator + count) evaluated by an `AchievementService` subscribed to… the tapes. One pipeline, every system.

## 7. Testing & tooling

- **Domain tests (extend existing):** pylon flip timing, surge thresholds, counter math, slot assignment, director lane choice distribution, board virus spread, crossover damage share.
- **Balance harness (headless):** sim N matches of scripted bots (board-only bot vs army bot vs mixed) per territory config; assert anti-degeneracy bounds (doc 12 §4: crossover ≤20% damage share; doc 04 economy bounds). Runs in CI on `*.Domain` + `NW.Data` changes.
- **Replay tool (editor):** record tapes to file in playtests; scrub-play them through presentation. Doubles as the juice-review tool (watch any reported moment exactly).
- **Juice debug panel (play mode):** fire any event manually, sliders bound to `MotionConfig`/`JuiceConfig`. This is how doc 13 gets to 8/10 — iteration speed *is* the quality.

## 8. Performance budget (60 fps @ GTX 1050/Deck)

| System | Budget |
|---|---|
| Sim tick (board+combat) | ≤1.0 ms (currently µs-scale; keep domains allocation-free in tick path) |
| Unit views | 48 pooled, ≤0.5 ms update (transform sets only) |
| Particles | ≤200 alive, pooled emitters, LOD under load |
| UI | TMP + atlased sprites, zero per-frame layout rebuilds (dirty-flag meters), banner/popup pools |
| GC | 0 B/frame steady-state in battle (pools everywhere; tapes use pooled buffers) |
| Draw calls | ≤120 battle scene (sprite atlas + lane mesh batching) |

## 9. Migration order (matches doc 09 P0)
1. Extract `BattleConfig`/`MotionConfig` SOs; revert debug values.
2. Combat event tape + stable unit ids (domain, tested) — *the keystone; everything binds to it.*
3. Lanes/pylons/frontline/slots in domain + tests.
4. New scene layout (vertical) as prefabs; `TapePump` + binder skeletons; delete procedural views.
5. Board partial-locking + crossover events.
6. JuiceRouter/AudioRouter + debug panel; then execute doc 13 row by row.

*The strategy in one line: the sim is already a game engine — stop hand-painting over it and build the theater it deserves.*
