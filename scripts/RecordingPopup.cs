using Godot;
using System;

namespace PitchGame
{
    public partial class RecordingPopup : PanelContainer
    {
        [Signal] public delegate void SkipToEndRequestedEventHandler();
        [Signal] public delegate void ReturnToJukeboxRequestedEventHandler();
        [Signal] public delegate void CancelRequestedEventHandler();

        public override void _Ready()
        {
            var btnSkip = GetNode<Button>("%BtnSkip");
            var btnReturn = GetNode<Button>("%BtnReturn");
            var btnCancel = GetNode<Button>("%BtnCancel");

            btnSkip.Pressed += () => EmitSignal(SignalName.SkipToEndRequested);
            btnReturn.Pressed += () => EmitSignal(SignalName.ReturnToJukeboxRequested);
            btnCancel.Pressed += () => EmitSignal(SignalName.CancelRequested);
            
            btnSkip.FocusMode = FocusModeEnum.None;
            btnReturn.FocusMode = FocusModeEnum.None;
            btnCancel.FocusMode = FocusModeEnum.None;
        }
        
        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                EmitSignal(SignalName.CancelRequested);
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
