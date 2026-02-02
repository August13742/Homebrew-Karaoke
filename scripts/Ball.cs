using Godot;
using System;

namespace PitchGame
{
    public partial class Ball : CharacterBody3D
    {
        [Export] public PitchDetector Detector;
        [Export] public float MinY = -4f;
        [Export] public float MaxY = 4f;
        
        [ExportGroup("Manual Range Fallback")]
        [Export] public int MinMidi = 48; // C3
        [Export] public int MaxMidi = 72; // C5

        [ExportGroup("Movement Settings")]
        [Export] public float SmoothSpeed = 20f;
        [Export] public float SilenceReturnSpeed = 3f;

        [ExportGroup("Adaptive Selection")]
        [Export] public bool UseAdaptiveRange = true;
        [Export] public float AdaptationSpeed = 1.0f; // How fast bounds expand
        [Export] public float ResetSpeed = 0.05f;      // How fast bounds converge back to default

        private float _targetY = 0f;
        private float _dynamicMinMidi = 60;
        private float _dynamicMaxMidi = 72;

        public override void _Ready()
        {
            _targetY = MinY;
            _dynamicMinMidi = MinMidi;
            _dynamicMaxMidi = MaxMidi;

            Vector3 pos = GlobalPosition;
            pos.Y = _targetY;
            GlobalPosition = pos;
        }

        public override void _Process(double delta)
        {
            if (Detector == null) return;

            if (Detector.IsDetected)
            {
                float currentMidi = Detector.CurrentMidiNote + Detector.CentDeviation;

                if (UseAdaptiveRange)
                {
                    // Expand bounds based on current input
                    if (currentMidi < _dynamicMinMidi)
                        _dynamicMinMidi = Mathf.Lerp(_dynamicMinMidi, currentMidi, (float)delta * AdaptationSpeed);
                    if (currentMidi > _dynamicMaxMidi)
                        _dynamicMaxMidi = Mathf.Lerp(_dynamicMaxMidi, currentMidi, (float)delta * AdaptationSpeed);

                    // Map to 0-1 based on dynamic range
                    float range = Mathf.Max(_dynamicMaxMidi - _dynamicMinMidi, 5.0f); // Minimum 5 semitone range
                    float t = (currentMidi - _dynamicMinMidi) / range;
                    t = Mathf.Clamp(t, 0, 1);
                    _targetY = Mathf.Lerp(MinY, MaxY, t);

                    // Slowly reset towards default range to prevent getting stuck in extreme bounds
                    _dynamicMinMidi = Mathf.Lerp(_dynamicMinMidi, MinMidi, (float)delta * ResetSpeed);
                    _dynamicMaxMidi = Mathf.Lerp(_dynamicMaxMidi, MaxMidi, (float)delta * ResetSpeed);
                }
                else
                {
                    float t = (Detector.CurrentMidiNote - MinMidi) / (float)(MaxMidi - MinMidi);
                    t = Mathf.Clamp(t, 0, 1);
                    _targetY = Mathf.Lerp(MinY, MaxY, t);
                }
            }
            else
            {
                // Smoothly return to the lowest point when silent
                _targetY = Mathf.Lerp(_targetY, MinY, (float)delta * SilenceReturnSpeed);
            }

            Vector3 pos = GlobalPosition;
            pos.Y = Mathf.Lerp(pos.Y, _targetY, (float)delta * SmoothSpeed);
            GlobalPosition = pos;
        }
    }
}
