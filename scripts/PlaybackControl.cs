using Godot;
using System;
using AudioSystem;

namespace PitchGame
{
	/// <summary>
	/// Self-contained playback transport: Play/Pause, ±1s/±5s seek, time label, progress slider.
	/// Operates directly against AudioManager singleton — no external wiring needed.
	/// </summary>
	public partial class PlaybackControl : VBoxContainer
	{
		[ExportGroup("Icons")]
		[Export] public Texture2D IconPlay { get; set; }
		[Export] public Texture2D IconPause { get; set; }

		private Button _btnPlay;
		private Button _btnRewind, _btnRewind1, _btnForward1, _btnForward;
		private HSlider _sliderProgress;
		private Label _lblTime;
		private bool _isDragging = false;

		public override void _Ready()
		{
			_btnPlay     = GetNodeOrNull<Button>("%BtnPlay");
			_btnRewind   = GetNodeOrNull<Button>("%BtnRewind");
			_btnRewind1  = GetNodeOrNull<Button>("%BtnRewind1");
			_btnForward1 = GetNodeOrNull<Button>("%BtnForward1");
			_btnForward  = GetNodeOrNull<Button>("%BtnForward");
			_sliderProgress = GetNodeOrNull<HSlider>("%SliderProgress");
			_lblTime     = GetNodeOrNull<Label>("%LblTime");

			if (_btnPlay != null)
			{
				_btnPlay.FocusMode = FocusModeEnum.None;
				_btnPlay.Pressed += () => AudioManager.Instance?.ToggleMusicPause();
			}

			WireSeekButton(_btnRewind,   -5.0);
			WireSeekButton(_btnRewind1,  -1.0);
			WireSeekButton(_btnForward1,  1.0);
			WireSeekButton(_btnForward,   5.0);

			if (_sliderProgress != null)
			{
				_sliderProgress.FocusMode = FocusModeEnum.None;
				_sliderProgress.DragStarted += () => _isDragging = true;
				_sliderProgress.DragEnded += (_) =>
				{
					_isDragging = false;
					AudioManager.Instance?.SeekMusic(_sliderProgress.Value);
				};
			}
		}

		public override void _Process(double delta)
		{
			if (AudioManager.Instance == null) return;

			bool isPlaying = AudioManager.Instance.IsMusicPlaying();
			bool isPaused  = AudioManager.Instance.IsMusicPaused();

			UpdatePlayIcon(isPlaying && !isPaused);
			UpdateTime();
			UpdateProgress();
		}

		/// <summary>Seek relative to current position (called by parent for keyboard shortcuts).</summary>
		public void SeekRelative(double seconds)
		{
			if (AudioManager.Instance == null) return;
			double target = AudioManager.Instance.GetMusicPlaybackPosition() + seconds;
			AudioManager.Instance.SeekMusic(Math.Max(0, target));
		}

		private void WireSeekButton(Button btn, double seconds)
		{
			if (btn == null) return;
			btn.FocusMode = FocusModeEnum.None;
			btn.Pressed += () => SeekRelative(seconds);
		}

		private void UpdatePlayIcon(bool isActive)
		{
			if (_btnPlay == null) return;
			if (isActive)
			{
				if (IconPause != null) _btnPlay.Icon = IconPause;
				else _btnPlay.Text = "||";
			}
			else
			{
				if (IconPlay != null) _btnPlay.Icon = IconPlay;
				else _btnPlay.Text = ">";
			}
		}

		private void UpdateTime()
		{
			if (_lblTime == null) return;
			double t = AudioManager.Instance.GetMusicPlaybackPosition();
			var ts = TimeSpan.FromSeconds(t);
			_lblTime.Text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
		}

		private void UpdateProgress()
		{
			if (_sliderProgress == null || _isDragging) return;

			float t = (float)AudioManager.Instance.GetMusicPlaybackPosition();
			_sliderProgress.Value = t;

			float len = AudioManager.Instance.GetMusicLength();
			if (len > 0.1f && Math.Abs(_sliderProgress.MaxValue - len) > 0.1f)
			{
				_sliderProgress.MaxValue = len;
			}
		}
	}
}
