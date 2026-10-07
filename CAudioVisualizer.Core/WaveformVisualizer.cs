using System;
using System.Collections.Generic;

namespace SeAudioVisualizer.Core
{
    public struct WaveformFrame
    {
        public Point2[] Points;
        public Color3 Color;
        public float Alpha;
        public float LineWidth;
    }

    public sealed class WaveformConfig
    {
        public bool Enabled { get; set; }
        public Color3 Color { get; set; }
        public float Amplitude { get; set; }
        public int PositionY { get; set; }
        public float LineThickness { get; set; }
        public int StartX { get; set; }
        public int EndX { get; set; }
        public bool UseTimeColor { get; set; }
        public bool UseRealTimeColor { get; set; }
        public bool InvertColor { get; set; }
        public float PositionX { get; set; }
        public bool EnableFadeTrail { get; set; }
        public float FadeSpeed { get; set; }
        public int TrailLength { get; set; }
        public bool UseFFT { get; set; }
        public bool MirrorH { get; set; }
        public bool MirrorV { get; set; }

        public WaveformConfig()
        {
            Enabled = true;
            Color = new Color3(0f, 1f, 0f);
            Amplitude = 1f;
            PositionY = -1;
            // Match the repository config.json defaults used by the original app.
            LineThickness = 1f;
            StartX = 0;
            EndX = -1;
            UseTimeColor = false;
            UseRealTimeColor = true;
            PositionX = .5f;
            EnableFadeTrail = true;
            FadeSpeed = .9f;
            TrailLength = 50;
            UseFFT = false;
        }
    }

    public sealed class WaveformVisualizer : IVisualizer
    {
        readonly WaveformConfig _config = new WaveformConfig();
        readonly List<WaveformFrame> _trailFrames = new List<WaveformFrame>();
        float[] _audioData = new float[0];
        float[] _fftData = new float[0];
        int _fftLength;
        int _width;
        int _height;

        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.Waveform; } }
        public WaveformConfig Config { get { return _config; } }
        public bool IsEnabled { get { return _config.Enabled; } set { _config.Enabled = value; } }

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            if (_config.PositionY == -1) _config.PositionY = height / 2;
            if (_config.EndX == -1) _config.EndX = width;
        }

        public void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime)
        {
            _audioData = waveformData;
            _fftData = fftData;
            _fftLength = fftLength;
        }

        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds)
        {
            if (!IsEnabled) return;
            if (_config.EnableFadeTrail)
            {
                UpdateTrailFrames(playbackTimeSeconds);
                for (int i = _trailFrames.Count - 1; i >= 0; i--)
                    RenderFrame(rasterizer, _trailFrames[i]);
            }
            else
            {
                RenderFrame(rasterizer, GenerateCurrentWaveform(playbackTimeSeconds));
            }
        }

        void UpdateTrailFrames(double playbackTimeSeconds)
        {
            _trailFrames.Insert(0, GenerateCurrentWaveform(playbackTimeSeconds));
            for (int i = _trailFrames.Count - 1; i >= 0; i--)
            {
                WaveformFrame frame = _trailFrames[i];
                frame.Alpha *= _config.FadeSpeed;
                if (frame.Alpha < .01f || i >= _config.TrailLength)
                    _trailFrames.RemoveAt(i);
                else
                    _trailFrames[i] = frame;
            }
        }

        WaveformFrame GenerateCurrentWaveform(double playbackTimeSeconds)
        {
            float[] dataSource = _config.UseFFT ? _fftData : _audioData;
            int sourceLength = _config.UseFFT ? _fftLength : dataSource.Length;
            if (sourceLength <= 0)
                return new WaveformFrame { Points = new Point2[0], Color = _config.Color, Alpha = 1f, LineWidth = _config.LineThickness };

            float centerY = _config.PositionY;
            float startPixel = _config.StartX;
            float endPixel = _config.EndX < 0 ? _width : _config.EndX;
            int waveformWidth = Math.Max(0, (int)(endPixel - startPixel));
            var points = new Point2[waveformWidth];
            Color3 color = _config.UseTimeColor ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color);
            if (_config.InvertColor) color = TimeColorHelper.InvertColor(color);

            for (int x = 0; x < waveformWidth; x++)
            {
                int actualX = _config.MirrorH ? waveformWidth - 1 - x : x;
                float exactIndex = actualX * (sourceLength / (float)waveformWidth);
                int sampleIndex = (int)exactIndex;
                if (sampleIndex >= sourceLength) sampleIndex = sourceLength - 1;
                // Upstream draws into its OpenGL projection while this port consumes
                // normalized PCM [-1,+1] directly into pixel coordinates. Convert the
                // normalized sample to a useful pixel displacement before applying the
                // original Amplitude multiplier.
                float scaledSample = dataSource[sampleIndex] * _config.Amplitude;
                if (_config.MirrorV) scaledSample = -scaledSample;
                float y = centerY - scaledSample;
                points[x] = new Point2(startPixel + x, y);
            }

            return new WaveformFrame { Points = points, Color = color, Alpha = 1f, LineWidth = _config.LineThickness };
        }

        static void RenderFrame(SoftwareRasterizer rasterizer, WaveformFrame frame)
        {
            if (frame.Points == null || frame.Points.Length < 2) return;
            for (int i = 1; i < frame.Points.Length; i++)
                rasterizer.DrawLine(frame.Points[i - 1].X, frame.Points[i - 1].Y, frame.Points[i].X, frame.Points[i].Y, frame.Color, frame.Alpha, frame.LineWidth);
        }
    }
}
