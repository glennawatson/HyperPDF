// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdf.Codecs.Dsp;

/// <summary>
/// The inverse MDCT of one block size. A block of <c>N</c> coefficients becomes <c>2N</c> samples through a DCT-IV of length <c>N</c>,
/// which is done with a complex FFT of length <c>N/2</c> between two sets of twiddle factors, so the cost grows with <c>N log N</c>.
/// The result is not scaled: it is the sum of each coefficient times a cosine, as Vorbis defines it.
/// </summary>
[DebuggerDisplay("InverseMdct: {Size} coefficients")]
internal sealed class InverseMdct
{
    /// <summary>A quarter, the offset of the post twiddle.</summary>
    private const double Quarter = 0.25;

    /// <summary>A full turn in radians.</summary>
    private const double Turn = 2 * Math.PI;

    /// <summary>The real parts of the values being transformed.</summary>
    private readonly float[] _real;

    /// <summary>The imaginary parts of the values being transformed.</summary>
    private readonly float[] _imaginary;

    /// <summary>The cosines of the twiddle applied before the FFT.</summary>
    private readonly float[] _preCosine;

    /// <summary>The sines of the twiddle applied before the FFT.</summary>
    private readonly float[] _preSine;

    /// <summary>The cosines of the twiddle applied after the FFT.</summary>
    private readonly float[] _postCosine;

    /// <summary>The sines of the twiddle applied after the FFT.</summary>
    private readonly float[] _postSine;

    /// <summary>The cosines of the FFT twiddles for the largest stage.</summary>
    private readonly float[] _stageCosine;

    /// <summary>The sines of the FFT twiddles for the largest stage.</summary>
    private readonly float[] _stageSine;

    /// <summary>The order the FFT input is placed in.</summary>
    private readonly int[] _reverse;

    /// <summary>The DCT-IV result.</summary>
    private readonly float[] _transform;

    /// <summary>Initializes a new instance of the <see cref="InverseMdct"/> class.</summary>
    /// <param name="size">The number of coefficients, a power of two of at least 16.</param>
    internal InverseMdct(int size)
    {
        Size = size;
        var half = size >> 1;
        _real = new float[half];
        _imaginary = new float[half];
        _transform = new float[size];
        _preCosine = new float[half];
        _preSine = new float[half];
        _postCosine = new float[half];
        _postSine = new float[half];
        for (var index = 0; index < half; index++)
        {
            var pre = -Math.PI * index / size;
            _preCosine[index] = (float)Math.Cos(pre);
            _preSine[index] = (float)Math.Sin(pre);
            var post = -Math.PI * (index + Quarter) / size;
            _postCosine[index] = (float)Math.Cos(post);
            _postSine[index] = (float)Math.Sin(post);
        }

        _stageCosine = new float[half];
        _stageSine = new float[half];
        for (var span = 1; span < half; span <<= 1)
        {
            for (var offset = 0; offset < span; offset++)
            {
                var angle = -Math.PI * offset / span;
                _stageCosine[span + offset] = (float)Math.Cos(angle);
                _stageSine[span + offset] = (float)Math.Sin(angle);
            }
        }

        _reverse = BuildReverse(half);
    }

    /// <summary>Gets the number of coefficients.</summary>
    internal int Size { get; }

    /// <summary>Turns coefficients into samples.</summary>
    /// <param name="spectrum">The coefficients.</param>
    /// <param name="output">Receives twice as many samples.</param>
    internal void Inverse(ReadOnlySpan<float> spectrum, Span<float> output)
    {
        DctFour(spectrum);
        var size = Size;
        var quarter = size >> 1;
        var threeQuarters = quarter + size;
        for (var index = 0; index < quarter; index++)
        {
            output[index] = _transform[index + quarter];
        }

        for (var index = quarter; index < threeQuarters; index++)
        {
            output[index] = -_transform[threeQuarters - 1 - index];
        }

        for (var index = threeQuarters; index < size << 1; index++)
        {
            output[index] = -_transform[index - threeQuarters];
        }
    }

