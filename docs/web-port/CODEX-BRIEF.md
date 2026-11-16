# Codex brief: NEON WARFARE web version (v4)

You are building the web version of NEON WARFARE as a new project. Read this whole file first, then `docs/design/19-web-port.md` (the contract) in this repo. If anything here conflicts with that contract, the contract wins and you tell the owner.

## Priorities (owner decisions, 2026-10-27)

1. **A stable, playable free web demo first.** Stability beats polish: a plain version that never breaks is better than a beautiful one that sometimes does. Ship each phase in a working state.
2. **Out of scope for now:** itch.io packaging, paid cosmetics, any payment code. Cosmetics are a later, separate project.
3. The Unity game stays on the owner's PC and is **not** on GitHub as part of this. The GitHub project is the web version only.

## Repository hygiene (hard rules)

- The `neon-warfare-web` repo contains the web project only: no Unity code, no Unity project files, no copies of Unity source.
- Firebase config values and anything environment-specific are **not committed**. Read them from `.env.local` (gitignored); commit only `.env.example` with empty names. The GitHub Actions workflow takes them from repository variables/secrets; document that in `SETUP.md`.
- `.gitignore` from the first commit: `node_modules`, `dist`, `.env*` (except `.env.example`), logs, editor folders, any `*.rules` drafts or service-account/credential files.
- Allowed to commit from this repo's `docs/web-port/`: the fixtures and `verify-fixtures.mjs`, copied into `test/golden/` (data and a checker, no Unity code). Do not commit `firestore.rules.proposed` or `CODEX-BRIEF.md`.

## Rules of engagement

- Read-only on this repo (`N:\Code\git repositories\My Repo\neon-warfare`). Do not edit it, commit to it, or run Unity.
- Build in a new sibling project: `N:\Code\git repositories\My Repo\neon-warfare-web` (own git repo, Vite + TypeScript).
- Commit locally. The owner reviews and pushes. Never push, and never change GitHub or Firebase settings or rules yourself.
- Do not read or write any LLM-Wiki / Obsidian files.
- Division of labour: **you** own design, rendering, UI, build, deploy. **Claude** owns the sim rules, the Firestore rules, the fixtures and bug triage. If the sim looks wrong, or a fixture will not match, write it up (fixture name, first mismatching tick/step, expected vs got) and stop on that item. Never change sim behaviour to make something look right.
- Mark every claim `verified` (you ran it) or `assumed`. Report real sizes and timings. No invented numbers. Ask before adding paid services or dependencies with licence limits.

## How the owner reviews (applies to every phase)

The owner pushes your commits to GitHub and plays the result on GitHub Pages. So:

1. Phase 0 comes first and proves push-to-play works.
2. Every phase ends with a deployable build that works, plus a `REVIEW.md` section: exact steps to try, and what they should see.
3. The title screen shows a build stamp (short commit hash and build time).
4. Asset filenames are content-hashed so caches never serve stale files.
5. `main` never contains a build that fails.

## Inputs you get from this repo

- `docs/design/19-web-port.md`: the contract (what must match, determinism rules, data model, open items).
- `docs/web-port/fixtures/*.json`: golden-master outputs of the real C# code. Copy them into your project (`test/golden/`) so it is self-contained. Re-copy when the owner says Claude regenerated them.
- `docs/web-port/verify-fixtures.mjs`: working reference code for the Rng, the state hash, `dailySeed`, `scoreTicks` (round-half-even), Elo/tiers and the username key. Copy the functions you need into TypeScript; run the script after each re-copy to prove the fixtures are intact.
- `docs/web-port/firestore.rules.proposed`: the rules you should code against (read-only for you).
- Source to port (read it, translate faithfully, do not redesign): `Assets/_Project/Scripts/Domain/Rng.cs`, `Combat/Domain/CombatSim.cs` + `CombatTypes.cs`, `Board/Domain/BoardModel.cs` + `BoardEvents.cs`, `App/UnitCatalog.cs`, `App/LevelConfig.cs`, `App/BattleSession.cs`, `App/RankLadder.cs`, ghost files in `App/`.
- Art: `Assets/_Project/Resources/TroopSheets/` (72 units x 3 motions, 1536x1024, 6x4 grid of 256x256 cells, 92 MB of PNG) and `Resources/Currency/` (5 gems + token). Your own earlier review project (`~/.codex/.chatgpt-projects/.../output/neon-review/`) holds the responsive battle-interface prototype and component art.

## Phases

**Phase 0 - skeleton and deploy.** Vite + TypeScript. One page: title, build stamp, Firebase anonymous sign-in showing the user id. A GitHub Actions workflow that builds and deploys to GitHub Pages (base path `/neon-warfare-web/`). `SETUP.md` listing the owner's manual steps (create repo; set Pages source to GitHub Actions; enable Anonymous and Email/Password in Firebase Auth; add authorized domains `infernalzeus.github.io` and the itch.io iframe host (check the real host once an itch page exists)). Stop and wait for the owner to confirm the live URL works.

**Phase 1 - sim port (TypeScript, no DOM).** Port `Rng`, `CombatSim`, `BoardModel`, `UnitCatalog`, `LevelConfig`, `BattleSession`. Follow the determinism rules in the contract section 3 (`Math.fround` on every float operation; `Math.imul`; unsigned math; no re-ordering of events). Provide `npm run fixtures` that runs every fixture and reports, per file, pass/fail and the first mismatching tick/step/row. **Done only when every fixture passes exactly.** Do not start Phase 2 until then.

**Phase 2 - game client.** PixiJS or Three.js (your recommendation, with reasons). Stable first, then ambitious: get a fully playable version working end to end before adding effects. Target a 2.5D diorama: tilted camera, billboarded painted troops, parallax, lanes that bend toward the cores, a camera that follows the frontline, a foreground board with energy streams from matched gems into the battlefield, a visible lane-open/lane-dormant treatment (levels 1-3 use 1 lane, 4-7 use 2, 8+ use 3). Portrait first, usable on desktop. Rendering reads sim state only; the sim runs on a fixed 0.05 s step independent of frame rate.

**Phase 3 - reuse your own UI work.** Use your responsive battle-interface prototype and component art, the painted gem/token art, and the hierarchy fixes from your own UI audit (wallet, mission identity, profile hierarchy, shared panel system). Landing, profile, level select and cosmetics pages must feel premium, not default.

**Phase 4 - art pipeline.** Convert the troop sheets to WebP or AVIF, load one theme at a time (9 units x 3 motions), report real sizes. Budget: first playable under 8 MB, under 5 s on a mid-range phone.

**Phase 5 - accounts and data.** Follow contract sections 7.1-7.3 exactly: username + password via the derived-email scheme, `usernames` claim in a transaction, `linkWithCredential` from the anonymous account, cloud saves at `users/{uid}/slots/{n}` and `users/{uid}/profile/main`, mirrored to local storage. The leaderboard document shape and daily-board key are fixed by 7.1. `recordedUtc` is **0** for now (open item). Provide screens for create profile / sign in / sign out with clear errors (name taken, wrong password, weak password) and a plain warning that a forgotten password cannot be recovered. Test sign-in inside an iframe. Firebase config comes from `.env.local` and is never committed (see Repository hygiene); no admin or service-account credentials, ever.

**Phase 6 - not now.** itch.io packaging and paid cosmetics are deferred until the free web demo is stable and the owner asks.

## Not in scope for you

Changing game rules, balance, unit stats or level tables. Editing Firestore rules. Unity work. Anything under the wiki. Payments, cosmetics shop, itch.io.
