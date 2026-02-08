using Godot;
using System;

namespace PitchGame
{
    public partial class KaraokeCursor : Control 
    {
        [Export] public PitchDetector Detector;
        [Export] public PitchGrid Grid; 
        
        [ExportGroup("Motion")]
        [Export] public float SmoothSpeed = 30f;
        [Export] public float GhostDelay = 0.5f; // Time before hiding
        
        private Control _visual; // Changed from CanvasItem to Control
        private float _visualMidi = 60f;
        private float _silenceTimer = 0f;
        private bool _wasSinging = false;

        public override void _Ready()
        {
            // Cast to Control to ensure we can access Scale/Size
            _visual = GetChildOrNull<Control>(0);
        }

        public override void _Process(double delta)
        {
            if (Grid == null || Detector == null) return;

            if (Detector.IsDetected)
            {
                _silenceTimer = 0f; // Reset ghost timer
                
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
                
                // Only hide after the delay to prevent flickering on breath/staccato
                if (_silenceTimer >= GhostDelay && _visual != null)
                {
                    _visual.Visible = false;
                }
            }

            // Calculate Position
            // Note: We update Position even if invisible so it's ready when voice returns
            float targetY = Grid.GetLocalYFromMidi(_visualMidi);
            
            // Center horizontally in the track, move vertically to the pitch
            Position = new Vector2(Grid.Size.X / 2f, targetY);

            // Visual feedback for detection "intensity"
            if (_visual != null && _visual.Visible)
            {
                // Pulse or scale based on active singing
                float targetScale = _wasSinging ? 1.2f : 1.0f;
                float s = Mathf.Lerp(_visual.Scale.X, targetScale, (float)delta * 10f);
                _visual.Scale = new Vector2(s, s);
            }
        }
    }
}