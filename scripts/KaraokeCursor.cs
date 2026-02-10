using Godot;
using System;

namespace PitchGame
{
    /// <summary>
    /// Shows the singer's pitch on the grid.
    /// No wrapping, no folding — raw pitch position, clamped to grid bounds.
    /// If the singer is out of the melody's range, the cursor goes to the edge.
    /// </summary>
    public partial class KaraokeCursor : Control 
    {
        [Export] public PitchDetector Detector;
        [Export] public PitchGrid Grid; 
        
        [ExportGroup("Motion")]
        [Export] public float SmoothSpeed = 30f;
        [Export] public float GhostDelay = 0.5f;
        
        private Control _visual;
        private float _visualMidi = 60f;
        private float _silenceTimer = 0f;
        private bool _wasSinging = false;

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
                
                // Raw pitch — NO wrapping, NO folding. What you sing is what you see.
                float rawMidi = Detector.CurrentMidiNote + (Detector.CentDeviation / 100f);
                
                if (!_wasSinging) _visualMidi = rawMidi; 
                else _visualMidi = Mathf.Lerp(_visualMidi, rawMidi, (float)delta * SmoothSpeed);

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

            if (_visual != null && _visual.Visible)
            {
                float targetScale = _wasSinging ? 1.2f : 1.0f;
                float s = Mathf.Lerp(_visual.Scale.X, targetScale, (float)delta * 10f);
                _visual.Scale = new Vector2(s, s);
            }
        }
    }
}