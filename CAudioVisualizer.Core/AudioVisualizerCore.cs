using System;

namespace SeAudioVisualizer.Core
{
    public sealed class AudioVisualizerCore : IDisposable
    {
        readonly AudioVisualizerOptions _options;
        readonly PcmRingBuffer _ringBuffer;
        readonly float[] _hannWindow;
        readonly Radix2Fft _fft;
        readonly Action<Action> _scheduleWork;
        readonly object _stateLock = new object();
        readonly VisualizerManager _visualizerManager;

        readonly float[] _workSamples;
        readonly float[] _fftReal;
        readonly float[] _fftImaginary;
        readonly float[] _fftData;

        SoftwareRasterizer _rasterizer;
        byte[] _frontPixels;
        byte[] _backPixels;
        int _renderWidth;
        int _renderHeight;

        bool _disposed;
        int _pendingLowByte = -1;
        bool _renderInFlight;
        bool _hasCompletedFrame;
        long _completedSequence;
        int _fftLength;
        double _scheduledDeltaTime = 1.0 / 30.0;
        double _scheduledPlaybackTime;
        double _lastScheduledPlaybackTime;
        bool _hasLastScheduledPlaybackTime;
        AudioVisualizerFlags _enabledVisualizers;

        public AudioVisualizerCore()
            : this(new AudioVisualizerOptions())
        {
        }

        public AudioVisualizerCore(AudioVisualizerOptions options)
            : this(options, delegate(Action work) { work(); })
        {
        }

        // Space Engineers supplies a scheduler backed by MyAPIGateway.Parallel.
        // The Core never creates Thread/Task instances itself.
        public AudioVisualizerCore(AudioVisualizerOptions options, Action<Action> scheduleWork)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (scheduleWork == null) throw new ArgumentNullException("scheduleWork");
            options.Validate();

            _options = options;
            _scheduleWork = scheduleWork;
            _ringBuffer = new PcmRingBuffer(options.RingBufferCapacity);
            _hannWindow = HannWindow.Create(options.FftSize);
            _fft = new Radix2Fft(options.FftSize);
            _workSamples = new float[options.FftSize];
            _fftReal = new float[options.FftSize];
            _fftImaginary = new float[options.FftSize];
            _fftData = new float[options.FftSize / 2];

            _renderWidth = options.TextureWidth;
            _renderHeight = options.TextureHeight;
            AllocateRenderTarget(_renderWidth, _renderHeight);

            _visualizerManager = new VisualizerManager(_renderWidth, _renderHeight);
            _enabledVisualizers = options.EnabledVisualizers;
            _visualizerManager.SetEnabledVisualizers(_enabledVisualizers);
        }

        public AudioVisualizerOptions Options { get { return _options; } }
        public VisualizerManager Visualizers { get { return _visualizerManager; } }
        public int RenderWidth { get { lock (_stateLock) return _renderWidth; } }
        public int RenderHeight { get { lock (_stateLock) return _renderHeight; } }

        public AudioVisualizerFlags EnabledVisualizers
        {
            get { lock (_stateLock) return _enabledVisualizers; }
            set
            {
                lock (_stateLock)
                {
                    ThrowIfDisposed();
                    _enabledVisualizers = value & AudioVisualizerFlags.All;
                }
            }
        }

        public bool IsRenderInFlight
        {
            get { lock (_stateLock) return _renderInFlight; }
        }

        public void PushPcm16Mono(byte[] pcm, int offset, int count)
        {
            if (pcm == null) throw new ArgumentNullException("pcm");
            if (offset < 0 || count < 0 || offset > pcm.Length - count)
                throw new ArgumentException("Invalid PCM range.");

            lock (_stateLock)
            {
                ThrowIfDisposed();
                int end = offset + count;
                for (int i = offset; i < end; i++)
                {
                    if (_pendingLowByte < 0)
                    {
                        _pendingLowByte = pcm[i];
                    }
                    else
                    {
                        short sample = unchecked((short)(_pendingLowByte | (pcm[i] << 8)));
                        _ringBuffer.Push(sample * (1.0f / 32768.0f));
                        _pendingLowByte = -1;
                    }
                }
            }
        }

