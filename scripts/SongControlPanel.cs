using Godot;
using System;
using AudioSystem;

namespace PitchGame
{
    /// <summary>
    /// Thin coordinator for the song control panel.
    /// Owns: Back button, song title, global keyboard shortcuts.
    /// Delegates: playback, volume, key shift to self-contained sub-components.
    /// </summary>
    public partial class SongControlPanel : PanelContainer
    {
        [Signal] public delegate void SeekedEventHandler(double time);
        [Signal] public delegate void SongEndRequestedEventHandler();

        [ExportGroup("Sub-Components")]
        [Export] public PlaybackControl Playback;
        [Export] public KeyShiftControl KeyShift;
        [Export] public VolumeControl MusicVolume;
        [Export] public VolumeControl VocalVolume;
        [Export] public VolumeControl MicVolume;

        [ExportGroup("UI")]
        [Export] public Button BtnBack;
        [Export] public Button BtnEnd;
        [Export] public Label LblSongTitle;

        /// <summary>Current key shift in semitones (read by PitchGrid, Cursor, TargetVisualizer).</summary>
        public float KeyShiftSemitones => KeyShift?.KeyShift ?? 0f;

        private PackedScene _popupScene;
        private RecordingPopup _popupInstance;

        public override void _Ready()
        {
            // --- Wire sub-component signals ---
            if (KeyShift != null)
            {
                KeyShift.KeyShiftChanged += OnKeyShiftChanged;
                KeyShift.SetKeyShift(SessionData.KeyShift);
            }
            
            if (Playback != null)
            {
                Playback.Seeked += (t) => EmitSignal(SignalName.Seeked, t);
            }

            if (MusicVolume != null) MusicVolume.VolumeChanged += (v) => AudioManager.Instance?.SetMusicVolume(v);
            if (VocalVolume != null) VocalVolume.VolumeChanged += (v) => AudioManager.Instance?.SetVocalVolume(v);
            if (MicVolume != null) MicVolume.VolumeChanged += (v) => AudioManager.Instance?.SetMicVolume(v);

            // --- Own UI ---
            if (BtnBack != null)
            {
                BtnBack.FocusMode = FocusModeEnum.None;
                BtnBack.Pressed += OnBackInternal;
            }
            
            if (BtnEnd == null) BtnEnd = GetNodeOrNull<Button>("%BtnEnd");
            if (BtnEnd != null)
            {
                BtnEnd.FocusMode = FocusModeEnum.None;
                BtnEnd.Pressed += OnEndSong;
            }

            if (LblSongTitle == null) LblSongTitle = GetNodeOrNull<Label>("%LblSongTitle");
            
            // Preload Popup
            _popupScene = GD.Load<PackedScene>("res://scenes/components/RecordingPopup.tscn");
        }

        public override void _Process(double delta)
        {
            if (LblSongTitle != null)
            {
                // Prioritize SessionData metadata for the display title
                if (SessionData.CurrentSong != null && !string.IsNullOrEmpty(SessionData.CurrentSong.Name))
                {
                    LblSongTitle.Text = SessionData.CurrentSong.Name;
                }
                else if (AudioManager.Instance != null)
                {
                    string name = AudioManager.Instance.CurrentMusicName;
                    if (!string.IsNullOrEmpty(name) && name != "None")
                        LblSongTitle.Text = name;
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (AudioManager.Instance == null) return;
            
            // If popup is open, don't handle shortcuts
            if (_popupInstance != null && IsInstanceValid(_popupInstance) && _popupInstance.Visible) return;

            if (@event.IsActionPressed("ui_cancel")) // Escape
            {
                ShowRecordingPopup();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_select") || @event.IsActionPressed("ui_accept")) // Space or Enter
            {
                // Prioritize Pause for Space/Enter during gameplay
                AudioManager.Instance.ToggleMusicPause();
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_left"))
            {
                Playback?.SeekRelative(-5);
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_right"))
            {
                Playback?.SeekRelative(5);
                GetViewport().SetInputAsHandled();
            }
        }

        private void ShowRecordingPopup()
        {
            if (_popupInstance == null || !IsInstanceValid(_popupInstance))
            {
                _popupInstance = _popupScene.Instantiate<RecordingPopup>();
                AddChild(_popupInstance);
                
                // Wire signals
                _popupInstance.SkipToEndRequested += OnEndSong;
                _popupInstance.ReturnToJukeboxRequested += OnBackInternal;
                _popupInstance.CancelRequested += () => {
                    _popupInstance.QueueFree();
                    _popupInstance = null;
                };
            }
        }

        private void OnEndSong()
        {
            // Close popup if open
            if (_popupInstance != null && IsInstanceValid(_popupInstance))
            {
                 _popupInstance.QueueFree();
                 _popupInstance = null;
            }
            
            EmitSignal(SignalName.SongEndRequested);
        }

        private void OnKeyShiftChanged(float newShift)
        {
            AudioManager.Instance?.SetMusicPitchShift(newShift);
            SessionData.KeyShift = newShift;
        }

        private void OnBackInternal()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
                AudioManager.Instance.SetMusicPitchShift(0f);
            }
            SessionData.KeyShift = 0f;
            GetTree().ChangeSceneToFile("res://scripts/Jukebox/Jukebox.tscn");
        }
    }
}
