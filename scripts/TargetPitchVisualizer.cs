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
        [Export] public Color GraceColor = new Color(0, 0.6f, 1.0f, 0.2f);
        [Export] public float LookaheadSeconds = 4.0f;
        [Export] public float LookbehindSeconds = 1.0f;

        [Export] public float GraceSemitones = 1.0f;

        private VocalGuide _guide;
        private bool _guideInitialized = false;

        public override void _Ready()
        {
        }

        public override void _Process(double delta)
        {
            if (Grid == null || LyricsSource == null) return;

            if (SessionData.VoiceOverMode && !_guideInitialized && LyricsSource.Data != null)
            {
                InitializeVocalGuide();
            }

            QueueRedraw();
        }

        private void InitializeVocalGuide()
        {
            GD.Print("[TargetPitchVisualizer] Voice Over Mode enabled & Data ready. Injecting VocalGuide.");
            _guide = new VocalGuide();
            _guide.Data = LyricsSource.Data;
            _guide.KeyShift = ControlPanel?.KeyShiftSemitones ?? SessionData.KeyShift;
            AddChild(_guide);
            _guide.Start();
            _guideInitialized = true;
        }

        public override void _Draw()
        {
            if (Grid == null || LyricsSource?.Data == null) return;
            if (AudioManager.Instance == null) return;

            double time = AudioManager.Instance.GetMusicPlaybackPosition();
            float centerX = Size.X / 2.0f;
            
            // Get key shift from control panel (0 if not found)
            float keyShift = ControlPanel?.KeyShiftSemitones ?? 0f;

            // Use Pitch events instead of Words
            var pitchEvents = LyricsSource.Data.Pitch;
            if (pitchEvents == null) return;

            // Pitch events are point data at ~0.1s intervals. drawing them as blocks of 0.1s width.
            // Or better: draw from current event time to next event time (clamped).
            // The python script says "median MIDI at ~100 ms intervals".
            float eventDuration = 0.1f;

            // Calculate note height based on grace semitones 
            // We'll use a semi-transparent 'safe zone' background
            float totalGraceHeight = NoteHeight + (GraceSemitones * 20.0f); // 20px per semitone approx? 

            foreach (var p in pitchEvents)
            {
                if (p.Midi <= 0) continue;

                // Simple culling
                if (p.Time < time - LookbehindSeconds) continue;
                if (p.Time > time + LookaheadSeconds) break; 

                float xStart = centerX + (float)((p.Time - time) * PixelsPerSecond);
                float xEnd = centerX + (float)((p.Time + eventDuration - time) * PixelsPerSecond);
                float width = Math.Max(xEnd - xStart, 1.0f);

                // Quantize + apply key shift
                float shiftedMidi = Mathf.Round(p.Midi) + keyShift;
                float y = Grid.GetLocalYFromMidi(shiftedMidi);

                // 1. Draw Grace Range (The "Outer Pill")
                Rect2 graceRect = new Rect2(xStart, y - totalGraceHeight / 2f, width, totalGraceHeight);
                DrawRect(graceRect, GraceColor, true);
                
                // 2. Draw Core Note (The "Sweet Spot")
                Rect2 coreRect = new Rect2(xStart, y - NoteHeight / 2f, width, NoteHeight);
                DrawRect(coreRect, NoteColor, true);

                // 3. Draw Perfect Line (The "Target")
                DrawLine(new Vector2(xStart, y), new Vector2(xStart + width, y), NoteColor.Lightened(0.5f), 1.0f);
            }
        }
    }
}
