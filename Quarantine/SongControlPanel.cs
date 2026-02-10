using Godot;
using System;

public partial class SongControlPanel : PanelContainer
{
    [Export] private float initialMusicVol = 0.5f;
    [Export] private float initialVocalVol = 1.0f;
    
    [ExportGroup("Icons")]
    [Export] public Texture2D IconPlay { get; set; }
    [Export] public Texture2D IconPause { get; set; }
    [Export] public Texture2D IconRewind { get; set; }
    [Export] public Texture2D IconForward { get; set; }
    [Export] public Texture2D IconBack { get; set; }
    
    [ExportGroup("Styling")]
    [Export] public Texture2D SliderGrabber { get; set; }
    
    // --- KEY SHIFT ---
    /// <summary>
    /// Key shift in semitones. Applied to both audio pitch and target note visuals.
    /// </summary>
    public float KeyShiftSemitones { get; private set; } = 0f;
    
    // Controls
    private Button _btnPlay;
    private Button _btnRewind, _btnRewind1; 
    private Button _btnForward, _btnForward1; 
    private Button _btnBack;
    private Button _btnKeyDown, _btnKeyUp;
    private Label _lblKeyShift;
    private HSlider _sliderProgress;
    private Label _lblTime;
    private Label _lblSongTitle;
    private HSlider _sliderMusicVol, _sliderVocalVol;

    private bool _isDraggingSlider = false;

    public override void _Ready()
    {
        _btnBack = GetNode<Button>("%BtnBack");
        _btnRewind = GetNodeOrNull<Button>("%BtnRewind");
        _btnRewind1 = GetNodeOrNull<Button>("%BtnRewind1");
        _btnPlay = GetNode<Button>("%BtnPlay");
        _btnForward = GetNodeOrNull<Button>("%BtnForward");
        _btnForward1 = GetNodeOrNull<Button>("%BtnForward1");
        _lblTime = GetNodeOrNull<Label>("%LblTime");
        _lblSongTitle = GetNodeOrNull<Label>("%LblSongTitle");
        _sliderProgress = GetNode<HSlider>("%SliderProgress");
        
        _sliderMusicVol = GetNodeOrNull<HSlider>("%SliderMusicVol");
        _sliderVocalVol = GetNodeOrNull<HSlider>("%SliderVocalVol");
        
        _btnKeyDown = GetNodeOrNull<Button>("%BtnKeyDown");
        _btnKeyUp = GetNodeOrNull<Button>("%BtnKeyUp");
        _lblKeyShift = GetNodeOrNull<Label>("%LblKeyShift");
        
        SetupLogic();
        ApplyStyling();

        if (AudioManager.Instance != null)
        {
            float len = AudioManager.Instance.GetMusicLength();
            if (len > 0.1f) _sliderProgress.MaxValue = len;
            
            if (_sliderMusicVol != null) _sliderMusicVol.Value = initialMusicVol;
            if (_sliderVocalVol != null) _sliderVocalVol.Value = initialVocalVol;
        }

        // Hide leftover editor-specific nodes
        GetNodeOrNull<Button>("%BtnRevertAll")?.Hide();
        GetNodeOrNull<Label>("%LblMode")?.Hide();
        GetNodeOrNull<Control>("%ConfirmRevert")?.Hide();
        
        UpdateKeyShiftLabel();
    }

