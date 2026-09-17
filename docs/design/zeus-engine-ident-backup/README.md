# ZEUS ENGINE ident — pulled from the app 2026-09-11

Not satisfied with this pass; pulled from `BattleScene`'s cold-launch flow completely so it's not
running the app. Kept here (outside `Assets/`, so Unity doesn't import or compile any of it) in
case pieces of this are worth reusing when it gets redone.

**Live/editable design source:** the interactive HTML prototype this was built from is still
published and can be reopened and edited directly:
https://claude.ai/code/artifact/8e20ae40-03ab-4ba2-bdae-5e0cb1942963

`zeus-engine-ident-artifact.html` in this folder is a frozen copy of that page as of the last
pass (Pass 10 — ENGINE=Flicker, ZEUS face=Metal, Thunder=Faint locked in), in case the live
artifact ever changes or is lost.

## What's here

| File | What it is |
|---|---|
| `zeus-engine-ident-artifact.html` | Frozen copy of the HTML/CSS/SVG/WebAudio design prototype |
| `EngineIntroScreen.cs` | The Unity `MonoBehaviour` that played it — sprite-sheet flipbook (80 frames/6 sheets) + separate thunder `AudioClip`, not a video |
| `zeus_engine_sheet_0..5.png` | The 80 baked frames (736×1264), tiled 5×3 per sheet, from the user's own Windows Game Bar recording of the HTML prototype |
| `zeus_engine_ident_audio.ogg` | The extracted thunder audio (Vorbis) |

## Why it was pulled — worth fixing if reused

- The baked frames carry a warm orange bleed at the edges (from the source HTML's
  `.stage.hot::before` strike-flash box-shadow + an off-centre `.haze` blob) that read as
  unwanted "red from the side" once cropped to a phone screen — a Unity-side radial vignette
  overlay was added to `EngineIntroScreen.cs` to mask it, but the user preferred to redo the
  design rather than patch around it. If reusing, fix the vignette color at the HTML/CSS level
  (tone `.stage.hot::before`'s warm shadow toward neutral, re-center `.haze`) before re-recording.
- Two unrelated Unity bugs were found and fixed while this was wired in — worth knowing if the
  flipbook approach is reused: [[first-frame-delta-time-spike]] and
  [[canvasgroup-fade-exposes-persistent-bg]] (LLM Wiki concepts).

Full write-up: LLM Wiki `sources/2026-09-11-neon-warfare-zeus-engine-ident.md`.
