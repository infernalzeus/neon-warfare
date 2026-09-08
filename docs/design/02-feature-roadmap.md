# NEON WARFARE — Feature Roadmap
**Document 02 of 09 · v1.0 · 2026-06-12**

Milestone-driven, vertical-slice-first. Durations assume a small team (1–2 engineers + contract art/audio) working with AI-assisted tooling; treat as relative sizing, not promises. **Rule: no milestone ships juice-less.** Polish is built per-milestone, not deferred to a mythical "polish phase" — game feel IS the product.

---

## M0 — PRE-PRODUCTION (this milestone) ✅ docs · ~2 weeks
**Goal: decide everything cheap to change on paper.**
- [x] Prototype audit (doc 00)
- [x] GDD, economy, progression, UI/screens, architecture, backlog, Steam plan (docs 01–08)
- [ ] Art bible v1: palette boards, 3 unit concept paints, 1 battlefield mock, UI style frame (key screen mock in Figma/image)
- [ ] Audio direction reel: 2 music sketches, 10 SFX sketches
- [ ] Unity 6 project scaffold per architecture doc (folders, asmdefs, packages, CI) — scaffold only, no gameplay
**Exit:** one battle-screen visual mock that passes the "is this key art?" test.

## M1 — VERTICAL SLICE · ~8 weeks
**Goal: 5 minutes that feel like the shipped game.** One mission, full juice.
- Match board: swap/cascade/refill + Line Laser, Cross Laser, Singularity + combo meter + full feedback ladder (GDD §4.1)
- Battlefield: 3 ground rows + air, 4 units (Drone, Shock Trooper, Titan, Interceptor), enemy budget-director, Core win/lose
- Cross-screen events: resource trails, 4-match battlefield laser, combo-5 orbital strike
- Hero: NOVA complete (passive/active/ultimate with full cinematic treatment)
- 1 Protocol draft moment with 9 protocols
- Battle HUD final-quality; placeholder-free art for everything on this screen; URP post stack; DOTween motion system; pitch-ladder audio; synthwave combat track (1)
- Debug panel v1: resource cheat, spawn menu, speed control, god mode
**Exit criteria (hard gates):** a first-time player completes the mission smiling; capture a 30s clip with zero placeholder art that could open a Steam page. **GO/NO-GO checkpoint: if the slice isn't fun, we iterate here — we do not advance.**

## M2 — CORE SYSTEMS COMPLETE · ~8 weeks
**Goal: every battle mechanic in, breadth-first.**
- All 12 units + abilities/cooldowns; class triangle tuning pass 1
- All special-tile chain combos (Laser+Laser, Singularity+Laser, Singularity×2 EMP)
- Plasma Storm / Time Freeze / Multiplier board events
- 30 protocols; rarity weighting; draft UI final
- Enemy elites ×3; difficulty levels; defeat-analytics panel
- Save system v1 (versioned, cloud-ready); settings screen (audio/video/accessibility core set)
- Object pooling everywhere; perf pass 1 (60fps min-spec with 48 units + cascading board)

## M3 — META LAYER · ~8 weeks
**Goal: the reason to play tomorrow.**
- Command Hub (base screen) with 5 buildings, levels, visual evolution, build/upgrade flow
- Tech tree screen: 40 nodes, 5 branches, respec
- Hero screen: 4 heroes, XP/levels, 18-node skill trees, ultimate upgrades
- Armory: unit blueprints + 3-rank upgrades
- Full economy loop: mission rewards → 4 meta currencies → sinks (doc 04 tuning v1)
- Mission select (sector map), star grading, loadout screen
- FTUE: 3 tutorial missions
**Exit:** a new player can play 3 hours with no dev guidance and always know what to do next.

## M4 — CONTENT PRODUCTION · ~10 weeks
**Goal: fill the world.**
- 39 campaign missions scripted (composition curves, modifiers, briefings)
- 3 bosses (WARDEN, MIRROR, HELIOS-PRIME) with bespoke mechanics + arenas
- Remaining 30 protocols (→60); heroes 2–4 ultimates cinematic pass
- The Breach (endless) + daily seed
- Full music (25 min), VO barks, announcer
- Balance pass 2 driven by telemetry from playtests (sim harness: headless 1000-battle autobalancer — see architecture doc)

## M5 — POLISH & HARDENING · ~6 weeks
- VFX/audio final pass against feel-ladder contract; photosensitivity audit
- Controller/Deck verification pass; UI scale & colorblind QA
- Perf: 4K/60 high-spec, 60fps Deck; memory & load-time budget enforcement
- Achievements (30), cloud save, rich presence
- Localization: EN + FIGS + zh-Hans + ja + ko + pt-BR (UI strings architected since M1)

## M6 — DEMO & LAUNCH RAMP · ~6 weeks
- Steam page + trailer (cut from real gameplay) — page live as early as M4 to bank wishlists
- Demo build: sector 1, 2 heroes, Next Fest entry
- Closed beta (500 keys), telemetry-driven tuning
- Launch candidate, day-one patch buffer
**→ 1.0 LAUNCH**

## Post-1.0 (free updates — premium goodwill engine)
1. **Update 1 "Ghost Protocol":** 5th hero + 15 protocols + weekly challenge ladder
2. **Update 2 "Twin Dive":** co-op (one player boards, one commands) — prototype first, ship only if great
3. **Update 3 "The Forge":** Steam Workshop — community SoundThemes + mission seeds
4. Mobile port evaluation (layout is portrait-native by design)

---

## Dependency spine
```
M0 docs ─► M1 slice (fun gate) ─► M2 breadth ─► M3 meta ─► M4 content ─► M5 polish ─► M6 launch
                 │                                  │
                 └── Steam page assets ◄────────────┘ (wishlists compound — start early)
```

## Top risks & mitigations
| Risk | Mitigation |
|---|---|
| Split-attention is fun for us, stressful for players | M1 playtest with externals; difficulty modes pace enemy budget, not player APM; Time-Freeze/auto-pause assists on Story difficulty |
| Art budget can't reach "premium" | Identity lives in shaders/VFX/motion (cheap, code-driven); buy base meshes/sprites, re-shade into locked palette; bespoke spend only on 4 heroes + key screens |
| Scope (this roadmap is ~48 weeks) | Fun gate at M1; cut order pre-agreed: co-op > endless daily > hero 4 > sector 3 elites > protocol count 60→40. Pillars and juice are never cut. |
| Balance complexity (12 units × 60 protocols × 4 heroes) | Headless battle simulator from M2; data-driven everything; telemetry in beta |
