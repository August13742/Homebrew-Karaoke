using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PitchGame;

public partial class ScrollingLyrics : Control
{
    [ExportCategory("Visuals")]
    [Export] public Font LyricFont; // MANDATORY: Assign .ttf or .tres
    [Export] public int FontSize = 52;
    [Export] public Color ActiveColor = new Color(1, 1, 0);
    [Export] public Color InactiveColor = new Color(1, 1, 1);
    [Export] public float MinEllipseTriggerDuration = 2.0f;
    // UI References
    private VBoxContainer _mainLayout;
    private Control[] _lineRenderers = new Control[2]; // Double buffer for lines
    private Label _waitIndicator;
    private Label _debugLabel;
    private GDScript _rendererScript;
    public string LyricsFilePath = "";
    // State
    public LyricData Data { get; private set; }
    private List<List<LyricWord>> _wordsByLine = new();
    private int _currentLineIndex = -1;
    private double _lastTime = -1.0;
    
    // Track which logical line is assigned to which UI renderer
    private int[] _rendererLineIndices = { -1, -1 }; 

    public override void _Ready()
    {
        if (LyricFont == null)
        {
            LyricFont = ThemeDB.GetFallbackFont();
            GD.PrintErr("[ScrollingLyrics] No Font assigned! Using fallback.");
        }

        // Load the GDScript
        _rendererScript = GD.Load<GDScript>("res://scripts/KaraokeLine.gd");
        if (_rendererScript == null)
        {
            GD.PrintErr("[ScrollingLyrics] Could not load KaraokeLine.gd");
            return;
        }

        SetupUI();
        
        // Only auto-load if a path was set via Inspector (backward compatibility)
        if (!string.IsNullOrEmpty(LyricsFilePath))
        {
            LoadLyrics();
        }
        
        Resized += OnResized;
    }

    private void OnResized()
    {
        // Re-scale active lines when the container size changes
        float maxWidth = (float)Size.X * 0.95f; // 5% padding
        foreach (var renderer in _lineRenderers)
        {
            if (renderer != null)
                renderer.Call("update_width", maxWidth);
        }
    }

