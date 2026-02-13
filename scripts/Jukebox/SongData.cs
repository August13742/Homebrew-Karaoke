using Godot;

namespace PitchGame
{
    /// <summary>
    /// Metadata for a discovered song in the Music/ folder.
    /// Expected structure: Music/SongName/{vocals.wav, instrumental.wav, karaoke.json}
    /// Inherits from GodotObject to allow passing via signals.
    /// </summary>
    public partial class SongData : RefCounted
    {
        public string Name;           // Display name (folder name)
        public string FolderPath;     // "res://Music/SongName"
        public string VocalsPath;     // "res://Music/SongName/vocals.wav"
        public string InstrumentalPath;
        public string KaraokeJsonPath;
        public bool IsValid;          // True if all required files exist
    }
}
