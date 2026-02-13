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
        [ExportGroup("Sub-Components")]
        [Export] public PlaybackControl Playback;
        [Export] public KeyShiftControl KeyShift;
        [Export] public VolumeControl MusicVolume;
        [Export] public VolumeControl VocalVolume;

        [ExportGroup("UI")]
        [Export] public Button BtnBack;
        [Export] public Label LblSongTitle;

        /// <summary>Current key shift in semitones (read by PitchGrid, Cursor, TargetVisualizer).</summary>
        public float KeyShiftSemitones => KeyShift?.KeyShift ?? 0f;

        public override void _Ready()
        {
            // --- Wire sub-component signals ---
            if (KeyShift != null)
            {
                KeyShift.KeyShiftChanged += OnKeyShiftChanged;
                KeyShift.SetKeyShift(SessionData.KeyShift);
            }

            if (MusicVolume != null) MusicVolume.VolumeChanged += (v) => AudioManager.Instance?.SetMusicVolume(v);
            if (VocalVolume != null) VocalVolume.VolumeChanged += (v) => AudioManager.Instance?.SetVocalVolume(v);

            // --- Own UI ---
            if (BtnBack != null)
            {
                BtnBack.FocusMode = FocusModeEnum.None;
                BtnBack.Pressed += OnBackInternal;
            }

            if (LblSongTitle == null) LblSongTitle = GetNodeOrNull<Label>("%LblSongTitle");
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

            if (@event.IsActionPressed("ui_select"))
            {
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
