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
    private List<HBoxContainer> _lineContainers = new();
    private List<List<Label>> _wordLabels = new();
    private int _currentLineIndex = -1;
    private int _currentWordIndex = -1;

    public override void _Ready()
    {
        _container = new Control();
        _container.Name = "LyricsContainer";
        AddChild(_container);
        
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
            
            // Re-map words to lines if necessary (it should be in the JSON)
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

        var wordsByLine = _data.Words.GroupBy(w => w.LineId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var line in _data.Lines)
        {
            HBoxContainer lineBox = new HBoxContainer();
            lineBox.Alignment = BoxContainer.AlignmentMode.Center;
            lineBox.CustomMinimumSize = new Vector2(0, LineHeight);
            _container.AddChild(lineBox);
            _lineContainers.Add(lineBox);

            List<Label> labelsInLine = new List<Label>();
            
            if (wordsByLine.ContainsKey(line.Id))
            {
                foreach (var word in wordsByLine[line.Id])
                {
                    Label label = new Label();
                    label.Text = word.Text + " ";
                    label.AddThemeFontSizeOverride("font_size", 32);
                    label.Modulate = InactiveColor;
                    lineBox.AddChild(label);
                    labelsInLine.Add(label);
                }
            }
            else
            {
                // Fallback for lines without word data
                Label label = new Label();
                label.Text = line.Text;
                label.AddThemeFontSizeOverride("font_size", 32);
                label.Modulate = InactiveColor;
                lineBox.AddChild(label);
                labelsInLine.Add(label);
            }
            
            _wordLabels.Add(labelsInLine);
            
            // Initial vertical positioning
            lineBox.Position = new Vector2(-lineBox.Size.X / 2, (_lineContainers.Count - 1) * LineHeight);
        }
    }

    public override void _Process(double delta)
    {
        if (_data == null || _lineContainers.Count == 0) return;

        double currentTime = AudioManager.Instance.GetMusicPlaybackPosition();
        UpdateHighlighting(currentTime);
        UpdateScrolling(currentTime);
    }

    private void UpdateHighlighting(double currentTime)
    {
        int foundLineIndex = -1;
        int foundWordIndex = -1;

        // Find the current line and word
        for (int i = 0; i < _data.Lines.Count; i++)
        {
            if (currentTime >= _data.Lines[i].Start && currentTime <= _data.Lines[i].End)
            {
                foundLineIndex = i;
                
                // Find word within this line
                var lineWords = _data.Words.Where(w => w.LineId == _data.Lines[i].Id).ToList();
                for (int j = 0; j < lineWords.Count; j++)
                {
                    if (currentTime >= lineWords[j].Start && currentTime <= lineWords[j].End)
                    {
                        foundWordIndex = j;
                        break;
                    }
                    else if (currentTime > lineWords[j].End)
                    {
                        // Word already passed
                        foundWordIndex = j;
                    }
                }
                break;
            }
        }

        if (foundLineIndex != _currentLineIndex || foundWordIndex != _currentWordIndex)
        {
            // Reset previous highlighting if line changed
            if (_currentLineIndex != -1 && _currentLineIndex != foundLineIndex)
            {
                foreach (var label in _wordLabels[_currentLineIndex])
                {
                    label.Modulate = InactiveColor;
                    label.AddThemeFontSizeOverride("font_size", 32);
                }
            }

            _currentLineIndex = foundLineIndex;
            _currentWordIndex = foundWordIndex;

            if (_currentLineIndex != -1)
            {
                // Highlight words up to and including currentWordIndex
                var labels = _wordLabels[_currentLineIndex];
                for (int j = 0; j < labels.Count; j++)
                {
                    if (j <= _currentWordIndex)
                    {
                        labels[j].Modulate = ActiveColor;
                        labels[j].AddThemeFontSizeOverride("font_size", j == _currentWordIndex ? 40 : 36);
                    }
                    else
                    {
                        labels[j].Modulate = InactiveColor;
                        labels[j].AddThemeFontSizeOverride("font_size", 32);
                    }
                }
            }
        }
    }

    private void UpdateScrolling(double currentTime)
    {
        int targetIndex = _currentLineIndex;
        if (targetIndex == -1)
        {
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

        float targetY = (float)(Size.Y / 2 - targetIndex * LineHeight + CenterOffset);
        _container.Position = _container.Position.Lerp(new Vector2(Size.X / 2, targetY), (float)(0.1f));
        
        for (int i = 0; i < _lineContainers.Count; i++)
        {
            _lineContainers[i].Position = new Vector2(-_lineContainers[i].Size.X / 2, i * LineHeight);
        }
    }
}
