using System;
using System.Collections.Generic;

namespace SeAudioVisualizer.Core
{
    public struct TriangleFrame
    {
        public Point2[] Vertices;
        public Color3 Color;
        public float Alpha;
        public bool Filled;
        public float LineWidth;
    }

    public sealed class TriangleConfig
    {
        public bool Enabled { get; set; }
        public Color3 Color { get; set; }
        public float BaseSize { get; set; }
        public float Amplitude { get; set; }
        public int RotationSpeed { get; set; }
        public int CurrentAngle { get; set; }
        public bool Filled { get; set; }
        public float LineThickness { get; set; }
        public float Sensitivity { get; set; }
        public bool UseTimeColor { get; set; }
        public bool UseRealTimeColor { get; set; }
        public bool InvertColor { get; set; }
        public bool EnableFadeTrail { get; set; }
        public float FadeSpeed { get; set; }
        public int TrailLength { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public bool UseFFT { get; set; }

        public TriangleConfig()
        {
            Enabled = true;
            Color = new Color3(0f, 1f, 1f);
            BaseSize = 50f;
            Amplitude = 300f;
            RotationSpeed = 0;
            CurrentAngle = 0;
            Filled = false;
            LineThickness = 2f;
            Sensitivity = 3f;
            EnableFadeTrail = false;
            FadeSpeed = .95f;
            TrailLength = 20;
            PositionX = -1;
            PositionY = -1;
            UseFFT = false;
        }
    }

    public sealed class TriangleVisualizer : IVisualizer
    {
        readonly TriangleConfig _config = new TriangleConfig();
        readonly List<TriangleFrame> _trailFrames = new List<TriangleFrame>();
        float[] _audioData = new float[0];
        float[] _fftData = new float[0];
        int _fftLength;
        float _currentRotation;

        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.Triangle; } }
        public TriangleConfig Config { get { return _config; } }
        public bool IsEnabled { get { return _config.Enabled; } set { _config.Enabled = value; } }

        public void Initialize(int width, int height)
        {
            if (_config.PositionX == -1) _config.PositionX = width / 2;
            if (_config.PositionY == -1) _config.PositionY = height / 2;
        }

        public void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime)
        {
            _audioData = waveformData;
            _fftData = fftData;
            _fftLength = fftLength;
            _currentRotation += _config.RotationSpeed;
            if (_currentRotation >= 360f) _currentRotation -= 360f;
            if (_currentRotation < 0f) _currentRotation += 360f;
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
                RenderFrame(rasterizer, GenerateCurrentTriangle(playbackTimeSeconds));
            }
        }

        void UpdateTrailFrames(double playbackTimeSeconds)
        {
            _trailFrames.Insert(0, GenerateCurrentTriangle(playbackTimeSeconds));
            for (int i = _trailFrames.Count - 1; i >= 0; i--)
            {
                TriangleFrame frame = _trailFrames[i];
                frame.Alpha *= _config.FadeSpeed;
                if (frame.Alpha < .01f || i >= _config.TrailLength)
                    _trailFrames.RemoveAt(i);
                else
                    _trailFrames[i] = frame;
            }
        }

        TriangleFrame GenerateCurrentTriangle(double playbackTimeSeconds)
        {
            float[] dataSource = _config.UseFFT ? _fftData : _audioData;
            int length = _config.UseFFT ? _fftLength : dataSource.Length;
            float rms = 0f;
            if (length > 0)
            {
                float sum = 0f;
                for (int i = 0; i < length; i++) sum += dataSource[i] * dataSource[i];
                rms = (float)Math.Sqrt(sum / length);
            }
            float amplifiedRms = (float)Math.Pow(rms * _config.Sensitivity, 1.5);
            float triangleSize = _config.BaseSize + amplifiedRms * _config.Amplitude;
            triangleSize = Math.Max(triangleSize, _config.BaseSize * .3f);
            float totalRotation = _config.CurrentAngle + _currentRotation;
            Color3 color = _config.UseTimeColor ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color);
            if (_config.InvertColor) color = TimeColorHelper.InvertColor(color);
            Point2[] vertices = new Point2[3];
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + totalRotation) * (float)(Math.PI / 180.0);
                vertices[i] = new Point2(
                    _config.PositionX + (float)Math.Cos(angle) * triangleSize,
                    _config.PositionY + (float)Math.Sin(angle) * triangleSize);
            }
            return new TriangleFrame { Vertices = vertices, Color = color, Alpha = 1f, Filled = _config.Filled, LineWidth = _config.LineThickness };
        }

        static void RenderFrame(SoftwareRasterizer rasterizer, TriangleFrame frame)
        {
            if (frame.Vertices == null || frame.Vertices.Length != 3) return;
            rasterizer.DrawTriangle(frame.Vertices[0], frame.Vertices[1], frame.Vertices[2], frame.Color, frame.Alpha, frame.Filled, frame.LineWidth);
        }
    }
}
