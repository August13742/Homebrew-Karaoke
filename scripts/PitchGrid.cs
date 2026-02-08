using Godot;
using System;

namespace PitchGame
{
    public partial class PitchGrid : Control
    {
        [Export] public PitchDetector Detector;
        
        [ExportGroup("View Settings")]
        [Export] public float SemitonesToPixels = 20.0f; // Zoom level
        [Export] public float DeadzoneSemitones = 4.0f;  // Singer can move this much before grid scrolls
        [Export] public float ScrollSpeed = 5.0f;        // Base camera follow speed
        [Export] public float ScrollDamping = 0.8f;      // 0.0 = snappy, 1.0 = loose

        [ExportGroup("Visuals")]
        [Export] public int RangeSemitones = 36;
        [Export] public Color GridColor = new Color(1, 1, 1, 0.1f);
        [Export] public Color OctaveColor = new Color(1, 1, 1, 0.25f);
        [Export] public Font FileFont; // Optional custom font

        // The current MIDI note at the exact vertical center of this Control
        public float VisualCenterMidi { get; private set; } = 60f;

        private float _targetCenterMidi = 60f;
        private float _currentVelocity = 0f; // For smooth damping

        public override void _Ready()
        {
            VisualCenterMidi = 60f;
            _targetCenterMidi = 60f;
        }

        public override void _Process(double delta)
        {
            if (Detector == null) return;

            // 1. Determine Target Center
            if (Detector.IsDetected)
            {
                float sungMidi = Detector.CurrentMidiNote + (Detector.CentDeviation / 100f);
                float distFromCenter = sungMidi - _targetCenterMidi;

                // 2. Deadzone Logic (Hysteresis)
                // Only move the target if the singer pushes outside the comfortable box
                if (Mathf.Abs(distFromCenter) > DeadzoneSemitones)
                {
                    // Calculate how far past the deadzone we are
                    float push = distFromCenter - (Mathf.Sign(distFromCenter) * DeadzoneSemitones);
                    
                    // Shift the target center to absorb this push
                    _targetCenterMidi += push;
                }
            }
            // Else: If silent, we maintain the last valid position (no awkward snapping back)

            // 3. Smooth Camera Movement (Spring/Damping)
            // Using SmoothDamp logic manually for organic camera feel
            if (!Mathf.IsEqualApprox(VisualCenterMidi, _targetCenterMidi, 0.01f))
            {
                // Simple proportional follow with variable speed based on distance (Adaptive)
                float diff = _targetCenterMidi - VisualCenterMidi;
                
                // If the jump is huge, move faster. If small, move gentle.
                float adaptiveSpeed = ScrollSpeed + (Mathf.Abs(diff) * 0.5f); 
                
                VisualCenterMidi = Mathf.Lerp(VisualCenterMidi, _targetCenterMidi, (float)delta * adaptiveSpeed);
                
                QueueRedraw();
            }
        }

        // --- PUBLIC API FOR CURSOR ---
        
        /// <summary>
        /// Converts a MIDI value to a local Y position relative to this Grid's center.
        /// </summary>
        public float GetLocalYFromMidi(float midiValue)
        {
            // Difference from the currently visible center
            float diff = midiValue - VisualCenterMidi;
            
            // Positive diff (Higher Pitch) -> Negative Y (Upwards)
            // We offset by Height/2 because 0,0 is top-left, but our logic centers on the Control.
            return (Size.Y / 2f) - (diff * SemitonesToPixels);
        }

        public override void _Draw()
        {
            float halfHeight = Size.Y / 2f;
            int startNote = Mathf.FloorToInt(VisualCenterMidi - (halfHeight / SemitonesToPixels));
            int endNote = Mathf.CeilToInt(VisualCenterMidi + (halfHeight / SemitonesToPixels));

            // Buffer to ensure lines just off-screen are drawn
            startNote -= 2;
            endNote += 2;

            var font = FileFont ?? GetThemeDefaultFont();

            for (int note = startNote; note <= endNote; note++)
            {
                float drawY = GetLocalYFromMidi(note);

                bool isOctave = (note % 12 == 0);
                Color color = isOctave ? OctaveColor : GridColor;
                float width = isOctave ? 2.0f : 1.0f;

                DrawLine(new Vector2(0, drawY), new Vector2(Size.X, drawY), color, width);

                if (isOctave)
                {
                    DrawString(font, new Vector2(8, drawY - 4), $"C{(note / 12) - 1}", HorizontalAlignment.Left, -1, 14, color);
                }
            }
        }
    }
}