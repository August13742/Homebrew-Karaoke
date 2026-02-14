using Godot;
using System;

namespace PitchGame
{
    public partial class ImportStatusPanel : Control
    {
        private ProgressBar _overallProgressBar;
        private ProgressBar _stageProgressBar;
        private Label _lblStatus;
        private Label _lblStage;
        private RichTextLabel _logBox;
        private Button _btnClose;
        private Control _modalContent;
        private ColorRect _scrim;

        public override void _Ready()
        {
            _overallProgressBar = GetNode<ProgressBar>("%OverallProgressBar");
            _stageProgressBar = GetNode<ProgressBar>("%StageProgressBar");
            _lblStatus = GetNode<Label>("%LblStatus");
            _lblStage = GetNode<Label>("%LblStage");
            _logBox = GetNode<RichTextLabel>("%LogBox");
            _btnClose = GetNode<Button>("%BtnClose");
            _modalContent = GetNode<Control>("%PanelContainer");
            _scrim = GetNode<ColorRect>("./ColorRect");

            _btnClose.Pressed += HideModal;
            _btnClose.Disabled = true;
            
            // Initial state
            Visible = false;
            _modalContent.Scale = new Vector2(0.8f, 0.8f);
            _modalContent.Modulate = new Color(1, 1, 1, 0);
            _scrim.Modulate = new Color(1, 1, 1, 0);
        }

        public void Start(string fileName)
        {
            ShowModal();
            _btnClose.Disabled = true;
            _lblStatus.Text = $"Importing: {fileName}";
            _overallProgressBar.Value = 0;
            _stageProgressBar.Value = 0;
            _logBox.Clear();
            _logBox.AppendText($"[color=cyan]Started import for {fileName}[/color]\n");
        }

        private void ShowModal()
        {
            Visible = true;
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(_modalContent, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_modalContent, "modulate", Colors.White, 0.2f);
            tween.TweenProperty(_scrim, "modulate", Colors.White, 0.3f);
        }

        private void HideModal()
        {
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(_modalContent, "scale", new Vector2(0.8f, 0.8f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.TweenProperty(_modalContent, "modulate", new Color(1, 1, 1, 0), 0.2f);
            tween.TweenProperty(_scrim, "modulate", new Color(1, 1, 1, 0), 0.3f);
            tween.Chain().TweenCallback(Callable.From(() => Visible = false));
        }

        public void UpdateProgress(string stage, float progress)
        {
            _stageProgressBar.Value = progress * 100;
            _lblStage.Text = $"{stage.ToUpper()} ({progress * 100:F0}%)";
            
            // Calculate overall progress based on pipeline stages
            // Stages: separation -> enhancement -> transcription -> alignment -> pitch
            float stageBase = stage switch {
                "separation" => 0.0f,
                "enhancement" => 0.2f,
                "transcription" => 0.4f,
                "alignment" => 0.6f,
                "pitch" => 0.8f,
                _ => 0.0f
            };
            
            float overall = stageBase + (progress * 0.2f);
            _overallProgressBar.Value = overall * 100;
        }

        public void AddLog(string message)
        {
            _logBox.AppendText($"{message}\n");
        }

        public void Complete(string songName, bool success)
        {
            _btnClose.Disabled = false;
            _stageProgressBar.Value = 100;
            
            if (success)
            {
                _overallProgressBar.Value = 100;
                _lblStatus.Text = "IMPORT COMPLETE!";
                _lblStatus.AddThemeColorOverride("font_color", Colors.SpringGreen);
                _lblStage.Text = "DONE";
                _logBox.AppendText($"\n[color=green]Successfully imported {songName}[/color]\n");
            }
            else
            {
                _lblStatus.Text = "IMPORT FAILED";
                _lblStatus.AddThemeColorOverride("font_color", Colors.DeepPink);
                _lblStage.Text = "ERROR";
                _logBox.AppendText($"\n[color=red]Failed to import {songName}. Check logs above.[/color]\n");
            }
        }
    }
}
