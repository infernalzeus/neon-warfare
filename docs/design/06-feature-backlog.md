# NEON WARFARE — Feature Backlog
**Document 06 of 09 · v1.0 · 2026-06-12**

Epics → stories, MoSCoW priority, T-shirt size (S<2d, M<1w, L<3w, XL>3w), milestone tag (doc 02). This is the working backlog seed; it moves to the issue tracker at M1 kickoff. Acceptance criteria reference the GDD feel-ladder (01 §4) — "done" includes juice, not just function.

## EPIC A — Project Foundation [M0–M1]
| ID | Story | Pri | Size |
|---|---|---|---|
| A1 | Unity 6 project scaffold: folders, asmdefs, URP 2D, packages (DOTween, Cinemachine, Input System, Addressables, UniTask) | MUST | S |
| A2 | CI: build on push (Windows/Linux), play-mode test runner | MUST | M |
| A3 | SO event-channel framework + GameStateMachine (doc 07 §3–4) | MUST | M |
| A4 | MotionConfig + UIThemeConfig token SOs, theme bootstrap | MUST | S |
| A5 | Object pool service (generic, addressable-aware) | MUST | S |
| A6 | Save service v1: versioned JSON, slots, cloud-path compliant | MUST | M |

## EPIC B — Compiler Board [M1–M2]
| ID | Story | Pri | Size |
|---|---|---|---|
| B1 | Grid model + swap/match/cascade/refill (port prototype math, unit-tested headless) | MUST | M |
| B2 | Gem visuals: 5 holographic gems, unique silhouettes, idle bob, hover/swap feedback | MUST | L |
| B3 | Special tiles: Line/Cross Laser, Singularity + creation rules | MUST | L |
| B4 | Special chain combos (L+L, S+L, S+S → EMP) | MUST | M |
| B5 | Combo meter + pitch-ladder audio + multiplier logic | MUST | M |
| B6 | Cross-screen events: resource trails, battlefield laser hits, orbital strike, EMP | MUST | L |
| B7 | Board events: Plasma Storm, Time Freeze, Multiplier zone | SHOULD | M |
| B8 | Deadlock reshuffle + "RESHUFFLING" hologram | MUST | S |
| B9 | Boss board corruption tiles (locked/virus) for HELIOS-PRIME | SHOULD | M |

## EPIC C — Battlefield [M1–M2]
| ID | Story | Pri | Size |
|---|---|---|---|
| C1 | Lane world: 3 ground rows + air layer, parallax circuit-city environment | MUST | L |
| C2 | Unit runtime: data-driven stats, targeting, class triangle, abilities/cooldowns framework | MUST | L |
| C3 | 12 units implemented to spec (GDD §3.4) w/ animations + per-unit VFX/SFX | MUST | XL |
| C4 | Enemy director: budget curve, comp weights, wave telegraphs | MUST | M |
| C5 | Damage numbers, hit-stop, slow-mo-on-elite-kill, de-rez deaths | MUST | M |
| C6 | Nexus cores: visuals, HP states, win/lose sequences | MUST | M |
| C7 | Cinemachine rig: drift, shake profiles, ult punch-ins | MUST | S |
| C8 | Elites ×6 + 3 bosses w/ bespoke mechanics | MUST | XL |
| C9 | Defense Grid turret integration | SHOULD | S |

## EPIC D — Heroes [M1, M3–M4]
| D1 | Hero runtime: passive/active/ultimate framework, charge model | MUST | M |
| D2 | NOVA complete incl. cinematic ult (vertical-slice quality bar) | MUST | L |
| D3 | Cyber Monk, Architect, Valkyrie-X | MUST | XL |
| D4 | Skill trees: data, UI, respec; ascension gates | MUST | L |
| D5 | Hero barks VO system | SHOULD | M |

## EPIC E — Protocols [M1–M2, M4]
| E1 | Draft flow: slow-mo, 3-card UI, rarity weights | MUST | M |
| E2 | Protocol effect framework (stat mods, triggers, spawn hooks) | MUST | M |
| E3 | Protocols ×30 (M2) → ×60 (M4) | MUST | L+L |
| E4 | Stable slots, rerolls, 4-choice tech hooks | SHOULD | S |

## EPIC F — Meta & Screens [M3]
| F1 | Command Hub: isometric base, building visuals ×5×5 levels, upgrade flow | MUST | XL |
| F2 | Tech tree screen: constellation, 40 nodes, purchase/respec | MUST | L |
| F3 | Mission select: sector map, grades, intel, rewards preview | MUST | L |
| F4 | Loadout screen | MUST | M |
| F5 | Results/defeat-analytics sequences | MUST | M |
| F6 | Armory: blueprints, ranks, inventory | MUST | M |
| F7 | Codex | COULD | M |
| F8 | Economy wiring: 4 currencies, faucets/sinks per doc 04 | MUST | M |
| F9 | FTUE: 3 tutorial missions + hub-reveal moment | MUST | L |

## EPIC G — Content [M4]
| G1 | 39 missions authored (curves, modifiers, briefs) | MUST | XL |
| G2 | The Breach endless + daily seed | SHOULD | L |
| G3 | NG+ modifiers | COULD | M |
| G4 | Music ×25min, full SFX set, announcer, mix pass | MUST | XL |
| G5 | SoundTheme hot-swap + Retro Arcade alt pack | SHOULD | M |

## EPIC H — Platform & Polish [M2, M5]
| H1 | Settings: video/audio/gameplay/accessibility/remap | MUST | L |
| H2 | Colorblind silhouettes, shake/flash sliders, photosensitivity cap | MUST | M |
| H3 | Controller + Deck verification pass | MUST | L |
| H4 | Perf budget enforcement (doc 07 §9): profiling gates per milestone | MUST | M |
| H5 | Achievements ×30, cloud save, rich presence | MUST | M |
| H6 | Localization framework (M1) + 7 languages (M5) | MUST | M+L |
| H7 | Telemetry (opt-in): mission funnels, economy rates, comp win-rates | SHOULD | M |

## EPIC I — Dev Tools [M1–M2]
| I1 | Debug panel: cheats/spawner/director/time/levels (doc 03 §3.9) | MUST | M |
| I2 | Test battle generator (seeded) | MUST | S |
| I3 | Headless balance simulator (1k battles/sweep) | SHOULD | L |
| I4 | Live tuning: SO hot-reload + feel sliders | SHOULD | M |

**WON'T (1.0):** co-op, PvP, mobile, Workshop, meta-narrative cutscenes, gacha/MTX of any kind (permanent WON'T for monetization).

**Cut order if schedule slips** (pre-agreed, from doc 02): G3 → F7 → G2 daily → E3 back to 40 → D5 → hero 4 to post-launch. Never cut: feel-ladder items, FTUE, accessibility MUSTs.
