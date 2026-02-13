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

        private int _cachedWordIndex = 0;
        private double _lastTime = 0;

        public override void _Process(double delta)
        {
            if (LyricsSource == null || LyricsSource.Data == null || Detector == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            
            // Find active note(s) at current time
            // Optimization: Cached index scan for time-sorted words
            var words = LyricsSource.Data.Words;
            LyricWord activeWord = null;

            // Start scanning from the cached index or 0 if time jumped backwards
            if (time < _lastTime) _cachedWordIndex = 0;
            _lastTime = time;

            for (int i = _cachedWordIndex; i < words.Count; i++)
            {
                var w = words[i];
                if (w.End < time) 
                {
                    _cachedWordIndex = i; // Advance cache
                    continue;
                }
                if (w.Start > time) break; // Past current time (words are sorted)
                
                if (w.PitchMidi > 0)
                {
                    activeWord = w;
                }
                break;
            }

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
