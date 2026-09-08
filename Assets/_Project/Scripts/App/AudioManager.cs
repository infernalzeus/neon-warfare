using System;
using System.Collections.Generic;
using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Procedural audio — generates AudioClips from PCM math, no asset imports needed.
    /// Clips are cached after first creation. Call AudioManager.Play(Sfx.X) anywhere.
    /// </summary>
    public static class AudioManager
    {
        public enum Sfx
        {
            GemSwap,       // quick click: start of a swap gesture
            GemMatch,      // arpeggio chord: gems cleared
            GemInvalid,    // short buzz: swap has no match
            Deploy,        // bass thud: troop deployed
            CoreHit,       // low boom: core takes damage
            LaneStrike,    // zap sweep: laser crossover
            Victory,       // ascending fanfare
            Defeat,        // descending fade
            Click,         // UI click
            Error,         // can't afford
            PylonCapture,  // crystalline ping-lock: pylon node secured or lost
            SurgeActivate, // power surge sweep: surge bonus activated
            ActChange,     // deep impact chord: act transition
            UnitHit,       // short crack: melee/ranged impact
            UnitDeath,     // low thud: unit eliminated
            GemMatchBig,   // elevated fanfare: 5+ gem clear
            AttackLaser,   // high zap: sniper/turret shot
            AttackCannon,  // low boom: mech/titan cannon
            AttackMelee,   // whoosh: melee swing
            AttackZap,     // electric crackle: drone/hacker/interceptor
            Purchase,      // coin fanfare: cosmetic unlocked
            Equip,         // solid clunk: cosmetic equipped
            UnitChirp,     // rising two-note robot voice: spawn / kill (pitch = class register)
            UnitDistress,  // falling two-note beep: critically damaged
        }

        const int SR = 22050;

        static AudioSource _src;
        static AudioSource _music;
        static bool        _musicMuted;
        static int         _currentMusicTheme = -1;
        static readonly Dictionary<int, AudioClip>  _cache      = new();
        static readonly Dictionary<int, AudioClip>  _musicCache = new();

        static float _intensity = 1f; // battle engagement 0..1 — scales music presence

        /// <summary>Persisted music volume scalar (0–1), multiplies the base level.</summary>
        public static float MusicVol
        {
            get => PlayerPrefs.GetFloat("aud_musicvol", 1f);
            set
            {
                PlayerPrefs.SetFloat("aud_musicvol", Mathf.Clamp01(value)); PlayerPrefs.Save();
                if (_music != null && _music.gameObject != null) _music.volume = MusicBase();
            }
        }

        /// <summary>Persisted SFX volume scalar (0–1).</summary>
        public static float SfxVol
        {
            get => PlayerPrefs.GetFloat("aud_sfxvol", 1f);
            set
            {
                PlayerPrefs.SetFloat("aud_sfxvol", Mathf.Clamp01(value)); PlayerPrefs.Save();
                if (_src != null && _src.gameObject != null) _src.volume = 0.55f * value;
            }
        }

        static float MusicBase() => _musicMuted ? 0f : Mathf.Lerp(0.14f, 0.34f, _intensity) * MusicVol;

        /// <summary>Battle engagement drives music presence — calm build-up, loud combat.</summary>
        public static void SetMusicIntensity(float f)
        {
            _intensity = Mathf.Clamp01(f);
            if (_music != null && _music.gameObject != null) _music.volume = MusicBase();
        }

        public static bool MusicMuted
        {
            get => _musicMuted;
            set
            {
                _musicMuted = value;
                if (_music != null && _music.gameObject != null)
                    _music.volume = MusicBase();
            }
        }

        static AudioSource Source
        {
            get
            {
                // Unity's == null check handles destroyed objects; recreate if needed
                if (_src != null && _src.gameObject != null) return _src;
                var go = new GameObject("[AudioManager]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.volume       = 0.55f * SfxVol;
                _src.playOnAwake  = false;
                _src.spatialBlend = 0f;
                // Ensure a listener exists so audio isn't silently dropped
                EnsureListener();
                return _src;
            }
        }

        static AudioSource MusicSource
        {
            get
            {
                if (_music != null && _music.gameObject != null) return _music;
                var go = new GameObject("[AudioManager_Music]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _music = go.AddComponent<AudioSource>();
                _music.volume       = MusicBase();
                _music.loop         = true;
                _music.playOnAwake  = false;
                _music.spatialBlend = 0f;
                return _music;
            }
        }

        /// <summary>Guarantees at least one AudioListener exists in the scene.</summary>
        static void EnsureListener()
        {
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var go = new GameObject("[AudioListener]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AudioListener>();
            }
        }

        /// <summary>Start or switch the looping background track for the given theme index.</summary>
        public static void PlayMusic(int themeIndex)
        {
            themeIndex = Mathf.Clamp(themeIndex, 0, 7);
            if (_currentMusicTheme == themeIndex && MusicSource.isPlaying) return;
            _currentMusicTheme = themeIndex;

            if (!_musicCache.TryGetValue(themeIndex, out var clip) || clip == null)
                _musicCache[themeIndex] = clip = BuildMusic(themeIndex);

            var ms = MusicSource;
            ms.clip   = clip;
            _intensity = 1f; // fresh track starts at full presence
            ms.volume = MusicBase();
            ms.Play();
        }

        /// <summary>Stop the music track immediately.</summary>
        public static void StopMusic()
        {
            if (_music != null && _music.gameObject != null) _music.Stop();
            _currentMusicTheme = -1;
        }

        public static void Play(Sfx id, float vol = 1f, float pitch = 1f)
        {
            // Guard: don't play if globally muted
            if (AudioListener.volume <= 0f) return;

            // Humanize: subtle pitch drift on every non-melodic SFX kills sample fatigue.
            // Stingers and jingles stay on-pitch so they don't clash with the music key.
            bool melodic = id is Sfx.Victory or Sfx.Defeat or Sfx.ActChange
                              or Sfx.SurgeActivate or Sfx.PylonCapture or Sfx.Purchase;
            if (!melodic) pitch *= UnityEngine.Random.Range(0.94f, 1.06f);

            int key = (int)id;
            if (!_cache.TryGetValue(key, out var clip))
            {
                clip = Build(id);
                _cache[key] = clip;
            }
            if (clip != null)
            {
                Source.pitch = pitch;
                Source.PlayOneShot(clip, vol);
                Source.pitch = 1f;
            }
        }

        /// <summary>
        /// Reset audio state — call on scene restart to un-mute and flush stale clip refs
        /// that may have been invalidated.
        /// </summary>
        public static void Reset()
        {
            AudioListener.volume = 1f;
            StopMusic();
            // Clear SFX clips; music clips are kept since they're expensive to rebuild
            foreach (var c in _cache.Values)
                if (c != null) UnityEngine.Object.Destroy(c);
            _cache.Clear();
        }

        public static float Volume
        {
            get => Source.volume;
            set => Source.volume = Mathf.Clamp01(value);
        }

        // ── clip builders ──────────────────────────────────────────────────────

        static AudioClip Build(Sfx id) => id switch
        {
            // Gem swap: soft mechanical tick — transient + two partials, no raw beep
            Sfx.GemSwap       => Make(Mix(
                                     Click(0.004f, 0.30f),
                                     Osc(620f,  0.05f, 26f, 0.35f, harm2: 0.4f))),
            // Match: detuned major chord + sparkle noise shimmer
            Sfx.GemMatch      => Make(Mix(
                                     Tone(new[] { 523f, 659f, 784f, 525f, 662f }, 0.30f, 6f, 0.40f),
                                     NoiseBurst(0.12f, 26f, 0.10f, bright: true))),
            // Big match: wider chord + sparkle + sub thump for weight
            Sfx.GemMatchBig   => Make(Mix(
                                     Tone(new[] { 659f, 784f, 988f, 1175f, 661f, 786f }, 0.40f, 4f, 0.42f),
                                     NoiseBurst(0.16f, 20f, 0.12f, bright: true),
                                     Osc(90f, 0.16f, 16f, 0.35f))),
            // Invalid: two detuned lows beating against each other — a real "denied" buzz
            Sfx.GemInvalid    => Make(Mix(
                                     Osc(196f, 0.13f, 9f, 0.30f),
                                     Osc(207f, 0.13f, 9f, 0.30f))),
            // Deploy: body thud + rising servo whine + click — mechanical drop-pod
            Sfx.Deploy        => Make(Mix(
                                     ThudBuf(130f, 0.20f),
                                     SweepBuf(280f, 950f, 0.12f, 0.14f),
                                     Click(0.003f, 0.35f))),
            // Core hit: sub boom with falling pitch + rumble noise — dangerous
            Sfx.CoreHit       => Make(Mix(
                                     FallOsc(85f, 48f, 0.30f, 7f, 0.55f),
                                     NoiseBurst(0.26f, 9f, 0.30f, bright: false))),
            // Lane strike: laser sweep + sizzle
            Sfx.LaneStrike    => Make(Mix(
                                     SweepBuf(1100f, 280f, 0.22f, 0.50f),
                                     NoiseBurst(0.18f, 14f, 0.14f, bright: true))),
            Sfx.Victory       => FanfareUp(),
            Sfx.Defeat        => FanfareDown(),
            // Click: soft two-partial tick — gentle, not piercing
            Sfx.Click         => Make(Mix(
                                     Osc(900f,  0.03f, 40f, 0.25f),
                                     Osc(1800f, 0.02f, 55f, 0.12f))),
            // Error: low beating buzz, slightly longer than invalid
            Sfx.Error         => Make(Mix(
                                     Osc(160f, 0.16f, 7f, 0.32f),
                                     Osc(166f, 0.16f, 7f, 0.32f))),
            Sfx.PylonCapture  => PylonPing(),
            Sfx.SurgeActivate => SurgePowerUp(),
            Sfx.ActChange     => ActImpact(),
            // Unit hit: noise crack + falling sweep body — a real impact, not a beep
            Sfx.UnitHit       => Make(Mix(
                                     NoiseBurst(0.05f, 60f, 0.42f, bright: true),
                                     SweepBuf(700f, 320f, 0.07f, 0.26f))),
            // Unit death: thud + descending power-down blip
            Sfx.UnitDeath     => Make(Mix(
                                     ThudBuf(105f, 0.14f),
                                     FallOsc(240f, 80f, 0.16f, 12f, 0.22f))),
            // Laser: bright fast zap with sizzle
            Sfx.AttackLaser   => Make(Mix(
                                     SweepBuf(1700f, 850f, 0.09f, 0.30f),
                                     NoiseBurst(0.06f, 45f, 0.12f, bright: true))),
            // Cannon: concussive low boom + click
            Sfx.AttackCannon  => Make(Mix(
                                     ThudBuf(95f, 0.16f),
                                     FallOsc(190f, 70f, 0.14f, 11f, 0.30f),
                                     Click(0.003f, 0.40f))),
            // Melee: dark air whoosh, no tone
            Sfx.AttackMelee   => Make(Mix(
                                     NoiseBurst(0.11f, 16f, 0.30f, bright: false),
                                     SweepBuf(420f, 190f, 0.10f, 0.14f))),
            // Zap: two-step electric crackle
            Sfx.AttackZap     => Make(Mix(
                                     SweepBuf(1200f, 500f, 0.05f, 0.24f),
                                     NoiseBurst(0.08f, 30f, 0.18f, bright: true),
                                     Osc(2400f, 0.03f, 60f, 0.10f))),
            // Purchase: two rising ticks into a bright major stab
            Sfx.Purchase      => Make(Mix(
                                     Osc(784f,  0.06f, 22f, 0.30f),
                                     Shift(Osc(988f, 0.06f, 22f, 0.32f), 0.07f),
                                     Shift(Tone(new[] { 1047f, 1318f, 1568f }, 0.30f, 6f, 0.40f), 0.14f))),
            // Equip: mechanical clunk-lock
            Sfx.Equip         => Make(Mix(
                                     Click(0.004f, 0.45f),
                                     Osc(300f, 0.07f, 24f, 0.35f),
                                     Shift(Osc(210f, 0.09f, 20f, 0.30f), 0.05f))),
            // Robot voice: rising two-note chirp (play with per-class pitch)
            Sfx.UnitChirp     => Make(Mix(
                                     Osc(620f, 0.05f, 22f, 0.30f, harm2: 0.35f),
                                     Shift(Osc(860f, 0.06f, 18f, 0.32f, harm2: 0.35f), 0.055f))),
            // Distress: falling two-note beep
            Sfx.UnitDistress  => Make(Mix(
                                     Osc(540f, 0.06f, 18f, 0.28f, harm2: 0.3f),
                                     Shift(Osc(390f, 0.08f, 15f, 0.28f, harm2: 0.3f), 0.07f))),
            _                 => Sine(440f,  0.1f,   decay: 5f,  amp: 0.4f),
        };

        // Delay a buffer by prepending silence — sequences events inside one Mix
        static float[] Shift(float[] src, float delaySec)
        {
            int off = Samples(delaySec);
            var d = new float[off + src.Length];
            for (int i = 0; i < src.Length; i++) d[off + i] = src[i];
            return d;
        }

        // ── layered-synth primitives ───────────────────────────────────────────

        // Mix buffers additively, then soft-normalize if the sum clips
        static float[] Mix(params float[][] parts)
        {
            int n = 0;
            foreach (var p in parts) n = Mathf.Max(n, p.Length);
            var d = new float[n];
            foreach (var p in parts)
                for (int i = 0; i < p.Length; i++) d[i] += p[i];
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            if (peak > 0.90f)
            {
                float g = 0.90f / peak;
                for (int i = 0; i < n; i++) d[i] *= g;
            }
            return d;
        }

        // Sine partial with optional 2nd harmonic for body
        static float[] Osc(float freq, float dur, float decay, float amp, float harm2 = 0f)
        {
            int n = Samples(dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * decay);
                d[i] = (Mathf.Sin(Tau(freq, t)) + harm2 * Mathf.Sin(Tau(freq * 2f, t))) * env * amp;
            }
            return d;
        }

        // Chord buffer (detune-friendly: pass duplicated freqs a few Hz apart)
        static float[] Tone(float[] freqs, float dur, float decay, float amp)
        {
            int n = Samples(dur);
            var d = new float[n];
            float a = amp / freqs.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * decay);
                foreach (float f in freqs) d[i] += Mathf.Sin(Tau(f, t)) * env * a;
            }
            return d;
        }

        // Oscillator whose pitch falls from f0 to f1 — boom/power-down character
        static float[] FallOsc(float f0, float f1, float dur, float decay, float amp)
        {
            int n = Samples(dur);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f0, f1, t / dur);
                phase += f / SR;
                d[i] = (float)Math.Sin(phase * 2 * Math.PI) * Mathf.Exp(-t * decay) * amp;
            }
            return d;
        }

        // Filtered noise burst. bright=true keeps hiss (sparkle); false = dark rumble
        static float[] NoiseBurst(float dur, float decay, float amp, bool bright)
        {
            int n = Samples(dur);
            var d = new float[n];
            var rng = new System.Random(1337);
            float lp = 0f;
            float k = bright ? 0.55f : 0.06f; // one-pole lowpass coefficient
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float w = (float)(rng.NextDouble() * 2 - 1);
                lp += k * (w - lp);
                d[i] = lp * Mathf.Exp(-t * decay) * amp;
            }
            return d;
        }

        // Millisecond-scale attack transient — the "contact" of an impact
        static float[] Click(float dur, float amp)
        {
            int n = Samples(dur);
            var d = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
                d[i] = (float)(rng.NextDouble() * 2 - 1) * (1f - i / (float)n) * amp;
            return d;
        }

        // Buffer versions of the old helpers, for use inside Mix
        static float[] ThudBuf(float baseFreq, float dur)
        {
            int n = Samples(dur);
            var d = new float[n];
            var rng = new System.Random(42);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * 14f);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float tone = Mathf.Sin(Tau(baseFreq, t));
                d[i] = (noise * 0.35f + tone * 0.65f) * env * 0.65f;
            }
            return d;
        }

        static float[] SweepBuf(float f0, float f1, float dur, float amp)
        {
            int n = Samples(dur);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f0, f1, t / dur);
                float env = 1f - t / dur;
                phase += f / SR;
                d[i] = (float)(Math.Sin(phase * 2 * Math.PI) * env * amp);
            }
            return d;
        }

        // Pure sine with exponential decay
        static AudioClip Sine(float freq, float dur, float decay, float amp)
        {
            int n = Samples(dur);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = Mathf.Sin(Tau(freq, t)) * Mathf.Exp(-t * decay) * amp;
            }
            return Make(d);
        }

        // Stacked harmonics (chord)
        static AudioClip Chord(float[] freqs, float dur, float decay, float amp)
        {
            int n = Samples(dur);
            float[] d = new float[n];
            float a = amp / freqs.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * decay);
                foreach (float f in freqs) d[i] += Mathf.Sin(Tau(f, t)) * env * a;
            }
            return Make(d);
        }

        // Frequency sweep (zap/laser)
        static AudioClip Sweep(float f0, float f1, float dur, float amp)
        {
            int n = Samples(dur);
            float[] d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t  = i / (float)SR;
                float f  = Mathf.Lerp(f0, f1, t / dur);
                float env = (1f - t / dur);
                phase += f / SR;
                d[i] = (float)(Math.Sin(phase * 2 * Math.PI) * env * amp);
            }
            return Make(d);
        }

        // Low-pass noise burst (deploy / core hit)
        static AudioClip Thud(float baseFreq, float dur)
        {
            int n = Samples(dur);
            float[] d = new float[n];
            var rng = new System.Random(42);
            for (int i = 0; i < n; i++)
            {
                float t   = i / (float)SR;
                float env = Mathf.Exp(-t * 14f);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float tone  = Mathf.Sin(Tau(baseFreq, t));
                d[i] = (noise * 0.35f + tone * 0.65f) * env * 0.65f;
            }
            return Make(d);
        }

        // Victory: G4→C5→E5→G5 pickup into a held C-major chord with shimmer — a real cadence
        static AudioClip FanfareUp()
        {
            int step  = Samples(0.11f);
            int hold  = Samples(0.85f);
            float[] notes = { 392f, 523f, 659f, 784f };
            int n = step * notes.Length + hold;
            var d = new float[n];
            for (int ni = 0; ni < notes.Length; ni++)
                for (int i = 0; i < step; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-t * 5f) * 0.42f;
                    d[ni * step + i] = (Mathf.Sin(Tau(notes[ni], t))
                                      + 0.35f * Mathf.Sin(Tau(notes[ni] * 2f, t))) * env;
                }
            // Resolution chord: C5+E5+G5+C6 with slow decay + sparkle noise
            int off = step * notes.Length;
            float[] chord = { 523f, 659f, 784f, 1047f, 525f, 661f };
            var rng = new System.Random(99); float lp = 0f;
            for (int i = 0; i < hold; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * 2.6f);
                float s = 0f;
                foreach (float f in chord) s += Mathf.Sin(Tau(f, t)) / chord.Length;
                float w = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.5f * (w - lp);
                d[off + i] = s * env * 0.55f + lp * Mathf.Exp(-t * 7f) * 0.06f;
            }
            return Make(d);
        }

        // Defeat: C5→Ab4→F4 descent onto a dark F-minor low pad — a real lament, not a scale
        static AudioClip FanfareDown()
        {
            int step = Samples(0.15f);
            int hold = Samples(0.9f);
            float[] notes = { 523f, 415f, 349f };
            int n = step * notes.Length + hold;
            var d = new float[n];
            for (int ni = 0; ni < notes.Length; ni++)
                for (int i = 0; i < step; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-t * 5f) * 0.40f;
                    d[ni * step + i] = (Mathf.Sin(Tau(notes[ni], t))
                                      + 0.30f * Mathf.Sin(Tau(notes[ni] * 2f, t))) * env;
                }
            int off = step * notes.Length;
            float[] pad = { 174f, 208f, 261f }; // F3-Ab3-C4 minor
            for (int i = 0; i < hold; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * 2.2f);
                float s = 0f;
                foreach (float f in pad) s += Mathf.Sin(Tau(f, t)) / pad.Length;
                d[off + i] = s * env * 0.5f;
            }
            return Make(d);
        }

        // ── music ducking ─────────────────────────────────────────────────────
        // Drop music under a stinger; caller restores (or it restores on next PlayMusic).

        public static void DuckMusic(float to = 0.06f)
        {
            if (_music != null && _music.gameObject != null && !_musicMuted)
                _music.volume = to;
        }

        public static void RestoreMusic()
        {
            if (_music != null && _music.gameObject != null)
                _music.volume = MusicBase();
        }

        // Crystalline ping-lock: two quick ascending sine notes (pylon secured / lost)
        static AudioClip PylonPing()
        {
            int n1 = Samples(0.10f), n2 = Samples(0.12f), total = n1 + n2;
            float[] d = new float[total];
            for (int i = 0; i < n1; i++)
            {
                float t = i / (float)SR;
                d[i] = Mathf.Sin(Tau(880f, t)) * Mathf.Exp(-t * 14f) * 0.45f;
            }
            for (int i = 0; i < n2; i++)
            {
                float t = i / (float)SR;
                d[n1 + i] = Mathf.Sin(Tau(1108f, t)) * Mathf.Exp(-t * 12f) * 0.50f;
            }
            return Make(d);
        }

        // Power surge sweep: fast rising frequency sweep into a bright chord stab
        static AudioClip SurgePowerUp()
        {
            int nSweep  = Samples(0.18f);
            int nStab   = Samples(0.22f);
            float[] d   = new float[nSweep + nStab];
            // Sweep: 180 Hz → 1400 Hz
            double phase = 0;
            for (int i = 0; i < nSweep; i++)
            {
                float t   = i / (float)SR;
                float f   = Mathf.Lerp(180f, 1400f, t / (nSweep / (float)SR));
                float env = (t / (nSweep / (float)SR));
                phase += f / SR;
                d[i]  = (float)(Math.Sin(phase * 2 * Math.PI) * env * 0.52f);
            }
            // Stab: bright chord at the top
            float[] stabFreqs = { 880f, 1108f, 1318f };
            for (int i = 0; i < nStab; i++)
            {
                float t   = i / (float)SR;
                float env = Mathf.Exp(-t * 9f);
                float s   = 0f;
                foreach (float f in stabFreqs) s += Mathf.Sin(Tau(f, t)) / stabFreqs.Length;
                d[nSweep + i] = s * env * 0.55f;
            }
            return Make(d);
        }

        // Deep impact chord: act transition (low chord + shimmer overtone)
        static AudioClip ActImpact()
        {
            float dur = 0.55f;
            int n     = Samples(dur);
            float[] d = new float[n];
            float[] lows = { 130f, 164f, 196f };     // C3-E3-G3 power chord
            float[] highs = { 392f, 523f, 659f };    // G4-C5-E5 shimmer
            for (int i = 0; i < n; i++)
            {
                float t   = i / (float)SR;
                float envL = Mathf.Exp(-t * 3.5f);
                float envH = Mathf.Exp(-t * 8f) * (1f - Mathf.Exp(-t * 30f));
                float s   = 0f;
                foreach (float f in lows)  s += Mathf.Sin(Tau(f, t)) * envL / lows.Length  * 0.55f;
                foreach (float f in highs) s += Mathf.Sin(Tau(f, t)) * envH / highs.Length * 0.32f;
                d[i] = s;
            }
            return Make(d);
        }

        // ── music builder ──────────────────────────────────────────────────────
        // 4-layer sequencer: kick+snare+hat / bass / chord pad / lead melody.
        // Each theme is a unique 4-bar loop with its own chord progression, BPM,
        // and lead melody line. Layers are mixed additively then normalized.

        static AudioClip BuildMusic(int theme)
        {
            float bpm = theme switch
            {
                0 => 130f, 1 => 126f, 2 => 112f, 3 =>  96f,
                4 => 138f, 5 =>  88f, 6 => 124f, 7 =>  76f,
                _ => 120f
            };

            float bps  = bpm / 60f;
            float beat = 1f / bps;
            float bar  = beat * 4f;
            float loop = bar * 4f;          // 4-bar loop
            int   N    = Samples(loop);
            var   buf  = new float[N];
            var   rng  = new System.Random(theme * 137 + 7);

            float[][] pad  = MusicPadChords(theme);  // [4 bars][chord freq array]
            float[]   bass = MusicBassRoots(theme);  // bass root Hz per bar
            float[]   lead = MusicLeadLine(theme);   // 8 melody notes

            bool hasDrums  = theme != 7;   // Dawn is beatless/ambient
            bool fourFloor = theme == 4;   // Industrial: kick every beat

            for (int b = 0; b < 4; b++)
            {
                float bs = b * bar;

                // ── Chord pad: theme-specific synthesized texture ─────────────
                float padDur = bar * 0.96f;
                float padAtk = theme == 5 ? 0.006f : beat * 0.9f;  // Sakura: instant pluck
                float padRel = beat * 1.0f;
                foreach (float pf in pad[b])
                    WritePadNote(buf, N, (int)(bs * SR), pf, padDur, padAtk, padRel, 0.15f, theme);

                // ── Bar arpeggio: 4 quick chord tones at 16th-note intervals ──
                if (theme != 7)  // not Dawn (beatless)
                {
                    float sixteenth = beat * 0.25f;
                    float arpAmp    = theme == 5 ? 0.07f : 0.09f;
                    float[] ac      = pad[b];
                    for (int k = 0; k < 4; k++)
                    {
                        float arpHz = k < ac.Length ? ac[k] * 2f : ac[0] * 4f;
                        int arpSt   = (int)((bs + k * sixteenth) * SR);
                        MusicCounterNote(buf, N, arpSt, arpHz, sixteenth * 0.78f, arpAmp);
                    }
                }

                // ── Bass: root on beats 1+3, fifth on 2+4 ────────────────────
                float root = bass[b], fifth = root * 1.498f;
                float[] bfreq = { root, fifth, root, fifth };
                float[] bdur  = { beat * 0.82f, beat * 0.78f, beat * 0.82f, beat * 0.78f };
                for (int k = 0; k < 4; k++)
                {
                    float bt = bs + k * beat;
                    float bf = bfreq[k], bd = bdur[k];
                    int bN = Samples(bd);
                    for (int i = 0; i < bN; i++)
                    {
                        int si = (int)(bt * SR) + i;
                        if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, bd, 0.008f, bd * 0.35f, squared: false);
                        float s   = Mathf.Sin(Tau(bf,      t)) * 0.65f
                                  + Mathf.Sin(Tau(bf * 2f, t)) * 0.22f
                                  + Mathf.Sin(Tau(bf * 3f, t)) * 0.09f;
                        buf[si] += s * env * 0.32f;
                    }
                }

                // ── Drums ──────────────────────────────────────────────────────
                if (hasDrums)
                {
                    int[] kickBeats = fourFloor ? new[]{0,1,2,3} : new[]{0,2};
                    foreach (int k in kickBeats)
                        MusicKick(buf, N, (int)((bs + k * beat) * SR));

                    MusicSnare(buf, N, (int)((bs + beat)        * SR), rng);
                    MusicSnare(buf, N, (int)((bs + 3f * beat)   * SR), rng);

                    float chordRoot = pad[b % 4][0];
                    for (int h = 0; h < 8; h++)
                    {
                        int hStart = (int)((bs + h * beat * 0.5f) * SR);
                        int roll   = rng.Next(7);
                        if      (roll == 0 && h % 4 == 0) MusicPiano(buf, N, hStart, chordRoot * 2f, 0.11f);
                        else if (roll == 1 && h % 4 == 2) MusicVocal(buf, N, hStart, chordRoot * 2f, 0.09f);
                        else
                        {
                            float ha = (h % 2 == 0) ? 0.09f : 0.045f;
                            MusicHat(buf, N, hStart, ha, rng);
                        }
                    }

                    // Bar-4 drum fill: 16th-note hi-hat cascade on beats 3–4 (turnaround feel)
                    if (b == 3)
                    {
                        for (int hf = 0; hf < 8; hf++)
                        {
                            float ht = bs + 2f * beat + hf * beat * 0.25f;
                            MusicHat(buf, N, (int)(ht * SR), 0.05f + (hf % 2) * 0.025f, rng);
                        }
                    }
                }
            }

            // ── Lead melody: 8 notes, one every half-bar ─────────────────────
            float leadDur = beat * 1.88f;
            float leadAtk = beat * 0.04f;
            float leadRel = beat * 0.55f;
            for (int i = 0; i < 8; i++)
            {
                float lt = i * bar * 0.5f;
                MusicLeadNote(buf, N, (int)(lt * SR), lead[i], leadDur, leadAtk, leadRel, theme);
            }

            // ── Counter-melody: responds in bars 2 & 4 (quarter-note chord tones) ──
            if (theme != 7)  // not Dawn
            {
                float cAmp = theme == 5 ? 0.10f : 0.12f;
                for (int b = 1; b < 4; b += 2)  // bars 2 and 4 (0-indexed)
                {
                    float bs   = b * bar;
                    float[] cn = pad[b];
                    for (int k = 0; k < 4; k++)
                    {
                        float cHz = k < cn.Length ? cn[k] * 2f : cn[cn.Length - 1] * 4f;
                        MusicCounterNote(buf, N, (int)((bs + k * beat) * SR), cHz, beat * 0.88f, cAmp);
                    }
                }
            }

            // ── Syncopated stabs ("and of 2" per bar) + bar-4 arpeggio fill ──
            float stabVol = theme switch
            {
                4 => 0.12f,  // Industrial: punchy
                5 => 0.06f,  // Sakura: delicate
                7 => 0.00f,  // Dawn: beatless, no stabs
                _ => 0.09f,
            };
            if (stabVol > 0f)
            {
                for (int b = 0; b < 4; b++)
                {
                    float bs     = b * bar;
                    float stabHz = pad[b][0] * 2f;  // chord root, upper octave
                    // "and of 2" — syncopated hit after beat 2
                    MusicStab(buf, N, (int)((bs + beat * 2.5f) * SR), stabHz, stabVol);
                    // Driving themes also hit "and of 1" on odd bars for extra momentum
                    if ((theme == 0 || theme == 1 || theme == 4) && b % 2 == 1)
                        MusicStab(buf, N, (int)((bs + beat * 0.5f) * SR), stabHz, stabVol * 0.70f);
                }

                // Bar-4 turnaround: 4-note ascending arpeggio on last beat (16th notes)
                float fillBase    = 3f * bar + 3f * beat;
                float sixteenth   = beat * 0.25f;
                float[] fc        = pad[3];
                float[] fillNotes = fc.Length >= 3
                    ? new[]{ fc[0] * 2f, fc[1] * 2f, fc[2] * 2f, fc[0] * 4f }
                    : new[]{ fc[0] * 2f, fc[0] * 2.52f, fc[0] * 3f, fc[0] * 4f };
                for (int k = 0; k < 4; k++)
                    MusicStab(buf, N, (int)((fillBase + k * sixteenth) * SR), fillNotes[k], 0.10f);
            }

            // ── Normalize to 86% headroom ─────────────────────────────────────
            float peak = 0f;
            for (int i = 0; i < N; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            if (peak > 0.02f)
            {
                float g = 0.86f / peak;
                for (int i = 0; i < N; i++)
                    buf[i] = Mathf.Clamp(buf[i] * g, -0.95f, 0.95f);
            }

            return Make(buf);
        }

        // ── music layer writers ────────────────────────────────────────────────

        static float MusicEnv(float t, float dur, float atk, float rel, bool squared)
        {
            float e = t < atk        ? t / atk
                    : t > dur - rel  ? Mathf.Max(0f, (dur - t) / rel)
                    : 1f;
            return squared ? e * e : e;
        }

        static void MusicKick(float[] buf, int N, int start)
        {
            // 808-style: sine with rapid pitch drop (95 Hz → 28 Hz)
            int len = Samples(0.22f);
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float t    = i / (float)SR;
                float env  = Mathf.Exp(-t * 20f);
                float freq = 95f * Mathf.Exp(-t * 38f) + 28f;
                phase += freq / SR;
                buf[si] += (float)(System.Math.Sin(phase * 2 * System.Math.PI) * env * 0.72f);
            }
        }

        static void MusicSnare(float[] buf, int N, int start, System.Random rng)
        {
            int len = Samples(0.16f);
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float t     = i / (float)SR;
                float env   = Mathf.Exp(-t * 16f);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float tone  = Mathf.Sin(Tau(185f, t));
                buf[si] += (noise * 0.65f + tone * 0.35f) * env * 0.42f;
            }
        }

        static void MusicHat(float[] buf, int N, int start, float amp, System.Random rng)
        {
            int len = Samples(0.034f);
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float t = i / (float)SR;
                buf[si] += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 65f) * amp;
            }
        }

        static void MusicPiano(float[] buf, int N, int start, float freq, float amp)
        {
            int len = Samples(0.38f);
            double ph1 = 0, ph2 = 0, ph3 = 0;
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float  t   = i / (float)SR;
                float  env = Mathf.Exp(-t * 7f);
                ph1 += freq        / SR;
                ph2 += freq * 2.0  / SR;
                ph3 += freq * 3.01 / SR;
                float s = (float)(System.Math.Sin(ph1 * 2 * System.Math.PI) * 0.68
                                 + System.Math.Sin(ph2 * 2 * System.Math.PI) * 0.22
                                 + System.Math.Sin(ph3 * 2 * System.Math.PI) * 0.10);
                buf[si] += s * env * amp;
            }
        }

        static void MusicVocal(float[] buf, int N, int start, float freq, float amp)
        {
            float dur = 0.28f;
            int   len = Samples(dur);
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float t   = i / (float)SR;
                float vib = 1f + 0.007f * Mathf.Sin(t * 5.8f * 2f * Mathf.PI);
                float env = MusicEnv(t, dur, 0.05f, 0.10f, squared: false);
                ph += freq * vib / SR;
                buf[si] += (float)System.Math.Sin(ph * 2 * System.Math.PI) * env * amp;
            }
        }

        // Soft harmonic note for counter-melody and bar arpeggio (piano-like, non-intrusive)
        static void MusicCounterNote(float[] buf, int N, int start, float freq, float dur, float amp)
        {
            int    len = Samples(dur);
            double ph1 = 0, ph2 = 0;
            for (int i = 0; i < len; i++)
            {
                int si = start + i; if (si >= N) break;
                float t   = i / (float)SR;
                float env = Mathf.Exp(-t * 5.5f) * (1f - Mathf.Exp(-t * 60f));
                ph1 += freq       / SR;
                ph2 += freq * 2.0 / SR;
                float s = (float)(System.Math.Sin(ph1 * 2 * System.Math.PI) * 0.65
                                 + System.Math.Sin(ph2 * 2 * System.Math.PI) * 0.22);
                buf[si] += s * env * amp;
            }
        }

        // Theme-specific pad note — replaces the generic sine pad; gives each theme a distinct texture
        static void WritePadNote(float[] buf, int N, int start, float freq, float dur,
                                 float atk, float rel, float amp, int theme)
        {
            int len = Samples(dur);
            switch (theme)
            {
                case 0: // Cyber Blue — 3-oscillator supersaw (classic 80s synth pad)
                {
                    float[] det = { 1f, 1.0042f, 0.9958f };
                    double[] ph = new double[3];
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: true);
                        float s = 0f;
                        for (int d = 0; d < 3; d++)
                        {
                            ph[d] += freq * det[d] / SR;
                            float sv = 0f;
                            for (int h = 1; h <= 5; h++) sv += Mathf.Sin((float)(ph[d] * h * 2 * System.Math.PI)) * (1f / h);
                            s += sv * (d == 0 ? 0.45f : 0.28f);
                        }
                        buf[si] += s * env * amp * 0.30f;
                    }
                    break;
                }
                case 1: // Synthwave Purple — warm string pad (many harmonics + late vibrato)
                {
                    double ph = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: true);
                        float vib = t > 0.15f ? 1f + 0.004f * Mathf.Sin(t * 5.2f * 2f * Mathf.PI) : 1f;
                        ph += freq * vib / SR;
                        float s = 0f;
                        for (int h = 1; h <= 9; h++)
                            s += (float)System.Math.Sin(ph * h * 2 * System.Math.PI) * (1f / h) * Mathf.Exp(-h * 0.36f);
                        buf[si] += s * env * amp * 0.42f;
                    }
                    break;
                }
                case 2: // Biopunk Green — mellow organic pad (two detuned sines, slow breath LFO)
                {
                    double ph1 = 0, ph2 = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t     = i / (float)SR;
                        float env   = MusicEnv(t, dur, atk, rel, squared: true);
                        float bLfo  = 1f + 0.007f * Mathf.Sin(t * 1.6f * 2f * Mathf.PI);
                        ph1 += freq * bLfo / SR;
                        ph2 += freq * 1.0038 * bLfo / SR;
                        float s = (float)(System.Math.Sin(ph1 * 2 * System.Math.PI) * 0.52
                                        + System.Math.Sin(ph2 * 2 * System.Math.PI) * 0.32
                                        + System.Math.Sin(ph1 * 4 * System.Math.PI) * 0.10
                                        + System.Math.Sin(ph2 * 6 * System.Math.PI) * 0.04);
                        buf[si] += s * env * amp * 0.40f;
                    }
                    break;
                }
                case 3: // Medieval Crimson — pipe organ (full overtone series, slow build)
                {
                    double ph = 0;
                    float longAtk = dur * 0.32f;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, longAtk, rel, squared: false);
                        ph += freq / SR;
                        float s = (float)System.Math.Sin(ph * 2 * System.Math.PI) * 0.48f
                                + Mathf.Sin(Tau(freq * 2f, t)) * 0.22f
                                + Mathf.Sin(Tau(freq * 3f, t)) * 0.17f
                                + Mathf.Sin(Tau(freq * 4f, t)) * 0.09f
                                + Mathf.Sin(Tau(freq * 6f, t)) * 0.06f
                                + Mathf.Sin(Tau(freq * 8f, t)) * 0.03f;
                        buf[si] += s * env * amp * 0.36f;
                    }
                    break;
                }
                case 4: // Industrial Ghost — buzzy clipped square (gritty, dark)
                {
                    double ph = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: true);
                        ph += freq / SR;
                        float s = 0f;
                        for (int h = 1; h <= 11; h += 2)
                            s += (float)System.Math.Sin(ph * h * 2 * System.Math.PI) / h;
                        s = Mathf.Sign(s) * (1f - Mathf.Exp(-Mathf.Abs(s) * 2.0f));
                        buf[si] += s * env * amp * 0.28f;
                    }
                    break;
                }
                case 5: // Sakura Dusk — plucked koto (instant attack, fast harmonic decay)
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = Mathf.Exp(-t * 2.6f) * (1f - Mathf.Exp(-t * 90f));
                        float s   = Mathf.Sin(Tau(freq,      t)) * 0.60f
                                  + Mathf.Sin(Tau(freq * 2f, t)) * Mathf.Exp(-t *  5f) * 0.27f
                                  + Mathf.Sin(Tau(freq * 3f, t)) * Mathf.Exp(-t * 10f) * 0.11f
                                  + Mathf.Sin(Tau(freq * 4f, t)) * Mathf.Exp(-t * 18f) * 0.04f;
                        buf[si] += s * env * amp * 0.56f;
                    }
                    break;
                }
                case 6: // Solar Forge — bright brass shimmer (strong harmonics, slight pitch rise)
                {
                    double ph = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t      = i / (float)SR;
                        float env    = MusicEnv(t, dur, atk * 0.50f, rel, squared: false);
                        float fmod   = freq * (1f - 0.010f * Mathf.Exp(-t * 14f));
                        ph += fmod / SR;
                        float s = (float)System.Math.Sin(ph * 2 * System.Math.PI) * 0.42f
                                + Mathf.Sin(Tau(freq * 2f, t)) * 0.30f
                                + Mathf.Sin(Tau(freq * 3f, t)) * 0.20f
                                + Mathf.Sin(Tau(freq * 4f, t)) * 0.11f
                                + Mathf.Sin(Tau(freq * 5f, t)) * 0.06f;
                        buf[si] += s * env * amp * 0.30f;
                    }
                    break;
                }
                default: // case 7: Dawn Light — ambient string pad (very slow attack, deep vibrato)
                {
                    double ph = 0;
                    float dawAtk = dur * 0.42f;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, dawAtk, rel, squared: true);
                        float vib = t > 0.22f ? 1f + 0.005f * Mathf.Sin(t * 6.0f * 2f * Mathf.PI) : 1f;
                        ph += freq * vib / SR;
                        float s = 0f;
                        for (int h = 1; h <= 10; h++)
                            s += (float)System.Math.Sin(ph * h * 2 * System.Math.PI) * (0.5f / h) * Mathf.Exp(-h * 0.26f);
                        buf[si] += s * env * amp * 0.35f;
                    }
                    break;
                }
            }
        }

        // Syncopated chord stab — tight piano-like hit for rhythmic punctuation
        static void MusicStab(float[] buf, int N, int start, float freq, float amp)
        {
            float  dur = 0.11f;
            int    len = Samples(dur);
            double ph1 = 0, ph2 = 0;
            for (int i = 0; i < len; i++)
            {
                int si = start + i;
                if (si >= N) break;
                float t   = i / (float)SR;
                float env = MusicEnv(t, dur, 0.004f, dur * 0.60f, squared: false);
                ph1 += freq       / SR;
                ph2 += freq * 2.0 / SR;
                float s = (float)(System.Math.Sin(ph1 * 2 * System.Math.PI) * 0.65
                                 + System.Math.Sin(ph2 * 2 * System.Math.PI) * 0.25);
                buf[si] += s * env * amp;
            }
        }

        // Theme-matched lead instrument: saw for cyber/synth, FM for biopunk, pluck for medieval,
        // odd-harmonic for industrial, piano for sakura, guitar for solar, flute for dawn.
        static void MusicLeadNote(float[] buf, int N, int start, float freq,
                                  float dur, float atk, float rel, int theme)
        {
            int len = Samples(dur);
            switch (theme)
            {
                case 0: // CYBER BLUE — sawtooth synth (8 harmonics)
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: false);
                        float s   = 0f;
                        for (int h = 1; h <= 8; h++) s += Mathf.Sin(Tau(freq * h, t)) / h;
                        buf[si] += s * 0.42f * env * 0.26f;
                    }
                    break;
                }
                case 1: // SYNTHWAVE PURPLE — two detuned saws
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: false);
                        float s   = 0f;
                        for (int h = 1; h <= 6; h++)
                            s += (Mathf.Sin(Tau(freq * h * 1.005f, t))
                                + Mathf.Sin(Tau(freq * h * 0.995f, t))) / h;
                        buf[si] += s * 0.38f * env * 0.24f;
                    }
                    break;
                }
                case 2: // BIOPUNK GREEN — FM modulated (organic warble)
                {
                    double ph = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: false);
                        float mod = Mathf.Sin(Tau(freq * 2.7f, t)) * 2.5f;
                        ph += freq / SR;
                        buf[si] += Mathf.Sin((float)(ph * 2 * System.Math.PI) + mod) * env * 0.22f;
                    }
                    break;
                }
                case 3: // MEDIEVAL CRIMSON — plucked lute string
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = Mathf.Exp(-t * 4.5f) * (1f - Mathf.Exp(-t * 80f));
                        float s   = Mathf.Sin(Tau(freq,      t)) * 0.58f
                                  + Mathf.Sin(Tau(freq * 2f, t)) * Mathf.Exp(-t *  8f) * 0.28f
                                  + Mathf.Sin(Tau(freq * 3f, t)) * Mathf.Exp(-t * 12f) * 0.11f
                                  + Mathf.Sin(Tau(freq * 4f, t)) * Mathf.Exp(-t * 18f) * 0.06f;
                        buf[si] += s * env * 0.32f;
                    }
                    break;
                }
                case 4: // INDUSTRIAL GHOST — odd harmonics (hollow, distorted)
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk, rel, squared: false);
                        float s   = 0f;
                        for (int h = 1; h <= 9; h += 2) s += Mathf.Sin(Tau(freq * h, t)) / h;
                        buf[si] += s * 0.50f * env * 0.26f;
                    }
                    break;
                }
                case 5: // SAKURA DUSK — piano / koto (sharp attack, fast harmonic decay)
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = Mathf.Exp(-t * 5.5f) * (1f - Mathf.Exp(-t * 100f));
                        float s   = Mathf.Sin(Tau(freq,      t)) * 0.65f
                                  + Mathf.Sin(Tau(freq * 2f, t)) * Mathf.Exp(-t *  7f) * 0.24f
                                  + Mathf.Sin(Tau(freq * 3f, t)) * Mathf.Exp(-t * 12f) * 0.09f;
                        buf[si] += s * env * 0.30f;
                    }
                    break;
                }
                case 6: // SOLAR FORGE — warm nylon guitar
                {
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = Mathf.Exp(-t * 3.0f) * (1f - Mathf.Exp(-t * 60f));
                        float s   = Mathf.Sin(Tau(freq,      t)) * 0.60f
                                  + Mathf.Sin(Tau(freq * 2f, t)) * Mathf.Exp(-t * 4.5f) * 0.28f
                                  + Mathf.Sin(Tau(freq * 3f, t)) * Mathf.Exp(-t * 7.0f) * 0.12f;
                        buf[si] += s * env * 0.30f;
                    }
                    break;
                }
                default: // case 7: DAWN LIGHT — breathy flute with vibrato
                {
                    double ph = 0;
                    for (int i = 0; i < len; i++)
                    {
                        int si = start + i; if (si >= N) break;
                        float t   = i / (float)SR;
                        float env = MusicEnv(t, dur, atk * 3f, rel, squared: false);
                        float vib = 1f + (t > 0.08f ? 0.005f * Mathf.Sin(t * 5.4f * 2f * Mathf.PI) : 0f);
                        ph += freq * vib / SR;
                        float s = Mathf.Sin((float)(ph * 2 * System.Math.PI)) * 0.72f
                                + Mathf.Sin(Tau(freq * 2f, t)) * 0.18f;
                        buf[si] += s * env * 0.26f;
                    }
                    break;
                }
            }
        }

        // ── chord / melody tables ──────────────────────────────────────────────

        // Note name → Hz.  Call MF("A4") etc.
        static float MF(string n) => n switch
        {
            "C2"  =>  65.41f, "D2"  =>  73.42f, "E2"  =>  82.41f, "F2"  =>  87.31f,
            "G2"  =>  98.00f, "Ab2" => 103.83f, "A2"  => 110.00f, "Bb2" => 116.54f, "B2" => 123.47f,
            "C3"  => 130.81f, "Db3" => 138.59f, "D3"  => 146.83f, "Eb3" => 155.56f, "E3" => 164.81f,
            "F3"  => 174.61f, "F#3" => 185.00f, "G3"  => 196.00f, "Ab3" => 207.65f, "A3" => 220.00f,
            "Bb3" => 233.08f, "B3"  => 246.94f,
            "C4"  => 261.63f, "Db4" => 277.18f, "D4"  => 293.66f, "Eb4" => 311.13f, "E4" => 329.63f,
            "F4"  => 349.23f, "F#4" => 369.99f, "G4"  => 392.00f, "Ab4" => 415.30f, "A4" => 440.00f,
            "Bb4" => 466.16f, "B4"  => 493.88f,
            "C5"  => 523.25f, "D5"  => 587.33f, "Eb5" => 622.25f, "E5"  => 659.26f,
            "F5"  => 698.46f, "F#5" => 739.99f, "G5"  => 783.99f, "Ab5" => 830.61f,
            "A5"  => 880.00f, "Bb5" => 932.33f, "B5"  => 987.77f, "C6"  =>1046.50f,
            _     => 440.00f
        };

        static float[] MNotes(params string[] ns)
        {
            var r = new float[ns.Length];
            for (int i = 0; i < ns.Length; i++) r[i] = MF(ns[i]);
            return r;
        }

        // 4-bar chord pad — one float[] (chord) per bar
        static float[][] MusicPadChords(int theme) => theme switch
        {
            // CYBER BLUE:       Am   –   F    –   C    –   G
            0 => new[]{ MNotes("A3","C4","E4"),  MNotes("F3","A3","C4"),
                        MNotes("C4","E4","G4"),  MNotes("G3","B3","D4") },
            // SYNTHWAVE PURPLE: Cm   –   Ab   –   Eb   –   Bb
            1 => new[]{ MNotes("C3","Eb3","G3"), MNotes("Ab3","C4","Eb4"),
                        MNotes("Eb3","G3","Bb3"),MNotes("Bb3","D4","F4") },
            // BIOPUNK GREEN:    Am   –   E    –   Dm   –   G
            2 => new[]{ MNotes("A3","C4","E4"),  MNotes("E3","Ab3","B3"),
                        MNotes("D3","F3","A3"),  MNotes("G3","B3","D4") },
            // MEDIEVAL CRIMSON: Dm   –   Bb   –   F    –   C
            3 => new[]{ MNotes("D3","F3","A3"),  MNotes("Bb3","D4","F4"),
                        MNotes("F3","A3","C4"),  MNotes("C4","E4","G4") },
            // INDUSTRIAL GHOST: Em   –   C    –   G    –   D
            4 => new[]{ MNotes("E3","G3","B3"),  MNotes("C4","E4","G4"),
                        MNotes("G3","B3","D4"),  MNotes("D4","F#4","A4") },
            // SAKURA DUSK:      D    –   A    –   G    –   A
            5 => new[]{ MNotes("D4","F#4","A4"), MNotes("A3","Db4","E4"),
                        MNotes("G3","B3","D4"),  MNotes("A3","Db4","E4") },
            // SOLAR FORGE:      C    –   F    –   G    –   C
            6 => new[]{ MNotes("C4","E4","G4"),  MNotes("F4","A4","C5"),
                        MNotes("G4","B4","D5"),  MNotes("C4","E4","G4") },
            // DAWN LIGHT:       C    –   Am   –   F    –   G
            7 => new[]{ MNotes("C4","E4","G4"),  MNotes("A3","C4","E4"),
                        MNotes("F3","A3","C4"),  MNotes("G3","B3","D4") },
            _ => new[]{ MNotes("A3","C4","E4"),  MNotes("F3","A3","C4"),
                        MNotes("C4","E4","G4"),  MNotes("G3","B3","D4") },
        };

        // Bass root Hz per bar — one octave below chord root
        static float[] MusicBassRoots(int theme) => theme switch
        {
            0 => new[]{ 110.00f,  87.31f, 130.81f,  98.00f }, // A2 F2 C3 G2
            1 => new[]{  65.41f, 103.83f, 155.56f, 116.54f }, // C2 Ab2 Eb3 Bb2
            2 => new[]{ 110.00f,  82.41f, 146.83f,  98.00f }, // A2 E2 D3 G2
            3 => new[]{ 146.83f, 116.54f,  87.31f, 130.81f }, // D3 Bb2 F2 C3
            4 => new[]{  82.41f, 130.81f,  98.00f, 146.83f }, // E2 C3 G2 D3
            5 => new[]{ 146.83f, 110.00f,  98.00f, 110.00f }, // D3 A2 G2 A2
            6 => new[]{ 130.81f, 174.61f, 196.00f, 130.81f }, // C3 F3 G3 C3
            7 => new[]{ 130.81f, 110.00f,  87.31f,  98.00f }, // C3 A2 F2 G2
            _ => new[]{ 110.00f,  87.31f, 130.81f,  98.00f },
        };

        // 8 lead melody notes played one every half-bar (2 per bar)
        static float[] MusicLeadLine(int theme) => theme switch
        {
            // CYBER BLUE — A minor pentatonic ascending / descending
            0 => new[]{ 659.26f, 880.00f, 783.99f, 659.26f, 523.25f, 587.33f, 659.26f, 440.00f },
            // SYNTHWAVE PURPLE — C minor, 80s synth feel
            1 => new[]{ 392.00f, 622.25f, 783.99f, 932.33f, 830.61f, 783.99f, 622.25f, 523.25f },
            // BIOPUNK GREEN — A natural minor, tense
            2 => new[]{ 440.00f, 523.25f, 659.26f, 523.25f, 440.00f, 392.00f, 349.23f, 329.63f },
            // MEDIEVAL CRIMSON — D Dorian march
            3 => new[]{ 587.33f, 698.46f, 880.00f, 587.33f, 523.25f, 466.16f, 440.00f, 587.33f },
            // INDUSTRIAL GHOST — E minor, driving
            4 => new[]{ 659.26f, 783.99f, 987.77f, 783.99f, 659.26f, 587.33f, 493.88f, 659.26f },
            // SAKURA DUSK — D pentatonic major, lyrical
            5 => new[]{ 587.33f, 739.99f, 880.00f, 987.77f, 880.00f, 739.99f, 659.26f, 587.33f },
            // SOLAR FORGE — C major, triumphant ascent
            6 => new[]{ 659.26f, 783.99f,1046.50f, 783.99f, 880.00f, 783.99f, 659.26f, 523.25f },
            // DAWN LIGHT — C major, gentle and floating
            7 => new[]{ 523.25f, 659.26f, 783.99f, 659.26f, 698.46f, 880.00f, 783.99f, 659.26f },
            _ => new[]{ 659.26f, 880.00f, 783.99f, 659.26f, 523.25f, 587.33f, 659.26f, 440.00f },
        };

        // ── helpers ────────────────────────────────────────────────────────────

        static int Samples(float dur) => Mathf.Max(1, Mathf.RoundToInt(SR * dur));

        static float Tau(float freq, float t) => 2f * Mathf.PI * freq * t;

        static AudioClip Make(float[] data)
        {
            var c = AudioClip.Create("_nw", data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
