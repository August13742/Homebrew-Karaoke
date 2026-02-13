using Godot;
using System;
using System.Text.Json;
using AudioSystem;

namespace PitchGame
{
    public partial class SongInspector : VBoxContainer
    {
        [Signal] public delegate void StartRequestedEventHandler(SongData data, float keyShift);

        private Label _lblSongName;
        private RichTextLabel _lblStats;
        private Button _btnStart;
        private Label _lblKeyShift;
        private CheckButton _chkVoiceOver;
        
        private SongData _currentData;
        private float _keyShift = 0f;

        public override void _Ready()
        {
            _lblSongName = GetNode<Label>("%LabelSongName");
            _lblStats = GetNode<RichTextLabel>("%LabelStats");
            _btnStart = GetNode<Button>("%ButtonLoad"); // Reuse reference button
            
            // Key Shift UI
            _lblKeyShift = GetNodeOrNull<Label>("%LabelKeyShift");
            
            var btnUp = GetNodeOrNull<Button>("%BtnKeyUp");
            var btnDown = GetNodeOrNull<Button>("%BtnKeyDown");
            
            if (btnUp != null) btnUp.Pressed += () => UpdateKeyShift(0.5f);
            if (btnDown != null) btnDown.Pressed += () => UpdateKeyShift(-0.5f);

            _btnStart.Pressed += OnStartPressed;
            _btnStart.Disabled = true;

            // Voice Over Toggle
            _chkVoiceOver = GetNodeOrNull<CheckButton>("%CheckVoiceOver");
            if (_chkVoiceOver != null)
            {
                _chkVoiceOver.Toggled += (on) => SessionData.VoiceOverMode = on;
                _chkVoiceOver.ButtonPressed = SessionData.VoiceOverMode;
            }
            
            Visible = false;
        }

        public void Inspect(SongData data)
        {
            _currentData = data;
            Visible = true;
            _btnStart.Disabled = false;
            _keyShift = 0f;
            UpdateKeyShiftLabels();

            _lblSongName.Text = data.Name;
            
            // Load JSON metadata
            string statsText = "[i]Loading metadata...[/i]";
            try
            {
                using var file = FileAccess.Open(data.KaraokeJsonPath, FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    var lyricData = JsonSerializer.Deserialize<LyricData>(file.GetAsText());
                    if (lyricData != null)
                    {
                        double duration = 0;
                        if (lyricData.Words != null && lyricData.Words.Count > 0)
                        {
                            duration = lyricData.Words[lyricData.Words.Count - 1].End;
                        }
                        
                        if (lyricData.Pitch != null && lyricData.Pitch.Count > 0)
                        {
                            duration = Math.Max(duration, lyricData.Pitch[lyricData.Pitch.Count - 1].Time);
                        }
                        
                        var ts = TimeSpan.FromSeconds(duration);
                        statsText = $"[b]Duration:[/b] {ts:mm\\:ss}\n" +
                                    $"[b]Lines:[/b] {lyricData.Lines?.Count ?? 0}\n" +
                                    $"[b]Words:[/b] {lyricData.Words?.Count ?? 0}\n" +
                                    $"[b]Pitch Intervals:[/b] {lyricData.Pitch?.Count ?? 0}\n\n" +
                                    $"[b]Lyrics:[/b]\n";

                        if (lyricData.Lines != null)
                        {
                            foreach (var line in lyricData.Lines)
                            {
                                if (!string.IsNullOrEmpty(line.Text))
                                {
                                    statsText += $"{line.Text.Trim()}\n";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                statsText = $"[color=red]Error reading metadata:[/color]\n{e.Message}";
            }
            
            _lblStats.Text = statsText;
        }

        private void UpdateKeyShift(float delta)
        {
            _keyShift = Mathf.Clamp(_keyShift + delta, -10f, 10f);
            UpdateKeyShiftLabels();
            
            if (AudioManager.Instance != null && AudioManager.Instance.IsMusicPlaying())
            {
                AudioManager.Instance.SetMusicPitchShift(_keyShift);
            }
        }

        private void UpdateKeyShiftLabels()
        {
            if (_lblKeyShift != null)
            {
                _lblKeyShift.Text = $"Key: {_keyShift:+#.0;-#.0;0}";
            }
        }

        private void OnStartPressed()
        {
            if (_currentData == null) return;
            EmitSignal(SignalName.StartRequested, _currentData, _keyShift);
        }
    }
}