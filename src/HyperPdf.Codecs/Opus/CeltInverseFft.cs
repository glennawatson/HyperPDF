// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// An unscaled inverse discrete Fourier transform of complex numbers, for sizes whose only prime factors are 2, 3 and 5. It splits the
/// input by its smallest factor, transforms each part, and joins them with a butterfly of that factor.
/// </summary>
internal sealed class CeltInverseFft
{
    /// <summary>The factor tried before any other, since a radix 4 butterfly does the work of two radix 2 ones.</summary>
    private const int PreferredFactor = 4;

    /// <summary>The most points one butterfly joins.</summary>
    private const int MaxFactor = 5;

    /// <summary>The numbers in a complex number.</summary>
    private const int ComplexParts = 2;

    /// <summary>The factor tried after the preferred one.</summary>
    private const int SecondFactor = 2;

    /// <summary>The factor tried after the second.</summary>
    private const int ThirdFactor = 3;

    /// <summary>How much the factor grows by once the first three are tried.</summary>
    private const int FactorStep = 2;

    /// <summary>The angle of a full turn.</summary>
    private const double FullTurn = 2.0 * Math.PI;

    /// <summary>The size of the transform, in complex numbers.</summary>
    private readonly int _size;

    /// <summary>The cosine of the angles of the roots of unity, for each multiple of a step of a full turn over the size.</summary>
    private readonly float[] _cos;

    /// <summary>The sine of the angles of the roots of unity, for each multiple of a step of a full turn over the size.</summary>
    private readonly float[] _sin;

    /// <summary>The factors of the size, in the order the transform splits by them.</summary>
    private readonly int[] _factors;

    /// <summary>Initializes a new instance of the <see cref="CeltInverseFft"/> class.</summary>
    /// <param name="size">The number of complex points; it may only have prime factors 2, 3 and 5.</param>
    internal CeltInverseFft(int size)
    {
        _size = size;
        _cos = new float[size];
        _sin = new float[size];
        for (var i = 0; i < size; i++)
        {
            var angle = FullTurn * i / size;
            _cos[i] = (float)Math.Cos(angle);
            _sin[i] = (float)Math.Sin(angle);
        }

        _factors = Factorise(size);
    }

    /// <summary>Gets the number of complex points the transform handles.</summary>
    internal int Size => _size;

    /// <summary>Transforms a list of complex numbers.</summary>
    /// <param name="input">The numbers, a real then an imaginary part each.</param>
    /// <param name="output">Receives the transform, in the same layout; it may not overlap the input.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Transform(ReadOnlySpan<float> input, Span<float> output) =>
        Split(input, new(0, 1), _size, output[..(_size * ComplexParts)], 0);

    /// <summary>Splits a number into the factors the transform divides by, with the preferred factor first.</summary>
    /// <param name="size">The number.</param>
    /// <returns>The factors.</returns>
    private static int[] Factorise(int size)
    {
        var factors = new List<int>();
        var remaining = size;
        var factor = PreferredFactor;
        while (remaining > 1)
        {
            while (remaining % factor != 0)
            {
                factor = NextFactor(factor);
            }

            factors.Add(factor);
            remaining /= factor;
        }

        return [.. factors];
    }

    /// <summary>Gets the next factor to try after one that does not divide.</summary>
    /// <param name="factor">The factor that failed.</param>
    /// <returns>The next factor: 4 is followed by 2, then 3, then 5.</returns>
    private static int NextFactor(int factor) => factor switch
    {
        PreferredFactor => SecondFactor,
        SecondFactor => ThirdFactor,
        _ => factor + FactorStep,
    };

    /// <summary>Transforms the part of the input that starts at a place and steps by a stride.</summary>
    /// <param name="input">The whole input.</param>
    /// <param name="source">Where the part starts and how far apart its points are, in complex numbers.</param>
    /// <param name="count">The points in the part.</param>
    /// <param name="output">The space the transform of the part goes to.</param>
    /// <param name="level">How many factors were already divided out.</param>
    private void Split(ReadOnlySpan<float> input, Slice source, int count, Span<float> output, int level)
    {
        if (count == 1)
        {
            output[0] = input[source.Start * ComplexParts];
            output[1] = input[(source.Start * ComplexParts) + 1];
            return;
        }

        var factor = _factors[level];
        var part = count / factor;
        for (var q = 0; q < factor; q++)
        {
            Split(input, new(source.Start + (q * source.Stride), source.Stride * factor), part, output.Slice(q * part * ComplexParts, part * ComplexParts), level + 1);
        }

        Join(output, factor, part, count);
    }

    /// <summary>Joins the transforms of the parts of a split into the transform of the whole.</summary>
    /// <param name="values">The transforms of the parts, one after another; replaced by the transform of the whole.</param>
    /// <param name="factor">The parts.</param>
    /// <param name="part">The points in each part.</param>
    /// <param name="count">The points in the whole.</param>
    private void Join(Span<float> values, int factor, int part, int count)
    {
        var step = _size / count;
        var turn = _size / factor;
        Span<float> real = stackalloc float[MaxFactor];
        Span<float> imaginary = stackalloc float[MaxFactor];
        for (var k = 0; k < part; k++)
        {
            for (var q = 0; q < factor; q++)
            {
                var at = ((q * part) + k) * ComplexParts;
                var twiddle = step * q * k % _size;
                real[q] = (values[at] * _cos[twiddle]) - (values[at + 1] * _sin[twiddle]);
                imaginary[q] = (values[at] * _sin[twiddle]) + (values[at + 1] * _cos[twiddle]);
            }

            for (var p = 0; p < factor; p++)
            {
                values[((p * part) + k) * ComplexParts] = SumPoint(real, imaginary, factor, p * turn, true);
                values[(((p * part) + k) * ComplexParts) + 1] = SumPoint(real, imaginary, factor, p * turn, false);
            }
        }
    }

    /// <summary>Adds the points of a butterfly, each turned by its own multiple of a root of unity.</summary>
    /// <param name="real">The real parts of the points.</param>
    /// <param name="imaginary">The imaginary parts of the points.</param>
    /// <param name="factor">The points.</param>
    /// <param name="rootStep">The root of unity for the output, as a multiple of a step of a full turn over the size.</param>
    /// <param name="wantReal">Whether to give the real part rather than the imaginary part.</param>
    /// <returns>The sum.</returns>
    private float SumPoint(ReadOnlySpan<float> real, ReadOnlySpan<float> imaginary, int factor, int rootStep, bool wantReal)
    {
        float sum = 0;
        for (var q = 0; q < factor; q++)
        {
            var at = q * rootStep % _size;
            sum += wantReal
                ? (real[q] * _cos[at]) - (imaginary[q] * _sin[at])
                : (real[q] * _sin[at]) + (imaginary[q] * _cos[at]);
        }

        return sum;
    }

    /// <summary>Where a part of the input starts and how far apart its points are.</summary>
    /// <param name="Start">The first point's position, in complex numbers.</param>
    /// <param name="Stride">The distance between points, in complex numbers.</param>
    private readonly record struct Slice(int Start, int Stride);
}
