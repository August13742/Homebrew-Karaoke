using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AudioSystem;

namespace PitchGame
{
    public partial class EndOfSongPanel : PanelContainer
    {
        [ExportGroup("UI References")]
        [Export] public Label ScoreLabel;
        [Export] public Button PlayButton;
        [Export] public Button SaveButton;
        [Export] public Button JukeboxButton;
        [Export] public Control LyricContainer;
        [Export] public ProgressBar SaveProgressBar;
        [Export] public HSlider PlaybackSlider;
        [Export] public Label TimeLabel;
        [Export] public Label DurationLabel;
        
        [Export] public VolumeControl MusicVolume;
        [Export] public VolumeControl VocalVolume;
        [Export] public VolumeControl AppVolume;

        private AudioStreamPlayer _recordingPlayer;
        private bool _isPlaying = false;
        private PackedScene _lyricLineScene;
        private List<ReviewLyricLine> _lyricLines = new List<ReviewLyricLine>();
        private ScrollContainer _lyricScroll;

        public override void _Ready()
        {
            _lyricLineScene = GD.Load<PackedScene>("res://scenes/components/ReviewLyricLine.tscn");
            _lyricScroll = GetNodeOrNull<ScrollContainer>("%LyricScroll");

            // Internal Player for the recording
            _recordingPlayer = new AudioStreamPlayer();
            _recordingPlayer.Name = "RecordingPlayback";
            _recordingPlayer.Bus = "SFX";
            AddChild(_recordingPlayer);

            // Wire UI
            if (PlayButton != null) PlayButton.Pressed += OnPlayPressed;
            if (SaveButton != null) SaveButton.Pressed += OnSavePressed;
            if (JukeboxButton != null) JukeboxButton.Pressed += OnJukeboxPressed;

            if (PlaybackSlider != null)
            {
                PlaybackSlider.DragEnded += (val) => Seek(PlaybackSlider.Value);
            }

            if (MusicVolume != null) MusicVolume.VolumeChanged += (v) => AudioManager.Instance?.SetMusicVolume(v);
            if (VocalVolume != null) VocalVolume.VolumeChanged += (v) => AudioManager.Instance?.SetVocalVolume(v);
            if (AppVolume != null) 
            {
                AppVolume.VolumeChanged += (v) => 
                {
                    if (IsInstanceValid(_recordingPlayer)) _recordingPlayer.VolumeDb = Mathf.LinearToDb(v);
                };
                if (IsInstanceValid(_recordingPlayer)) _recordingPlayer.VolumeDb = Mathf.LinearToDb(AppVolume.Volume);
            }

            // Init State
            if (SessionData.LastRecording != null)
            {
                _recordingPlayer.Stream = SessionData.LastRecording;
                if (SaveButton != null) SaveButton.Disabled = false;
                if (PlayButton != null) PlayButton.Disabled = false;
            }

            PopulateLyrics();
        }

        private void PopulateLyrics()
        {
            if (LyricContainer == null || SessionData.CurrentSong == null) return;

            // Clear existing
            foreach (var child in LyricContainer.GetChildren()) child.QueueFree();
            _lyricLines.Clear();

            // Load lyric data (should be cached in SessionData if it was just played)
            var lyrics = SessionData.CurrentLyrics;
            if (lyrics == null) return;

            foreach (var line in lyrics.Lines)
            {
                var lineNode = _lyricLineScene.Instantiate<ReviewLyricLine>();
                lineNode.Text = line.Text;
                lineNode.Timestamp = line.Start;
                lineNode.LineClicked += OnLyricLineClicked;
                
                LyricContainer.AddChild(lineNode);
                _lyricLines.Add(lineNode);
            }
        }

        private void OnLyricLineClicked(double timestamp)
        {
            GD.Print($"[EndOfSongPanel] Seeking to {timestamp}");
            Seek(timestamp);
            if (!_isPlaying) StartPlayback();
        }

