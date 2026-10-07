using System;
using System.Collections.Generic;

namespace SeAudioVisualizer.Core
{
    // Source-faithful manager stages: background, visualizer instances, post-processing.
    // The SE port keeps the instance list stable and uses the flag mask only as the
    // external configuration gate.
    public sealed class VisualizerManager
    {
        readonly List<IVisualizer> _instances = new List<IVisualizer>();
        readonly List<bool> _configuredEnabled = new List<bool>();
        readonly BackgroundRenderer _backgroundRenderer = new BackgroundRenderer();
        readonly PostProcessingRenderer _postProcessingRenderer = new PostProcessingRenderer();
        AudioVisualizerFlags _enabledVisualizers;

        public BackgroundRenderer BackgroundRenderer { get { return _backgroundRenderer; } }
        public PostProcessingRenderer PostProcessingRenderer { get { return _postProcessingRenderer; } }
        public IList<IVisualizer> Instances
        {
            get { lock (_instances) return Array.AsReadOnly(_instances.ToArray()); }
        }
        public AudioVisualizerFlags EnabledVisualizers { get { return _enabledVisualizers; } }

        public VisualizerManager(int width, int height)
        {
            // Same available-type order as upstream VisualizerFactory.
            _instances.Add(new TriangleVisualizer());
            _instances.Add(new CircleVisualizer());
            _instances.Add(new WaveformVisualizer());
            _instances.Add(new SpectrumVisualizer());
            _instances.Add(new CustomShaderVisualizer());
            _instances.Add(new DebugInfoVisualizer());
            for (int i = 0; i < _instances.Count; i++)
            {
                _instances[i].Initialize(width, height);
                _configuredEnabled.Add(true);
            }
            _enabledVisualizers = AudioVisualizerFlags.Waveform | AudioVisualizerFlags.Debug;
            ApplyEnabledFlags();
        }

        public void SetEnabledVisualizers(AudioVisualizerFlags flags)
        {
            lock (_instances)
            {
                _enabledVisualizers = flags & AudioVisualizerFlags.All;
                ApplyEnabledFlags();
            }
        }

        // The supplied sequence is the configured stack, including repeated types.
        // Each object has its own configuration and animation state.
        public void SetInstances(IList<IVisualizer> instances)
        {
            if (instances == null) throw new ArgumentNullException("instances");
            lock (_instances)
            {
                _instances.Clear();
                _configuredEnabled.Clear();
                for (int i = 0; i < instances.Count; i++)
                {
                    IVisualizer instance = instances[i];
                    if (instance == null) continue;
                    _instances.Add(instance);
                    _configuredEnabled.Add(instance.IsEnabled);
                }
                ApplyEnabledFlags();
            }
        }

        void ApplyEnabledFlags()
        {
            for (int i = 0; i < _instances.Count; i++)
                _instances[i].IsEnabled = _configuredEnabled[i] && (_enabledVisualizers & _instances[i].Flag) != 0;
        }

        public T GetVisualizer<T>() where T : class, IVisualizer
        {
            lock (_instances)
            {
                for (int i = 0; i < _instances.Count; i++)
                {
                    T result = _instances[i] as T;
                    if (result != null) return result;
                }
            }
            return null;
        }

        public void UpdateVisualizers(float[] waveformData, float[] fftData, int fftLength, double deltaTime)
        {
            _backgroundRenderer.Update(waveformData, fftData, fftLength, deltaTime);
            _postProcessingRenderer.Update(waveformData, fftData, fftLength, deltaTime);
            lock (_instances)
                for (int i = 0; i < _instances.Count; i++)
                    if (_instances[i].IsEnabled)
                        _instances[i].Update(waveformData, fftData, fftLength, deltaTime);
        }

        public void RenderFrame(SoftwareRasterizer rasterizer, byte[] pixels, double playbackTimeSeconds,
            float[] waveformData, float[] fftData, int fftLength, double deltaTime, AudioVisualizerFlags flags)
        {
            lock (_instances)
            {
                SetEnabledVisualizers(flags);
                UpdateVisualizers(waveformData, fftData, fftLength, deltaTime);
                RenderVisualizers(rasterizer, pixels, playbackTimeSeconds);
            }
        }

        /// <summary>Leaves the texture transparent behind the visualizers instead of painting the background.</summary>
        public bool TransparentBackground { get; set; }

        public void RenderVisualizers(SoftwareRasterizer rasterizer, byte[] pixels, double playbackTimeSeconds)
        {
            if (_enabledVisualizers == AudioVisualizerFlags.None)
            {
                if (TransparentBackground)
                    rasterizer.ClearTransparent();
                else
                    rasterizer.Clear(new Color3(0f, 0f, 0f));
                return;
            }

            if (TransparentBackground)
                rasterizer.ClearTransparent();
            else
                _backgroundRenderer.Render(rasterizer, playbackTimeSeconds);

            // Linux renders instances in creation order: later entries draw above earlier ones.
            lock (_instances)
                for (int i = 0; i < _instances.Count; i++)
                    if (_instances[i].IsEnabled)
                        _instances[i].Render(rasterizer, playbackTimeSeconds);

            _postProcessingRenderer.Apply(pixels, rasterizer.Width, rasterizer.Height, playbackTimeSeconds);
        }

        // Preserve relative placement when the media surface changes dimensions.
        // Per-visualizer pixel sizes remain source-faithful; only screen-relative
        // positions/ranges are remapped to the new render target.
        public void Resize(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            if (oldWidth <= 0) oldWidth = newWidth;
            if (oldHeight <= 0) oldHeight = newHeight;
            float sx = newWidth / (float)oldWidth;
            float sy = newHeight / (float)oldHeight;

            lock (_instances)
            {
                for (int i = 0; i < _instances.Count; i++)
                {
                    IVisualizer instance = _instances[i];
                    TriangleVisualizer triangle = instance as TriangleVisualizer;
                    if (triangle != null)
                    {
                        triangle.Config.PositionX = ScaleInt(triangle.Config.PositionX, sx);
                        triangle.Config.PositionY = ScaleInt(triangle.Config.PositionY, sy);
                    }
                    CircleVisualizer circle = instance as CircleVisualizer;
                    if (circle != null)
                    {
                        circle.Config.PositionX = ScaleInt(circle.Config.PositionX, sx);
                        circle.Config.PositionY = ScaleInt(circle.Config.PositionY, sy);
                    }
                    WaveformVisualizer waveform = instance as WaveformVisualizer;
                    if (waveform != null)
                    {
                        waveform.Config.PositionY = ScaleInt(waveform.Config.PositionY, sy);
                        waveform.Config.StartX = ScaleInt(waveform.Config.StartX, sx);
                        waveform.Config.EndX = ScaleInt(waveform.Config.EndX, sx);
                    }
                    SpectrumVisualizer spectrum = instance as SpectrumVisualizer;
                    if (spectrum != null)
                    {
                        spectrum.Config.PositionX = ScaleInt(spectrum.Config.PositionX, sx);
                        spectrum.Config.PositionY = ScaleInt(spectrum.Config.PositionY, sy);
                    }
                    CustomShaderVisualizer shader = instance as CustomShaderVisualizer;
                    if (shader != null)
                    {
                        shader.Config.PositionX = ScaleInt(shader.Config.PositionX, sx);
                        shader.Config.PositionY = ScaleInt(shader.Config.PositionY, sy);
                    }
                    instance.Initialize(newWidth, newHeight);
                }
            }
        }

        static int ScaleInt(int value, float scale)
        {
            if (value < 0) return value;
            return (int)Math.Round(value * scale);
        }
    }
}