        // Resize is intentionally applied only between render jobs. This avoids
        // touching buffers that a MyAPIGateway.Parallel worker currently owns.
        public bool TryResize(int width, int height)
        {
            if (width < 1 || width > AudioVisualizerOptions.MaximumTextureSize)
                throw new ArgumentException("Width must be between 1 and AudioVisualizerOptions.MaximumTextureSize.", "width");
            if (height < 1 || height > AudioVisualizerOptions.MaximumTextureSize)
                throw new ArgumentException("Height must be between 1 and AudioVisualizerOptions.MaximumTextureSize.", "height");

            lock (_stateLock)
            {
                ThrowIfDisposed();
                if (_renderInFlight)
                    return false;
                if (_renderWidth == width && _renderHeight == height)
                    return true;

                int oldWidth = _renderWidth;
                int oldHeight = _renderHeight;
                _renderWidth = width;
                _renderHeight = height;
                _options.TextureWidth = width;
                _options.TextureHeight = height;
                AllocateRenderTarget(width, height);
                _visualizerManager.Resize(oldWidth, oldHeight, width, height);

                // Do not publish a stale frame whose dimensions no longer describe
                // the newly allocated backing store. The SE host keeps the previous
                // texture alive until the first new-size frame is completed.
                _hasCompletedFrame = false;
                return true;
            }
        }

        public bool TryScheduleRender()
        {
            double time;
            lock (_stateLock)
            {
                time = _hasLastScheduledPlaybackTime
                    ? _lastScheduledPlaybackTime + (1.0 / 30.0)
                    : 0.0;
            }
            return TryScheduleRenderAt(time);
        }

        // Compatibility overload retained for callers that used to pass delta time.
        public bool TryScheduleRender(double deltaTime)
        {
            double time;
            lock (_stateLock)
            {
                if (deltaTime < 0.0) deltaTime = 0.0;
                time = _hasLastScheduledPlaybackTime
                    ? _lastScheduledPlaybackTime + deltaTime
                    : deltaTime;
            }
            return TryScheduleRenderAt(time);
        }

        // Preferred API: the media player's exact playback timestamp is snapshotted
        // together with the PCM window and used as the canonical animation clock.
        public bool TryScheduleRenderAt(double playbackTimeSeconds)
        {
            if (double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds))
                playbackTimeSeconds = 0.0;
            if (playbackTimeSeconds < 0.0)
                playbackTimeSeconds = 0.0;

            lock (_stateLock)
            {
                ThrowIfDisposed();
                if (_renderInFlight) return false;

                _ringBuffer.SnapshotLatest(_workSamples);

                double deltaTime = 1.0 / 30.0;
                if (_hasLastScheduledPlaybackTime)
                {
                    double playbackDelta = playbackTimeSeconds - _lastScheduledPlaybackTime;

                    // Pause => freeze stateful animation. Small forward deltas follow
                    // music exactly. Large jumps/seeks avoid applying seconds worth of
                    // trail/peak decay in one frame; absolute effects still jump to the
                    // exact supplied timestamp.
                    if (playbackDelta >= 0.0 && playbackDelta <= 0.25)
                        deltaTime = playbackDelta;
                    else if (playbackDelta < 0.0)
                        deltaTime = 0.0;
                }

                _scheduledPlaybackTime = playbackTimeSeconds;
                _scheduledDeltaTime = deltaTime;
                _lastScheduledPlaybackTime = playbackTimeSeconds;
                _hasLastScheduledPlaybackTime = true;
                _renderInFlight = true;
            }

