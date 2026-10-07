using System;

namespace SeAudioVisualizer.SpaceEngineers
{
    public struct VisualizerRenderSize
    {
        public int Width;
        public int Height;

        public VisualizerRenderSize(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public sealed class VisualizerHostOptions
    {
        public int RenderEveryNthTick { get; set; }
        public string TextureNameHint { get; set; }

        // Optional pull callback. The media player may update its desired surface
        // dimensions at any time; the host samples this callback between worker jobs.
        // Values are clamped to the generated-texture API limit.
        public Func<VisualizerRenderSize> RenderSizeProvider { get; set; }

        public VisualizerHostOptions()
        {
            RenderEveryNthTick = 2;
            TextureNameHint = "AudioVisualizer";
        }
    }
}
