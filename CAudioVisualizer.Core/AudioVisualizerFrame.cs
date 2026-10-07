namespace SeAudioVisualizer.Core
{
    public struct AudioVisualizerFrame
    {
        public readonly byte[] Pixels;
        public readonly int Width;
        public readonly int Height;
        public readonly long Sequence;

        public AudioVisualizerFrame(byte[] pixels, int width, int height, long sequence)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            Sequence = sequence;
        }
    }
}
