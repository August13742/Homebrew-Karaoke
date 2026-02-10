using Godot;
using System;

namespace PitchGame
{
    /// <summary>
    /// Fixed pitch grid covering the human vocal range.
    /// No dynamic adjustment — same range for every song.
    /// Target notes and cursor are drawn at their actual MIDI positions.
    /// </summary>
    public partial class PitchGrid : Control
    {
        [ExportGroup("Range")]
        [Export] public float RangeMinMidi = 36f;  // C2 — low bass
        [Export] public float RangeMaxMidi = 84f;  // C6 — high soprano

        [ExportGroup("Visuals")]
        [Export] public Color GridColor = new Color(1, 1, 1, 0.08f);

        // --- PUBLIC API ---
        public float VisualCenterMidi => (RangeMinMidi + RangeMaxMidi) / 2f;
        public float SemitonesToPixels => Size.Y / (RangeMaxMidi - RangeMinMidi);

        /// <summary>
        /// Converts a MIDI value to a local Y position.
        /// </summary>
        public float GetLocalYFromMidi(float midiValue)
        {
            float diff = midiValue - VisualCenterMidi;
            return (Size.Y / 2f) - (diff * SemitonesToPixels);
        }

        public override void _Draw()
        {
            int startNote = (int)RangeMinMidi;
            int endNote = (int)RangeMaxMidi;

            for (int note = startNote; note <= endNote; note++)
            {
                float drawY = GetLocalYFromMidi(note);
                DrawLine(new Vector2(0, drawY), new Vector2(Size.X, drawY), GridColor, 1.0f);
            }
        }
    }
}