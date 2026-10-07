using System;

namespace SeAudioVisualizer.Core
{
    internal sealed class Radix2Fft
    {
        readonly int _size;
        readonly int[] _bitReversed;

        public Radix2Fft(int size)
        {
            _size = size;
            _bitReversed = BuildBitReversed(size);
        }

        public void Transform(float[] real, float[] imaginary)
        {
            int i;
            for (i = 0; i < _size; i++)
            {
                int j = _bitReversed[i];
                if (j > i)
                {
                    float tr = real[i];
                    real[i] = real[j];
                    real[j] = tr;

                    float ti = imaginary[i];
                    imaginary[i] = imaginary[j];
                    imaginary[j] = ti;
                }
            }

            for (int length = 2; length <= _size; length <<= 1)
            {
                int halfLength = length >> 1;
                double angle = -2.0 * Math.PI / length;
                float phaseStepReal = (float)Math.Cos(angle);
                float phaseStepImaginary = (float)Math.Sin(angle);

                for (i = 0; i < _size; i += length)
                {
                    float ur = 1f;
                    float ui = 0f;

                    for (int j = 0; j < halfLength; j++)
                    {
                        int evenIndex = i + j;
                        int oddIndex = evenIndex + halfLength;

                        float oddReal = real[oddIndex] * ur - imaginary[oddIndex] * ui;
                        float oddImaginary = real[oddIndex] * ui + imaginary[oddIndex] * ur;

                        float evenReal = real[evenIndex];
                        float evenImaginary = imaginary[evenIndex];

                        real[evenIndex] = evenReal + oddReal;
                        imaginary[evenIndex] = evenImaginary + oddImaginary;
                        real[oddIndex] = evenReal - oddReal;
                        imaginary[oddIndex] = evenImaginary - oddImaginary;

                        float nextUr = ur * phaseStepReal - ui * phaseStepImaginary;
                        ui = ur * phaseStepImaginary + ui * phaseStepReal;
                        ur = nextUr;
                    }
                }
            }
        }

        static int[] BuildBitReversed(int size)
        {
            int bits = 0;
            for (int value = size; value > 1; value >>= 1)
                bits++;

            var result = new int[size];
            for (int i = 0; i < size; i++)
            {
                int reversed = 0;
                int value = i;
                for (int b = 0; b < bits; b++)
                {
                    reversed = (reversed << 1) | (value & 1);
                    value >>= 1;
                }
                result[i] = reversed;
            }

            return result;
        }
    }
}
