using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Lightweight coroutine-based tween utility. All tweens run on a single
    /// DontDestroyOnLoad TweenRunner — no MonoBehaviour reference needed at call-site.
    /// Uses Time.unscaledDeltaTime so tweens survive pause (Time.timeScale = 0).
    /// </summary>
    public static class Tween
    {
        public enum Ease { Linear, EaseOut, EaseIn, EaseInOut, EaseOutBack, EaseOutElastic }

        static TweenRunner _runner;
        static TweenRunner Runner
        {
            get
            {
                if (_runner != null && _runner.gameObject != null) return _runner;
                var go = new GameObject("[Tweens]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<TweenRunner>();
                return _runner;
            }
        }

        // ── easing ────────────────────────────────────────────────────────────

        static float Eval(float t, Ease ease) => ease switch
        {
            Ease.EaseOut      => 1f - (1f - t) * (1f - t),
            Ease.EaseIn       => t * t,
            Ease.EaseInOut    => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f,
            Ease.EaseOutBack  => 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f),
            Ease.EaseOutElastic => t == 0f ? 0f : t == 1f ? 1f
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f,
            _ => t,
        };

        // ── public API ────────────────────────────────────────────────────────

        /// Generic float tween — drives any value via setter delegate.
        public static Coroutine Float(Action<float> set, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Runner.Run(FloatCo(set, from, to, dur, ease, done));

        /// Uniform scale tween (all axes).
        public static Coroutine Scale(Transform tr, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Float(v => { if (tr != null) tr.localScale = Vector3.one * v; }, from, to, dur, ease, done);

        /// CanvasGroup alpha fade.
        public static Coroutine Fade(CanvasGroup cg, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Float(v => { if (cg != null) cg.alpha = v; }, from, to, dur, ease, done);

        /// Graphic (Image / Text) alpha fade without touching color.rgb.
        public static Coroutine FadeImg(Graphic g, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Float(v =>
            {
                if (g == null) return;
                var c = g.color; c.a = v; g.color = c;
            }, from, to, dur, ease, done);

        /// Full RGBA color tween on any Graphic.
        public static Coroutine Tint(Graphic g, Color from, Color to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Runner.Run(TintCo(g, from, to, dur, ease, done));

        /// Animates RectTransform.anchoredPosition.y.
        public static Coroutine MoveY(RectTransform rt, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Float(v =>
            {
                if (rt == null) return;
                var p = rt.anchoredPosition; p.y = v; rt.anchoredPosition = p;
            }, from, to, dur, ease, done);

        /// Animates RectTransform.anchoredPosition.x.
        public static Coroutine MoveX(RectTransform rt, float from, float to, float dur,
            Ease ease = Ease.EaseOut, Action done = null)
            => Float(v =>
            {
                if (rt == null) return;
                var p = rt.anchoredPosition; p.x = v; rt.anchoredPosition = p;
            }, from, to, dur, ease, done);

        /// Scale 0.88→1.0 with EaseOutBack — call right after activating a panel.
        public static void PopIn(Transform tr, float dur = 0.22f)
        {
            if (tr == null) return;
            tr.localScale = Vector3.one * 0.88f;
            Scale(tr, 0.88f, 1f, dur, Ease.EaseOutBack);
        }

        /// Quick over-scale pulse — great for button confirms and card selection.
        public static void Punch(Transform tr, float amount = 0.10f, float dur = 0.28f)
        {
            if (tr != null) Runner.Run(PunchCo(tr, amount, dur));
        }

        /// Horizontal shake — use for error feedback (can't afford, invalid swap).
        public static void ShakeH(RectTransform rt, float amount = 5f, float dur = 0.28f)
        {
            if (rt != null) Runner.Run(ShakeHCo(rt, amount, dur));
        }

        /// Fire a callback after `seconds` (unscaled time — works while paused).
        public static Coroutine Delay(float seconds, Action done)
            => Runner.Run(DelayCo(seconds, done));

        // ── coroutines ────────────────────────────────────────────────────────

        static IEnumerator FloatCo(Action<float> set, float from, float to, float dur, Ease ease, Action done)
        {
            set(from);
            if (dur <= 0f) { set(to); done?.Invoke(); yield break; }
            for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
            {
                set(Mathf.LerpUnclamped(from, to, Eval(Mathf.Clamp01(e / dur), ease)));
                yield return null;
            }
            set(to);
            done?.Invoke();
        }

        static IEnumerator TintCo(Graphic g, Color from, Color to, float dur, Ease ease, Action done)
        {
            if (g == null) { done?.Invoke(); yield break; }
            g.color = from;
            if (dur <= 0f) { g.color = to; done?.Invoke(); yield break; }
            for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
            {
                if (g == null) { done?.Invoke(); yield break; }
                g.color = Color.LerpUnclamped(from, to, Eval(Mathf.Clamp01(e / dur), ease));
                yield return null;
            }
            if (g != null) g.color = to;
            done?.Invoke();
        }

        static IEnumerator PunchCo(Transform tr, float amount, float dur)
        {
            if (tr == null) yield break;
            Vector3 orig = tr.localScale;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                if (tr == null) yield break;
                float p = t / dur;
                tr.localScale = orig * (1f + amount * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.5f));
                yield return null;
            }
            if (tr != null) tr.localScale = orig;
        }

        static IEnumerator ShakeHCo(RectTransform rt, float amount, float dur)
        {
            if (rt == null) yield break;
            Vector2 orig = rt.anchoredPosition;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float p = t / dur;
                rt.anchoredPosition = orig + new Vector2(amount * Mathf.Sin(p * Mathf.PI * 6f) * (1f - p), 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = orig;
        }

        static IEnumerator DelayCo(float seconds, Action done)
        {
            yield return new WaitForSecondsRealtime(seconds);
            done?.Invoke();
        }
    }

    // Minimal MonoBehaviour runner — auto-created, DontDestroyOnLoad.
    internal sealed class TweenRunner : MonoBehaviour
    {
        public Coroutine Run(IEnumerator co) => StartCoroutine(co);
    }
}