        private void Seek(double timestamp)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SeekMusic(timestamp);
            if (IsInstanceValid(_recordingPlayer)) _recordingPlayer.Seek((float)timestamp);
        }

        public override void _Process(double delta)
        {
            double currentTime = 0;
            double duration = 0;

            if (AudioManager.Instance != null)
            {
                currentTime = AudioManager.Instance.GetMusicPlaybackPosition();
                duration = AudioManager.Instance.GetMusicLength();
            }
            else if (IsInstanceValid(_recordingPlayer))
            {
                currentTime = _recordingPlayer.GetPlaybackPosition();
                if (_recordingPlayer.Stream != null) duration = _recordingPlayer.Stream.GetLength();
            }

            if (PlaybackSlider != null)
            {
                PlaybackSlider.MaxValue = duration;
                if (!PlaybackSlider.Editable) PlaybackSlider.Value = currentTime; // Or check if dragging
                // Standard Godot HSlider doesn't have a reliable 'isDragging' property easily, but DragStarted/Ended works.
                // Let's just update if it's not being manipulated.
            }

            if (TimeLabel != null) TimeLabel.Text = FormatTime(currentTime);
            if (DurationLabel != null) DurationLabel.Text = FormatTime(duration);

            if (!_isPlaying) return;

            UpdateLyricHighlight(currentTime);
        }

        private string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private void UpdateLyricHighlight(double currentTime)
        {
            ReviewLyricLine activeLine = null;

            for (int i = 0; i < _lyricLines.Count; i++)
            {
                bool isPast = currentTime >= _lyricLines[i].Timestamp;
                bool isBeforeNext = (i + 1 == _lyricLines.Count) || currentTime < _lyricLines[i + 1].Timestamp;

                if (isPast && isBeforeNext)
                {
                    _lyricLines[i].SetActive(true);
                    activeLine = _lyricLines[i];
                }
                else
                {
                    _lyricLines[i].SetActive(false);
                }
            }

            if (activeLine != null && _lyricScroll != null)
            {
                // Calculate target Y such that the active line is centered
                float targetY = activeLine.Position.Y - (_lyricScroll.Size.Y * 0.5f) + (activeLine.Size.Y * 0.5f);
                
                // Use a tween for smooth scrolling
                var tween = CreateTween();
                tween.TweenProperty(_lyricScroll, "scroll_vertical", (int)targetY, 0.3f)
                     .SetTrans(Tween.TransitionType.Quad)
                     .SetEase(Tween.EaseType.Out);
            }
        }

        public void Setup(float score)
        {
            if (ScoreLabel != null)
            {
                ScoreLabel.Text = $"Final Score: {Mathf.RoundToInt(score)}";
            }
        }

        private void OnPlayPressed()
        {
            if (_isPlaying) StopPlayback();
            else StartPlayback();
        }

        private void StartPlayback()
        {
            if (AudioManager.Instance == null) return;
            
            AudioManager.Instance.StopMusic(0f);
            if (SessionData.CurrentSong != null)
            {
                var musicRes = new MusicResource
                {
                    Clip = GD.Load<AudioStream>(SessionData.CurrentSong.InstrumentalPath),
                    VocalClip = GD.Load<AudioStream>(SessionData.CurrentSong.VocalsPath),
                    Volume = 1.0f,
                    FadeTime = 0.5f
                };
                AudioManager.Instance.PlayMusic(musicRes);
                AudioManager.Instance.SeekMusic(0);
            }
            
            if (_recordingPlayer.Stream != null) _recordingPlayer.Play();

            _isPlaying = true;
            PlayButton.Text = "Stop";
        }

        private void StopPlayback()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.StopMusic(0.5f);
            if (IsInstanceValid(_recordingPlayer) && _recordingPlayer.Playing) _recordingPlayer.Stop();
            