    /// <summary>Builds the bit reversal order for the FFT.</summary>
    /// <param name="length">The length of the FFT, a power of two.</param>
    /// <returns>The index each input goes to.</returns>
    private static int[] BuildReverse(int length)
    {
        var bits = BitOperations.Log2((uint)length);
        var order = new int[length];
        for (var index = 0; index < length; index++)
        {
            var reversed = 0;
            for (var bit = 0; bit < bits; bit++)
            {
                reversed |= ((index >> bit) & 1) << (bits - 1 - bit);
            }

            order[index] = reversed;
        }

        return order;
    }

    /// <summary>Runs one butterfly on a vector's worth of adjacent pairs.</summary>
    /// <param name="real">The real parts.</param>
    /// <param name="imaginary">The imaginary parts.</param>
    /// <param name="cosines">The twiddle cosines of the stage.</param>
    /// <param name="sines">The twiddle sines of the stage.</param>
    /// <param name="top">The index of the first top element.</param>
    /// <param name="span">The distance from a top element to its bottom partner.</param>
    /// <param name="twiddle">The index of the first twiddle.</param>
    private static void ButterflyVector(Span<float> real, Span<float> imaginary, ReadOnlySpan<float> cosines, ReadOnlySpan<float> sines, int top, int span, int twiddle)
    {
        var cosine = new Vector<float>(cosines[twiddle..]);
        var sine = new Vector<float>(sines[twiddle..]);
        var bottomReal = new Vector<float>(real[(top + span)..]);
        var bottomImaginary = new Vector<float>(imaginary[(top + span)..]);
        var topReal = new Vector<float>(real[top..]);
        var topImaginary = new Vector<float>(imaginary[top..]);
        var re = (bottomReal * cosine) - (bottomImaginary * sine);
        var im = (bottomReal * sine) + (bottomImaginary * cosine);
        (topReal - re).CopyTo(real[(top + span)..]);
        (topImaginary - im).CopyTo(imaginary[(top + span)..]);
        (topReal + re).CopyTo(real[top..]);
        (topImaginary + im).CopyTo(imaginary[top..]);
    }

    /// <summary>Computes the DCT-IV of the coefficients into the transform buffer.</summary>
    /// <param name="spectrum">The coefficients.</param>
    private void DctFour(ReadOnlySpan<float> spectrum)
    {
        var size = Size;
        var half = size >> 1;
        for (var index = 0; index < half; index++)
        {
            var real = spectrum[index << 1];
            var imaginary = spectrum[size - 1 - (index << 1)];
            var place = _reverse[index];
            _real[place] = (real * _preCosine[index]) - (imaginary * _preSine[index]);
            _imaginary[place] = (real * _preSine[index]) + (imaginary * _preCosine[index]);
        }

        Fft(half);
        for (var index = 0; index < half; index++)
        {
            var real = (_real[index] * _postCosine[index]) - (_imaginary[index] * _postSine[index]);
            var imaginary = (_real[index] * _postSine[index]) + (_imaginary[index] * _postCosine[index]);
            _transform[index << 1] = real;
            _transform[size - 1 - (index << 1)] = -imaginary;
        }
    }

    /// <summary>Runs the radix 2 FFT on the values, which are already in bit reversed order.</summary>
    /// <param name="length">The length.</param>
    private void Fft(int length)
    {
        var real = _real.AsSpan(0, length);
        var imaginary = _imaginary.AsSpan(0, length);
        var width = Vector<float>.Count;
        for (var span = 1; span < length; span <<= 1)
        {
            var cosines = _stageCosine.AsSpan(span, span);
            var sines = _stageSine.AsSpan(span, span);
            for (var start = 0; start < length; start += span << 1)
            {
                var offset = 0;
                if (Vector.IsHardwareAccelerated && span >= width)
                {
                    for (; offset <= span - width; offset += width)
                    {
                        ButterflyVector(real, imaginary, cosines, sines, start + offset, span, offset);
                    }
                }

                for (; offset < span; offset++)
                {
                    var top = start + offset;
                    var bottom = top + span;
                    var cosine = cosines[offset];
                    var sine = sines[offset];
                    var re = (real[bottom] * cosine) - (imaginary[bottom] * sine);
                    var im = (real[bottom] * sine) + (imaginary[bottom] * cosine);
                    real[bottom] = real[top] - re;
                    imaginary[bottom] = imaginary[top] - im;
                    real[top] += re;
                    imaginary[top] += im;
                }
            }
        }
    }
}
