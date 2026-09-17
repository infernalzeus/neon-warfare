using System;
using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// "ZEUS ENGINE" studio ident on a cold app launch, then hands off to the NEON WARFARE
    /// welcome toast.
    ///
    /// Rendered as a pre-baked **frame flipbook** — 80 native-res frames (736×1264) packed into
    /// six sprite sheets (<c>Resources/zeus_engine_sheet_0..5.png</c>), stepped at 40 fps by
    /// moving a <see cref="RawImage"/>'s <c>uvRect</c>. No video decoder is involved, so it can
    /// never stutter, drop frames, or flash garbage — the failure mode that dogged the
    /// VideoPlayer version. The thunder is a separate <see cref="AudioClip"/>
    /// (<c>Resources/zeus_engine_ident_audio.ogg</c>) started with the first frame.
    ///
    /// If the sheets are missing this bails immediately so the launch flow is never blocked.
    /// Tap anywhere to skip. <see cref="OnDone"/> fires exactly once.
    /// </summary>
    public sealed class EngineIntroScreen : MonoBehaviour
    {
        public Action OnDone;

        const int   SheetCount   = 6;
        const int   Cols = 5, Rows = 3;              // frame grid per sheet
        const int   FrameCount    = 80;
        const float Fps           = 40f;
        const float TailHold      = 0.18f;           // hold the last frame before the fade
        const int   FrameW = 736, FrameH = 1264;

        // Vignette: the baked frames carry a warm orange bleed at the edges (from the source
        // animation's strike-flash box-shadow + an off-centre haze blob) that reads as "red from
        // the side" and makes edge compression blockiness more visible. Rather than re-record the
        // whole ident to fix a color, a native radial-gradient overlay pulls the corners back
        // toward the game's own deep navy and leaves the centre (ZEUS/ENGINE) untouched — tunable
        // here without touching the HTML/capture pipeline again.
        const int   VigSize      = 256;
        const float VigInner     = 0.30f;   // normalized radius where darkening starts
        const float VigOuter     = 0.74f;   // normalized radius where it hits VigMaxAlpha
        const float VigMaxAlpha  = 0.78f;   // corner opacity of the overlay color

        CanvasGroup   _cg;
        RawImage      _raw;
        AudioSource   _audio;
        Texture2D[]   _sheets;
        Texture2D     _vignetteTex;
        float         _startTime = -1f;   // wall-clock anchor, set on the FIRST rendered frame
        int           _curSheet = -1;
        bool          _finished;
        bool          _fading;

        public void Init(Font font)   // font arg kept for call-site parity; unused
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Opaque base, deliberately NOT part of the fade — stays solid black until this whole
            // screen is destroyed. The canvas's persistent background art (BuildCanvas's BG /
            // Board_BgImage, always sitting underneath every screen) must never be exposed while
            // the artwork above is fading out; that flash was the "rectangle" after the ident.
            gameObject.AddComponent<Image>().color = Color.black;

            var skip = gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(Skip);

            _sheets = new Texture2D[SheetCount];
            for (int i = 0; i < SheetCount; i++)
                _sheets[i] = Resources.Load<Texture2D>("zeus_engine_sheet_" + i);

            if (_sheets[0] == null)
            {
                Debug.LogWarning("[EngineIntro] no frame sheets in Resources — skipping ident.");
                Finish();
                return;
            }

            // Everything that actually fades lives one level down, under its own CanvasGroup —
            // fading THIS reveals the opaque black base above, never whatever is behind the screen.
            var fadeGo = new GameObject("fade");
            fadeGo.transform.SetParent(transform, false);
            var frt = fadeGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            _cg = fadeGo.AddComponent<CanvasGroup>();
            _cg.alpha = 1f;

            var surfGo = new GameObject("surface");
            surfGo.transform.SetParent(fadeGo.transform, false);
            _raw = surfGo.AddComponent<RawImage>();
            var srt = _raw.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(FrameW, FrameH);
            _raw.raycastTarget = false;
            var fit = surfGo.AddComponent<AspectRatioFitter>();
            fit.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;   // fill screen, crop overflow
            fit.aspectRatio = (float)FrameW / FrameH;

            ShowFrame(0);

            // Vignette overlay — sits above the flipbook surface, inside the same fade group so
            // it appears/disappears with everything else.
            var vigGo = new GameObject("vignette");
            vigGo.transform.SetParent(fadeGo.transform, false);
            var vrt = vigGo.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            _vignetteTex = BuildVignette();
            var vig = vigGo.AddComponent<RawImage>();
            vig.texture = _vignetteTex;
            vig.raycastTarget = false;

            var thunder = Resources.Load<AudioClip>("zeus_engine_ident_audio");
            if (thunder != null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.clip = thunder;
                _audio.playOnAwake = false;
                _audio.loop = false;
                _audio.volume = 1f;
                _audio.spatialBlend = 0f;
                _audio.ignoreListenerPause = true;

                // The intro doesn't go through AudioManager (which normally creates one lazily) —
                // without a listener in the scene yet, this AudioSource would just play into the void.
                if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                    new GameObject("[IntroListener]").AddComponent<AudioListener>();
            }
        }

        void Update()
        {
            if (_finished || _raw == null) return;

            // On a cold launch the FIRST Update's Time.unscaledDeltaTime is the entire app-load
            // time (often 2-4s) — accumulating it would jump straight to the last frame and tear
            // the screen down in ~0.5s with the audio barely started. Anchor to wall-clock time
            // instead, starting from this first real frame, so a load spike can't be counted.
            if (_startTime < 0f)
            {
                _startTime = Time.unscaledTime;
                if (_audio != null) _audio.Play();
                return;
            }

            float t = Time.unscaledTime - _startTime;
            int f = (int)(t * Fps);
            if (f >= FrameCount)
            {
                ShowFrame(FrameCount - 1);
                if (!_fading && t >= FrameCount / Fps + TailHold) { _fading = true; FadeOut(); }
                return;
            }
            ShowFrame(f);
        }

        void ShowFrame(int f)
        {
            f = Mathf.Clamp(f, 0, FrameCount - 1);
            int per   = Cols * Rows;
            int sheet = Mathf.Min(f / per, SheetCount - 1);
            int cell  = f - sheet * per;
            int col   = cell % Cols;
            int row   = cell / Cols;

            var tex = _sheets[sheet] != null ? _sheets[sheet] : _sheets[0];
            if (sheet != _curSheet && tex != null) { _raw.texture = tex; _curSheet = sheet; }

            // ffmpeg tile lays frames left→right, top→bottom; RawImage uv origin is bottom-left.
            _raw.uvRect = new Rect(col / (float)Cols, 1f - (row + 1) / (float)Rows,
                                   1f / Cols, 1f / Rows);
        }

        // Radially-symmetric alpha ramp, tinted to the game's own deep navy (not the ident's baked
        // orange) — stretching a square texture across a portrait RawImage naturally turns the
        // circle into an ellipse that hugs the screen's corners, same trick the source HTML used
        // with CSS radial-gradient ellipses.
        static Texture2D BuildVignette()
        {
            var edge = NeonTheme.Active.BgDeep;
            var tex = new Texture2D(VigSize, VigSize, TextureFormat.RGBA32, false);
            var px = new Color[VigSize * VigSize];
            float c = VigSize * 0.5f;
            float rInner = VigSize * VigInner, rOuter = VigSize * VigOuter;
            for (int y = 0; y < VigSize; y++)
                for (int x = 0; x < VigSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float t = Mathf.InverseLerp(rInner, rOuter, dist);
                    t = t * t * (3f - 2f * t);   // smoothstep
                    px[y * VigSize + x] = new Color(edge.r, edge.g, edge.b, VigMaxAlpha * t);
                }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        void Skip() { if (!_fading) { _fading = true; FadeOut(0.14f); } }

        void FadeOut(float dur = 0.3f)
        {
            if (_finished) return;
            if (_cg != null) Tween.Fade(_cg, _cg.alpha, 0f, dur, done: Finish);
            else Finish();
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            if (_audio != null) _audio.Stop();
            var cb = OnDone; OnDone = null;
            cb?.Invoke();
        }

        void OnDestroy()
        {
            if (_sheets != null)
                foreach (var s in _sheets)
                    if (s != null) Resources.UnloadAsset(s);
            if (_vignetteTex != null) Destroy(_vignetteTex);   // runtime-generated, not a Resources asset
            if (!_finished) { _finished = true; OnDone?.Invoke(); OnDone = null; }
        }
    }
}
