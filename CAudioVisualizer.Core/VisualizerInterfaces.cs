namespace SeAudioVisualizer.Core
{
    public interface IVisualizer
    {
        bool IsEnabled { get; set; }
        AudioVisualizerFlags Flag { get; }
        void Initialize(int width, int height);
        void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime);
        void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds);
    }
}
