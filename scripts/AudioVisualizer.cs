using Godot;
using System;
using System.Collections.Generic;

namespace PitchGame
{
    public partial class AudioVisualizer : Control
    {
        [Export] public string BusName = "Record";
        [Export] public int SpectrumResolution = 64;
        [Export] public float MinFreq = 20f;
        [Export] public float MaxFreq = 20000f;
        [Export] public float DecaySpeed = 15.0f; // Higher = faster drop

        private AudioEffectSpectrumAnalyzerInstance _spectrum;
        private float[] _prevValues;
        private readonly float[] _labelFrequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

        public override void _Ready()
        {
            _prevValues = new float[SpectrumResolution];
            int busIndex = AudioServer.GetBusIndex(BusName);
            
            for (int i = 0; i < AudioServer.GetBusEffectCount(busIndex); i++)
            {
                if (AudioServer.GetBusEffectInstance(busIndex, i) is AudioEffectSpectrumAnalyzerInstance inst)
                {
                    _spectrum = inst;
                    break;
                }
            }
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_spectrum == null) return;

            Vector2 size = Size;
            float paddingY = 20f;
            float paddingX = 10f;
            float graphWidth = size.X - (paddingX * 2);
            float graphHeight = size.Y - paddingY; // Reserve space for labels
            float barWidth = graphWidth / SpectrumResolution;

            // 1. Draw Spectrum Bars
            for (int i = 0; i < SpectrumResolution; i++)
            {
                // Logarithmic frequency distribution
                float f1 = MinFreq * Mathf.Pow(MaxFreq / MinFreq, (float)i / SpectrumResolution);
                float f2 = MinFreq * Mathf.Pow(MaxFreq / MinFreq, (float)(i + 1) / SpectrumResolution);

                Vector2 mag = _spectrum.GetMagnitudeForFrequencyRange(f1, f2);
                float energy = (mag.X + mag.Y) / 2.0f;
                float heightTarget = Mathf.Clamp(Mathf.LinearToDb(energy + 0.0001f) + 60, 0, 60) / 60f * graphHeight;

                // Temporal Smoothing (Attack/Release)
                if (heightTarget > _prevValues[i])
                    _prevValues[i] = heightTarget;
                else
                    _prevValues[i] = Mathf.Lerp(_prevValues[i], heightTarget, (float)GetProcessDeltaTime() * DecaySpeed);

                // Draw rounded "pill" bars similar to EasyEffects
                Rect2 barRect = new Rect2(paddingX + i * barWidth, graphHeight - _prevValues[i], barWidth * 0.8f, _prevValues[i]);
                DrawRect(barRect, new Color(0.2f, 0.8f, 0.5f));
            }

            // 2. Draw X-Axis Labels
            Font defaultFont = ThemeDB.GetFallbackFont();
            int fontSize = 12;

            foreach (float freq in _labelFrequencies)
            {
                // Determine horizontal position based on the same log logic used for bars
                float normX = Mathf.Log(freq / MinFreq) / Mathf.Log(MaxFreq / MinFreq);
                float xPos = paddingX + normX * graphWidth;

                string text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString();
                
                // Draw vertical tick
                DrawLine(new Vector2(xPos, graphHeight), new Vector2(xPos, graphHeight + 5), new Color(0.5f, 0.5f, 0.5f));
                
                // Draw Label
                DrawString(defaultFont, new Vector2(xPos - 10, size.Y), text, HorizontalAlignment.Left, -1, fontSize, new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }
}