using Godot;
using System;

namespace PitchGame
{
    /// <summary>
    /// Self-contained volume slider with label.
    /// Emits VolumeChanged signal for the parent to route to the correct AudioManager bus.
    /// </summary>
    public partial class VolumeControl : HBoxContainer
    {
        [Signal] public delegate void VolumeChangedEventHandler(float newValue);

        [Export] public string LabelText = "Volume";
        [Export] public float DefaultValue = 1.0f;

        private HSlider _slider;
        private Label _label;

        public float Volume => (float)(_slider?.Value ?? DefaultValue);

        public void SetVolume(float value)
        {
            if (_slider != null) _slider.Value = value;
        }

        public override void _Ready()
        {
            _label  = GetNodeOrNull<Label>("%Label");
            _slider = GetNodeOrNull<HSlider>("%Slider");

            if (_label != null) _label.Text = LabelText;

            if (_slider != null)
            {
                _slider.FocusMode = FocusModeEnum.None;
                _slider.Value = DefaultValue;
                _slider.ValueChanged += (val) => EmitSignal(SignalName.VolumeChanged, (float)val);
            }
        }
    }
}
