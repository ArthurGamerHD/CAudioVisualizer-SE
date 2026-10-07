using System;
using System.Collections.Generic;

namespace SeAudioVisualizer.Core
{
    public sealed class SpectrumConfig
    {
        public bool Enabled { get; set; }
        public Color3 Color { get; set; }
        public float Amplitude { get; set; }
        public int BarCount { get; set; }
        public float BarWidth { get; set; }
        public float BarSpacing { get; set; }
        public float Size { get; set; }
        public int BarAngle { get; set; }
        public bool EnablePeakIndicators { get; set; }
        public float PeakDropSpeed { get; set; }
        public float PeakLength { get; set; }
        public float PeakOffset { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public bool UseTimeColor { get; set; }
        public bool UseRealTimeColor { get; set; }
        public bool InvertColor { get; set; }
        public bool EnableFadeTrail { get; set; }
        public float FadeSpeed { get; set; }
        public int TrailLength { get; set; }
        public bool UseFFT { get; set; }

        public SpectrumConfig()
        {
            Enabled = true;
            Color = new Color3(0f, 1f, .8f);
            Amplitude = 30f;
            BarCount = 64;
            BarWidth = 8f;
            BarSpacing = 2f;
            Size = 200f;
            BarAngle = 0;
            EnablePeakIndicators = true;
            PeakDropSpeed = 5f;
            PeakLength = 8f;
            PeakOffset = 5f;
            PositionX = -1;
            PositionY = -1;
            FadeSpeed = .95f;
            TrailLength = 20;
            UseFFT = true;
        }
    }

    public struct SpectrumSegment
    {
        public Point2 Start;
        public Point2 End;
        public Point2 PeakStart;
        public Point2 PeakEnd;
        public bool HasPeak;
    }

    public struct SpectrumFrame
    {
        public SpectrumSegment[] Segments;
        public Color3 Color;
        public float Alpha;
        public float BarWidth;
    }

