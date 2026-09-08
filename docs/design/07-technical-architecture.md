# NEON WARFARE — Technical Architecture
**Document 07 of 09 · v1.0 · 2026-06-12**

# 0. Engine decision (explicit)

The prototype is Godot 4/GDScript; the mandate is Unity 6 + URP + DOTween + Cinemachine + Addressables. Decision: **fresh Unity 6 (6000.x LTS) project.** Rationale: zero binary assets exist to migrate; ~55KB of GDScript is rewritten faster than ported; the mandated middleware (DOTween/Cinemachine) is Unity-native; the asset-store pipeline for premium sci-fi art is materially stronger on Unity. The Godot project stays frozen as a *reference implementation* — its board-resolve math, lane-combat behavior, and tuned timing constants are the spec for B1/C2. Its JSON balance files seed the SO database. **No code is ported.**

# 1. Stack
| Layer | Choice |
|---|---|
| Engine | Unity 6 LTS, URP **2D Renderer** (2D lights, bloom via Volume) |
| Lang/runtime | C# 12, async via **UniTask** (no coroutines in gameplay code) |
| Tweening | DOTween Pro (sequenced via `MotionConfig` SO tokens) |
| Camera | Cinemachine 3 (impulse sources for shake profiles) |
| Input | Input System (action maps: Board / Command / UI; M+KB, pad, touch) |
| UI | UI Toolkit for meta screens (themeable via USS variables ← `UIThemeConfig`); uGUI+DOTween for battle HUD (perf-critical, particle-integrated) |
| Content | **Addressables** (groups: Core, Battle, Units, Heroes, UI, Audio, PerSector) |
| Audio | Unity Audio + central mixer (4 buses: Music/SFX/UI/VO) behind `SoundTheme` SO indirection; FMOD evaluated at M2 gate if layering needs outgrow mixer snapshots |
| Tests | NUnit edit-mode for pure logic; play-mode smoke per screen; headless sim (§8) |

