# NEON WARFARE — Pre-production Design Suite
**v2.0 · 2026-06-12 · status: rescue redesign filed — doc 09 is the controlling document**

> **Read [09-rescue-redesign.md](09-rescue-redesign.md) first.** It critiques the current Unity build (2/10 overall), adds three systems the v1.0 suite lacked (lane ownership/frontline, territory-conquest campaign, cosmetic metagame), and supersedes docs 01/05/08 where they conflict.

A ground-up redesign of the `civibattle-remake` prototype into a premium futuristic strategy game: **match-3 reactor board powering a real-time auto-battler**, with heroes, roguelite drafting, base building, and a full meta layer. Unity 6 / URP / Steam premium.

| # | Document | Contents |
|---|---|---|
| 00 | [Prototype Audit](00-prototype-audit.md) | What the Godot prototype is, why it fails commercially, what we salvage |
| 01 | [Game Design Document](01-game-design-document.md) | Vision, pillars, world, full gameplay spec (board, combat, units, heroes, protocols), game-feel bible, art & audio direction |
| 02 | [Feature Roadmap](02-feature-roadmap.md) | Milestones M0–M6 + post-launch, fun-gates, risks, cut order |
| 03 | [Screens, UI System & Wireframes](03-screens-ui-system.md) | ARC//OS design system, screen flow map, wireframes for all 9+ screens, UX flows |
| 04 | [Economy Design](04-economy-design.md) | Tactical & meta economies, formulas, faucet/sink tables, anti-degeneracy audits |
| 05 | [Progression Design](05-progression-design.md) | Campaign structure, 40-node tech tree, hero skill trees, base buildings, FTUE contract |
| 06 | [Feature Backlog](06-feature-backlog.md) | 9 epics, ~60 stories, MoSCoW + sizing + milestone tags |
| 07 | [Technical Architecture](07-technical-architecture.md) | Engine decision, Unity 6 stack, assemblies, data/event design, sim, perf budgets |
| 08 | [Steam Release Plan](08-steam-release-plan.md) | Premium positioning, wishlist funnel, platform checklist, success metrics |
| **09** | [**Rescue Redesign**](09-rescue-redesign.md) | **CONTROLLING DOC** — build critique (2/10), v2 deltas, 9-phase plan, P0 definition |
| 10 | [Vision v2](10-vision-v2.md) | Pillars + kill-tests, pillar 6 (map tells the score), GIF test, scope guardrails |
| 11 | [Battlefield v2](11-battlefield-v2.md) | 4+1 lanes, pylons/frontline math, deploy paths, formations, structures, camera |
| 12 | [Board v2](12-board-v2.md) | Resources, specials, lane-targeting rule, crossover contract, animation timings |
| 13 | [Juice Bible v2](13-juice-v2.md) | Master feel table (5 channels × every action), trauma system, audio, accessibility |
| 14 | [Campaign v2](14-campaign-v2.md) | All 20 territories specced, 3 faction doctrines, counterattack raids, FTUE |
| 15 | [Metagame v2](15-metagame-v2.md) | Cosmetic catalog, ~40 achievements, seasonal pipeline, zero-MTX retention model |
| 16 | [Architecture v2](16-architecture-v2.md) | Event-tape contract, assemblies, SO schemas, testing/replay tooling, migration order |
| 17 | [Theme Art Bible v2](17-theme-art-v2.md) | Procedural unit-art pipeline, pose contract, livery rule, all 9 units specced for all 8 themes, implementation order |
| 18 | [Online Leaderboard (Firebase)](18-leaderboard-firebase.md) | Ghost-based ranked ladder → shared daily-board leaderboard on Firestore + Anon Auth; data model, `score` key, rules, index, cost, Firebase + Play Console setup steps. Local pool is the fallback; hand this to whoever wires the backend |

**v2 docs (09–16) supersede v1 docs (01–08) wherever they conflict.** v1 docs remain authoritative for: economy formulas (04), tech tree (05), heroes/protocols detail (01, P3-gated), Steam funnel (08).

**Reading order for newcomers:** 01 → 03 → 05 → 04 → 07.
**The one rule:** every feature serves a pillar (GDD §1.2) and ships with its feel-ladder entries (GDD Part IV) — function without juice is not done.

Next step (M1): Unity 6 project scaffold (backlog A1–A6), then the vertical slice — one mission that feels like the shipped game.