    private void SetupUI()
    {
        // Debug Label
        _debugLabel = new Label {
            Modulate = Colors.Red,
            Position = new Vector2(10, 10),
            ZIndex = 99
        };
        AddChild(_debugLabel);

        // Layout
        _mainLayout = new VBoxContainer {
            LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        AddChild(_mainLayout);

        // Create 2 Renderers (Top/Bottom)
        for (int i = 0; i < 2; i++)
        {
            var r = (Control)_rendererScript.New();
            r.Name = $"LineRenderer_{i}";
            r.CustomMinimumSize = new Vector2(0, 120); // Height of line slot
            r.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; // Keep centered
            
            _lineRenderers[i] = r;
            _mainLayout.AddChild(r);

            // Add spacer between lines
            if (i == 0)
            {
                _waitIndicator = new Label {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CustomMinimumSize = new Vector2(0, 60),
                    Modulate = new Color(1,1,1,0.5f)
                };
                _waitIndicator.AddThemeFontSizeOverride("font_size", 32);
                _mainLayout.AddChild(_waitIndicator);
            }
        }
    }

    public void LoadLyrics(string overridePath = null)
    {
        if (overridePath != null) LyricsFilePath = overridePath;
        if (string.IsNullOrEmpty(LyricsFilePath)) return;

        string path = LyricsFilePath;
        if (!path.StartsWith("res://") && !path.StartsWith("user://") && !path.IsAbsolutePath())
        {
            path = "res://" + path;
        }

        if (!FileAccess.FileExists(path)) 
        { 
            _debugLabel.Text = $"File not found: {path}";
            _debugLabel.Visible = true;
            GD.PrintErr($"[ScrollingLyrics] File not found: {path}");
            return; 
        }

        try 
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            Data = JsonSerializer.Deserialize<LyricData>(file.GetAsText());
            
            if (Data?.Words == null) return;

            // Reset state for new song
            _wordsByLine.Clear();
            ResetState();

            // Bucket sort words
            var grouped = Data.Words.GroupBy(w => w.LineId).OrderBy(g => g.Key);
            int maxId = grouped.LastOrDefault()?.Key ?? 0;
            for(int i=0; i<=maxId; i++) _wordsByLine.Add(new List<LyricWord>());
            foreach(var g in grouped) _wordsByLine[g.Key] = g.ToList();

            _debugLabel.Visible = false;
        }
        catch (Exception e) 
        { 
            _debugLabel.Text = e.Message; 
            _debugLabel.Visible = true;
            GD.PrintErr($"[ScrollingLyrics] Error loading lyrics: {e.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (Data == null) return;
        if (AudioManager.Instance==null) return;

        double time = AudioManager.Instance.GetMusicPlaybackPosition();

        // Seek Reset
        if (Math.Abs(time - _lastTime) > 1.0) ResetState();
        _lastTime = time;

        UpdateLogic(time);
        UpdateVisuals(time);
    }

    private void UpdateLogic(double time)
    {
        // Advance Line?
        if (_currentLineIndex < Data.Lines.Count - 1)
        {
            int nextIdx = _currentLineIndex + 1;
            if (time >= Data.Lines[nextIdx].Start - 0.1) // Slight tolerance
            {
                _currentLineIndex = nextIdx;
            }
        }

        // Lookahead Management
        // We want to ensure the Current Line AND the Next Line are loaded into renderers
        EnsureLineLoaded(_currentLineIndex);
        
        int lookaheadIdx = _currentLineIndex + 1;
        if (lookaheadIdx < Data.Lines.Count)
        {
             // Only load if upcoming soon (Hinting)
            if (Data.Lines[lookaheadIdx].Start - time < 4.0)
            {
                EnsureLineLoaded(lookaheadIdx);
            }
        }
    }

    private void EnsureLineLoaded(int lineIdx)
    {
        if (lineIdx < 0 || lineIdx >= _wordsByLine.Count) return;

        // Check if already loaded
        if (_rendererLineIndices[0] == lineIdx || _rendererLineIndices[1] == lineIdx) return;

        // Pick target slot (Even -> 0, Odd -> 1)
        int slot = lineIdx % 2;
        
        // Prepare Data for GDScript
        var wordsList = _wordsByLine[lineIdx];
        var gdArray = new Godot.Collections.Array();
        foreach(var w in wordsList)
        {
            var dict = new Godot.Collections.Dictionary();
            dict["text"] = w.Text;
            dict["start"] = w.Start;
            dict["end"] = w.End;
            gdArray.Add(dict);
        }

        // Setup GDScript Node
        float maxWidth = (float)Size.X * 0.95f; // 5% padding
        _lineRenderers[slot].Call("setup", gdArray, LyricFont, FontSize, ActiveColor, InactiveColor, maxWidth);
        _rendererLineIndices[slot] = lineIdx;
    }

    private void ResetState()
    {
        _currentLineIndex = -1;
        _rendererLineIndices[0] = -1;
        _rendererLineIndices[1] = -1;
        
        // Clear Renderers
        var empty = new Godot.Collections.Array();
        _lineRenderers[0].Call("setup", empty, LyricFont, FontSize, ActiveColor, InactiveColor);
        _lineRenderers[1].Call("setup", empty, LyricFont, FontSize, ActiveColor, InactiveColor);
    }

    private void UpdateVisuals(double time)
    {
        // Update Renderers
        for (int i = 0; i < 2; i++)
        {
            int loadedLine = _rendererLineIndices[i];
            bool isActive = (loadedLine == _currentLineIndex);
            
            // Pass time to GDScript
            _lineRenderers[i].Call("update_time", time, isActive);
        }

        // Update Wait Indicator
        UpdateWaitIndicator(time);
    }

    private void UpdateWaitIndicator(double time)
    {
        if (_currentLineIndex >= Data.Lines.Count - 1) 
        {
            _waitIndicator.Text = "";
            return;
        }

        int nextIdx = _currentLineIndex + 1;
        double prevEnd = (_currentLineIndex >= 0) ? Data.Lines[_currentLineIndex].End : 0;
        double nextStart = Data.Lines[nextIdx].Start;
        double gap = nextStart - prevEnd;

        // Only show if gap is sufficient (> 2s) and we are inside it
        if (gap > MinEllipseTriggerDuration && time > prevEnd && time < nextStart)
        {
            float progress = (float)((time - prevEnd) / gap);
            // 6 dots -> 0 dots
            int count = 6 - (int)(progress * 6);
            count = Math.Clamp(count, 0, 6);
            
            string txt = "";
            for(int k=0; k<count; k++) txt += "･ ";//japanese full-width dot for better visibility
            _waitIndicator.Text = txt;
        }
        else
        {
            _waitIndicator.Text = "";
        }
    }
}