# NEON WARFARE — Privacy Policy

**Effective date:** 8 September 2026
**App:** NEON WARFARE (Android · `com.zeusengine.neonwarfare`)
**Contact:** infernalzeus69@gmail.com

NEON WARFARE is a single-player strategy game. It works fully offline. Two features send data off
your device: the **online leaderboard** and **crash diagnostics**. This policy explains what each
collects and why.

## What is collected, and why

To run the online leaderboard the game stores the following in Google Cloud Firestore:

| Data | Purpose |
|---|---|
| A **Firebase Anonymous Authentication ID** — a random identifier created on your device. It is **not** linked to a Google account, email, or your real identity. | Distinguishes one player's leaderboard entry from another's. |
| A **random install identifier** generated locally by the game. | The key for your row on each daily leaderboard, so a better score replaces your previous one. |
| The **pilot name you type** in the game's profile screen. | Shown next to your score on the leaderboard. Choose any name; you are not asked for your real one. |
| **Per-match results:** level number, win/loss, match duration, the game's internal skill rating (MMR), a timestamp, and a **compact recording of your own match** (a "ghost"). | Ranks the leaderboard and lets other players replay your match as an opponent. The recording contains only your in-game actions — no personal data. |

The game does **not** collect: your real name, email address, phone number, precise or coarse
location, contacts, photos, files, messages, the device advertising ID, or any analytics or
usage-tracking data.

## Crash diagnostics

If the game hits an unexpected error, it sends a diagnostic report so we can fix it. The report
contains: the error message and stack trace, which screen you were on, the app and engine version,
your device model and OS version, and a **random identifier generated fresh each time you open the
game** (not stored, not linkable to you or your leaderboard entry). No name, email, contacts,
location, or file contents. Reports go to Google Cloud Firestore and are used only to diagnose and
fix problems. Crash reporting can be turned off in the game's settings.

## No ads, no analytics, no selling data

NEON WARFARE contains no advertising and no third-party analytics or tracking SDKs. Your data is
**never sold** and is **never shared** with third parties for advertising or any other purpose.

## Who processes the data

Google, through **Firebase Authentication** and **Cloud Firestore**, stores this data on our
behalf as a data processor, in Google Cloud data centres (currently in the **United States**).
Google's handling of the data is governed by the
[Firebase Data Processing Terms](https://firebase.google.com/terms/data-processing-terms).

## Retention and deletion

Leaderboard entries are kept while the game continues to operate the online leaderboard. Old daily
boards may be cleared periodically. To have your leaderboard data deleted, email
**infernalzeus69@gmail.com** from any address and describe your pilot name; we will remove your
rows. Uninstalling the game stops any further data being sent but does not by itself delete rows
already on the server — use the email request for that.

## Security

Data is transmitted over encrypted connections (HTTPS/TLS) and stored within Google Cloud's
infrastructure.

## Children

NEON WARFARE is intended for players aged 13 and older and is not directed to children under 13.
We do not knowingly collect data from children under 13. If you believe a child has provided data,
contact us and we will delete it.

## Changes to this policy

If this policy changes, the updated version will be posted at the same URL with a new effective
date.

## Contact

Questions or requests: **infernalzeus69@gmail.com**
