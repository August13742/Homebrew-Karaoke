using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AudioSystem
{
    public partial class RecordingManager : Node
    {
        [Export] public string RecordBusName = "Record";
        
        public bool IsRecording { get; private set; }
        public float RecordedLength => _samples.Count / (float)_mixRate;

        private AudioEffectCapture _capture;
        private List<float> _samples = new List<float>();
        private int _mixRate;
        private float _currentSeekTime = 0f; // Track where we are logically in the song

        public override void _Ready()
        {
            // We need our OWN capture effect because PitchDetector consumes the existing one.
            int busIdx = AudioServer.GetBusIndex(RecordBusName);
            if (busIdx != -1)
            {
                _capture = new AudioEffectCapture();
                AudioServer.AddBusEffect(busIdx, _capture);
                _mixRate = (int)AudioServer.GetMixRate();
                GD.Print($"RecordingManager: Added dedicated Capture effect to bus '{RecordBusName}' at {_mixRate}Hz");
            }
            else
            {
                GD.PrintErr($"RecordingManager: Bus '{RecordBusName}' not found!");
            }
        }

        public override void _ExitTree()
        {
            // Clean up the effect if possible, though Godot might handle it.
            // It's safer to leave it or remove it? removing indices dynamicall can be risky if other things depend on indices.
            // given this is a singleton-like node in the game scene, it's fine.
        }

        public override void _Process(double delta)
        {
            if (!IsRecording || _capture == null) return;

            // Poll Capture
            int framesAvailable = _capture.GetFramesAvailable();
            if (framesAvailable > 0)
            {
                Vector2[] tasks = _capture.GetBuffer(framesAvailable);
                // Convert to Mono and add
                foreach (var sample in tasks)
                {
                    // Mix stereo to mono (average)
                    float mono = (sample.X + sample.Y) * 0.5f;
                    _samples.Add(mono);
                }
                
                // Update logical time
                _currentSeekTime = _samples.Count / (float)_mixRate;
            }
        }

        public void StartRecording()
        {
            if (_capture != null) _capture.ClearBuffer();
            _samples.Clear();
            _currentSeekTime = 0f;
            IsRecording = true;
            GD.Print("Recording Started");
        }

        public void StopRecording()
        {
            IsRecording = false;
            GD.Print($"Recording Stopped. Total Samples: {_samples.Count} ({RecordedLength:F2}s)");
        }

        /// <summary>
        /// Called when the user seeks via UI controls.
        /// </summary>
        /// <param name="newTime">The target time in the song in seconds</param>
        public void NotifySeek(float newTime)
        {
            if (!IsRecording) return;
            
            // Flush any pending buffer first to ensure we are up to date
            _Process(0);

            float currentTime = RecordedLength;
            
            if (newTime < currentTime)
            {
                // BACKWARD SEEK: Truncate
                // e.g. at 10s, seek to 5s. Remove everything after 5s.
                int samplesKeep = (int)(newTime * _mixRate);
                if (samplesKeep < 0) samplesKeep = 0;
                if (samplesKeep < _samples.Count)
                {
                    int removeCount = _samples.Count - samplesKeep;
                    _samples.RemoveRange(samplesKeep, removeCount);
                    GD.Print($"RecordingManager: Backward seek detected. Truncated {removeCount} samples. New Length: {RecordedLength:F2}s");
                }
            }
            else if (newTime > currentTime)
            {
                // FORWARD SEEK: Insert Silence
                // e.g. at 10s, seek to 20s. Insert 10s of silence.
                float durationToAdd = newTime - currentTime;
                int samplesAdd = (int)(durationToAdd * _mixRate);
                if (samplesAdd > 0)
                {
                    // Efficiently add silence
                    float[] silence = new float[samplesAdd]; 
                    _samples.AddRange(silence);
                    GD.Print($"RecordingManager: Forward seek detected. Inserted {samplesAdd} silence samples ({durationToAdd:F2}s). New Length: {RecordedLength:F2}s");
                }
            }
            
            _currentSeekTime = newTime;
        }

        public AudioStreamWav GetFinalRecording()
        {
            if (_samples.Count == 0) return null;

            var wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = _mixRate;
            wav.Stereo = false; // We mixed to mono
            
            // Convert float[] to byte[] (16-bit PCM)
            byte[] bytes = new byte[_samples.Count * 2];
            for (int i = 0; i < _samples.Count; i++)
            {
                float sample = Mathf.Clamp(_samples[i], -1f, 1f);
                short shortSample = (short)(sample * short.MaxValue);
                
                // Little Endian
                bytes[i * 2] = (byte)(shortSample & 0xFF);
                bytes[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
            }
            
            wav.Data = bytes;
            return wav;
        }

        public bool SaveRecording(string path)
        {
            var wav = GetFinalRecording();
            if (wav == null) return false;
            
            try
            {
                // Write standard WAV header + Data
                using (var fs = File.OpenWrite(path))
                using (var bw = new BinaryWriter(fs))
                {
                    // RIFF chunk
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + wav.Data.Length); // File size - 8
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    
                    // fmt chunk
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16); // Chunk size (16 for PCM)
                    bw.Write((short)1); // AudioFormat (1 = PCM)
                    bw.Write((short)1); // NumChannels (Mono)
                    bw.Write(_mixRate); // SampleRate
                    bw.Write(_mixRate * 2); // ByteRate (SampleRate * BlockAlign)
                    bw.Write((short)2); // BlockAlign (channels * bits/8)
                    bw.Write((short)16); // BitsPerSample
                    
                    // data chunk
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(wav.Data.Length);
                    bw.Write(wav.Data);
                }
                GD.Print($"Saved recording to {path}");
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Failed to save recording: {e.Message}");
                return false;
            }
        }
    }
}
