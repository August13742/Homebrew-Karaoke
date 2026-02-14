using Godot;
using System;
using System.IO;

namespace PitchGame
{
    public static class ExternalAudioLoader
    {
        public static AudioStream LoadAudio(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string ext = path.GetExtension().ToLower();

            // 1. Try GD.Load first (it's faster and handles imported assets properly)
            // Note: This might fail at runtime for new files, which is why we have fallbacks.
            try
            {
                if (ResourceLoader.Exists(path))
                {
                    var res = GD.Load<AudioStream>(path);
                    if (res != null) return res;
                }
            }
            catch { }

            // 2. Fallback to manual loading for unimported files
            string globalPath = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(globalPath))
            {
                GD.PrintErr($"[ExternalAudioLoader] File not found: {globalPath}");
                return null;
            }

            switch (ext)
            {
                case "ogg":
                    return AudioStreamOggVorbis.LoadFromFile(path);
                case "mp3":
                    var mp3 = new AudioStreamMP3();
                    mp3.Data = Godot.FileAccess.GetFileAsBytes(path);
                    return mp3;
                case "wav":
                    // WAV loading is complex because Godot 4 doesn't have a simple LoadFromFile for WAV
                    // that parses the header automatically. We'd need to parse RIFF header.
                    // However, our pipeline now favors OGG.
                    GD.PrintErr("[ExternalAudioLoader] Runtime WAV loading not fully implemented. Please use OGG.");
                    return null;
                default:
                    GD.PrintErr($"[ExternalAudioLoader] Unsupported format for runtime loading: {ext}");
                    return null;
            }
        }
    }
}
