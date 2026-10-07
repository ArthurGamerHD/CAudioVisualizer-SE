using System;

namespace SeAudioVisualizer.Core
{
    internal static class HannWindow
    {
        public static float[] Create(int size)
        {
            var window = new float[size];
            if (size == 1)
            {
                window[0] = 1f;
                return window;
            }

            for (int i = 0; i < size; i++)
            {
                window[i] = 0.5f - 0.5f * (float)Math.Cos((2.0 * Math.PI * i) / (size - 1));
            }

            return window;
        }
    }
}
