using System;

namespace SeAudioVisualizer.Core
{
    [Flags]
    public enum AudioVisualizerFlags : byte
    {
        None = 0,
        Triangle = 1 << 0,
        Circle = 1 << 1,
        Waveform = 1 << 2,
        Spectrum = 1 << 3,
        CustomShader = 1 << 4,
        Debug = 1 << 5,
        All = Triangle | Circle | Waveform | Spectrum | CustomShader | Debug
    }
}
