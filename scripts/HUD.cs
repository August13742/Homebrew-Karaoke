using Godot;
using System;

namespace PitchGame
{
    public partial class HUD : Control
    {
        [Export] public PitchDetector Detector;
        private Label _pitchLabel;

        public override void _Ready()
        {
            _pitchLabel = GetNodeOrNull<Label>("VBoxContainer/PitchLabel");
        }

        public override void _Process(double delta)
        {
            if (_pitchLabel == null || Detector == null) return;

            if (Detector.IsDetected)
            {
                _pitchLabel.Text = $"Pitch: {Detector.CurrentFrequency:#} Hz | MIDI: {Detector.CurrentMidiNote}\nDev: {Detector.CentDeviation:+0.00;-0.00} cents | Amp: {Detector.CurrentAmplitude:F4}";
            }
            else
            {
                _pitchLabel.Text = $"Pitch: --- (Silence)\nAmp: {Detector.CurrentAmplitude:F4}";
            }
        }
    }
}
