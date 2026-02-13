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

        public override void _Ready()
        {
            // Auto-wire if not assigned (fallback)
            if (LyricsSource == null) LyricsSource = GetNode<ScrollingLyrics>("%ScrollingLyrics");
            if (Detector == null) Detector = GetNode<PitchDetector>("%PitchDetector");
            if (MicInput == null) MicInput = GetNode<AudioStreamPlayer>("%MicInput");

            SetupAudio();
            LoadSessionSong();
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


            EmitSignal(SignalName.SongStarted);
        }
    }
}
