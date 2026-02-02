using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace PitchGame
{
    public partial class PitchDetector : Node
    {
        [ExportGroup("Audio Settings")]
        [Export] public string BusName = "Record";
        [Export] public float AmplitudeThreshold = 0.02f;
        [Export] public float MinFreq = 65f;   
        [Export] public float MaxFreq = 1000f; 

        [ExportGroup("Stabilization")]
        [Export] public float SmoothingSpeed = 15.0f;
        [Export] public int HistorySize = 5; // Size of Median Filter (Odd number)

        // --- PUBLIC DATA ---
        public float CurrentFrequency { get; private set; } 
        public int CurrentMidiNote { get; private set; } 
        public float CentDeviation { get; private set; }
        public float CurrentAmplitude { get; private set; }
        public bool IsDetected { get; private set; }

        private AudioEffectCapture _capture;
        private int _sampleRate;
        
        // Stabilizers
        private float _smoothHz;
        private readonly List<float> _pitchHistory = new List<float>();

        private float[] _processBuffer; 

        public override void _Ready()
        {
            int busIdx = AudioServer.GetBusIndex(BusName);
            if (busIdx == -1)
            {
                GD.PrintErr($"PitchDetector: Bus '{BusName}' not found.");
                return;
            }

            GD.Print($"PitchDetector: Found bus '{BusName}' at index {busIdx}");

            _capture = (AudioServer.GetBusEffect(busIdx, 0) as AudioEffectCapture);
            if (_capture == null)
            {
                GD.PrintErr($"PitchDetector: AudioEffectCapture not found on bus '{BusName}' at index 0.");
            }
            else
            {
                _capture.ClearBuffer();
                GD.Print("PitchDetector: AudioEffectCapture initialized successfully.");
            }

            _sampleRate = (int)AudioServer.GetMixRate();
            _processBuffer = new float[4096]; // Larger buffer for safety
        }

        public override void _Process(double delta)
        {
            if (_capture == null) return;
            int frames = _capture.GetFramesAvailable();
            if (frames < 1024) return; 

            // 1. Read & Mono Mix
            Vector2[] raw = _capture.GetBuffer(frames);
            // Optimization: Process only the freshest data needed for MinFreq
            int neededLen = Mathf.Min(raw.Length, 2048); 
            
            // Fill buffer from the END of the raw stream (freshest audio)
            int offset = raw.Length - neededLen;
            float maxAmp = 0;
            
            for (int i = 0; i < neededLen; i++)
            {
                float sample = (raw[offset + i].X + raw[offset + i].Y) * 0.5f;
                _processBuffer[i] = sample;
                if (Mathf.Abs(sample) > maxAmp) maxAmp = Mathf.Abs(sample);
            }

            CurrentAmplitude = maxAmp;

            if (maxAmp < AmplitudeThreshold)
            {
                if (IsDetected) 
                {
                   IsDetected = false;
                }
                return;
            }

            // 2. Detect Raw Pitch
            float instantHz = StabilizedAutocorrelation(_processBuffer, neededLen);

            if (instantHz > 0)
            {
                // 3. MEDIAN FILTERING (Kill the Glitches)
                _pitchHistory.Add(instantHz);
                if (_pitchHistory.Count > HistorySize) _pitchHistory.RemoveAt(0);

                // Find Median
                var sorted = _pitchHistory.OrderBy(x => x).ToList();
                float medianHz = sorted[sorted.Count / 2];

                // 4. Smooth the Median
                _smoothHz = Mathf.Lerp(_smoothHz, medianHz, SmoothingSpeed * (float)delta);
                
                // Update Public Data
                CurrentFrequency = _smoothHz;
                UpdateMidiData();
                IsDetected = true;
            }
        }

        private float StabilizedAutocorrelation(float[] buffer, int length)
        {
            int minPeriod = (int)(_sampleRate / MaxFreq);
            int maxPeriod = (int)(_sampleRate / MinFreq);
            int searchLen = length / 2;

            // 1. Calculate Signal Energy for Normalization
            float signalEnergy = 0;
            for (int i = 0; i < searchLen; i++) signalEnergy += buffer[i] * buffer[i];
            if (signalEnergy < 0.0001f) return 0;

            // 2. Find GLOBAL Maximum first
            float globalMaxCorr = 0;
            int globalBestLag = -1;

            for (int lag = minPeriod; lag < maxPeriod; lag++)
            {
                if (lag + searchLen >= length) break;
                
                float dot = 0;
                for(int i=0; i<searchLen; i++) dot += buffer[i] * buffer[i + lag];
                
                float norm = dot / signalEnergy;

                if (norm > globalMaxCorr)
                {
                    globalMaxCorr = norm;
                    globalBestLag = lag;
                }
            }

            // Step B: Re-scan for the "First Strong Peak" (The Anchor)
            float strengthThreshold = globalMaxCorr * 0.85f;
            int finalLag = globalBestLag;

            for (int lag = minPeriod; lag < globalBestLag; lag++)
            {
                if (lag + searchLen >= length) break;
                
                float dot = 0;
                for(int i=0; i<searchLen; i++) dot += buffer[i] * buffer[i + lag];
                float norm = dot / signalEnergy;

                if (norm > strengthThreshold)
                {
                    finalLag = lag;
                    break; 
                }
            }

            if (finalLag > 0)
            {
                return ParabolicInterpolation(buffer, finalLag, searchLen);
            }
            return 0;
        }

        private float ParabolicInterpolation(float[] buffer, int lag, int n)
        {
             // Safety check
            if (lag - 1 < 0 || lag + 1 + n >= buffer.Length) 
                return (float)_sampleRate / lag;

            float prev = 0, curr = 0, next = 0;
            for (int i = 0; i < n; i++)
            {
                prev += buffer[i] * buffer[i + lag - 1];
                curr += buffer[i] * buffer[i + lag];
                next += buffer[i] * buffer[i + lag + 1];
            }

            float denominator = 2 * (2 * curr - prev - next);
            if (Mathf.Abs(denominator) < 0.00001f) return (float)_sampleRate / lag;
            
            float delta = (prev - next) / denominator;
            return (float)_sampleRate / (lag + delta);
        }

        private void UpdateMidiData()
        {
            double midiFloat = 69.0 + 12.0 * Math.Log(CurrentFrequency / 440.0, 2.0);
            CurrentMidiNote = (int)Math.Round(midiFloat);
            CentDeviation = (float)(midiFloat - CurrentMidiNote); 
        }
    }
}
