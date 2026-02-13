using Godot;
using System;
using System.Linq;

namespace PitchGame
{
    /// <summary>
    /// Draws target pitch bars from lyric data.
    /// Notes are quantized to the nearest semitone.
    /// Applies KeyShift from SongControlPanel to offset target notes.
    /// </summary>
    public partial class TargetPitchVisualizer : Control
    {
        [Export] public ScrollingLyrics LyricsSource;
        [Export] public PitchGrid Grid;
        [Export] public SongControlPanel ControlPanel;

        [ExportGroup("Visual Settings")]
        [Export] public float PixelsPerSecond = 100.0f;
        [Export] public float NoteHeight = 8.0f;
        [Export] public Color NoteColor = new Color(0, 0.6f, 1.0f, 0.8f);
        [Export] public float LookaheadSeconds = 4.0f;
        [Export] public float LookbehindSeconds = 1.0f;

        public override void _Ready()
        {
        }

        public override void _Process(double delta)
        {
            if (Grid == null || LyricsSource == null) return;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Grid == null || LyricsSource?.Data == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            float centerX = Size.X / 2.0f;
            
            // Get key shift from control panel (0 if not found)
            float keyShift = ControlPanel?.KeyShiftSemitones ?? 0f;

            foreach (var word in LyricsSource.Data.Words)
            {
                if (word.PitchMidi <= 0) continue;

                if (word.End < time - LookbehindSeconds) continue;
                if (word.Start > time + LookaheadSeconds) break;

                float xStart = centerX + (float)((word.Start - time) * PixelsPerSecond);
                float xEnd = centerX + (float)((word.End - time) * PixelsPerSecond);
                float width = Math.Max(xEnd - xStart, 4.0f);

                // Quantize + apply key shift
                float shiftedMidi = Mathf.Round(word.PitchMidi) + keyShift;
                float y = Grid.GetLocalYFromMidi(shiftedMidi);

                Rect2 rect = new Rect2(xStart, y - NoteHeight / 2f, width, NoteHeight);
                DrawRect(rect, NoteColor, true);
                DrawRect(rect, NoteColor.Lightened(0.5f), false, 1.0f);
            }
        }
    }
}
