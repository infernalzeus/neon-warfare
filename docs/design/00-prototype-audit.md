# NEON WARFARE — Prototype Audit
**Document 00 of 09 · Pre-production suite · 2026-06-12**
**Author role: Creative Director / Senior Engineer**

This audit covers the existing `civibattle-remake` Godot 4 prototype. Verdict up front: **the prototype is a competent mechanical proof-of-concept and nothing more.** The core hook (match-3 feeding a live auto-battler) is validated and fun in principle. Everything else — identity, presentation, depth, retention — does not exist yet. We keep the *lessons*, not the code.

---

## 1. What the prototype actually is

| Aspect | Current state |
|---|---|
| Engine | Godot 4.3, GDScript, single scene (`main.tscn`) |
| Art | 100% procedural `_draw()` — rectangles, stick figures, wooden planks |
| Audio | Runtime-synthesized beeps (`audio_manager.gd`) |
| Screens | Exactly one: the battle screen. Victory/defeat is a translucent `ColorRect` overlay |
| Units | 5 medieval units (militia, archer, knight, catapult, mage) in `units.json` |
| Resources | 6 medieval resources (wood, stone, food, iron, gold, mana) |
| Match-3 | 8×8 grid, swap/cascade/refill, deadlock reshuffle — solid logic, zero spectacle |
| Combat | 1D lane march, nearest-target, no abilities, no classes, no counters |
| Meta | None. `buildings.json` exists but is **never consumed by any code** |
| Progression | 5 levels that are stat-multiplier reskins of each other |
| Save | Minimal JSON autosave (level index + stockpile) |
| Spells | One (`fireball`), barely integrated |

Total scripts: 12 files, ~55KB. Code quality is actually good for a prototype — signal-driven, pooled, data-driven JSON balance. The *engineering hygiene* is worth keeping as a pattern; the *product* is not shippable.

## 2. Everything that feels prototype-quality (the indictment)

### Visual identity — F
- No art. Literally zero image assets. Units are stick figures with colored tunics drawn in `_draw()`.
- The "medieval theme" (`medieval_theme.gd`) is brown rectangles. No font choice, no palette discipline, no logo, no identity.
- One static camera, no lighting, no post-processing, no bloom, no shaders beyond Godot defaults.
- Tile gems are flat colored shapes. Matches scale-to-zero and emit ~20 default particles. No anticipation, no impact frames, no screen-space feedback hierarchy.

### Game feel / dopamine — D
- The good: cascades multiply payouts, big matches shake the battlefield, floating `+N` text exists. The skeleton of juice is there.
- The bad: no combo counter UI, no escalating audio pitch ladder beyond a 0.12 multiplier, no slow-motion on kill, no hit-stop, no special tiles, **no 4/5-match powerups at all** — a 5-match pays slightly more and looks identical to a 3-match. This is the single biggest wasted dopamine opportunity.

### Combat depth — D
- Units walk right, hit the first thing in range, die. No abilities, no cooldowns (only attack timers), no counters or class triangle, no formations, no air layer, no commander/boss behaviors.
- Enemy "AI" is a spawn timer that accelerates (`ramp: 0.97`). No threat reading, no composition strategy, no scripted moments.
- Death = disappear + orange particle puff. No corpses, no ragdoll, no kill confirm feedback.

### UX — D
- No main menu, no settings, no pause, no tutorial/FTUE, no tooltips beyond `desc` strings on buttons.
- Spawn bar buttons are default Godot `Button`s with text labels. No cost affordability states beyond disabled, no cooldown radials, no drag-to-deploy.
- Defeat screen offers "Retry" with zero information about *why* you lost.

### Progression / retention — F
- 5 levels, then "New Campaign" resets to level 0. Session 2 has nothing new.
- `buildings.json` defines 5 buildings with effects — **dead data, no screen, no code path consumes it.**
- No unlocks beyond `unlock_level` gating, no upgrades, no collection, no roguelite choice, no reason to replay.

### Audio — F
- Synthesized square-wave placeholder SFX. One generated music loop. No mixing, no ducking, no themes.

### Technical debt for our purposes
- GDScript / Godot: incompatible with the mandated Unity 6 + URP + DOTween + Addressables stack. Nothing ports.
- All rendering is immediate-mode `_draw()` — even in Godot this would have to be thrown away for a real art pipeline.

## 3. What we keep (the salvage list)

1. **The validated core loop** — match-3 income feeding real-time spawns creates genuine "play two games at once" tension. This is the product's soul. Keep it.
2. **Data-driven balance philosophy** — everything in JSON worked. Translates directly to ScriptableObjects + JSON export in Unity.
3. **Signal/event decoupling** — Match3 → GameState → HUD via signals maps 1:1 to ScriptableObject event channels.
4. **Specific tuned numbers as starting points** — swap time 0.16s, fall 0.09s/cell, cascade cap ×3, refund-on-failed-spawn, deadlock reshuffle guard. These were iterated and feel right; port the *values*.
5. **Object pooling discipline** — pooled tiles/particles/labels/units from day one. Keep the pattern.
6. **One genuinely smart design beat** — resource stockpile carries over between battles. Evolves into our meta-economy.

## 4. Root-cause diagnosis

The prototype fails commercially for one reason: **it answers "does the mechanic work?" and never attempts "why would anyone care?"** No fantasy, no identity, no escalation, no ownership (nothing is *yours* — no heroes, no base, no builds), and no second-session reason to return. The rebuild plan (documents 01–08) attacks exactly these gaps in priority order: identity → juice → depth → meta.

## 5. Disposition

- The Godot project is **frozen as a reference implementation** (board resolve logic, lane combat math, tuning values). It is not deleted and not extended.
- A new Unity 6 project, **NEON WARFARE**, is built from scratch per `07-technical-architecture.md`.
- `data/*.json` balance values seed the new ScriptableObject database where mechanics overlap.

→ Next: `01-game-design-document.md`
