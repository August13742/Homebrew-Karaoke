using Godot;
using System;

namespace PitchGame
{
    public partial class ImportStatusPanel : Control
    {
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private RichTextLabel _logBox;
        private Button _btnClose;
        private PanelContainer _background;

        public override void _Ready()
        {
            _progressBar = GetNode<ProgressBar>("%ProgressBar");
            _lblStatus = GetNode<Label>("%LblStatus");
            _logBox = GetNode<RichTextLabel>("%LogBox");
            _btnClose = GetNode<Button>("%BtnClose");

            _btnClose.Pressed += () => Visible = false;
            _btnClose.Disabled = true;
            Visible = false;
        }

        public void Start(string fileName)
        {
            Visible = true;
            _btnClose.Disabled = true;
            _lblStatus.Text = $"Importing: {fileName}";
            _progressBar.Value = 0;
            _logBox.Clear();
            _logBox.AppendText($"[color=cyan]Started import for {fileName}[/color]\n");
        }

        public void UpdateProgress(string stage, float progress)
        {
            _progressBar.Value = progress * 100;
            _lblStatus.Text = $"Stage: {stage} ({progress * 100:F0}%)";
        }

        public void AddLog(string message)
        {
            _logBox.AppendText($"{message}\n");
        }

        public void Complete(string songName, bool success)
        {
            _btnClose.Disabled = false;
            if (success)
            {
                _lblStatus.Text = "Import Complete!";
                _logBox.AppendText($"\n[color=green]Successfully imported {songName}[/color]\n");
            }
            else
            {
                _lblStatus.Text = "Import Failed";
                _logBox.AppendText($"\n[color=red]Failed to import {songName}. Check logs above.[/color]\n");
            }
        }
    }
}
