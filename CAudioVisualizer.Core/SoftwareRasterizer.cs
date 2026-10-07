using System;

namespace SeAudioVisualizer.Core
{
    public sealed class SoftwareRasterizer
    {
        readonly int _width;
        readonly int _height;
        byte[] _pixels;

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        public SoftwareRasterizer(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public void Bind(byte[] pixels)
        {
            if (pixels == null || pixels.Length < _width * _height * 4)
                throw new ArgumentException("Pixel buffer is too small.", "pixels");
            _pixels = pixels;
        }

        public void Clear(Color3 color)
        {
            byte r = ToByte(color.X);
            byte g = ToByte(color.Y);
            byte b = ToByte(color.Z);
            int p = 0;
            int count = _width * _height;
            for (int i = 0; i < count; i++)
            {
                _pixels[p++] = r;
                _pixels[p++] = g;
                _pixels[p++] = b;
                _pixels[p++] = 255;
            }
        }

        /// <summary>Clears every pixel to fully transparent, for a visualizer drawn over other content.</summary>
        public void ClearTransparent()
        {
            Array.Clear(_pixels, 0, _width * _height * 4);
        }

        public void SetPixel(int x, int y, Color3 color, float alpha)
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height || alpha <= 0f)
                return;
            if (alpha > 1f) alpha = 1f;
            int p = (y * _width + x) * 4;
            if (alpha >= .999f)
            {
                _pixels[p] = ToByte(color.X);
                _pixels[p + 1] = ToByte(color.Y);
                _pixels[p + 2] = ToByte(color.Z);
                _pixels[p + 3] = 255;
                return;
            }

            // Straight-alpha "over": identical to a plain blend over an opaque pixel, and builds up coverage over a
            // transparent one.
            float below = _pixels[p + 3] / 255f * (1f - alpha);
            float coverage = alpha + below;
            _pixels[p] = (byte)((ToByte(color.X) * alpha + _pixels[p] * below) / coverage);
            _pixels[p + 1] = (byte)((ToByte(color.Y) * alpha + _pixels[p + 1] * below) / coverage);
            _pixels[p + 2] = (byte)((ToByte(color.Z) * alpha + _pixels[p + 2] * below) / coverage);
            _pixels[p + 3] = (byte)(coverage * 255f + .5f);
        }

        public void DrawLine(float x0f, float y0f, float x1f, float y1f, Color3 color, float alpha, float thickness)
        {
            int x0 = (int)Math.Round(x0f);
            int y0 = (int)Math.Round(y0f);
            int x1 = (int)Math.Round(x1f);
            int y1 = (int)Math.Round(y1f);
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int radius = Math.Max(0, (int)(thickness * .5f));

            while (true)
            {
                DrawDisc(x0, y0, radius, color, alpha);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void DrawDisc(float xf, float yf, float radiusf, Color3 color, float alpha)
        {
            int cx = (int)Math.Round(xf);
            int cy = (int)Math.Round(yf);
            int radius = Math.Max(1, (int)Math.Ceiling(radiusf));
            int rr = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= rr)
                        SetPixel(cx + x, cy + y, color, alpha);
        }

        public void DrawTriangle(Point2 a, Point2 b, Point2 c, Color3 color, float alpha, bool filled, float thickness)
        {
            if (!filled)
            {
                DrawLine(a.X, a.Y, b.X, b.Y, color, alpha, thickness);
                DrawLine(b.X, b.Y, c.X, c.Y, color, alpha, thickness);
                DrawLine(c.X, c.Y, a.X, a.Y, color, alpha, thickness);
                return;
            }

            float minXf = Math.Min(a.X, Math.Min(b.X, c.X));
            float maxXf = Math.Max(a.X, Math.Max(b.X, c.X));
            float minYf = Math.Min(a.Y, Math.Min(b.Y, c.Y));
            float maxYf = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            int minX = Math.Max(0, (int)Math.Floor(minXf));
            int maxX = Math.Min(_width - 1, (int)Math.Ceiling(maxXf));
            int minY = Math.Max(0, (int)Math.Floor(minYf));
            int maxY = Math.Min(_height - 1, (int)Math.Ceiling(maxYf));
            float area = Edge(a, b, c.X, c.Y);
            if (Math.Abs(area) < .00001f) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w0 = Edge(b, c, x, y);
                    float w1 = Edge(c, a, x, y);
                    float w2 = Edge(a, b, x, y);
                    bool inside = area > 0f ? (w0 >= 0f && w1 >= 0f && w2 >= 0f) : (w0 <= 0f && w1 <= 0f && w2 <= 0f);
                    if (inside) SetPixel(x, y, color, alpha);
                }
            }
        }

        static float Edge(Point2 a, Point2 b, float x, float y)
        {
            return (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
        }

        public void DrawGradient(Color3 color1, Color3 color2, BackgroundType type, GradientType gradientType, bool invert, float audioMix, bool audioReactive)
        {
            for (int y = 0; y < _height; y++)
            {
                float v = _height <= 1 ? 0f : y / (float)(_height - 1);
                for (int x = 0; x < _width; x++)
                {
                    float u = _width <= 1 ? 0f : x / (float)(_width - 1);
                    float t;
                    if (type == BackgroundType.Solid)
                    {
                        t = audioReactive ? audioMix : 0f;
                    }
                    else
                    {
                        if (gradientType == GradientType.Horizontal) t = u;
                        else if (gradientType == GradientType.Vertical) t = v;
                        else
                        {
                            float dx = u - .5f;
                            float dy = v - .5f;
                            t = (float)Math.Sqrt(dx * dx + dy * dy) / 0.70710678f;
                            if (t > 1f) t = 1f;
                        }
                        if (invert) t = 1f - t;
                        if (audioReactive)
                        {
                            float threshold = 1f - audioMix;
                            t = SmoothStep(threshold - .1f, threshold + .5f, t);
                        }
                        else
                        {
                            t = SmoothStep(0f, 1f, t);
                        }
                    }
                    SetPixel(x, y, Lerp(color1, color2, t), 1f);
                }
            }
        }

        static float SmoothStep(float edge0, float edge1, float x)
        {
            if (edge0 == edge1) return x < edge0 ? 0f : 1f;
            float t = (x - edge0) / (edge1 - edge0);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        public static Color3 Lerp(Color3 a, Color3 b, float t)
        {
            return new Color3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        static byte ToByte(float value)
        {
            if (value <= 0f) return 0;
            if (value >= 1f) return 255;
            return (byte)(value * 255f + .5f);
        }
    }
}
