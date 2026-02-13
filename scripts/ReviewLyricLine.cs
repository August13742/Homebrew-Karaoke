using Godot;
using System;

namespace PitchGame
{
    public partial class ReviewLyricLine : Button
    {
        [Signal] public delegate void LineClickedEventHandler(double timestamp);

        public double Timestamp { get; set; }
        public bool IsActive { get; private set; }

        private Color _normalColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
        private Color _activeColor = new Color(1.0f, 0.84f, 0.0f, 1.0f); // Gold

        public override void _Ready()
        {
            FocusMode = FocusModeEnum.None;
            Pressed += () => EmitSignal(SignalName.LineClicked, Timestamp);
            UpdateStyle();
        }

        public void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            UpdateStyle();
        }

        private void UpdateStyle()
        {
            AddThemeColorOverride("font_color", IsActive ? _activeColor : _normalColor);
            AddThemeColorOverride("font_pressed_color", _activeColor);
            AddThemeColorOverride("font_hover_color", Colors.White);
        }
    }
}
