using System;

namespace SeAudioVisualizer.Core
{
    public sealed class CustomShaderConfig
    {
        public bool Enabled { get; set; }
        public Color3 Color { get; set; }
        public float Intensity { get; set; }
        public float Scale { get; set; }
        public float Speed { get; set; }
        public float AudioReactivity { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public bool UseTimeColor { get; set; }
        public bool UseRealTimeColor { get; set; }
        public bool InvertColor { get; set; }
        public bool UseFFT { get; set; }
        public string FragmentShaderSource { get; set; }

        public CustomShaderConfig()
        {
            Enabled = true;
            Color = new Color3(1f, .5f, 0f);
            Intensity = 10f;
            Scale = .75f;
            Speed = 1f;
            AudioReactivity = .4f;
            PositionX = -1;
            PositionY = -1;
            UseFFT = true;
            FragmentShaderSource = string.Empty;
        }
    }

    // Space Engineers generated textures expose pixels, not a GLSL compiler.
    // The upstream state/update behavior is preserved; rendering uses a CPU translation
    // of the bundled default shader. Arbitrary FragmentShaderSource is intentionally unsupported.
    public sealed class CustomShaderVisualizer : IVisualizer
    {
        readonly CustomShaderConfig _config = new CustomShaderConfig();
        float[] _audioData = new float[0];
        float[] _fftData = new float[0];
        int _fftLength;
        float _time;
        float _smoothedAudioAmplitude;
        int _width;
        int _height;

        public AudioVisualizerFlags Flag { get { return AudioVisualizerFlags.CustomShader; } }
        public CustomShaderConfig Config { get { return _config; } }
        public bool IsEnabled { get { return _config.Enabled; } set { _config.Enabled = value; } }
        public bool IsArbitraryShaderSourceSupported { get { return false; } }

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            if (_config.PositionX == -1) _config.PositionX = width / 2;
            if (_config.PositionY == -1) _config.PositionY = height / 2;
        }

        public void Update(float[] audioData, float[] fftData, int fftLength, double deltaTime)
        {
            _audioData = audioData;
            _fftData = fftData;
            _fftLength = fftLength;
            float[] dataSource = _config.UseFFT ? _fftData : _audioData;
            int length = _config.UseFFT ? _fftLength : dataSource.Length;
            float currentAmplitude = 0f;
            if (length > 0)
            {
                float sum = 0f;
                int sampleCount = Math.Min(64, length);
                for (int i = 0; i < sampleCount; i++) sum += Math.Abs(dataSource[i]);
                currentAmplitude = sum / sampleCount * _config.AudioReactivity;
            }
            float smoothingFactor = 1f - (float)Math.Exp(-deltaTime * 8.0);
            _smoothedAudioAmplitude = _smoothedAudioAmplitude * (1f - smoothingFactor) + currentAmplitude * smoothingFactor;
            float audioSpeedBoost = 1f + Math.Max(0f, _smoothedAudioAmplitude * 2f);
            _time += (float)deltaTime * _config.Speed * audioSpeedBoost;
        }

        public void Render(SoftwareRasterizer rasterizer, double playbackTimeSeconds)
        {
            if (!IsEnabled) return;
            Color3 baseColor = _config.UseTimeColor ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColor ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.Color);
            if (_config.InvertColor) baseColor = TimeColorHelper.InvertColor(baseColor);

            // CPU fallback intentionally samples every 2 pixels and fills a 2x2 block.
            // This keeps the bundled effect practical at 512px in a mod worker.
            int step = (_width >= 768 || _height >= 768) ? 4 : 2;
            float scale = Math.Max(.05f, _config.Scale);
            float dynamicIntensity = _config.Intensity * (1f + _smoothedAudioAmplitude * .5f);
            for (int y = 0; y < _height; y += step)
            {
                for (int x = 0; x < _width; x += step)
                {
                    float px = ((x * 2f - _width) / _height) / scale;
                    float py = ((y * 2f - _height) / _height) / scale;
                    float radius = (float)Math.Sqrt(px * px + py * py);
                    float wave = (float)(Math.Sin(px * 8f + _time) + Math.Cos(py * 7f - _time * .73f));
                    float ring = (float)Math.Sin((radius * 18f - _time * 2f));
                    float glow = 1f / (1f + radius * radius * 3f);
                    float amount = (.5f + .25f * wave + .25f * ring) * glow * dynamicIntensity * .12f;
                    if (amount < 0f) amount = 0f;
                    if (amount > 1f) amount = 1f;
                    Color3 color = new Color3(baseColor.X * amount, baseColor.Y * amount, baseColor.Z * amount);
                    for (int yy = 0; yy < step; yy++)
                        for (int xx = 0; xx < step; xx++)
                            rasterizer.SetPixel(x + xx, y + yy, color, amount);
                }
            }
        }
    }
}
