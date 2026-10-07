namespace SeAudioVisualizer.Core
{
    // Upstream DebugInfoVisualizer depends on ImGui's font/text renderer.
    // SE generated textures do not expose that renderer, so the type/flag is retained
    // for configuration compatibility but it intentionally draws nothing.
    public sealed class DebugInfoVisualizer : IVisualizer
    {
        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.Debug; } }
        public bool IsEnabled { get; set; }
        public DebugInfoVisualizer() { IsEnabled = true; }
        public void Initialize(int width, int height) { }
        public void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime) { }
        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds) { }
    }
}
