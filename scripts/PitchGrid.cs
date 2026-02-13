using Godot;
using System;
using System.Collections.Generic;

namespace PitchGame
{
    /// <summary>
    /// Adaptive pitch grid that tracks the current melody region.
    /// Range slides smoothly to follow the notes visible on screen,
    /// handling duet songs and register shifts without jumps.
    /// Falls back to fixed defaults when no melody data is available.
    /// </summary>
    public partial class PitchGrid : Control
    {
        [ExportGroup("Dependencies")]
        [Export] public ScrollingLyrics LyricsSource;

        [ExportGroup("Range")]
        [Export] public float DefaultMinMidi = 36f;  // C2 — fallback low
        [Export] public float DefaultMaxMidi = 84f;  // C6 — fallback high

        [ExportGroup("Adaptive Range")]
        [Export] public float Padding = 6f;           // Semitones above/below visible notes
        [Export] public float MinVisibleSpan = 18f;   // Minimum grid height in semitones
        [Export] public float RangeSmoothSpeed = 3.0f; // Lerp speed for range transitions
        [Export] public float LookaheadSeconds = 4.0f;
        [Export] public float LookbehindSeconds = 1.0f;

        [ExportGroup("Visuals")]
        [Export] public Color GridColor = new Color(1, 1, 1, 0.08f);

        // --- RANGE STATE ---
        public float RangeMinMidi { get; private set; }
        public float RangeMaxMidi { get; private set; }

        // --- PUBLIC API ---
        public float VisualCenterMidi => (RangeMinMidi + RangeMaxMidi) / 2f;
        public float SemitonesToPixels => Size.Y / Math.Max(RangeMaxMidi - RangeMinMidi, 1f);

        // Key shift applied to target notes (set externally)
        private float _keyShift = 0f;
        private SongControlPanel _controlPanel;

        public override void _Ready()
        {
            RangeMinMidi = DefaultMinMidi;
            RangeMaxMidi = DefaultMaxMidi;
            _controlPanel = GetTree().Root.FindChild("SongControlPanel", true, false) as SongControlPanel;
        }

        public override void _Process(double delta)
        {
            _keyShift = _controlPanel?.KeyShiftSemitones ?? 0f;
            UpdateRange((float)delta);
            QueueRedraw();
        }

        /// <summary>
        /// Converts a MIDI value to a local Y position.
        /// </summary>
        public float GetLocalYFromMidi(float midiValue)
        {
            float diff = midiValue - VisualCenterMidi;
            return (Size.Y / 2f) - (diff * SemitonesToPixels);
        }

        /// <summary>
        /// Sliding window range update. Scans notes near the current playback time
        /// and smoothly lerps the grid range to fit them.
        /// </summary>
        private void UpdateRange(float delta)
        {
            if (LyricsSource?.Data?.Words == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            var words = LyricsSource.Data.Words;

            float windowMin = float.MaxValue;
            float windowMax = float.MinValue;
            bool found = false;

            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (w.PitchMidi <= 0) continue;
                if (w.End < time - LookbehindSeconds) continue;
                if (w.Start > time + LookaheadSeconds) break;

                float midi = Mathf.Round(w.PitchMidi) + _keyShift;
                if (midi < windowMin) windowMin = midi;
                if (midi > windowMax) windowMax = midi;
                found = true;
            }

            if (!found) return; // Keep previous range during silence

            // Add padding and enforce minimum span
            float targetMin = windowMin - Padding;
            float targetMax = windowMax + Padding;
            float span = targetMax - targetMin;
            if (span < MinVisibleSpan)
            {
                float center = (targetMin + targetMax) / 2f;
                targetMin = center - MinVisibleSpan / 2f;
                targetMax = center + MinVisibleSpan / 2f;
            }

            // Smooth lerp to avoid jumps during register shifts
            RangeMinMidi = Mathf.Lerp(RangeMinMidi, targetMin, RangeSmoothSpeed * delta);
            RangeMaxMidi = Mathf.Lerp(RangeMaxMidi, targetMax, RangeSmoothSpeed * delta);
        }

        public override void _Draw()
        {
            int startNote = (int)Mathf.Floor(RangeMinMidi);
            int endNote = (int)Mathf.Ceil(RangeMaxMidi);

            for (int note = startNote; note <= endNote; note++)
            {
                float drawY = GetLocalYFromMidi(note);
                DrawLine(new Vector2(0, drawY), new Vector2(Size.X, drawY), GridColor, 1.0f);
            }
        }
    }
}