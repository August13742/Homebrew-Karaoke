using Godot;
using System;

namespace PitchGame
{
    /// <summary>
    /// Self-contained key shift control: ▼/▲ buttons + label.
    /// Emits KeyShiftChanged for the parent to route to AudioManager.
    /// </summary>
    public partial class KeyShiftControl : HBoxContainer
    {
        [Signal] public delegate void KeyShiftChangedEventHandler(float newShift);

        [Export] public float Step = 0.5f;
        [Export] public float Range = 10f;

        private Label _lblKeyShift;
        private Button _btnDown, _btnUp;
        private float _currentShift = 0f;

        public float KeyShift => _currentShift;

        public void SetKeyShift(float shift)
        {
            _currentShift = Mathf.Clamp(shift, -Range, Range);
            UpdateLabel();
        }

        public override void _Ready()
        {
            _lblKeyShift = GetNodeOrNull<Label>("%LblKeyShift");
            _btnDown     = GetNodeOrNull<Button>("%BtnKeyDown");
            _btnUp       = GetNodeOrNull<Button>("%BtnKeyUp");

            if (_btnDown != null) { _btnDown.FocusMode = FocusModeEnum.None; _btnDown.Pressed += () => ChangeShift(-Step); }
            if (_btnUp   != null) { _btnUp.FocusMode   = FocusModeEnum.None; _btnUp.Pressed   += () => ChangeShift(Step);  }

            UpdateLabel();
        }

        private void ChangeShift(float delta)
        {
            _currentShift = Mathf.Clamp(_currentShift + delta, -Range, Range);
            UpdateLabel();
            EmitSignal(SignalName.KeyShiftChanged, _currentShift);
        }

        private void UpdateLabel()
        {
            if (_lblKeyShift != null)
            {
                _lblKeyShift.Text = _currentShift == 0f ? "0" : _currentShift.ToString("+0.#;-0.#");
            }
        }
    }
}
