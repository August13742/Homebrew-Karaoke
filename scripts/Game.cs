using Godot;
using System;
using AudioSystem;

namespace PitchGame
{
    public partial class Game : Node
    {
        [Signal] public delegate void SongStartedEventHandler();
        [Signal] public delegate void SongFinishedEventHandler();

        [Export] public ScrollingLyrics LyricsSource;
        [Export] public PitchDetector Detector;
        [Export] public AudioStreamPlayer MicInput;
        
        // Removed ScoreUpdated signal as it is handled by KaraokeScorer directly

        [Export] public SongControlPanel Controls;
        [Export] public KaraokeScorer Scorer;
        
        [Export] public RecordingManager Recorder;
        
        private bool _songStarted = false;
        private PackedScene _endPanelScene;

        public override void _Ready()
        {
            // Auto-wire if not assigned (fallback)
            if (LyricsSource == null) LyricsSource = GetNode<ScrollingLyrics>("%ScrollingLyrics");
            if (Detector == null) Detector = GetNode<PitchDetector>("%PitchDetector");
            if (MicInput == null) MicInput = GetNode<AudioStreamPlayer>("%MicInput");
            if (Scorer == null) Scorer = GetNode<KaraokeScorer>("%KaraokeScorer");
            if (Recorder == null) Recorder = GetNodeOrNull<RecordingManager>("%RecordingManager");

            // Look for Controls if not assigned
            if (Controls == null) 
                Controls = GetNodeOrNull<SongControlPanel>("%SongControlPanel") 
                           ?? GetNodeOrNull<SongControlPanel>("CanvasLayer/SongControlPanel");

            if (Controls != null)
            {
                Controls.Seeked += (t) => Recorder?.NotifySeek((float)t);
                Controls.SongEndRequested += EndSong;
            }

            _endPanelScene = GD.Load<PackedScene>("res://scenes/EndOfSongPanel.tscn");

            SetupAudio();
            LoadSessionSong();
        }
        
        public override void _Process(double delta)
        {
            // Check for natural song completion
            if (_songStarted && AudioManager.Instance != null)
            {
                // If music stopped playing and NOT paused, we are done.
                if (!AudioManager.Instance.IsMusicPlaying() && !AudioManager.Instance.IsMusicPaused())
                {
                    EndSong();
                }
            }
        }

        private void SetupAudio()
        {
            if (MicInput != null && !MicInput.Playing)
            {
                MicInput.Playing = true;
                GD.Print($"[Game] MicInput bus: {MicInput.Bus}");
            }
        }

        private void LoadSessionSong()
        {
            var song = SessionData.CurrentSong;
            if (song == null)
            {
                GD.Print("[Game] No session song found (SessionData.CurrentSong is null). Walking mode?");
                return;
            }

            GD.Print($"[Game] Loading Session Song: {song.Name}");

            // Configure Lyrics
            if (LyricsSource != null)
            {
                LyricsSource.LoadLyrics(song.KaraokeJsonPath);
                SessionData.CurrentLyrics = LyricsSource.Data;
            }
            
            var musicRes = new MusicResource
            {
                Clip = GD.Load<AudioStream>(song.InstrumentalPath),
                VocalClip = GD.Load<AudioStream>(song.VocalsPath),
                Volume = 1.0f,
                FadeTime = 1.0f
            };

            // Start Background playback
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(musicRes);
                AudioManager.Instance.SeekMusic(0); // Ensure we start from 0
                
                // key shift is applied in SongControlPanel._Ready(), 
                // but we can apply it here too to be safe/instant check
                AudioManager.Instance.SetMusicPitchShift(SessionData.KeyShift);
            }
            
            // Start Recording
            Recorder?.StartRecording();
            _songStarted = true;

            EmitSignal(SignalName.SongStarted);
        }

        private void EndSong()
        {
            if (!_songStarted) return;
            _songStarted = false; // Prevent double trigger

            GD.Print("[Game] Song Finished. Stopping recording...");
            Recorder?.StopRecording();
            SessionData.LastRecording = Recorder?.GetFinalRecording();

            AudioManager.Instance?.StopMusic();
            
            // Instantiate End Screen
            if (_endPanelScene != null)
            {
                var endPanel = _endPanelScene.Instantiate<EndOfSongPanel>();
                GetNode("CanvasLayer").AddChild(endPanel);
                
                if (Scorer != null)
                {
                    endPanel.Setup(Scorer.CurrentScore);
                }
            }

            EmitSignal(SignalName.SongFinished);
        }
    }
}
