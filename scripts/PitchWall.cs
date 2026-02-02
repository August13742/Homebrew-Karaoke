using Godot;
using System;

namespace PitchGame
{
    public partial class PitchWall : Node3D
    {
        [Export] public int TargetMidiNote = 60;
        [Export] public float MoveSpeed = 6f;
        
        private Node3D _hole;

        public override void _Ready()
        {
            _hole = GetNodeOrNull<Node3D>("Hole");
            UpdateHolePosition();
        }

        public override void _Process(double delta)
        {
            Vector3 pos = Position;
            pos.X -= MoveSpeed * (float)delta;
            Position = pos;
            
            if (GlobalPosition.X < -25f)
            {
                QueueFree();
            }
        }

        public void Setup(int midi, float minY, float maxY, int minMidi, int maxMidi)
        {
            TargetMidiNote = midi;
            UpdateHolePosition(minY, maxY, minMidi, maxMidi);
        }

        private void UpdateHolePosition(float minY = -4f, float maxY = 4f, int minMidi = 48, int maxMidi = 72)
        {
            if (_hole == null) return;
            
            float t = (TargetMidiNote - minMidi) / (float)(maxMidi - minMidi);
            t = Mathf.Clamp(t, 0, 1);
            float yPos = Mathf.Lerp(minY, maxY, t);
            
            _hole.Position = new Vector3(0, yPos, 0);
            
            var label = _hole.GetNodeOrNull<Label3D>("Label3D");
            if (label != null)
            {
                label.Text = MidiToNoteName(TargetMidiNote);
            }
        }

        private string MidiToNoteName(int midi)
        {
            string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return notes[midi % 12] + (midi / 12 - 1).ToString();
        }
    }
}