    public sealed class SpectrumVisualizer : IVisualizer
    {
        readonly SpectrumConfig _config = new SpectrumConfig();
        readonly List<SpectrumFrame> _trailFrames = new List<SpectrumFrame>();
        float[] _audioData = new float[0];
        float[] _fftData = new float[0];
        int _fftLength;
        float[] _peakPositions = new float[0];
        int _width;
        int _height;

        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.Spectrum; } }
        public SpectrumConfig Config { get { return _config; } }
        public bool IsEnabled { get { return _config.Enabled; } set { _config.Enabled = value; } }

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            if (_config.PositionX == -1) _config.PositionX = width / 2;
            if (_config.PositionY == -1) _config.PositionY = height / 2;
        }

        public void Update(float[] audioData, float[] fftData, int fftLength, double deltaTime)
        {
            _audioData = audioData;
            _fftData = fftData;
            _fftLength = fftLength;
        }

        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds)
        {
            if (!IsEnabled) return;
            if (_config.EnableFadeTrail)
            {
                _trailFrames.Insert(0, GenerateCurrentSpectrum(playbackTimeSeconds));
                for (int i = _trailFrames.Count - 1; i >= 0; i--)
                {
                    SpectrumFrame frame = _trailFrames[i];
                    frame.Alpha *= _config.FadeSpeed;
                    if (frame.Alpha < .01f || i >= _config.TrailLength) _trailFrames.RemoveAt(i);
                    else _trailFrames[i] = frame;
                }
                for (int i = _trailFrames.Count - 1; i >= 0; i--) RenderFrame(rasterizer, _trailFrames[i]);
            }
            else RenderFrame(rasterizer, GenerateCurrentSpectrum(playbackTimeSeconds));
        }

        SpectrumFrame GenerateCurrentSpectrum(double playbackTimeSeconds)
        {
            float[] dataSource = _config.UseFFT ? _fftData : _audioData;
            int sourceLength = _config.UseFFT ? _fftLength : dataSource.Length;
            int count = Math.Max(1, _config.BarCount);
            var segments = new SpectrumSegment[count];
            Color3 color = _config.UseTimeColor ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color);
            if (_config.InvertColor) color = TimeColorHelper.InvertColor(color);
            if (sourceLength <= 0)
                return new SpectrumFrame { Segments = segments, Color = color, Alpha = 1f, BarWidth = _config.BarWidth };

            if (_peakPositions.Length != count) _peakPositions = new float[count];
            float distributedSize = _config.Size / (count * 2f);
            float adjustedBarWidth = _config.BarWidth + _config.BarWidth * distributedSize;
            float adjustedBarSpacing = _config.BarSpacing + _config.BarSpacing * distributedSize;
            float w = count * (adjustedBarWidth + adjustedBarSpacing);
            float arcAngle = _config.BarAngle / 180f * (float)Math.PI;
            float r = Math.Abs(arcAngle) < 1e-3f ? 0f : w / arcAngle;
            Point2 center = new Point2(_config.PositionX, _config.PositionY + r);
            float thetaStart = (float)Math.PI / 2f + arcAngle / 2f;

            for (int i = 0; i < count; i++)
            {
                int sourceIndex = (int)(i * (sourceLength / (float)count));
                if (sourceIndex >= sourceLength) sourceIndex = sourceLength - 1;
                float value = dataSource[sourceIndex] * _config.Amplitude;
                if (_config.EnablePeakIndicators)
                {
                    _peakPositions[i] -= _config.PeakDropSpeed;
                    _peakPositions[i] = Math.Max(_peakPositions[i], value);
                    float maxPeak = Math.Max(_height * 2f, _width);
                    if (_peakPositions[i] < 0f) _peakPositions[i] = 0f;
                    if (_peakPositions[i] > maxPeak) _peakPositions[i] = maxPeak;
                }

                float theta = thetaStart - arcAngle * i / count;
                Point2 start;
                if (Math.Abs(arcAngle) < 1e-3f)
                {
                    float ix = count <= 1 ? .5f : i / (float)(count - 1);
                    start = new Point2(_config.PositionX - w / 2f + ix * w, _config.PositionY);
                }
                else
                {
                    start = new Point2(center.X + r * (float)Math.Cos(theta), center.Y - r * (float)Math.Sin(theta));
                }
                float dirX = (float)Math.Cos(theta);
                float dirY = -(float)Math.Sin(theta);
                float len = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                if (len > 0f) { dirX /= len; dirY /= len; }
                Point2 end = new Point2(start.X + dirX * value, start.Y + dirY * value);
                SpectrumSegment seg = new SpectrumSegment { Start = start, End = end };
                if (_config.EnablePeakIndicators)
                {
                    seg.HasPeak = true;
                    seg.PeakStart = new Point2(start.X + dirX * (_peakPositions[i] + _config.PeakOffset), start.Y + dirY * (_peakPositions[i] + _config.PeakOffset));
                    seg.PeakEnd = new Point2(seg.PeakStart.X + dirX * _config.PeakLength, seg.PeakStart.Y + dirY * _config.PeakLength);
                }
                segments[i] = seg;
            }

            return new SpectrumFrame { Segments = segments, Color = color, Alpha = 1f, BarWidth = adjustedBarWidth };
        }

        static void RenderFrame(SoftwareRasterizer rasterizer, SpectrumFrame frame)
        {
            if (frame.Segments == null) return;
            for (int i = 0; i < frame.Segments.Length; i++)
            {
                SpectrumSegment s = frame.Segments[i];
                rasterizer.DrawLine(s.Start.X, s.Start.Y, s.End.X, s.End.Y, frame.Color, frame.Alpha, frame.BarWidth);
                if (s.HasPeak)
                    rasterizer.DrawLine(s.PeakStart.X, s.PeakStart.Y, s.PeakEnd.X, s.PeakEnd.Y, frame.Color, frame.Alpha, Math.Max(1f, frame.BarWidth * .5f));
            }
        }
    }
}
