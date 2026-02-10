using Godot;
using System;
using System.Linq;

namespace PitchGame
{
    public partial class KaraokeScorer : Node
    {
        [Signal] public delegate void ScoreUpdatedEventHandler(float newScore, string accuracyText);

        [Export] public ScrollingLyrics LyricsSource;
        [Export] public PitchDetector Detector;

        [ExportGroup("Scoring")]
        [Export] public float ScoreMultiplier = 10.0f; 
        
        public float CurrentScore { get; private set; } = 0f;

        public override void _Process(double delta)
        {
            if (LyricsSource == null || LyricsSource.Data == null || Detector == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            
            // Find active note(s) at current time
            // Optimization: Could cache index, but LINQ FirstOrDefault is okay for ~300 items
            var activeWord = LyricsSource.Data.Words.FirstOrDefault(w => w.Start <= time && w.End >= time && w.PitchMidi > 0);

            if (activeWord != null)
            {
                if (Detector.IsDetected)
                {
                    var acc = Detector.EvaluateAccuracy(activeWord.PitchMidi);
                    float points = 0f;
                    string text = "";

                    switch (acc)
                    {
                        case PitchAccuracy.Perfect:
                            points = 10f;
                            text = "PERFECT!";
                            break;
                        case PitchAccuracy.Good:
                            points = 5f;
                            text = "GOOD";
                            break;
                        case PitchAccuracy.Ok:
                            points = 1f;
                            text = "OK";
                            break;
                        default:
                            text = "";
                            break;
                    }

                    if (points > 0)
                    {
                        CurrentScore += points * (float)delta * ScoreMultiplier;
                        EmitSignal(SignalName.ScoreUpdated, CurrentScore, text);
                    }
                }
            }
        }
    }
}
