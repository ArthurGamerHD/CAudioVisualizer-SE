using System;

namespace SeAudioVisualizer.Core
{
    // Port of upstream Core/TimeColorHelper.cs.
    // Stateless in the SE port: playback time is supplied explicitly by the render job.
    public static class TimeColorHelper
    {
        public static Color3 GetTimeBasedColor(double playbackTimeSeconds)
        {
            playbackTimeSeconds = NormalizeTime(playbackTimeSeconds);
            float time = (float)(playbackTimeSeconds * 0.1);
            float hue = (time % 1.0f) * 360.0f;
            if (hue < 0f) hue += 360f;
            return HsvToRgb(hue, 1.0f, 1.0f);
        }

        public static Color3 GetRealTimeBasedColor(double playbackTimeSeconds)
        {
            playbackTimeSeconds = NormalizeTime(playbackTimeSeconds);
            // A continuous, slow color cycle follows the music timeline. Mapping
            // seconds directly to the blue channel made it jump from nearly 1 to
            // 0 at every minute, causing an abrupt flash (or disappearance when
            // inverted). The hue wheel stays bright and joins smoothly at its seam.
            float hue = (float)((playbackTimeSeconds % 600.0) / 600.0 * 360.0);
            return HsvToRgb(hue, 1.0f, 1.0f);
        }

        static double NormalizeTime(double playbackTimeSeconds)
        {
            if (double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds) || playbackTimeSeconds < 0.0)
                return 0.0;
            return playbackTimeSeconds;
        }

        public static Color3 HsvToRgb(float h, float s, float v)
        {
            while (h < 0f) h += 360f;
            while (h >= 360f) h -= 360f;
            h /= 60.0f;
            int i = (int)Math.Floor(h);
            float f = h - i;
            float p = v * (1.0f - s);
            float q = v * (1.0f - s * f);
            float t = v * (1.0f - s * (1.0f - f));
            switch (i)
            {
                case 0: return new Color3(v, t, p);
                case 1: return new Color3(q, v, p);
                case 2: return new Color3(p, v, t);
                case 3: return new Color3(p, q, v);
                case 4: return new Color3(t, p, v);
                default: return new Color3(v, p, q);
            }
        }

        public static Color3 InvertColor(Color3 color)
        {
            return new Color3(1.0f - color.X, 1.0f - color.Y, 1.0f - color.Z);
        }
    }
}
