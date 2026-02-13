using Godot;

namespace PitchGame
{
    /// <summary>
    /// Static session state to pass information from the Jukebox to the Game scene.
    /// </summary>
    public static class SessionData
    {
        public static SongData CurrentSong;
        public static float KeyShift = 0f;
        public static bool VoiceOverMode = false;
        public static LyricData CurrentLyrics;
        public static AudioStreamWav LastRecording;
    }
}