            _isPlaying = false;
            PlayButton.Text = "Play Recording";
        }

        private void UpdateSaveProgress(float percent)
        {
            if (SaveProgressBar != null) SaveProgressBar.Value = percent;
        }

        private const string SAVE_LOG_VERSION = "2.0.1-ROBUST";

        private async void OnSavePressed()
        {
            GD.Print($"[EndOfSongPanel] Save triggered. Version: {SAVE_LOG_VERSION}");
            if (SessionData.LastRecording == null) 
            {
                GD.PrintErr("[EndOfSongPanel] No recording found in SessionData!");
                return;
            }

            SaveButton.Disabled = true;
            SaveButton.Text = "Saving...";
            if (SaveProgressBar != null)
            {
                SaveProgressBar.Visible = true;
                SaveProgressBar.Value = 0;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string songName = SessionData.CurrentSong?.Name ?? "Unknown";
            string filename = $"{songName}_{timestamp}.wav";
            string recordingDir = System.IO.Path.Combine(OS.GetUserDataDir(), "Recordings");
            
            try 
            {
                if (!System.IO.Directory.Exists(recordingDir)) System.IO.Directory.CreateDirectory(recordingDir);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[EndOfSongPanel] Failed to create directory: {ex.Message}");
                SaveButton.Text = "Dir Error";
                SaveButton.Disabled = false;
                return;
            }

            string fullPath = System.IO.Path.Combine(recordingDir, filename);
            GD.Print($"[EndOfSongPanel] Target path: {fullPath}");

            try
            {
                float musicVol = MusicVolume?.Volume ?? 1.0f;
                float vocalVol = VocalVolume?.Volume ?? 1.0f;
                float recVol = AppVolume?.Volume ?? 1.0f;

                var recording = SessionData.LastRecording;
                AudioStreamWav music = null;
                AudioStreamWav vocals = null;

                if (SessionData.CurrentSong != null)
                {
                    GD.Print($"[EndOfSongPanel] Checking Instrumental: {SessionData.CurrentSong.InstrumentalPath}");
                    if (!string.IsNullOrEmpty(SessionData.CurrentSong.InstrumentalPath) && ResourceLoader.Exists(SessionData.CurrentSong.InstrumentalPath))
                    {
                        var res = GD.Load<AudioStream>(SessionData.CurrentSong.InstrumentalPath) as AudioStreamWav;
                        if (res != null && res.Format == AudioStreamWav.FormatEnum.Format16Bits) music = res;
                        else if (res != null) GD.PrintErr($"[EndOfSongPanel] Instrumental is NOT 16-bit PCM (Format: {res.Format}). Skipping mixdown.");
                    }

                    GD.Print($"[EndOfSongPanel] Checking Vocals: {SessionData.CurrentSong.VocalsPath}");
                    if (!string.IsNullOrEmpty(SessionData.CurrentSong.VocalsPath) && ResourceLoader.Exists(SessionData.CurrentSong.VocalsPath))
                    {
                        var res = GD.Load<AudioStream>(SessionData.CurrentSong.VocalsPath) as AudioStreamWav;
                        if (res != null && res.Format == AudioStreamWav.FormatEnum.Format16Bits) vocals = res;
                        else if (res != null) GD.PrintErr($"[EndOfSongPanel] Vocals are NOT 16-bit PCM (Format: {res.Format}). Skipping mixdown.");
                    }
                }

                GD.Print("[EndOfSongPanel] Starting background mixdown...");
                bool success = await Task.Run(() => 
                {
                    try {
                        return MixAndSaveRobust(music, vocals, recording, fullPath, musicVol, vocalVol, recVol);
                    }
                    catch (Exception loopEx) {
                        GD.PrintErr($"[EndOfSongPanel] Mixer Crash: {loopEx.Message}\n{loopEx.StackTrace}");
                        return false;
                    }
                });

                if (success)
                {
                    GD.Print("[EndOfSongPanel] Save completed successfully.");
                    SaveButton.Text = "Saved!";
                    OS.ShellOpen(recordingDir);
                }
                else
                {
                    GD.PrintErr("[EndOfSongPanel] Mixer reported failure.");
                    SaveButton.Text = "Failed";
                    SaveButton.Disabled = false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[EndOfSongPanel] Async Task Error: {ex.Message}");
                SaveButton.Text = "Error";
                SaveButton.Disabled = false;
            }
            finally
            {
                if (SaveProgressBar != null) SaveProgressBar.Visible = false;
            }
        }

        private bool MixAndSaveRobust(AudioStreamWav music, AudioStreamWav vocals, AudioStreamWav rec, string path, float volM, float volV, float volR)
        {
            try {
                GD.Print($"[Mixer] Version: {SAVE_LOG_VERSION} | Endian: {BitConverter.IsLittleEndian}");
                int mixRate = rec.MixRate;
                byte[] recBytes = rec.Data;
                if (recBytes == null || recBytes.Length == 0) return false;

                byte[] musicBytes = music?.Data;
                byte[] vocalsBytes = vocals?.Data;
                bool mStereo = music?.Stereo ?? false;
                bool vStereo = vocals?.Stereo ?? false;

                int totalSamples = recBytes.Length / 2;
                byte[] outputBytes = new byte[recBytes.Length];
                int clipCount = 0;

                GD.Print($"[Mixer] Samples: {totalSamples} | RecLen: {recBytes.Length} | MusLen: {musicBytes?.Length ?? 0}");

                for (int i = 0; i < totalSamples; i++)
                {
                    float sum = 0f;
                    
                    // 1. Recording (Mono 16-bit)
                    sum += (BitConverter.ToInt16(recBytes, i * 2) / 32768f) * volR;

                    // 2. Music (Instrumental 16-bit)
                    if (musicBytes != null)
                    {
                        int offset = i * 2 * (mStereo ? 2 : 1);
                        if (offset + 1 < musicBytes.Length)
                            sum += (BitConverter.ToInt16(musicBytes, offset) / 32768f) * volM;
                    }

                    // 3. Vocals (Guide 16-bit)
                    if (vocalsBytes != null)
                    {
                        int offset = i * 2 * (vStereo ? 2 : 1);
                        if (offset + 1 < vocalsBytes.Length)
                            sum += (BitConverter.ToInt16(vocalsBytes, offset) / 32768f) * volV;
                    }

                    if (sum > 1.0f || sum < -1.0f) clipCount++;

                    short outSample = (short)(Mathf.Clamp(sum, -1.0f, 1.0f) * 32767f);
                    outputBytes[i * 2] = (byte)(outSample & 0xFF);
                    outputBytes[i * 2 + 1] = (byte)((outSample >> 8) & 0xFF);
                    
                    if (i % 100000 == 0) CallDeferred("UpdateSaveProgress", (float)i / totalSamples * 100f);
                    
                    if (i < 5) GD.Print($"[Mixer] Sample {i}: sum={sum:F4} -> val={outSample}");
                }

                if (clipCount > 0) GD.Print($"[Mixer] Warning: {clipCount} samples clipped.");

                GD.Print("[Mixer] Writing WAV...");
                using (var fs = System.IO.File.Create(path))
                using (var bw = new System.IO.BinaryWriter(fs))
                {
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + outputBytes.Length);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16); bw.Write((short)1); bw.Write((short)1); 
                    bw.Write(mixRate); bw.Write(mixRate * 2); bw.Write((short)2); bw.Write((short)16);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    bw.Write(outputBytes.Length);
                    bw.Write(outputBytes);
                }
                GD.Print("[Mixer] Done.");
                return true;
            } catch (Exception e) { 
                GD.PrintErr($"[Mixer] Error: {e.Message}"); 
                return false; 
            }
        }

        private void OnJukeboxPressed()
        {
            StopPlayback();
            GetTree().ChangeSceneToFile("res://scripts/Jukebox/Jukebox.tscn");
        }

        public override void _ExitTree() => StopPlayback();
    }
}
