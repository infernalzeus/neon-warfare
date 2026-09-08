# NEON WARFARE — Online Leaderboard (Firebase)
**Document 18 · 2026-09-05 (rev 2026-09-08: Android-only launch) · the shared daily-board leaderboard · supersedes doc 15 §4 "global leaderboard (depth)" storage notes**

The ranked ladder runs on **ghosts** (doc: `ghost-as-effect-trace`). Profiles and ghosts stay **local**.
This document covers only the **shared leaderboard**: how a better score on a level's daily board is
pushed to a Firestore collection every player reads.

> **Launch scope (2026-09-08): Android / Google Play only.** iOS steps below are kept for later and
> marked *(deferred)*. Firebase is **not** an Android-only service — a Firebase project can serve any
> number of platforms; for this launch we simply register one Android app and skip the iOS app.
> Audience is global; the game is **not** real-time PvP (async score writes + occasional reads), so
> the Firestore region is not latency-critical.

---

## 1. Decision

- **Mobile-first** → Steam Leaderboards are unavailable; Play Games / Game Center are number-only
  (can't carry a ghost blob) and platform-split. **Firestore + Anonymous Auth** is the fit.
- The game **fully works offline** on the local ghost pool. Firebase is additive — if the SDK is
  absent, sign-in fails, or the device is offline, `Leaderboard` stays on `LocalLeaderboardBackend`
  and nothing else changes.
- **No `Firebase.*` type appears outside `FirebaseLeaderboardBackend.cs`.** Everything else talks to
  the `Leaderboard` facade.

## 2. Identity

| | |
|---|---|
| **Auth** | Firebase **Anonymous** — each install gets a stable `uid`. No account, no PII, no friction. |
| **Display name** | the player's **local profile slot name** (`PlayerProgress.CurrentSlotName()`), stored in the row's `pilot` field. Players rename in `ProfileSelectScreen` (`SetSlotName` already exists). Un-named players show as `PILOT n` until they set one. |
| **Row key** | `PlayerProgress.PilotId` — a GUID in `PlayerPrefs`, generated on first use, survives slot switches. This is the Firestore document id; the anon `uid` is mapped onto it at sign-in. |
| **Cross-device later (optional)** | to make a score follow a person across reinstalls / phones, add Google Sign-In post-launch and `LinkWithCredentialAsync` the anon uid to it. **No schema change.** Not needed for v1. |

## 3. Firestore data model

```
leaderboards/{level}_{dailySeed}/entries/{pilotId}
    pilot          string     display name
    pilotId        string     == document id
    mmr            number
    result         number     1 = win, 0 = loss
    durationTicks  number      CombatSim ticks (20/sec)
    recordedUtc    number      DateTime.ToBinary()
    score          number      RankLadder.SortKey — see below
    ghost          string      the GhostRecord JSON, < 60 KB. Pulled ONLY on CHALLENGE, never while listing.
    updatedAt      timestamp   serverTimestamp
```

- **One row per pilot per daily board.** A better run overwrites; a worse run is dropped client-side
  (`RankLadder.Beats`). Bounds document count to `players × active-days × levels`.
- **`dailySeed`** = `RankLadder.DailySeed(level)` = `((y*10000+m*100+d)*31 + level) & 0x7FFFFFFF` on the
  UTC date. Everyone playing that level today shares one board.
- **`score`** collapses the sort rule (win > loss; fastest clear / longest survival; earliest breaks
  ties) into one monotonic number so the board ranks with a single `orderBy`:
  `score = (won ? 1e9 : 0) + (won ? 1e9 - durationTicks : durationTicks)`.
  `durationTicks` is minutes-scale (« 1e9) so win and loss bands never overlap.
- **Old boards** just accumulate (a stale daily board is a few dozen tiny docs). A scheduled
  Cloud Function to delete `updatedAt < now-30d` is a later nicety, not required.

## 4. Cost (Firestore Spark / free tier)

| Limit (per day) | This game |
|---|---|
| 50 K document reads | opening RANKS = ≤ 8 reads (one query, `limit 8`). Thousands of opens/day fit. |
| 20 K writes | one write per *personal best* per level per day. Comfortable early. |
| 1 GiB stored | a ghost is a few KB; 20 K rows ≈ 100 MB. |
| 10 GiB/month egress | listing 8 rows ≈ 40 KB (blobs ride along — see note). |

**Note:** the Unity SDK can't field-mask a query, so `FetchTop` downloads the `ghost` blob of every
listed row even though it isn't shown. 8 rows × ~5 KB = fine. If a board ever lists 50+, move the
blob to a `ghostRef` in Firebase Storage and fetch lazily. Not needed at launch.

Blaze (pay-as-you-go) at small scale is cents/month; enable it only when you exceed Spark.

---

## 5. Setup — Firebase project

1. **console.firebase.google.com** → *Add project* → name it (e.g. `neon-warfare`). Disable Google
   Analytics for the project (we don't use it; keeps the data-safety story simple).
2. *Project settings* → *Your apps* → **Add app → Android**:
   - Package name: `com.zeusengine.neonwarfare`
   - App nickname: `NEON WARFARE (Android)`. Debug SHA-1: **skip** (only needed for Google Sign-In /
     Dynamic Links / Play Integrity — none used).
   - Download **`google-services.json`** → put it in `Assets/` (root of Assets, not a subfolder).
3. **Add app → iOS** — *(deferred — not shipping iOS at launch)*. When the time comes: bundle id from
   `PlayerSettings → iOS`, download `GoogleService-Info.plist` → `Assets/`.
4. *Build* → **Firestore Database** → *Create database* → **Native mode** → **production mode**
   (rules in §7). Region: **not latency-critical** (async only). Launch pick: **`europe-west1`**
   (single-region, EU data residency — dev is EU-based; `us-central1` is marginally cheaper).
   **Permanent once created.**
5. *Build* → **Authentication** → *Get started* → **Sign-in method** → enable **Anonymous**.

## 6. Setup — Unity

1. Download the **Firebase Unity SDK** (firebase.google.com/download/unity). Import **only**:
   - `FirebaseAuth.unitypackage`
   - `FirebaseFirestore.unitypackage`
   - (the External Dependency Manager comes bundled — let it import)
   - **Do NOT import** `FirebaseAnalytics`.
2. Let **EDM4U** resolve Android dependencies (*Assets → External Dependency Manager → Android
   Resolver → Force Resolve*).
3. `PlayerSettings → Other Settings → Scripting Define Symbols` → add **`NW_FIREBASE`** for
   **Android**. This is what compiles `FirebaseLeaderboardBackend.cs` in.
4. Android: `minSdkVersion` is already 25 (fine — Firebase needs ≥ 23). **Target API 35** — Play's
   current floor for new apps; `AndroidTargetSdkVersion` is currently `0` (auto), verify Unity
   resolves it to ≥ 35 at build, else set it explicitly and install the platform via Hub.
5. Play the game once in the editor — the console prints `[Leaderboard] Firebase backend online.`
   when sign-in succeeds; `RANKS` header then shows `● ONLINE`.

## 7. Security rules

*Firestore → Rules*:

```
rules_version = '2';
service cloud.firestore {
  match /databases/{db}/documents {

    match /leaderboards/{board}/entries/{pilotId} {

      allow read: if true;

      allow create, update: if request.auth != null
        && request.auth.uid != null
        && request.resource.data.pilotId == pilotId
        && request.resource.data.keys().hasOnly(
             ['pilot','pilotId','mmr','result','durationTicks','recordedUtc','score','ghost','updatedAt'])
        && request.resource.data.pilot is string
        && request.resource.data.pilot.size() <= 24
        && request.resource.data.result in [0, 1]
        && request.resource.data.durationTicks is int
        && request.resource.data.durationTicks >= 0
        && request.resource.data.durationTicks < 1000000000
        && request.resource.data.mmr is int
        && request.resource.data.ghost is string
        && request.resource.data.ghost.size() < 60000
        && request.resource.data.score ==
             (request.resource.data.result * 1000000000
              + (request.resource.data.result == 1
                   ? 1000000000 - request.resource.data.durationTicks
                   : request.resource.data.durationTicks));

      allow delete: if false;
    }
  }
}
```

Rules enforce **shape, size, and that `score` matches the formula** — they cannot verify a score is
*legitimately achievable*. That's acceptable for a cosmetic ladder; because the row carries the
ghost, a later pass can replay it to validate. Note: anyone can `pilotId`-spoof another player's
row only if they know that id (a GUID) — low risk, and `delete` is denied.

## 8. Composite index

The `FetchTop` query is `orderBy(score, desc).limit(n)` on a subcollection — a **collection-group
is not used**, so a single-field descending index on `score` is auto-created. The `FetchAroundMe`
queries (`where score > x orderBy score asc` and `where score < x orderBy score desc`) also work on
the single-field index.

If Firestore logs a *"query requires an index"* link on first run, click it — it pre-fills the
console. Expected index: collection `entries`, field `score` (Ascending **and** Descending).

---

## 9. Play Console — deployment (Android)

App draft already created; package `com.zeusengine.neonwarfare` (locked at first upload).

1. **App content / "Set up your app" checklist** — every item:
   - **App access** — *All functionality available without special access* (no login).
   - **Ads** — *No ads*.
   - **Content ratings** — questionnaire, category *Game*, contact `infernalzeus69@gmail.com`.
     Answer: mild/fantasy **violence** (stylised laser combat, no blood/gore); **No** to sex,
     language, controlled substances, gambling; user interaction = leaderboard scores only.
     Submit → accept the generated ratings.
   - **Target audience & content** — age groups **13–15, 16–17, 18+** (not under 13); *not* designed
     to appeal to children → **Families policy does not apply** (we also carry no ad SDK / analytics).
   - **Data safety** (also under App content):
     - Collects data: **Yes**.
     - *Device or other IDs* — collected, **not shared**, purpose **App functionality**, required.
       (Firebase anonymous `uid` + the `PlayerProgress.PilotId` install GUID.)
     - *App activity → Other user-generated content* — collected, not shared, App functionality,
       required. (The pilot name the user types + their per-match result row on the board.)
     - **No** advertising / analytics / location / personal info / financial info / messages.
     - Encrypted in transit: **Yes**. Deletion available: **Yes** — `infernalzeus69@gmail.com` +
       stated in the privacy policy.
   - **News app** / **COVID-19 contact tracing** / **Government apps** / **Financial features** /
     **Health** — all **No**.
   - **Privacy policy** — URL is mandatory. `PRIVACY.md` / `docs/privacy-policy.html` in this repo
     is the source; host it on GitHub Pages and paste that URL.
2. **App signing** — Play App Signing is on by default. Create an **upload keystore** in Unity
   (`Player Settings → Publishing Settings → Keystore Manager → Create New`), strong password,
   **back it up outside the repo** (`*.keystore` / `*.jks` are gitignored). If the upload key is
   ever lost, Play can reset it; the Google-held app-signing key is safe regardless.
3. **Store listing** (placeholder pass for Internal Testing):
   - Name `NEON WARFARE`; short description ≤ 80 chars; full description ≤ 4000.
   - Category **Games → Strategy**; contact email; **privacy policy URL**.
   - Icon 512², feature graphic 1024×500, ≥ 2 phone screenshots. Rough art is fine for Internal
     Testing; production rollout requires final art.
4. **Build the AAB** — `Build Settings → Android`, check **Build App Bundle (Google Play)**, Switch
   Platform if needed, Build → `neon-warfare/build/*.aab` (gitignored).
5. **Internal testing** (`Test and release → Testing → Internal testing`):
   - *Create new release* → upload the AAB → release name `1.0 (1)` → notes → **Save** (not "Review
     release" until ready).
   - *Testers* → add an email list with your account.
   - Copy the **opt-in URL** → open on device → install → verify `● ONLINE`, a submitted score in
     the Firestore console, and CHALLENGE pulling a ghost.
6. Later: **Closed → Open → Production** as confidence grows. First production review ~ a few days.
7. **iOS / App Store** — *(deferred, not this launch)*: App Store Connect app,
   `GoogleService-Info.plist` in Xcode, App Privacy questionnaire mirroring the Data-safety answers,
   ATT not required (no tracking), TestFlight before submit.

## 10. Split of work

| Done in code (this session) | Yours |
|---|---|
| `ILeaderboardBackend`, `LocalLeaderboardBackend`, `Leaderboard` facade | Firebase project + Firestore + Anonymous Auth (§5) |
| `FirebaseLeaderboardBackend` (behind `#if NW_FIREBASE`) — sign-in, submit-if-better, top-N, around-me, fetch-ghost | SDK import + `google-services.json` + `NW_FIREBASE` define (§6) |
| `PlayerProgress.PilotId`, `GhostRecord.pilotId`, `RankLadder.Beats` / `SortKey` / explicit-seed `Standings` | Paste rules (§7), create index if prompted (§8) |
| Submit on every match end; RANKS → LADDER standings view, `● ONLINE` / `○ LOCAL` chip, CHALLENGE fetches the blob | Play Console: data safety, privacy policy, signing, staged rollout (§9) |
| `LeaderboardTests.cs` — `Beats`/`SortKey` invariant, explicit-seed `Standings`, facade swap | — |

## 11. Open

- Bundled ghost pool in `Resources/Ghosts/` so LADDER isn't empty pre-launch.
- BREACH weekly-seed endless mode still stubbed.
- Server-side / on-device ghost **replay validation** of submitted scores (anti-cheat) — the ghost
  blob makes it possible; deferred.
- Old-board cleanup Cloud Function once row volume justifies it.

## Related

- `docs/design/15-metagame-v2.md` §4 — seasonal challenges & the Breach leaderboard concept
- `ghost-as-effect-trace` (LLM wiki concept) — why ghosts are cheap and transport-swappable