            try
            {
                _scheduleWork(RenderWork);
            }
            catch
            {
                lock (_stateLock) _renderInFlight = false;
                throw;
            }
            return true;
        }

        public bool TryGetLatestFrame(out AudioVisualizerFrame frame)
        {
            lock (_stateLock)
            {
                if (_disposed || !_hasCompletedFrame)
                {
                    frame = default(AudioVisualizerFrame);
                    return false;
                }
                frame = new AudioVisualizerFrame(_frontPixels, _renderWidth, _renderHeight, _completedSequence);
                return true;
            }
        }

        void RenderWork()
        {
            try
            {
                AudioVisualizerFlags flags;
                double deltaTime;
                double playbackTime;
                lock (_stateLock)
                {
                    if (_disposed) return;
                    flags = _enabledVisualizers;
                    deltaTime = _scheduledDeltaTime;
                    playbackTime = _scheduledPlaybackTime;
                }
                RenderLatestSnapshot(flags, deltaTime, playbackTime);
            }
            finally
            {
                lock (_stateLock) _renderInFlight = false;
            }
        }

        void RenderLatestSnapshot(AudioVisualizerFlags flags, double deltaTime, double playbackTime)
        {
            ProcessAudioDataLikeUpstream();
            _rasterizer.Bind(_backPixels);
            _visualizerManager.RenderFrame(_rasterizer, _backPixels, playbackTime,
                _workSamples, _fftData, _fftLength, deltaTime, flags);

            lock (_stateLock)
            {
                if (_disposed) return;
                byte[] swap = _frontPixels;
                _frontPixels = _backPixels;
                _backPixels = swap;
                _completedSequence++;
                _hasCompletedFrame = true;
            }
        }

        // Port of AudioVisualizerWindow.ProcessAudioData().
        // The only material change is replacing MathNet Complex32/Fourier with Radix2Fft
        // and reusing _fftData rather than allocating a new compact array every frame.
        void ProcessAudioDataLikeUpstream()
        {
            bool isSilent = true;
            for (int i = 0; i < _workSamples.Length; i++)
            {
                if (Math.Abs(_workSamples[i]) >= .0001f) isSilent = false;
                _fftReal[i] = _workSamples[i] * _hannWindow[i];
                _fftImaginary[i] = 0f;
            }

            int fftLen = _options.FftSize / 2;
            if (isSilent)
            {
                Array.Clear(_fftData, 0, _fftData.Length);
                _fftLength = fftLen;
                return;
            }

            _fft.Transform(_fftReal, _fftImaginary);
            int belowThreshold = 0;
            for (int i = 0; i < fftLen; i++)
            {
                float magnitude = (float)Math.Sqrt(_fftReal[i] * _fftReal[i] + _fftImaginary[i] * _fftImaginary[i]);
                if (magnitude < .005f) belowThreshold++;
            }

            float cutFraction = Math.Min(.25f, belowThreshold / (float)fftLen);
            int cutLen = (int)(cutFraction * fftLen);
            int midStart = (fftLen - cutLen) / 2;
            int midEnd = midStart + cutLen;
            int output = 0;
            for (int i = 0; i < fftLen; i++)
            {
                if (i >= midStart && i < midEnd) continue;
                _fftData[output++] = (float)Math.Sqrt(_fftReal[i] * _fftReal[i] + _fftImaginary[i] * _fftImaginary[i]);
            }
            for (int i = output; i < _fftData.Length; i++) _fftData[i] = 0f;
            _fftLength = output;
        }

        void AllocateRenderTarget(int width, int height)
        {
            int pixelCount = checked(width * height * 4);
            _frontPixels = new byte[pixelCount];
            _backPixels = new byte[pixelCount];
            _rasterizer = new SoftwareRasterizer(width, height);
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed) return;
                _disposed = true;
                _hasCompletedFrame = false;
            }
        }

        void ThrowIfDisposed()
        {
            if (_disposed) throw new InvalidOperationException("AudioVisualizerCore is disposed.");
        }
    }
}
