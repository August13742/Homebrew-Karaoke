using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

public partial class ScrollingLyrics : Control
{
    [Export] public string LyricsFilePath = "TestSong/Voyaging Star's Farewell_karaoke.json";
    [Export] public Color ActiveColor = new Color(1, 1, 0); // Yellow
    [Export] public Color InactiveColor = new Color(1, 1, 1); // White
    [Export] public float ScrollSpeed = 300.0f;
    [Export] public float LineHeight = 60.0f;
    [Export] public int CenterOffset = 0; // Vertical offset from center

    private Control _container;
    private LyricData _data;
    private List<Label> _labels = new();
    private int _currentLineIndex = -1;

    public override void _Ready()
    {
        _container = new Control();
        _container.Name = "LyricsContainer";
        AddChild(_container);
        
        // Center the container horizontally
        _container.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);

        LoadLyrics();
    }

    private void LoadLyrics()
    {
        string absolutePath = ProjectSettings.GlobalizePath("res://" + LyricsFilePath);
        if (!FileAccess.FileExists(absolutePath))
        {
            GD.PrintErr($"Lyrics file not found: {absolutePath}");
            return;
        }

        using var file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();
        
        try
        {
            _data = JsonSerializer.Deserialize<LyricData>(json);
            CreateLabels();
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to parse lyrics: {e.Message}");
        }
    }

    private void CreateLabels()
    {
        if (_data == null || _data.Lines == null) return;

        foreach (var line in _data.Lines)
        {
            Label label = new Label();
            label.Text = line.Text;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            
            // Set some default style/font size if possible
            label.AddThemeFontSizeOverride("font_size", 32);
            label.Modulate = InactiveColor;
            
            _container.AddChild(label);
            _labels.Add(label);
            
            // Initial positioning
            label.Position = new Vector2(-label.Size.X / 2, _labels.Count * LineHeight);
        }
    }

    public override void _Process(double delta)
    {
        if (_data == null || _labels.Count == 0) return;

        double currentTime = AudioManager.Instance.GetMusicPlaybackPosition();
        UpdateHighlighting(currentTime);
        UpdateScrolling(currentTime);
    }

    private void UpdateHighlighting(double currentTime)
    {
        int foundIndex = -1;
        for (int i = 0; i < _data.Lines.Count; i++)
        {
            if (currentTime >= _data.Lines[i].Start && currentTime <= _data.Lines[i].End)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex != _currentLineIndex)
        {
            if (_currentLineIndex >= 0 && _currentLineIndex < _labels.Count)
            {
                _labels[_currentLineIndex].Modulate = InactiveColor;
                _labels[_currentLineIndex].AddThemeFontSizeOverride("font_size", 32);
            }
            
            _currentLineIndex = foundIndex;
            
            if (_currentLineIndex >= 0 && _currentLineIndex < _labels.Count)
            {
                _labels[_currentLineIndex].Modulate = ActiveColor;
                _labels[_currentLineIndex].AddThemeFontSizeOverride("font_size", 40);
            }
        }
    }

    private void UpdateScrolling(double currentTime)
    {
        // Find the "target" line to center on. 
        // If we are between lines, we might want to interpolate.
        
        int targetIndex = _currentLineIndex;
        if (targetIndex == -1)
        {
            // Find the next line
            for (int i = 0; i < _data.Lines.Count; i++)
            {
                if (currentTime < _data.Lines[i].Start)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        if (targetIndex == -1) return;

        // Calculate target Y position for the container
        // We want the targetIndex line to be at vertical center (Size.Y / 2)
        float targetY = (float)(Size.Y / 2 - targetIndex * LineHeight + CenterOffset);
        
        // Smooth scroll
        _container.Position = _container.Position.Lerp(new Vector2(Size.X / 2, targetY), (float)(0.1f));
        
        // Update individual label horizontal centering
        for (int i = 0; i < _labels.Count; i++)
        {
            _labels[i].Position = new Vector2(-_labels[i].Size.X / 2, i * LineHeight);
        }
    }
}
