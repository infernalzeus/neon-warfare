using System;
using System.Collections.Generic;
using UnityEngine;

namespace NW.App
{
    // Shared music-theory foundation for the procedural music system in AudioManager.cs.
    // Single source of truth for a theme's key/scale/chords (root + scale + scale-degree
    // progression), replacing the old pattern of hand-transcribing Hz values in three
    // separate places (MusicPadChords / MusicBassRoots / MusicLeadLine).
    public static partial class AudioManager
    {
        enum ChordQuality { Maj, Min, Dim, Aug, Maj7, Min7, Dom7, Sus2, Sus4 }

        struct ChordDeg
        {
            public int Degree;
            public ChordQuality Quality;
            public int Inversion;
            public ChordDeg(int degree, ChordQuality quality, int inversion = 0)
            {
                Degree = degree; Quality = quality; Inversion = inversion;
            }
        }

        // One lead/counter-melody note resolved to an absolute pitch and clip-relative timing.
        struct LeadNote { public float Hz; public float StartBeat; public float DurBeats; }
        struct CounterNote { public float Hz; public float StartBeat; public float DurBeats; public float Amp; }

        // Fully-resolved per-theme song data that BuildMusic consumes uniformly, whether it
        // came from the new scale/chord resolver (reworked themes) or from the legacy
        // hand-transcribed tables (themes not yet reworked, or intentionally left untouched).
        struct ResolvedMusic
        {
            public int TotalBars;
            public float[][] ChordHz;      // [bar][chord tone] — drives pad, bar-arpeggio, stabs, turnaround fill
            public float[][] BassBeatHz;   // [bar][4 beats]
            public bool[] DrumFillBar;     // [bar] — triggers the hi-hat cascade fill
            public bool HasDrums;
            public bool FourFloor;         // kick on every beat instead of 1+3
            public float StabVol;
            public bool[] ExtraSyncBar;    // [bar] — extra "and of 1" stab for driving themes
            public LeadNote[] LeadNotes;
            public CounterNote[] CounterNotes;
        }

        // Common scale interval patterns, in semitones from the root.
        static readonly int[] ScaleNatMinor = { 0, 2, 3, 5, 7, 8, 10 };
        static readonly int[] ScaleMajor    = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] ScaleDorian   = { 0, 2, 3, 5, 7, 9, 10 };
        static readonly int[] ScaleMinPent  = { 0, 3, 5, 7, 10 };
        static readonly int[] ScaleMajPent  = { 0, 2, 4, 7, 9 };

        static readonly Dictionary<ChordQuality, int[]> ChordIntervals = new()
        {
            { ChordQuality.Maj,  new[]{0,4,7} },
            { ChordQuality.Min,  new[]{0,3,7} },
            { ChordQuality.Dim,  new[]{0,3,6} },
            { ChordQuality.Aug,  new[]{0,4,8} },
            { ChordQuality.Maj7, new[]{0,4,7,11} },
            { ChordQuality.Min7, new[]{0,3,7,10} },
            { ChordQuality.Dom7, new[]{0,4,7,10} },
            { ChordQuality.Sus2, new[]{0,2,7} },
            { ChordQuality.Sus4, new[]{0,5,7} },
        };

        // Equal temperament: MIDI 69 = A4 = 440Hz.
        static float MidiToHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

        // Resolves a scale degree (may be negative or exceed scale.Length; wraps octaves).
        static int DegreeToMidi(int rootMidi, int[] scale, int degree)
        {
            int len = scale.Length;
            int oct = FloorDiv(degree, len);
            int idx = ((degree % len) + len) % len;
            return rootMidi + oct * 12 + scale[idx];
        }

        static float DegreeToHz(int rootMidi, int[] scale, int degree) =>
            MidiToHz(DegreeToMidi(rootMidi, scale, degree));

