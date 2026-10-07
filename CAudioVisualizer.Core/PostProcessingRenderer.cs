using System;

namespace SeAudioVisualizer.Core
{
    public sealed class PostProcessingConfig
    {
        public bool EnableBloom { get; set; }
        public float BloomThreshold { get; set; }
        public float BloomIntensity { get; set; }
        public float BloomRadius { get; set; }
        public bool BloomAudioReactive { get; set; }
        public float BloomAudioStrength { get; set; }
        public float BloomAudioDecay { get; set; }
        public bool EnableChromaticAberration { get; set; }
        public float ChromaticStrength { get; set; }
        public bool ChromaticAudioReactive { get; set; }
        public float ChromaticAudioStrength { get; set; }
        public float ChromaticAudioDecay { get; set; }
        public bool EnableVignette { get; set; }
        public float VignetteStrength { get; set; }
        public float VignetteSize { get; set; }
        public bool EnableColorGrading { get; set; }
        public float Contrast { get; set; }
        public float Brightness { get; set; }
        public float Saturation { get; set; }
        public Color3 ColorTint { get; set; }
        public bool UseTimeColorTint { get; set; }
        public bool UseRealTimeColorTint { get; set; }
        public bool InvertColorTint { get; set; }
        public bool EnableFilmGrain { get; set; }
        public float GrainStrength { get; set; }

        public PostProcessingConfig()
        {
            EnableBloom = false;
            BloomThreshold = .5f;
            BloomIntensity = 1f;
            BloomRadius = 4f;
            BloomAudioReactive = false;
            BloomAudioStrength = .02f;
            BloomAudioDecay = 2f;
            EnableChromaticAberration = false;
            ChromaticStrength = .005f;
            ChromaticAudioReactive = false;
            ChromaticAudioStrength = .02f;
            ChromaticAudioDecay = 2f;
            EnableVignette = false;
            VignetteStrength = .8f;
            VignetteSize = .5f;
            EnableColorGrading = false;
            Contrast = 1f;
            Brightness = 0f;
            Saturation = 1f;
            ColorTint = new Color3(1f, 1f, 1f);
            EnableFilmGrain = false;
            GrainStrength = .2f;
        }
    }

    public sealed class PostProcessingRenderer
    {
        readonly PostProcessingConfig _config = new PostProcessingConfig();
        float _bloomAudioEnvelope;
        float _chromaticAudioEnvelope;
        float _maxBassLevel;
        double _time;

        public PostProcessingConfig Config { get { return _config; } }

        public void Update(float[] waveformData, float[] fftData, int fftLength, double deltaTime)
        {
            _time += deltaTime;
            bool bloomAudioActive = _config.EnableBloom && _config.BloomAudioReactive;
            bool chromaticAudioActive = _config.EnableChromaticAberration && _config.ChromaticAudioReactive;
            if (!bloomAudioActive && !chromaticAudioActive)
            {
                _bloomAudioEnvelope = 0f;
                _chromaticAudioEnvelope = 0f;
                _maxBassLevel = 0f;
                return;
            }

            int bassBins = Math.Min(Math.Min(fftLength, fftData.Length), 32);
            float bass = 0f;
            for (int i = 0; i < bassBins; i++) bass += Math.Abs(fftData[i]);
            if (bassBins > 0) bass /= bassBins;
            _maxBassLevel *= (float)Math.Pow(.995, deltaTime * 60.0);
            if (bass > _maxBassLevel) _maxBassLevel = bass;
            if (_maxBassLevel < .0001f) _maxBassLevel = .0001f;
            float level = bass / _maxBassLevel;
            if (level > 1f) level = 1f;

            if (bloomAudioActive) UpdateEnvelope(ref _bloomAudioEnvelope, level, deltaTime, _config.BloomAudioDecay);
            if (chromaticAudioActive) UpdateEnvelope(ref _chromaticAudioEnvelope, level, deltaTime, _config.ChromaticAudioDecay);
        }

        static void UpdateEnvelope(ref float envelope, float level, double deltaTime, float decaySeconds)
        {
            if (level > envelope) envelope = level;
            else
            {
                float releaseFactor = (float)Math.Pow(.001, deltaTime / Math.Max(.1, decaySeconds));
                envelope *= releaseFactor;
            }
            if (envelope < 0f) envelope = 0f;
            if (envelope > 1f) envelope = 1f;
        }

