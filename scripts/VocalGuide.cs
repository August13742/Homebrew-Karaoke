using Godot;
using System.Collections.Generic;
using AudioSystem;
using System.Linq;

namespace PitchGame
{
    /// <summary>
    /// Monitors playback time and plays synthesized vocal notes from LyricData.
    /// Useful for verifying pitch and timing.
    /// </summary>
    public partial class VocalGuide : Node
    {
        public LyricData Data { get; set; }
        public float KeyShift { get; set; } = 0f;

        private List<WordClip> _clips = new();
        private int _currentIndex = 0;
        private double _lastTime = -1;

        private struct WordClip
        {
            public double Start;
            public SFXResource Resource;
        }

        public override void _Ready()
        {
            GD.Print("[VocalGuide] Node ready.");
        }

        public void Start()
        {
            if (Data == null || Data.Pitch == null || Data.Pitch.Count == 0)
            {
                GD.PrintErr("[VocalGuide] Cannot start: Pitch data is null or empty.");
                return;
            }

            GD.Print($"[VocalGuide] Analyzing {Data.Pitch.Count} pitch events for coalescence (KeyShift: {KeyShift})");
            
            _clips.Clear();
            
            // The JSON pitch events are typically spaced every ~0.1s.
            // We want to merge consecutive events that have the same MIDI
            // and are close together in time (to handle continuous voicing).
            for (int i = 0; i < Data.Pitch.Count; i++)
            {
                var p = Data.Pitch[i];
                if (p.Midi <= 0) continue;

                double startTime = p.Time;
                int currentMidi = p.Midi;
                int groupEndIndex = i;

                // Look ahead and coalesce same MIDI events with small gaps (< 200ms)
                while (groupEndIndex + 1 < Data.Pitch.Count)
                {
                    var next = Data.Pitch[groupEndIndex + 1];
                    double gap = next.Time - Data.Pitch[groupEndIndex].Time;
                    
                    if (next.Midi == currentMidi && gap < 0.2)
                    {
                        groupEndIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Calculate duration: time span of the group + a small constant for the last event's duration
                double endTime = Data.Pitch[groupEndIndex].Time + 0.1; 
                float duration = (float)(endTime - startTime);

                // Ensure duration is valid for synthesis
                if (duration > 0.05f)
                {
                    int midi = Mathf.RoundToInt(currentMidi + KeyShift);
                    var res = VocalSynthesiser.GenerateVocal(midi, VocalSynthesiser.VowelType.A, duration);
                    _clips.Add(new WordClip { Start = startTime, Resource = res });
                }

                // Advance the outer loop index
                i = groupEndIndex;
            }

            GD.Print($"[VocalGuide] Successfully coalesced into {_clips.Count} vocal clips.");
        }

        public override void _Process(double delta)
        {
            if (AudioManager.Instance == null || _clips.Count == 0) return;

            double currentTime = AudioManager.Instance.GetMusicPlaybackPosition();

            // Handle seeking: if time jumped backwards or significantly forwards, reset index
            if (currentTime < _lastTime || currentTime > _lastTime + 1.0)
            {
                _currentIndex = _clips.FindIndex(c => c.Start >= currentTime);
                if (_currentIndex == -1) _currentIndex = _clips.Count;
            }

            // Play clips that have started
            while (_currentIndex < _clips.Count && currentTime >= _clips[_currentIndex].Start)
            {
                GD.Print($"[VocalGuide] Playing clip at {currentTime:F2} (target {_clips[_currentIndex].Start:F2})");
                AudioManager.Instance.PlaySFX(_clips[_currentIndex].Resource);
                _currentIndex++;
            }

            _lastTime = currentTime;
        }
    }
}
