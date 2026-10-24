// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The inverse modified discrete cosine transform of CELT (RFC 6716 section 4.3.7): a quarter-size complex transform with a
/// rotation before and after it, then a fold of the result into a block of time samples whose ends overlap the neighbouring blocks.
/// </summary>
internal sealed class CeltMdct
{
    /// <summary>The longest transform in samples, which is two long blocks of 960.</summary>
    internal const int MaxSize = 1920;

    /// <summary>The number of shortened transforms the sizes are halved for: a block of 960, 480, 240 or 120.</summary>
    private const int Shifts = 4;

    /// <summary>The shift that takes a transform's size to a quarter of it.</summary>
    private const int QuarterShift = 2;

    /// <summary>A quarter of the longest transform's size, the points of its complex transform.</summary>
    private const int LongestQuarter = MaxSize >> QuarterShift;

    /// <summary>The real and imaginary part of a complex number.</summary>
    private const int ComplexParts = 2;

    /// <summary>One eighth of a turn, the angle the rotation is corrected by.</summary>
    private const double EighthTurn = 0.125;

    /// <summary>The angle of a full turn.</summary>
    private const double FullTurn = 2.0 * Math.PI;

    /// <summary>The cosines of a full turn divided into the longest transform's size.</summary>
    private readonly float[] _trig;

    /// <summary>The complex transforms, for the longest block and each shortened one.</summary>
    private readonly CeltInverseFft[] _ffts;

    /// <summary>Initializes a new instance of the <see cref="CeltMdct"/> class.</summary>
    internal CeltMdct()
    {
        _trig = new float[LongestQuarter + 1];
        for (var i = 0; i <= LongestQuarter; i++)
        {
            _trig[i] = (float)Math.Cos(FullTurn * i / MaxSize);
        }

        _ffts = new CeltInverseFft[Shifts];
        for (var shift = 0; shift < Shifts; shift++)
        {
            _ffts[shift] = new(LongestQuarter >> shift);
        }
    }

    /// <summary>Gets the scratch space the transform needs, in numbers.</summary>
    internal static int ScratchSize => MaxSize;

    /// <summary>Turns the spectrum of a block into time samples, adding the overlap with the blocks around it.</summary>
    /// <param name="spectrum">The spectrum; the block's entries are a stride apart.</param>
    /// <param name="stride">The distance between the entries of the block.</param>
    /// <param name="output">Receives the samples, which are added to the overlap already there; the block starts at the beginning and is the block's entries plus the overlap long.</param>
    /// <param name="shape">The window and how many times the longest transform is halved.</param>
    /// <param name="scratch">Space for the transform, <see cref="ScratchSize"/> numbers.</param>
    internal void Backward(ReadOnlySpan<float> spectrum, int stride, Span<float> output, CeltMdctShape shape, Span<float> scratch)
    {
        var size = MaxSize >> shape.Shift;
        var half = size >> 1;
        var quarter = size >> QuarterShift;
        var rotated = scratch[..half];
        var folded = scratch.Slice(half, half);
        PreRotate(spectrum, stride, folded, shape.Shift);
        _ffts[shape.Shift].Transform(folded, rotated);
        PostRotate(rotated, shape.Shift);
        Deshuffle(rotated, folded, quarter, half);
        Overlap(folded, output, shape, size);
    }

    /// <summary>Takes every other number from the two ends of the rotated pairs to make the middle of the window.</summary>
    /// <param name="values">The rotated pairs.</param>
    /// <param name="target">Receives the shuffled numbers.</param>
    /// <param name="quarter">A quarter of the transform's size.</param>
    /// <param name="half">Half of the transform's size.</param>
    private static void Deshuffle(ReadOnlySpan<float> values, Span<float> target, int quarter, int half)
    {
        var first = 0;
        var last = half - 1;
        for (var i = 0; i < quarter; i++)
        {
            target[i * ComplexParts] = -values[first];
            target[(i * ComplexParts) + 1] = values[last];
            first += ComplexParts;
            last -= ComplexParts;
        }
    }

    /// <summary>Mirrors the numbers on both sides and windows their ends so neighbouring blocks cancel each other's aliasing.</summary>
    /// <param name="values">The shuffled numbers.</param>
    /// <param name="output">Receives the samples; the overlap already in it is added to.</param>
    /// <param name="shape">The window and the overlap.</param>
    /// <param name="size">The transform's size.</param>
    private static void Overlap(ReadOnlySpan<float> values, Span<float> output, CeltMdctShape shape, int size)
    {
        var half = size >> 1;
        var quarter = size >> QuarterShift;
        var overlap = shape.Overlap;
        var window = shape.Window;
        var edge = overlap >> 1;
        var origin = -((half - overlap) >> 1);
        var flat = quarter - edge;
        var low = quarter - 1;
        var upper = origin + half - 1;
        var lower = origin + flat;
        for (var i = 0; i < flat; i++)
        {
            output[upper - i] = values[low - i];
        }

        for (var i = 0; i < edge; i++)
        {
            var x = values[low - flat - i];
            output[lower + i] += -window[i] * x;
            output[upper - flat - i] += window[overlap - 1 - i] * x;
        }

        var top = origin + half;
        var end = origin + size - 1 - flat;
        for (var i = 0; i < flat; i++)
        {
            output[top + i] = values[quarter + i];
        }

        for (var i = 0; i < edge; i++)
        {
            var x = values[quarter + flat + i];
            output[end - i] = window[i] * x;
            output[top + flat + i] = window[overlap - 1 - i] * x;
        }
    }

    /// <summary>Rotates the spectrum's entries in pairs from its two ends, ready for the complex transform.</summary>
    /// <param name="spectrum">The spectrum.</param>
    /// <param name="stride">The distance between the entries.</param>
    /// <param name="target">Receives the rotated pairs.</param>
    /// <param name="shift">How many times the longest transform is halved.</param>
    private void PreRotate(ReadOnlySpan<float> spectrum, int stride, Span<float> target, int shift)
    {
        var quarter = LongestQuarter >> shift;
        var sine = (float)(FullTurn * EighthTurn / (MaxSize >> shift));
        var first = 0;
        var last = stride * ((quarter * ComplexParts) - 1);
        for (var i = 0; i < quarter; i++)
        {
            var cosine = _trig[i << shift];
            var complement = _trig[(quarter - i) << shift];
            var yr = (-spectrum[last] * cosine) + (spectrum[first] * complement);
            var yi = (-spectrum[last] * complement) - (spectrum[first] * cosine);
            target[i * ComplexParts] = yr - (yi * sine);
            target[(i * ComplexParts) + 1] = yi + (yr * sine);
            first += ComplexParts * stride;
            last -= ComplexParts * stride;
        }
    }

    /// <summary>Rotates the transformed pairs back.</summary>
    /// <param name="values">The transformed pairs, replaced by the rotated ones.</param>
    /// <param name="shift">How many times the longest transform is halved.</param>
    private void PostRotate(Span<float> values, int shift)
    {
        var quarter = LongestQuarter >> shift;
        var sine = (float)(FullTurn * EighthTurn / (MaxSize >> shift));
        for (var i = 0; i < quarter; i++)
        {
            var re = values[i * ComplexParts];
            var im = values[(i * ComplexParts) + 1];
            var cosine = _trig[i << shift];
            var complement = _trig[(quarter - i) << shift];
            var yr = (re * cosine) - (im * complement);
            var yi = (im * cosine) + (re * complement);
            values[i * ComplexParts] = yr - (yi * sine);
            values[(i * ComplexParts) + 1] = yi + (yr * sine);
        }
    }
}