        public void Apply(byte[] pixels, int width, int height, double playbackTimeSeconds)
        {
            if (!_config.EnableChromaticAberration && !_config.EnableVignette && !_config.EnableColorGrading && !_config.EnableFilmGrain && !_config.EnableBloom)
                return;

            byte[] source = new byte[pixels.Length];
            Buffer.BlockCopy(pixels, 0, source, 0, pixels.Length);
            int chromaticPixels = 0;
            if (_config.EnableChromaticAberration)
            {
                float strength = _config.ChromaticStrength;
                if (_config.ChromaticAudioReactive) strength += _chromaticAudioEnvelope * _config.ChromaticAudioStrength;
                chromaticPixels = Math.Max(0, (int)(Math.Abs(strength) * width));
            }

            Color3 tint = _config.UseTimeColorTint ? TimeColorHelper.GetTimeBasedColor(playbackTimeSeconds) :
                (_config.UseRealTimeColorTint ? TimeColorHelper.GetRealTimeBasedColor(playbackTimeSeconds) : _config.ColorTint);
            if (_config.InvertColorTint) tint = TimeColorHelper.InvertColor(tint);
            float bloomIntensity = _config.BloomIntensity;
            if (_config.BloomAudioReactive) bloomIntensity += _bloomAudioEnvelope * _config.BloomAudioStrength;

            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0f : y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    int p = (y * width + x) * 4;
                    float r = Read(source, width, height, x + chromaticPixels, y, 0);
                    float g = Read(source, width, height, x, y, 1);
                    float b = Read(source, width, height, x - chromaticPixels, y, 2);

                    if (_config.EnableBloom)
                    {
                        float br = Math.Max(r, Math.Max(g, b));
                        if (br > _config.BloomThreshold)
                        {
                            r += r * bloomIntensity * .25f;
                            g += g * bloomIntensity * .25f;
                            b += b * bloomIntensity * .25f;
                        }
                    }

                    if (_config.EnableColorGrading)
                    {
                        r += _config.Brightness; g += _config.Brightness; b += _config.Brightness;
                        r = (r - .5f) * _config.Contrast + .5f;
                        g = (g - .5f) * _config.Contrast + .5f;
                        b = (b - .5f) * _config.Contrast + .5f;
                        float lum = r * .299f + g * .587f + b * .114f;
                        r = lum + (r - lum) * _config.Saturation;
                        g = lum + (g - lum) * _config.Saturation;
                        b = lum + (b - lum) * _config.Saturation;
                        r *= tint.X; g *= tint.Y; b *= tint.Z;
                    }

                    if (_config.EnableVignette)
                    {
                        float dx = u - .5f;
                        float dy = v - .5f;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        float vig = 1f - SmoothStep(_config.VignetteSize, 1f, dist);
                        vig = (1f - _config.VignetteStrength) + vig * _config.VignetteStrength;
                        r *= vig; g *= vig; b *= vig;
                    }

                    if (_config.EnableFilmGrain)
                    {
                        float grain = HashNoise(x, y, _time) - .5f;
                        r += grain * _config.GrainStrength;
                        g += grain * _config.GrainStrength;
                        b += grain * _config.GrainStrength;
                    }

                    pixels[p] = ToByte(r);
                    pixels[p + 1] = ToByte(g);
                    pixels[p + 2] = ToByte(b);
                    // Keep a transparent background transparent; effects such as bloom stay visible as they brighten.
                    pixels[p + 3] = Math.Max(source[p + 3], ToByte(Math.Max(r, Math.Max(g, b))));
                }
            }
        }

        static float Read(byte[] source, int width, int height, int x, int y, int c)
        {
            if (x < 0) x = 0; if (x >= width) x = width - 1;
            if (y < 0) y = 0; if (y >= height) y = height - 1;
            return source[(y * width + x) * 4 + c] / 255f;
        }

        static float SmoothStep(float e0, float e1, float x)
        {
            float t = (x - e0) / Math.Max(.00001f, e1 - e0);
            if (t < 0f) t = 0f; if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        static float HashNoise(int x, int y, double time)
        {
            double v = Math.Sin(x * 12.9898 + y * 78.233 + time * .1) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        static byte ToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            return (byte)(v * 255f + .5f);
        }
    }
}
