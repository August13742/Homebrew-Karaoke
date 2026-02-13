using Godot;
using System;
using System.Linq;

namespace PitchGame
{
    /// <summary>
    /// Shows the singer's pitch on the grid with target-anchored octave folding.
    /// The cursor position is computed as an offset from the current target note,
    /// folded to ±6 semitones. This ensures singers in different octaves still
    /// see their cursor near the target bars, without modulo discontinuities.
    /// </summary>
    public partial class KaraokeCursor : Control 
    {
        [Export] public PitchDetector Detector;
        [Export] public PitchGrid Grid; 
        [Export] public ScrollingLyrics LyricsSource;
        
        [ExportGroup("Motion")]
        [Export] public float SmoothSpeed = 30f;
        [Export] public float GhostDelay = 0.5f;
        
        [ExportGroup("Octave Folding")]
        [Export] public bool EnableOctaveFolding = true;

        [ExportGroup("Timing Line")]
        [Export] public bool ShowTimingLine = true;
        [Export] public Color TimingLineColor = new Color(1, 1, 1, 0.2f);

        [Export] public SongControlPanel ControlPanel;
        
        private Control _visual;
        private float _visualMidi = 60f;
        private float _silenceTimer = 0f;
        private bool _wasSinging = false;
        
        // Target-anchored folding state
        private float _anchorMidi = 0f;

        public override void _Ready()
        {
            _visual = GetChildOrNull<Control>(0);
        }

        public override void _Process(double delta)
        {
            if (Grid == null || Detector == null) return;

            if (Detector.IsDetected)
            {
                _silenceTimer = 0f; 
                
                float rawMidi = Detector.CurrentMidiNote + (Detector.CentDeviation / 100f);
                
                // Apply target-anchored folding if enabled
                float displayMidi = EnableOctaveFolding ? GetDisplayMidi(rawMidi) : rawMidi;
                
                if (!_wasSinging) _visualMidi = displayMidi; 
                else _visualMidi = Mathf.Lerp(_visualMidi, displayMidi, (float)delta * SmoothSpeed);

                _wasSinging = true;
                if (_visual != null) _visual.Visible = true;
            }
            else
            {
                _silenceTimer += (float)delta;
                _wasSinging = false;
                
                if (_silenceTimer >= GhostDelay && _visual != null)
                {
                    _visual.Visible = false;
                }
            }

            // Position — clamp Y to grid bounds so cursor doesn't disappear off-screen
            float rawY = Grid.GetLocalYFromMidi(_visualMidi);
            float clampedY = Mathf.Clamp(rawY, 0, Grid.Size.Y);
            Position = new Vector2(Grid.Size.X / 2f, clampedY);

            QueueRedraw();

            if (_visual != null && _visual.Visible)
            {
                float targetScale = _wasSinging ? 1.2f : 1.0f;
                float s = Mathf.Lerp(_visual.Scale.X, targetScale, (float)delta * 10f);
                _visual.Scale = new Vector2(s, s);
            }
        }

        public override void _Draw()
        {
            if (ShowTimingLine && Grid != null)
            {
                // Vertical line spanning the grid height at the center (where cursor is)
                DrawLine(new Vector2(0, -Position.Y), new Vector2(0, Grid.Size.Y - Position.Y), TimingLineColor, 1.0f);
            }
        }
        
        /// <summary>
        /// Target-anchored display: fold singer pitch relative to the current target note.
        /// Offset is wrapped to ±6 semitones so octave-displaced singing still appears
        /// near the target bar. Anchor is "sticky" — persists between notes.
        /// </summary>
        private float GetDisplayMidi(float rawMidi)
        {
            // Update anchor from current target note
            UpdateAnchor();
            
            // No anchor yet — return raw
            if (_anchorMidi <= 0f) return rawMidi;
            
            // Fold the offset, not the absolute pitch
            float offset = rawMidi - _anchorMidi;
            while (offset > 6f)  offset -= 12f;
            while (offset < -6f) offset += 12f;
            
            return _anchorMidi + offset;
        }
        
        /// <summary>
        /// Find the active target note and update the anchor MIDI value.
        /// Anchor is sticky: if no target is currently active, we keep
        /// the last anchor to prevent cursor drift between notes.
        /// </summary>
        private void UpdateAnchor()
        {
            if (LyricsSource?.Data?.Words == null) return;
            if (AudioManager.Instance == null) return;
            
            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            float keyShift = ControlPanel?.KeyShiftSemitones ?? 0f;
            
            // Find the first word covering the current time with a valid pitch
            var words = LyricsSource.Data.Words;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (w.PitchMidi <= 0) continue;
                if (w.End < time) continue;
                if (w.Start > time) break; // Words are time-sorted; past current time
                
                // Active word found
                _anchorMidi = Mathf.Round(w.PitchMidi) + keyShift;
                return;
            }
            // No active word — keep sticky anchor
        }
    }
}