        // Builds a chord's Hz array from a scale-degree root + chord quality + inversion.
        // Interval offsets are chromatic (semitones) from the chord's own root pitch, not
        // further scale-degree lookups — this deliberately allows out-of-scale chord tones
        // (e.g. a Dorian iv chord's major 3rd), exactly like the existing hand-picked chords.
        static float[] BuildChordHz(int rootMidi, int[] scale, ChordDeg cd)
        {
            int[] intervals = ChordIntervals[cd.Quality];
            int chordRootMidi = DegreeToMidi(rootMidi, scale, cd.Degree);
            var hz = new float[intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
                hz[i] = MidiToHz(chordRootMidi + intervals[i]);

            for (int inv = 0; inv < cd.Inversion; inv++)
            {
                float lowest = hz[0]; int lowestIdx = 0;
                for (int i = 1; i < hz.Length; i++)
                    if (hz[i] < lowest) { lowest = hz[i]; lowestIdx = i; }
                hz[lowestIdx] *= 2f;
            }
            Array.Sort(hz);
            return hz;
        }

        // Resolves a chord-definition array to per-bar Hz arrays plus their root scale degrees
        // (the latter needed by BassPatternForBars).
        static (float[][] chordHz, int[] rootDeg) ResolveChords(int rootMidi, int[] scale, ChordDeg[] defs)
        {
            var chordHz = new float[defs.Length][];
            var rootDeg = new int[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                chordHz[i] = BuildChordHz(rootMidi, scale, defs[i]);
                rootDeg[i] = defs[i].Degree;
            }
            return (chordHz, rootDeg);
        }

        // Builds a lead-melody note sequence from one or more phrase "phases" (scale degrees +
        // beat-count durations). Each phase's durations sum to that phase's length in beats;
        // phases concatenate back-to-back. Shared by every theme's lead-phrase builder so the
        // phase/cumulative-timing bookkeeping isn't duplicated per theme.
        static LeadNote[] BuildLeadNotes(int rootMidi, int[] scale, params (int[] degs, int[] durs)[] phases)
        {
            var list = new List<LeadNote>();
            float phaseStart = 0f;
            foreach (var phase in phases)
            {
                float t = phaseStart;
                for (int i = 0; i < phase.degs.Length; i++)
                {
                    list.Add(new LeadNote
                    {
                        Hz        = DegreeToHz(rootMidi, scale, phase.degs[i]),
                        StartBeat = t,
                        DurBeats  = phase.durs[i] * 0.94f,
                    });
                    t += phase.durs[i];
                }
                phaseStart = t;
            }
            return list.ToArray();
        }

        // One independent counter-melody note per bar, on the "and of 1", using the chord's
        // 3rd an octave below the pad — not a doubled arpeggio.
        static CounterNote[] BuildBarCounterMelody(float[][] chordHz, float amp = 0.10f)
        {
            var arr = new CounterNote[chordHz.Length];
            for (int b = 0; b < chordHz.Length; b++)
            {
                float[] c = chordHz[b];
                float cHz = c.Length > 1 ? c[1] * 0.5f : c[0] * 0.5f;
                arr[b] = new CounterNote { Hz = cHz, StartBeat = b * 4f + 1.5f, DurBeats = 1.2f, Amp = amp };
            }
            return arr;
        }

        // Marks the last bar of each section (for DrumFillBar) given each section's bar count.
        static bool[] BuildSectionFillFlags(params int[] sectionBars)
        {
            int total = 0; foreach (var s in sectionBars) total += s;
            var flags = new bool[total];
            int idx = -1;
            foreach (var s in sectionBars) { idx += s; flags[idx] = true; }
            return flags;
        }

        // Scale-aware bass pattern: beat1=root (one octave down), beat2=diatonic "fifth"
        // (4 scale steps up from that root), beat3=root, beat4=a diatonic leading tone
        // approaching next bar's root from a scale-step below — replaces the old fixed
        // root/(root*1.498) pattern that ignored chord quality entirely.
        static float[][] BassPatternForBars(int rootMidi, int[] scale, int[] chordRootDegrees)
        {
            int n = chordRootDegrees.Length;
            var result = new float[n][];
            for (int i = 0; i < n; i++)
            {
                int rootDeg  = chordRootDegrees[i] - scale.Length;               // one octave down
                int fifthDeg = rootDeg + 4;                                      // ~diatonic fifth
                int nextDeg  = chordRootDegrees[(i + 1) % n] - scale.Length;

                float beat0 = DegreeToHz(rootMidi, scale, rootDeg);
                float beat1 = DegreeToHz(rootMidi, scale, fifthDeg);
                float beat2 = DegreeToHz(rootMidi, scale, rootDeg);
                float beat3 = nextDeg == rootDeg
                    ? beat0
                    : DegreeToHz(rootMidi, scale, nextDeg - 1); // approach from below

                result[i] = new[] { beat0, beat1, beat2, beat3 };
            }
            return result;
        }
    }
}
