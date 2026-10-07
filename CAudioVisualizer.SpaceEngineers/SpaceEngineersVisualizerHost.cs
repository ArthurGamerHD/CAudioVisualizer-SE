using System;
using Sandbox.ModAPI;
using SeAudioVisualizer.Core;

namespace SeAudioVisualizer.SpaceEngineers
{
    public sealed class SpaceEngineersVisualizerHost : IDisposable
    {
        readonly AudioVisualizerCore _core;
        readonly IGeneratedTextureFactory _textureFactory;
        readonly VisualizerHostOptions _hostOptions;
        int _tick;
        long _lastPublishedFrameSequence;
        IGeneratedTexture _currentTexture;
        bool _disposed;
        double _fallbackPlaybackTime;

        public SpaceEngineersVisualizerHost(AudioVisualizerCore core, IGeneratedTextureFactory textureFactory)
            : this(core, textureFactory, new VisualizerHostOptions())
        {
        }

        public SpaceEngineersVisualizerHost(AudioVisualizerCore core, IGeneratedTextureFactory textureFactory, VisualizerHostOptions hostOptions)
        {
            if (core == null) throw new ArgumentNullException("core");
            if (textureFactory == null) throw new ArgumentNullException("textureFactory");
            if (hostOptions == null) throw new ArgumentNullException("hostOptions");
            if (hostOptions.RenderEveryNthTick <= 0) throw new ArgumentException("RenderEveryNthTick must be positive.", "hostOptions");
            _core = core;
            _textureFactory = textureFactory;
            _hostOptions = hostOptions;
        }

        public static SpaceEngineersVisualizerHost CreateDefault()
        {
            return Create(new AudioVisualizerOptions(), new VisualizerHostOptions());
        }

        public static SpaceEngineersVisualizerHost Create(AudioVisualizerOptions coreOptions, VisualizerHostOptions hostOptions)
        {
            if (coreOptions == null) throw new ArgumentNullException("coreOptions");
            if (hostOptions == null) throw new ArgumentNullException("hostOptions");
            AudioVisualizerCore core = new AudioVisualizerCore(
                coreOptions,
                delegate(Action work)
                {
                    if (MyAPIGateway.Parallel == null)
                        work();
                    else
                        MyAPIGateway.Parallel.Start(work);
                });
            return new SpaceEngineersVisualizerHost(core, new SpaceEngineersGeneratedTextureFactory(), hostOptions);
        }

        public IGeneratedTexture CurrentTexture { get { return _currentTexture; } }
        public string CurrentTextureName { get { return _currentTexture == null ? null : _currentTexture.Name; } }
        public VisualizerManager Visualizers { get { return _core.Visualizers; } }

        public AudioVisualizerFlags EnabledVisualizers
        {
            get { return _core.EnabledVisualizers; }
            set { _core.EnabledVisualizers = value; }
        }

        public byte EnabledVisualizersByte
        {
            get { return (byte)_core.EnabledVisualizers; }
            set { _core.EnabledVisualizers = (AudioVisualizerFlags)value; }
        }

        public void PushPcm16Mono(byte[] pcm, int offset, int count)
        {
            _core.PushPcm16Mono(pcm, offset, count);
        }

        // Backward-compatible tick. New integrations should pass the exact media
        // playback timestamp to Tick(double).
        public bool Tick()
        {
            _fallbackPlaybackTime += 1.0 / 60.0;
            return Tick(_fallbackPlaybackTime);
        }

        public bool Tick(double playbackTimeSeconds)
        {
            ThrowIfDisposed();
            if (double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds))
                playbackTimeSeconds = 0.0;
            if (playbackTimeSeconds < 0.0)
                playbackTimeSeconds = 0.0;

            _tick++;
            if ((_tick % _hostOptions.RenderEveryNthTick) != 0)
                return false;

            bool published = TryPublishCompletedFrame();

            if (!_core.IsRenderInFlight)
            {
                ApplyRequestedRenderSize();
                _core.TryScheduleRenderAt(playbackTimeSeconds);
            }

            return published;
        }

        // Explicit resize path for hosts that prefer push-style layout updates.
        public bool TryResize(int width, int height)
        {
            ThrowIfDisposed();
            ClampSize(ref width, ref height);
            return _core.TryResize(width, height);
        }

        void ApplyRequestedRenderSize()
        {
            if (_hostOptions.RenderSizeProvider == null)
                return;

            VisualizerRenderSize requested = _hostOptions.RenderSizeProvider();
            int width = requested.Width;
            int height = requested.Height;
            ClampSize(ref width, ref height);
            if (width <= 0 || height <= 0)
                return;

            _core.TryResize(width, height);
        }

        void ClampSize(ref int width, ref int height)
        {
            int maxSide = _textureFactory.MaxTextureSide;
            if (maxSide <= 0)
            {
                width = 0;
                height = 0;
                return;
            }
            if (maxSide > AudioVisualizerOptions.MaximumTextureSize)
                maxSide = AudioVisualizerOptions.MaximumTextureSize;

            if (width < 1) width = 1;
            if (height < 1) height = 1;
            if (width > maxSide) width = maxSide;
            if (height > maxSide) height = maxSide;
        }

        bool TryPublishCompletedFrame()
        {
            AudioVisualizerFrame frame;
            if (!_core.TryGetLatestFrame(out frame)) return false;
            if (frame.Sequence <= _lastPublishedFrameSequence) return false;
            if (_textureFactory.MaxTextureSide <= 0) return false;
            if (frame.Width > _textureFactory.MaxTextureSide || frame.Height > _textureFactory.MaxTextureSide)
                throw new InvalidOperationException("Frame size exceeds generated texture factory limits.");

            bool needsReplacement = _currentTexture == null ||
                _currentTexture.Width != frame.Width ||
                _currentTexture.Height != frame.Height;

            IGeneratedTexture target = needsReplacement
                ? _textureFactory.Create(_hostOptions.TextureNameHint, frame.Width, frame.Height)
                : _currentTexture;

            if (target == null) return false;

            byte[] destination = target.BeginUpdate();
            if (destination == null)
            {
                if (needsReplacement) target.Dispose();
                return false;
            }
            if (destination.Length < frame.Pixels.Length)
            {
                if (needsReplacement) target.Dispose();
                throw new InvalidOperationException("Generated texture update buffer is invalid.");
            }

            Buffer.BlockCopy(frame.Pixels, 0, destination, 0, frame.Pixels.Length);
            target.EndUpdate(destination);

            if (needsReplacement)
            {
                // The replacement is fully populated before the old texture is
                // released, preventing a blank/stretch frame during surface resize.
                IGeneratedTexture previous = _currentTexture;
                _currentTexture = target;
                if (previous != null)
                    previous.Dispose();
            }

            _lastPublishedFrameSequence = frame.Sequence;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_currentTexture != null)
            {
                _currentTexture.Dispose();
                _currentTexture = null;
            }
            _core.Dispose();
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new InvalidOperationException("SpaceEngineersVisualizerHost is disposed.");
        }
    }
}
