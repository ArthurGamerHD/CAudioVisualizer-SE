using System;

namespace SeAudioVisualizer.Core
{
    public sealed class AudioVisualizerOptions
    {
        public const int DefaultSampleRate = 24000;
        public const int DefaultTextureSize = 512;
        public const int MaximumTextureSize = 1024;
        public const int DefaultFftSize = 2048;

        public int SampleRate { get; set; }
        public int TextureWidth { get; set; }
        public int TextureHeight { get; set; }
        public int FftSize { get; set; }
        public int RingBufferCapacity { get; set; }
        public AudioVisualizerFlags EnabledVisualizers { get; set; }

        public AudioVisualizerOptions()
        {
            SampleRate = DefaultSampleRate;
            TextureWidth = DefaultTextureSize;
            TextureHeight = DefaultTextureSize;
            FftSize = DefaultFftSize;
            RingBufferCapacity = 16384;
            // Matches the repository's default config.json.
            EnabledVisualizers = AudioVisualizerFlags.Waveform | AudioVisualizerFlags.Debug;
        }

        public void Validate()
        {
            if (SampleRate <= 0) throw new ArgumentException("SampleRate");
            if (TextureWidth <= 0 || TextureWidth > MaximumTextureSize) throw new ArgumentException("TextureWidth");
            if (TextureHeight <= 0 || TextureHeight > MaximumTextureSize) throw new ArgumentException("TextureHeight");
            if (FftSize < 64 || FftSize > 65536 || !IsPowerOfTwo(FftSize)) throw new ArgumentException("FftSize");
            if (RingBufferCapacity < FftSize * 2) throw new ArgumentException("RingBufferCapacity");
        }

        static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