    public override void _Process(double delta)
    {
        if (AudioManager.Instance == null) return;
        
        bool isPlaying = AudioManager.Instance.IsMusicPlaying();
        bool isPaused = AudioManager.Instance.IsMusicPaused();

        if (isPlaying && !isPaused)
        {
            if (IconPause != null) _btnPlay.Icon = IconPause;
            else _btnPlay.Text = "||";
        }
        else
        {
            if (IconPlay != null) _btnPlay.Icon = IconPlay;
            else _btnPlay.Text = ">";
        }
        
        float currentTime = (float)AudioManager.Instance.GetMusicPlaybackPosition();
        if (_lblTime != null)
        {
            var ts = TimeSpan.FromSeconds(currentTime);
            _lblTime.Text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        
        if (!_isDraggingSlider)
        {
            _sliderProgress.Value = currentTime;
            float len = AudioManager.Instance.GetMusicLength();
            if (len > 0.1f && Math.Abs(_sliderProgress.MaxValue - len) > 0.1f)
            {
                _sliderProgress.MaxValue = len;
            }
        }

        if (_lblSongTitle != null && (string.IsNullOrEmpty(_lblSongTitle.Text) || _lblSongTitle.Text == "Song Title"))
        {
            string musicName = AudioManager.Instance.CurrentMusicName;
            if (!string.IsNullOrEmpty(musicName) && musicName != "None")
            {
                _lblSongTitle.Text = musicName;
            }
        }
    }

    private void SetupLogic()
    {
        _btnPlay.Pressed += TogglePlay;
        
        if (_btnRewind != null) _btnRewind.Pressed += () => SeekRel(-5);
        if (_btnRewind1 != null) _btnRewind1.Pressed += () => SeekRel(-1);
        if (_btnForward1 != null) _btnForward1.Pressed += () => SeekRel(1);
        if (_btnForward != null) _btnForward.Pressed += () => SeekRel(5);
        
        _sliderProgress.DragStarted += () => _isDraggingSlider = true;
        _sliderProgress.DragEnded += (bool val) => 
        {
            _isDraggingSlider = false;
            AudioManager.Instance.SeekMusic(_sliderProgress.Value);
        };
        
        // Volume
        if (_sliderMusicVol != null) _sliderMusicVol.ValueChanged += (v) => AudioManager.Instance.SetMusicVolume((float)v);
        if (_sliderVocalVol != null) _sliderVocalVol.ValueChanged += (v) => AudioManager.Instance.SetVocalVolume((float)v);
        
        // Key Shift — 0.5 semitone steps, like real karaoke machines
        if (_btnKeyDown != null) _btnKeyDown.Pressed += () => ShiftKey(-0.5f);
        if (_btnKeyUp != null) _btnKeyUp.Pressed += () => ShiftKey(+0.5f);
        
        // Back Button
        if (_btnBack != null) _btnBack.Pressed += () => GD.Print("Back pressed - Jukebox not implemented");

        // Focus Management
        foreach (var node in new Control[] { _btnPlay, _btnBack, _btnRewind, _btnRewind1, _btnForward, _btnForward1, _sliderProgress, _sliderMusicVol, _sliderVocalVol, _btnKeyDown, _btnKeyUp })
        {
            if (node != null) node.FocusMode = FocusModeEnum.None;
        }
    }
    
    private void ShiftKey(float semitones)
    {
        KeyShiftSemitones += semitones;
        KeyShiftSemitones = Math.Clamp(KeyShiftSemitones, -10f, 10f);
        
        // Apply to audio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicPitchShift(KeyShiftSemitones);
        }
        
        UpdateKeyShiftLabel();
    }
    
    private void UpdateKeyShiftLabel()
    {
        if (_lblKeyShift != null)
        {
            if (KeyShiftSemitones == 0f)
                _lblKeyShift.Text = "0";
            else
                _lblKeyShift.Text = KeyShiftSemitones.ToString("+0.#;-0.#");
        }
    }
    
    private void TogglePlay() => AudioManager.Instance.ToggleMusicPause();
    
    private void SeekRel(float delta)
    {
        double target = AudioManager.Instance.GetMusicPlaybackPosition() + delta;
        if (target < 0) target = 0;
        AudioManager.Instance.SeekMusic(target);
    }

    private void ApplyStyling()
    {
        if (IconBack != null && _btnBack != null) { _btnBack.Icon = IconBack; _btnBack.Text = ""; }
        if (IconRewind != null && _btnRewind != null) { _btnRewind.Icon = IconRewind; _btnRewind.Text = ""; }
        if (IconForward != null && _btnForward != null) { _btnForward.Icon = IconForward; _btnForward.Text = ""; }
        
        if (SliderGrabber != null)
        {
            _sliderProgress.AddThemeIconOverride("grabber", SliderGrabber);
            _sliderProgress.AddThemeIconOverride("grabber_highlight", SliderGrabber);
            _sliderProgress.AddThemeIconOverride("grabber_disabled", SliderGrabber);
        }
    } 
}
