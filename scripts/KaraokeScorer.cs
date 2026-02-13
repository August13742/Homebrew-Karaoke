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

        private int _cachedPitchIndex = 0;
        private double _lastTime = 0;

        public override void _Process(double delta)
        {
            if (LyricsSource == null || LyricsSource.Data == null || Detector == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            
            // 1. Find active pitch event at current time
            var pitchEvents = LyricsSource.Data.Pitch;
            if (pitchEvents == null || pitchEvents.Count == 0) return;

            // Start scanning from the cached index or 0 if time jumped backwards
            if (time < _lastTime) _cachedPitchIndex = 0;
            _lastTime = time;

            PitchEvent activeEvent = null;
            // The python script generates events at ~0.1s intervals.
            // We find the event such that p.Time <= time < p.Time + interval (approx 0.1s).
            // For simplicity and robustness, we find the LAST event that is <= current time.
            for (int i = _cachedPitchIndex; i < pitchEvents.Count; i++)
            {
                var p = pitchEvents[i];
                if (p.Time <= time)
                {
                    activeEvent = p;
                    _cachedPitchIndex = i;
                }
                else
                {
                    // p.Time > time, so we found our boundary (assuming sorted)
                    break;
                }
            }

            // Optional: check if the event is "too old" (e.g. if there's a huge gap in pitch data)
            if (activeEvent != null && (time - activeEvent.Time) > 0.2)
            {
                activeEvent = null; // Too far from the last recorded pitch point
            }

            if (activeEvent != null && activeEvent.Midi > 0)
            {
                if (Detector.IsDetected)
                {
                    var acc = Detector.EvaluateAccuracy(activeEvent.Midi);
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
