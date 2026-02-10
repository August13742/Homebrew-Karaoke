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
        [Export] public float MaxFreq = 1200f;
        
        [ExportGroup("Filtering")]
        [Export] public float FilterCutoffHz = 2000f; 

        [ExportGroup("Stabilization")]
        [Export] public float SmoothingSpeed = 15.0f;
        [Export] public int HistorySize = 5;

        [ExportGroup("Karaoke/Scoring Features")]
        [Export] public float ConfidenceThreshold = 0.15f; // YIN-style: lower = stricter
        [Export] public bool EnableOnsetDetection = true;
        [Export] public float OnsetThresholdDb = -40f; // dB threshold for note onset
        [Export] public float OffsetHoldTime = 0.08f; // Seconds to hold note after amplitude drops
        
        [ExportGroup("Quantization (for Scoring)")]
        [Export] public bool QuantizeToSemitone = false; // Enable for karaoke-style scoring
        [Export] public float PerfectCentWindow = 25f;   // ±25 cents = "perfect"
        [Export] public float GoodCentWindow = 50f;      // ±50 cents = "good"

        // --- PUBLIC DATA ---
        public float CurrentFrequency { get; private set; } 
        public float RawFrequency { get; private set; } // Unsmoothed, for analysis
        public int CurrentMidiNote { get; private set; } 
        public float CentDeviation { get; private set; }
        public float CurrentAmplitude { get; private set; }
        public float CurrentAmplitudeDb { get; private set; }
        public bool IsDetected { get; private set; }
        
        // --- KARAOKE SCORING DATA ---
        public float Confidence { get; private set; } // 0-1, higher = more reliable detection
        public bool IsNoteOnset { get; private set; }  // True on the frame a note begins
        public bool IsNoteOffset { get; private set; } // True on the frame a note ends
        public PitchAccuracy GetAccuracy(int targetMidiNote) => EvaluateAccuracy(targetMidiNote);

        private AudioEffectCapture _capture;
        private int _sampleRate;
        
        // Stabilizers
        private float _smoothHz;
        private readonly List<float> _pitchHistory = new List<float>();

        private float[] _processBuffer; 
        private float[] _yinBuffer; // YIN difference function buffer
        
        // Filter state
        private float _lowPassState = 0f;
        
        // Onset/Offset state
        private bool _wasDetected = false;
        private float _offsetHoldTimer = 0f;
        private float _lastConfidence = 0f;

        public override void _Ready()
        {
            int busIdx = AudioServer.GetBusIndex(BusName);
            GD.Print($"PitchDetector: Initializing on bus '{BusName}' (Index: {busIdx})");

            if (busIdx == -1)
            {
                GD.PrintErr($"PitchDetector: Bus '{BusName}' not found.");
                return;
            }

            _capture = (AudioServer.GetBusEffect(busIdx, 0) as AudioEffectCapture);
            if (_capture == null)
            {
                GD.PrintErr($"PitchDetector: AudioEffectCapture not found on bus '{BusName}' at index 0.");
            }
            else
            {
                GD.Print("PitchDetector: Successfully found AudioEffectCapture.");
                _capture.ClearBuffer();
            }

            _sampleRate = (int)AudioServer.GetMixRate();
            _processBuffer = new float[4096];
            _yinBuffer = new float[2048]; // For YIN difference function
        }

        public override void _Process(double delta)
        {
            if (_capture == null) return;
            int frames = _capture.GetFramesAvailable();
            
            if (frames == 0) return;

            if (frames < 512) return; 

            // 1. Read & Mono Mix
            Vector2[] raw = _capture.GetBuffer(frames);

            // Optimization: Process only the freshest data
            int neededLen = Mathf.Min(raw.Length, 2048); 
            
            // Fill buffer from the END of the raw stream (freshest audio)
            int offset = raw.Length - neededLen;
            float maxAmp = 0; // Main variable for processing logic
            
            // Calculate Filter Coefficient (One-pole Lowpass)
            float rc = 1.0f / (Mathf.Tau * FilterCutoffHz);
            float dt = 1.0f / _sampleRate;
            float alpha = dt / (rc + dt);
            
            for (int i = 0; i < neededLen; i++)
            {
                float rawSample = (raw[offset + i].X + raw[offset + i].Y) * 0.5f;
                
                // Apply Low Pass: Output = Previous + Alpha * (Input - Previous)
                _lowPassState = _lowPassState + alpha * (rawSample - _lowPassState);
                
                _processBuffer[i] = _lowPassState; // Use the FILTERED sample
                
                if (Mathf.Abs(rawSample) > maxAmp) maxAmp = Mathf.Abs(rawSample);
            }

            CurrentAmplitude = maxAmp;
            CurrentAmplitudeDb = maxAmp > 0 ? 20f * Mathf.Log(maxAmp) / Mathf.Log(10) : -100f;

            // Onset/Offset Detection
            IsNoteOnset = false;
            IsNoteOffset = false;

            if (maxAmp < AmplitudeThreshold)
            {
                if (EnableOnsetDetection && _offsetHoldTimer > 0)
                {
                    _offsetHoldTimer -= (float)delta;
                    if (_offsetHoldTimer <= 0 && _wasDetected)
                    {
                        IsNoteOffset = true;
                        _wasDetected = false;
                        IsDetected = false;
                    }
                    return;
                }
                
                if (IsDetected && !EnableOnsetDetection) 
                {
                    IsDetected = false;
                }
                return;
            }
            
            _offsetHoldTimer = OffsetHoldTime; // Reset hold timer when sound is present

            // 2. Detect Raw Pitch using YIN-inspired algorithm
            float confidence;
            float instantHz = YinPitchDetection(_processBuffer, neededLen, out confidence);
            Confidence = confidence;

            // Reject low-confidence detections (key difference from basic autocorrelation)
            if (instantHz > 0 && confidence > (1f - ConfidenceThreshold))
            {
                RawFrequency = instantHz;
                
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
                
                // Onset detection
                if (EnableOnsetDetection && !_wasDetected)
                {
                    IsNoteOnset = true;
                    _wasDetected = true;
                }
                
                IsDetected = true;
            }
            else if (instantHz > 0)
            {
                // Low confidence - keep previous value but mark as less reliable
                Confidence = confidence;
            }
        }

        /// <summary>
        /// YIN-inspired pitch detection algorithm.
        /// Key improvements over basic autocorrelation:
        /// 1. Cumulative Mean Normalized Difference Function (CMND) - better octave detection
        /// 2. Absolute threshold for first minimum - reduces octave errors
        /// 3. Confidence output - know when to trust the result
        /// </summary>
        private float YinPitchDetection(float[] buffer, int length, out float confidence)
        {
            int minPeriod = (int)(_sampleRate / MaxFreq);
            int maxPeriod = Mathf.Min((int)(_sampleRate / MinFreq), length / 2 - 1);
            int W = length / 2; // Analysis window

            confidence = 0f;
            
            if (maxPeriod <= minPeriod || W < maxPeriod) 
                return 0;

            // Step 1: Difference Function d(tau)
            // d(tau) = sum of (x[j] - x[j+tau])^2 for j in window
            for (int tau = 0; tau < maxPeriod; tau++)
            {
                float sum = 0;
                for (int j = 0; j < W; j++)
                {
                    float delta = buffer[j] - buffer[j + tau];
                    sum += delta * delta;
                }
                _yinBuffer[tau] = sum;
            }

            // Step 2: Cumulative Mean Normalized Difference Function d'(tau)
            // d'(0) = 1, d'(tau) = d(tau) / ((1/tau) * sum(d(j)) for j=1 to tau)
            // This normalization is KEY to avoiding octave errors
            _yinBuffer[0] = 1f;
            float runningSum = 0;
            for (int tau = 1; tau < maxPeriod; tau++)
            {
                runningSum += _yinBuffer[tau];
                _yinBuffer[tau] = _yinBuffer[tau] * tau / runningSum;
            }

            // Step 3: Absolute Threshold
            // Find the FIRST tau where d'(tau) < threshold (typically 0.1-0.2)
            // This is the key difference from autocorrelation - we want the FIRST good minimum
            int bestTau = -1;
            for (int tau = minPeriod; tau < maxPeriod; tau++)
            {
                if (_yinBuffer[tau] < ConfidenceThreshold)
                {
                    // Find local minimum
                    while (tau + 1 < maxPeriod && _yinBuffer[tau + 1] < _yinBuffer[tau])
                        tau++;
                    bestTau = tau;
                    break;
                }
            }

            // Fallback: If no value below threshold, find global minimum
            if (bestTau < 0)
            {
                float minVal = float.MaxValue;
                for (int tau = minPeriod; tau < maxPeriod; tau++)
                {
                    if (_yinBuffer[tau] < minVal)
                    {
                        minVal = _yinBuffer[tau];
                        bestTau = tau;
                    }
                }
            }

            if (bestTau < 0) return 0;

            // Step 4: Parabolic Interpolation for sub-sample accuracy
            float refinedTau = ParabolicInterpolationYin(bestTau, maxPeriod);
            
            // Confidence = 1 - d'(tau), clamped to 0-1
            confidence = Mathf.Clamp(1f - _yinBuffer[bestTau], 0f, 1f);
            
            return (float)_sampleRate / refinedTau;
        }
        
        private float ParabolicInterpolationYin(int tau, int maxTau)
        {
            if (tau < 1 || tau >= maxTau - 1)
                return tau;
                
            float s0 = _yinBuffer[tau - 1];
            float s1 = _yinBuffer[tau];
            float s2 = _yinBuffer[tau + 1];
            
            float denominator = 2f * (2f * s1 - s0 - s2);
            if (Mathf.Abs(denominator) < 0.0001f)
                return tau;
                
            return tau + (s0 - s2) / denominator;
        }

        // Keep the old method for reference/comparison
        private float StabilizedAutocorrelation(float[] buffer, int length)
        {
            int minPeriod = (int)(_sampleRate / MaxFreq);
            int maxPeriod = (int)(_sampleRate / MinFreq);
            int searchLen = length / 2;

            float signalEnergy = 0;
            for (int i = 0; i < searchLen; i++) signalEnergy += buffer[i] * buffer[i];
            if (signalEnergy < 0.0001f) return 0;

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
            CentDeviation = (float)((midiFloat - CurrentMidiNote) * 100.0); // Now in actual cents (-50 to +50)
        }
        
        /// <summary>
        /// Evaluate pitch accuracy. Allows octave transposition (e.g., singing C3 for C4 is OK).
        /// </summary>
        public PitchAccuracy EvaluateAccuracy(float targetMidiNote)
        {
            if (!IsDetected)
                return PitchAccuracy.Silent;

            // 1. Calculate the raw semitone difference including cents
            float currentPitchFloat = CurrentMidiNote + (CentDeviation / 100f);
            float diff = currentPitchFloat - targetMidiNote;

            // 2. Handle Octave Equivalence (Modulo 12)
            // We want the difference to be within -6 to +6 range regardless of octave
            // e.g. If target is 60 and I sing 48 (diff -12), this should become 0.
            
            // Wrap to +/- 6 semitones
            while (diff > 6.0f) diff -= 12.0f;
            while (diff < -6.0f) diff += 12.0f;

            // 3. Convert back to absolute cents for scoring
            float absCentsError = Mathf.Abs(diff * 100f);

            if (absCentsError <= PerfectCentWindow)
                return PitchAccuracy.Perfect;
            else if (absCentsError <= GoodCentWindow)
                return PitchAccuracy.Good;
            else if (absCentsError <= 100f) // Within a semitone
                return PitchAccuracy.Ok;
            else
                return PitchAccuracy.Miss;
        }
    }
    
    /// <summary>
    /// Accuracy levels for karaoke-style scoring.
    /// </summary>
    public enum PitchAccuracy
    {
        Silent,   // No sound detected
        Miss,     // More than a semitone off
        Ok,       // Within a semitone
        Good,     // Within ±50 cents
        Perfect   // Within ±25 cents
    }
}
