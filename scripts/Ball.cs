using Godot;
using System;

namespace PitchGame
{
    public partial class Ball : CharacterBody3D
    {
        [Export] public PitchDetector Detector;
        [Export] public float MinY = -4f;
        [Export] public float MaxY = 4f;
        
        [ExportGroup("Elastic Range (The Granularity Fix)")]
        [Export] public int BaseRangeSemitones = 12; // Start with 1 octave (High zoom/sensitivity)
        [Export] public float ExpandSpeed = 100f;    // Instant expansion
        [Export] public float ShrinkSpeed = 0.5f;    // Slow shrinking (Elastic return)
        [Export] public float MinRangeSize = 12f;    // Never shrink smaller than 1 octave (prevents jitter)

        [ExportGroup("Movement Physics")]
        [Export] public float ResponseSpeed = 40f;   // Doubled for snappiness
        [Export] public float Gravity = 15f;         // Falling speed when silent
        
        // Dynamic Range State
        private float _currentMinMidi = 55f; // G2 (Default start)
        private float _currentMaxMidi = 67f; // G3
        private float _targetY = 0f;

        public override void _Ready()
        {
            _targetY = MinY;
            // Center the initial range roughly where an average voice might be
            _currentMinMidi = 57; 
            _currentMaxMidi = 57 + BaseRangeSemitones;
        }

        public override void _Process(double delta)
        {
            if (Detector == null) return;

            // 1. INPUT HANDLING
            // If we have a confident signal, we move. If not, we fall.
            if (Detector.IsDetected && Detector.Confidence > 0.4f)
            {
                // Get high-precision pitch (Note + Cents)
                float pitchValue = Detector.CurrentMidiNote + (Detector.CentDeviation / 100f);
                
                // 2. ELASTIC RANGE LOGIC
                // A. Instant Expansion (No invisible ceiling)
                if (pitchValue > _currentMaxMidi) _currentMaxMidi = pitchValue;
                if (pitchValue < _currentMinMidi) _currentMinMidi = pitchValue;

                // B. Slow Contraction (The "Zoom" Effect)
                // We slowly pull the bounds TOWARDS the player's current pitch.
                // This ensures the screen range is always tight around where you are singing.
                
                // Pull Max down towards current pitch (but keep MinRange buffer)
                float targetMax = Mathf.Max(pitchValue + BaseRangeSemitones * 0.5f, _currentMinMidi + MinRangeSize);
                _currentMaxMidi = Mathf.Lerp(_currentMaxMidi, targetMax, (float)delta * ShrinkSpeed);

                // Pull Min up towards current pitch
                float targetMin = Mathf.Min(pitchValue - BaseRangeSemitones * 0.5f, _currentMaxMidi - MinRangeSize);
                _currentMinMidi = Mathf.Lerp(_currentMinMidi, targetMin, (float)delta * ShrinkSpeed);

                // 3. MAPPING
                float range = _currentMaxMidi - _currentMinMidi;
                // Prevent divide by zero
                if (range < 1f) range = 1f; 

                // Normalized position (0 to 1)
                float t = (pitchValue - _currentMinMidi) / range;
                t = Mathf.Clamp(t, 0f, 1f);

                // Map to Screen Coordinates
                _targetY = Mathf.Lerp(MinY, MaxY, t);

                // 4. MOVE TO TARGET (Snappy)
                Vector3 pos = GlobalPosition;
                pos.Y = Mathf.Lerp(pos.Y, _targetY, (float)delta * ResponseSpeed);
                GlobalPosition = pos;
            }
            else
            {
                // 5. GRAVITY (When silent)
                // Instead of lerping to bottom, let's use a "Gravity" feel
                Vector3 pos = GlobalPosition;
                pos.Y = Mathf.MoveToward(pos.Y, MinY, (float)delta * Gravity);
                GlobalPosition = pos;
            }
        }
    }
}