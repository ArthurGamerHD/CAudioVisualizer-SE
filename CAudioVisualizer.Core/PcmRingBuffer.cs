using System;

namespace SeAudioVisualizer.Core
{
    internal sealed class PcmRingBuffer
    {
        readonly float[] _samples;
        int _writeIndex;
        int _count;
        readonly object _sync = new object();

        public PcmRingBuffer(int capacity)
        {
            _samples = new float[capacity];
        }

        public void Push(float sample)
        {
            lock (_sync)
            {
                _samples[_writeIndex] = sample;
                _writeIndex++;
                if (_writeIndex >= _samples.Length)
                    _writeIndex = 0;

                if (_count < _samples.Length)
                    _count++;
            }
        }

        public void SnapshotLatest(float[] destination)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");

            lock (_sync)
            {
                int requested = destination.Length;
                int available = _count;
                int missing = requested - available;
                if (missing < 0)
                    missing = 0;

                int i;
                for (i = 0; i < missing; i++)
                    destination[i] = 0f;

                int copyCount = requested - missing;
                if (copyCount <= 0)
                    return;

                int start = _writeIndex - copyCount;
                if (start < 0)
                    start += _samples.Length;

                for (int c = 0; c < copyCount; c++, i++)
                {
                    destination[i] = _samples[start];
                    start++;
                    if (start >= _samples.Length)
                        start = 0;
                }
            }
        }
    }
}