# 2. Assemblies & folders
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/        NW.Core      — state machine, services, events, pooling, save
│   │   ├── Data/        NW.Data      — SO definitions + runtime models (no Unity scene deps)
│   │   ├── Board/       NW.Board     — match-3 domain (pure C# core + view layer)
│   │   ├── Combat/      NW.Combat    — units, director, heroes, protocols
│   │   ├── Meta/        NW.Meta      — economy, tech, base, campaign, missions
│   │   ├── UI/          NW.UI        — screens, components, HUD
│   │   ├── Audio/       NW.Audio
│   │   └── Debug/       NW.Debug     — stripped from release via define
│   ├── Settings/        URP assets, volumes, input actions, mixers
│   ├── Data/            SO instances (units, heroes, protocols, tech, missions…)
│   ├── Art/  Audio/  Prefabs/  Scenes/  VFX/  Localization/
└── Plugins/
```
Dependency rule (enforced by asmdefs): `Core ← Data ← {Board, Combat, Meta} ← UI`; nothing references `Debug`; **`NW.Board.Domain` and `NW.Combat.Domain` are pure C# (no UnityEngine)** so the simulator and unit tests run headless.

# 3. Data-driven design (ScriptableObjects)
Every balance/content fact is an SO: `UnitDef, HeroDef, SkillNodeDef, ProtocolDef, TechNodeDef, BuildingDef, MissionDef, GemDef, SpecialTileDef, DifficultyDef, MotionConfig, UIThemeConfig, SoundTheme, FeelProfile` (shake/hitstop/flash budgets). Conventions:
- Defs are immutable at runtime; runtime state lives in plain C# models (`UnitState`, `EconomyState`) — never mutate SOs.
- Every Def has a stable string `id` (saves reference ids, not asset refs).
- CSV/JSON export-import round-trip for balance sheets; editor validation (`OnValidate` + a project-wide audit window: missing refs, orphan ids, cost-curve outliers).
- The prototype's `units.json` values import as the first balance seed where overlapping.

# 4. Event-driven core
SO **event channels** (typed: `VoidEvent, IntEvent, MatchClearedEvent, UnitSpawnedEvent…`) for cross-system signals — the Unity equivalent of the prototype's signal wiring, keeping Board/Combat/UI mutually ignorant:
```
BoardChannel.MatchCleared(size, chain, gemType, worldPos)
  ├─ EconomySystem  → income
  ├─ CrossoverSystem→ laser/orbital/EMP battlefield effects
  ├─ FeelSystem     → shake/hitstop/flash (budget-arbitrated)
  └─ AudioSystem    → pitch-ladder cue
```
`FeelSystem` is the single arbiter of camera shake / hitstop / slow-mo / flash so stacked events respect global caps (photosensitivity setting lives here).
**GameStateMachine** (Core): `Boot → Title → Hub → MissionSetup → Battle{Intro, Running, Draft, Outro} → Results`, async transitions, each state owns Addressables loads/releases.

# 5. Battle architecture
- **Fixed-tick simulation** (50ms) for combat logic, decoupled from render (interpolated views). Determinism by design: seeded RNG streams (board / director / combat) → replayable bugs, daily seeds, headless sim for free.
- Units = pooled view prefabs bound to `UnitState`; behavior = lightweight state pattern (March/Engage/Ability/Die), no per-unit Update — `CombatSystem` ticks arrays (cache-friendly; 48 units is small, but boss waves + protocols can triple counts).
- Director: budget accumulator + weighted comp table per `MissionDef` (doc 04 §1.4).
- Protocols: effect framework = composable `IProtocolEffect` (StatMod / Trigger / SpawnHook / BoardHook) authored in SOs — new protocols are data, not code, after the first ~10 archetypes.

# 6. Board architecture
Pure-C# `BoardModel` (grid, resolve loop, specials, RNG stream) emitting an **event tape** (`TileCleared, SpecialCreated, CascadeStep…`); `BoardView` consumes the tape and schedules DOTween visuals. Benefits: unit-testable match logic (port prototype's resolve semantics + timing constants as test fixtures), AI/autoplay for sim, and view can lag animation without desyncing logic.

# 7. Save system
- JSON, versioned (`schemaVersion` + ordered migration chain), atomic write (temp+swap), 3 rotating backups; Steam Cloud via auto-cloud folder.
- Saved: campaign/grades, currencies, tech, hero xp/skills/ascension, buildings, ranks, settings, stable protocols, stats/achievement progress. Never saved: mid-battle state (missions are short; abandon = defeat-lite payout).
- Anti-corruption: checksum field; on mismatch load newest valid backup + toast.

# 8. Balance simulator
Headless harness referencing only Domain assemblies: plays N seeded battles with scripted "player skill" profiles (APM, target-priority quality) × comps × protocols; outputs win-rate / mission-length / income CSVs. CI job runs the standard sweep on every balance-data PR; doc 04's anti-degeneracy audits are encoded as failing assertions (e.g., single-unit comp win-rate >55% fails the build).

# 9. Performance budgets (gates, profiled per milestone)
| Metric | Budget |
|---|---|
| Min-spec (GTX 1050 / Deck) | 60 fps during 5-match cascade + 48 units + ult |
| Frame: sim / view / UI | 2ms / 6ms / 2ms @min-spec |
| Draw calls battle | <150 (sprite atlases per Addressables group; UI batched) |
| GC | 0 alloc/frame steady-state (pooling, no LINQ in tick, cached strings; damage numbers via pooled TMP) |
| Particles | hard caps per FeelProfile; pooled `ParticleSystem`s, no runtime instantiation |
| Load: boot→title / battle | <5s / <3s (Addressables preload during jack-in tunnel) |
| Memory | <2GB working set (per-sector content in own group, released on leave) |

# 10. Practices
- Conventions: nullable enabled, analyzers on, `NW.` namespaces mirror asmdefs; no singletons except a `ServiceLocator`-style `Game.Services` composition root (explicit registration in Boot).
- Localization-ready from M1 (string tables; no literals in UI).
- Telemetry opt-in, local-first queue.
- Git: trunk + short-lived branches; LFS for art; build per push (A2).
- Definition of done for any visible feature = function + feel-ladder entries + a11y check + perf budget held.
