using Godot;
using System;

namespace PitchGame
{
    public partial class HUD : Control
    {
        [Export] public PitchDetector Detector;
        
        private Label _pitchLabel;
        private Label _scoreLabel;

        public override void _Ready()
        {
            // Try GameScene path
            _pitchLabel = GetNodeOrNull<Label>("TopInfo/PitchLabel");
            
            // Try TestScene path (fallback)
            if (_pitchLabel == null)
            {
                _pitchLabel = GetNodeOrNull<Label>("VBoxContainer/PitchLabel");
            }
            
            _scoreLabel = GetNodeOrNull<Label>("TopInfo/ScoreLabel");
        }

        public override void _Process(double delta)
        {
            if (_pitchLabel == null || Detector == null) return;

            if (Detector.IsDetected)
            {
                _pitchLabel.Text = $"Pitch: {Detector.CurrentFrequency:#} Hz | MIDI: {Detector.CurrentMidiNote:F0}\nDev: {Detector.CentDeviation:+0.00;-0.00} cents | Amp: {Detector.CurrentAmplitude:F2}";
            }
            else
            {
                _pitchLabel.Text = $"Pitch: --- (Silence)\nAmp: {Detector.CurrentAmplitude:F2}";
            }
        }
        
        // Signal Callback from KaraokeScorer
        public void OnScoreUpdated(float score, string accuracy)
        {
            if (_scoreLabel != null)
                _scoreLabel.Text = $"Score: {score:F0}  {accuracy}";
        }
    }
}
