# NEON WARFARE — first-run setup

Unity **6000.4.11f1**, active build target **Android**.

Fresh clone → open in Unity → it regenerates `Library/`. Then two things are gitignored and
must be restored:

## 1. Firebase (online leaderboard)

The Firebase Unity SDK (~300 MB of binaries) is **not** in the repo. Restore it:

1. Download the Firebase Unity SDK from <https://firebase.google.com/download/unity> (13.16.0 or
   later) and unzip.
2. **Assets → Import Package → Custom Package…** → import **`FirebaseAuth.unitypackage`**, then
   **`FirebaseFirestore.unitypackage`**. Do **not** import Analytics/Crashlytics/Messaging/etc.
3. Answer **No** to the two "share usage info with Google?" prompts; **Enable** Android
   auto-resolution.
4. **Assets → External Dependency Manager → Android Resolver → Force Resolve.**
5. Put **`google-services.json`** (Firebase console → project `neon-warfare` → Android app
   `com.zeusengine.neonwarfare`) at `Assets/google-services.json`. Also gitignored.
6. The `NW_FIREBASE` scripting-define (Android) is already committed in
   `ProjectSettings/ProjectSettings.asset` — it's what compiles `FirebaseLeaderboardBackend.cs` in.

Verify: enter Play mode → Console logs `[Leaderboard] Firebase backend online.` and the RANKS panel
header shows `● ONLINE`. Without the SDK the game still runs fully — it falls back to the on-device
`LocalLeaderboardBackend`.

Firestore rules live in `firestore.rules`. Full backend + Play Console guide: `docs/design/18-leaderboard-firebase.md`.

## 2. Android signing

The upload keystore (`*.keystore` / `*.jks`) is gitignored. For a signed build create one via
**Player Settings → Publishing Settings → Keystore Manager → Create New**, and back it up outside
the repo.

## 3. Before the Production track — REMOVE `NW_DEV`

`ProjectSettings/ProjectSettings.asset` ships with **`Android: NW_FIREBASE;NW_DEV`**.

`NW_DEV` turns on dev conveniences: **Pilot 3 starts fully unlocked** and the pilot-select screen
shows a "TESTING BUILD" notice (`PlayerProgress.DevMode`). Fine for Internal / Closed testing.

**Before building for the Production track:** Player Settings → Other Settings → Scripting Define
Symbols (Android) → delete `NW_DEV` (keep `NW_FIREBASE`). Real players then get a clean start —
empty wallet, only level 1 open — and no notice. The editor keeps dev unlocks regardless
(`Application.isEditor`).
