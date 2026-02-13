# Homebrew Karaoke - Agent Guide

## Project Structure
- `scenes/`: Main Godot scenes (tscn). `GameScene.tscn` is the combined production scene.
- `scripts/`: Application logic. 
    - C# (`.cs`): Performance-critical (Audio, Pitch Detection, Visualizers).
    - GDScript (`.gd`): High-level scene management and UI logic.
- `AudioSystem/`: Core audio management (`AudioManager.cs`).
- `Quarantine/`: Legacy/ported components being integrated.
- `TestSong/`: JSON lyric/pitch data and audio assets.

## Coding Style
- **Language Split**: C# for data-heavy/audio tasks; GDScript for UI/Glue.
- **No Magic Numbers**: Use exported variables or constants.
- **Audio**: `PitchDetector` uses `AudioEffectCapture` on the "Record" bus.
- **Rendering**: `PitchGrid` auto-zooms to fit the song's pitch range (static, no scrolling). `KaraokeCursor` is octave-folded into that range via `Grid.FoldMidiIntoRange()`. Target notes are quantized to nearest semitone.
- **Deprecated files**: Moved to `deprecated/` folder, not deleted.

## Key Components
- `PitchDetector.cs`: YIN-based real-time pitch detection.
- `ScrollingLyrics.cs`: Synchronized lyric display from JSON.
- `PitchGrid.cs`: Background lines for MIDI notes.
- `KaraokeCursor.cs`: Visualizes singer pitch on the grid.
- `TargetPitchVisualizer.cs`: Visualizes melody notes from data.
- `KaraokeScorer.cs`: Scoring logic with 12-semitone modulo.
