using System;

namespace SeAudioVisualizer.Core
{
    public enum BackgroundMode
    {
        Static,
        AudioReactive
    }

    public enum BackgroundType
    {
        Solid,
        Gradient
    }

    public enum GradientType
    {
        Horizontal,
        Vertical,
        Circular
    }

    public sealed class BackgroundConfig
    {
        public BackgroundMode Mode { get; set; }
        public BackgroundType StaticType { get; set; }
        public GradientType GradientType { get; set; }
        public Color3 Color1 { get; set; }
        public Color3 Color2 { get; set; }
        public bool UseTimeColor1 { get; set; }
        public bool UseRealTimeColor1 { get; set; }
        public bool UseTimeColor2 { get; set; }
        public bool UseRealTimeColor2 { get; set; }
        public bool InvertColor1 { get; set; }
        public bool InvertColor2 { get; set; }
        public float TransitionTime { get; set; }
        public float MaxLevelDecay { get; set; }
        public bool UseRMS { get; set; }
        public bool InvertGradient { get; set; }

        public BackgroundConfig()
        {
            Mode = BackgroundMode.Static;
            StaticType = BackgroundType.Solid;
            GradientType = GradientType.Vertical;
            Color1 = new Color3(0f, 0f, 0f);
            Color2 = new Color3(.2f, 0f, .4f);
            TransitionTime = 2f;
            MaxLevelDecay = .95f;
            UseRMS = true;
        }
    }

    public sealed class BackgroundRenderer
    {
        readonly BackgroundConfig _config;
        float _currentAudioLevel;
        float _maxAudioLevelEverHeard;
        float _targetColorMix;
        float _currentColorMix;

        public bool IsEnabled { get; set; }
        public BackgroundConfig Config { get { return _config; } }

        public BackgroundRenderer()
        {
            _config = new BackgroundConfig();
            IsEnabled = true;
        }

        public void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime)
        {
            if (_config.Mode != BackgroundMode.AudioReactive)
            {
                _currentAudioLevel = 0f;
                _currentColorMix = 0f;
                return;
            }

            float audioLevel = 0f;
            if (_config.UseRMS)
            {
                float sum = 0f;
                int length = Math.Min(fftLength, fftData.Length);
                for (int i = 0; i < length; i++)
                    sum += fftData[i] * fftData[i];
                if (length > 0)
                    audioLevel = (float)Math.Sqrt(sum / length);
            }
            else
            {
                for (int i = 0; i < waveformData.Length; i++)
                    audioLevel = Math.Max(audioLevel, Math.Abs(waveformData[i]));
            }

            _currentAudioLevel = audioLevel;
            float decayPerSecond = (float)Math.Pow(_config.MaxLevelDecay, deltaTime);
            _maxAudioLevelEverHeard *= decayPerSecond;
            _maxAudioLevelEverHeard = Math.Max(_maxAudioLevelEverHeard, .001f);
            if (audioLevel > _maxAudioLevelEverHeard)
                _maxAudioLevelEverHeard = audioLevel;

            _targetColorMix = _currentAudioLevel / _maxAudioLevelEverHeard;
            float transitionTime = Math.Max(.001f, _config.TransitionTime);
            float transitionRate = (float)(1.0 / transitionTime * deltaTime);
            _currentColorMix += (_targetColorMix - _currentColorMix) * transitionRate;
            if (_currentColorMix < 0f) _currentColorMix = 0f;
            if (_currentColorMix > 1f) _currentColorMix = 1f;
        }

        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds)
        {
            if (!IsEnabled)
            {
                rasterizer.Clear(new Color3(0f, 0f, 0f));
                return;
            }

            Color3 color1 = _config.UseTimeColor1 ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor1 ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color1);
            Color3 color2 = _config.UseTimeColor2 ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor2 ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color2);
            if (_config.InvertColor1) color1 = TimeColorHelper.InvertColor(color1);
            if (_config.InvertColor2) color2 = TimeColorHelper.InvertColor(color2);

            rasterizer.DrawGradient(
                color1,
                color2,
                _config.StaticType,
                _config.GradientType,
                _config.InvertGradient,
                _currentColorMix,
                _config.Mode == BackgroundMode.AudioReactive);
        }
    }
}
