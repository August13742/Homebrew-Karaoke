using Godot;
using System;

namespace PitchGame
{
    public partial class SongListItem : Button
    {
        public SongData Data { get; private set; }
        
        [Signal] public delegate void SelectedEventHandler(SongListItem item);
        
        public void Initialise(SongData data)
        {
            Data = data;
            Text = data.Name;
            
            // Set some basic styling if needed
            Alignment = HorizontalAlignment.Left;
            CustomMinimumSize = new Vector2(0, 50);
            
            Pressed += () => EmitSignal(SignalName.Selected, this);
        }
    }
}
