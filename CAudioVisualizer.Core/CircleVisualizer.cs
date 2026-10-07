using System;
using System.Collections.Generic;

namespace SeAudioVisualizer.Core
{
    public struct CircleFrame
    {
        public Point2[] DotPositions;
        public Color3 Color;
        public float Alpha;
        public float DotSize;
    }

    public sealed class CircleConfig
    {
        public bool Enabled { get; set; }
        public Color3 Color { get; set; }
        public float CircleSize { get; set; }
        public int DotsMin { get; set; }
        public int DotsMax { get; set; }
        public float DotSize { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public float Sensitivity { get; set; }
        public bool UseTimeColor { get; set; }
        public bool UseRealTimeColor { get; set; }
        public bool InvertColor { get; set; }
        public bool EnableFadeTrail { get; set; }
        public float FadeSpeed { get; set; }
        public int TrailLength { get; set; }
        public bool UseFFT { get; set; }

        public CircleConfig()
        {
            Enabled = true;
            Color = new Color3(0f, 1f, 1f);
            CircleSize = 75f;
            DotsMin = 300;
            DotsMax = 500;
            DotSize = 2f;
            PositionX = -1;
            PositionY = -1;
            Sensitivity = 1f;
            EnableFadeTrail = false;
            FadeSpeed = .95f;
            TrailLength = 20;
            UseFFT = false;
        }
    }

    public sealed class CircleVisualizer : IVisualizer
    {
        readonly CircleConfig _config = new CircleConfig();
        readonly List<CircleFrame> _trailFrames = new List<CircleFrame>();
        Random _random = new Random(12345);
        float[] _audioData = new float[0];
        float[] _fftData = new float[0];
        int _fftLength;

        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.Circle; } }
        public CircleConfig Config { get { return _config; } }
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
        }

        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds)
        {
            if (!IsEnabled) return;
            if (_config.EnableFadeTrail)
            {
                _trailFrames.Insert(0, GenerateCurrentCircle(playbackTimeSeconds));
                for (int i = _trailFrames.Count - 1; i >= 0; i--)
                {
                    CircleFrame frame = _trailFrames[i];
                    frame.Alpha *= _config.FadeSpeed;
                    if (frame.Alpha < .01f || i >= _config.TrailLength) _trailFrames.RemoveAt(i);
                    else _trailFrames[i] = frame;
                }
                for (int i = _trailFrames.Count - 1; i >= 0; i--) RenderFrame(rasterizer, _trailFrames[i]);
            }
            else RenderFrame(rasterizer, GenerateCurrentCircle(playbackTimeSeconds));
        }

        CircleFrame GenerateCurrentCircle(double playbackTimeSeconds)
        {
            float[] dataSource = _config.UseFFT ? _fftData : _audioData;
            int sourceLength = _config.UseFFT ? _fftLength : dataSource.Length;
            if (sourceLength <= 0)
                return new CircleFrame { DotPositions = new Point2[0], Color = _config.Color, Alpha = 1f, DotSize = _config.DotSize };

            int dots = _random.Next(_config.DotsMin, _config.DotsMax + 1);
            _random = new Random(12345);
            if ((dots & 1) != 0) dots++;
            Point2[] positions = new Point2[dots];
            int p = 0;
            Color3 color = _config.UseTimeColor ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color);
            if (_config.InvertColor) color = TimeColorHelper.InvertColor(color);

            for (int i = 1; i <= dots; i += 2)
            {
                int dataIndex = _random.Next(0, sourceLength);
                float fft = Math.Abs(dataSource[dataIndex]);
                float rootRootFft = _config.CircleSize * (float)Math.Sqrt(Math.Sqrt(fft * 100000f * _config.Sensitivity));
                float angle = (float)(-Math.PI * ((float)i / dots));
                positions[p++] = new Point2(_config.PositionX + (float)Math.Cos(angle) * rootRootFft, _config.PositionY + (float)Math.Sin(angle) * rootRootFft);
            }
            for (int i = 2; i <= dots; i += 2)
            {
                int dataIndex = _random.Next(0, sourceLength);
                float fft = Math.Abs(dataSource[dataIndex]);
                float rootRootFft = _config.CircleSize * (float)Math.Sqrt(Math.Sqrt(fft * 100000f * _config.Sensitivity));
                float angle = (float)(Math.PI * ((float)i / dots));
                positions[p++] = new Point2(_config.PositionX + (float)Math.Cos(angle) * rootRootFft, _config.PositionY + (float)Math.Sin(angle) * rootRootFft);
            }
            return new CircleFrame { DotPositions = positions, Color = color, Alpha = 1f, DotSize = _config.DotSize };
        }

        static void RenderFrame(SoftwareRasterizer rasterizer, CircleFrame frame)
        {
            if (frame.DotPositions == null) return;
            float radius = Math.Max(.5f, frame.DotSize * .5f);
            for (int i = 0; i < frame.DotPositions.Length; i++)
                rasterizer.DrawDisc(frame.DotPositions[i].X, frame.DotPositions[i].Y, radius, frame.Color, frame.Alpha);
        }
    }
}
