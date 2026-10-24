// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The scaling and inverse transforms of residual blocks, as clause 8.5 of ITU-T H.264 gives them.</summary>
internal static class H264Transform
{
    /// <summary>The pixels in a 4x4 block.</summary>
    internal const int BlockSize = 16;

    /// <summary>The width of a 4x4 block.</summary>
    internal const int BlockWidth = 4;

    /// <summary>The samples in an 8x8 block.</summary>
    private const int LargeBlockSize = 64;

    /// <summary>The width of an 8x8 block.</summary>
    private const int LargeBlockWidth = 8;

    /// <summary>The bits the scaled 8x8 coefficients are shifted right by before they are used.</summary>
    private const int LargeScaleShift = 6;

    /// <summary>The index of the fifth entry of a row.</summary>
    private const int Fourth = 4;

    /// <summary>The index of the sixth entry of a row.</summary>
    private const int Fifth = 5;

    /// <summary>The index of the seventh entry of a row.</summary>
    private const int Sixth = 6;

    /// <summary>The index of the eighth entry of a row.</summary>
    private const int Seventh = 7;

    /// <summary>The index of the third entry of a row.</summary>
    private const int Column2 = 2;

    /// <summary>The index of the fourth entry of a row.</summary>
    private const int Column3 = 3;

    /// <summary>The index of the second row.</summary>
    private const int Row1 = 4;

    /// <summary>The index of the third row.</summary>
    private const int Row2 = 8;

    /// <summary>The index of the fourth row.</summary>
    private const int Row3 = 12;

    /// <summary>The steps of the quantiser that double the scale.</summary>
    private const int QpPerOctave = 6;

    /// <summary>The quantiser from which the luma DC scaling shifts left.</summary>
    private const int DcLeftShiftQp = 36;

    /// <summary>The bits the scaled coefficients are shifted right by before they are used.</summary>
    private const int ScaleShift = 4;

    /// <summary>The bits the luma DC scaling is shifted right by at low quantisers.</summary>
    private const int DcScaleShift = 6;

    /// <summary>The bits the chroma DC scaling is shifted right by.</summary>
    private const int ChromaDcShift = 5;

    /// <summary>The bits the 4x4 transform output is shifted right by.</summary>
    private const int OutputShift = 6;

    /// <summary>The rounding added before that shift.</summary>
    private const int OutputRound = 32;

    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>Scales the coefficients of a 4x4 block.</summary>
    /// <param name="coefficients">The 16 levels by raster position; the scaled values replace them.</param>
    /// <param name="qp">The quantiser.</param>
    /// <param name="factors">The level scale factors of the list and remainder.</param>
    /// <param name="first">The first position to scale: 1 when the DC value is scaled separately.</param>
    internal static void Scale4x4(Span<int> coefficients, int qp, ReadOnlySpan<int> factors, int first)
    {
        var shift = (qp / QpPerOctave) - ScaleShift;
        if (shift >= 0)
        {
            for (var index = first; index < BlockSize; index++)
            {
                coefficients[index] = (coefficients[index] * factors[index]) << shift;
            }

            return;
        }

        var round = 1 << (-shift - 1);
        for (var index = first; index < BlockSize; index++)
        {
            coefficients[index] = ((coefficients[index] * factors[index]) + round) >> -shift;
        }
    }

    /// <summary>Transforms and scales the DC levels of an Intra 16x16 macroblock.</summary>
    /// <param name="levels">The 16 DC levels as a 4x4 matrix by raster position; the scaled DC values replace them.</param>
    /// <param name="qp">The quantiser.</param>
    /// <param name="factor">The level scale factor of position 0.</param>
    internal static void LumaDc(Span<int> levels, int qp, int factor)
    {
        Hadamard4x4(levels);
        var shift = (qp / QpPerOctave) - DcScaleShift;
        if (qp >= DcLeftShiftQp)
        {
            for (var index = 0; index < BlockSize; index++)
            {
                levels[index] = (levels[index] * factor) << shift;
            }

            return;
        }

        var round = 1 << (-shift - 1);
        for (var index = 0; index < BlockSize; index++)
        {
            levels[index] = ((levels[index] * factor) + round) >> -shift;
        }
    }

    /// <summary>Transforms and scales the DC levels of a chroma component of 4:2:0 video.</summary>
    /// <param name="levels">The 4 DC levels in raster order; the scaled DC values replace them.</param>
    /// <param name="qp">The chroma quantiser.</param>
    /// <param name="factor">The level scale factor of position 0.</param>
    internal static void ChromaDc2x2(Span<int> levels, int qp, int factor)
    {
        var a = levels[0] + levels[1];
        var b = levels[0] - levels[1];
        var c = levels[Column2] + levels[Column3];
        var d = levels[Column2] - levels[Column3];
        levels[0] = a + c;
        levels[1] = b + d;
        levels[Column2] = a - c;
        levels[Column3] = b - d;
        for (var index = 0; index < levels.Length; index++)
        {
            levels[index] = ((levels[index] * factor) << (qp / QpPerOctave)) >> ChromaDcShift;
        }
    }

    /// <summary>Runs the inverse 4x4 transform on scaled coefficients and adds the result to the prediction already in the picture.</summary>
    /// <param name="coefficients">The 16 scaled coefficients by raster position; they are used up.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row of the plane.</param>
    internal static void AddInverse4x4(Span<int> coefficients, byte[] plane, int offset, int stride)
    {
        for (var row = 0; row < BlockWidth; row++)
        {
            var at = row * BlockWidth;
            var e0 = coefficients[at] + coefficients[at + Column2];
            var e1 = coefficients[at] - coefficients[at + Column2];
            var e2 = (coefficients[at + 1] >> 1) - coefficients[at + Column3];
            var e3 = coefficients[at + 1] + (coefficients[at + Column3] >> 1);
            coefficients[at] = e0 + e3;
            coefficients[at + 1] = e1 + e2;
            coefficients[at + Column2] = e1 - e2;
            coefficients[at + Column3] = e0 - e3;
        }

        for (var column = 0; column < BlockWidth; column++)
        {
            var g0 = coefficients[column] + coefficients[column + Row2];
            var g1 = coefficients[column] - coefficients[column + Row2];
            var g2 = (coefficients[column + Row1] >> 1) - coefficients[column + Row3];
            var g3 = coefficients[column + Row1] + (coefficients[column + Row3] >> 1);
            AddSample(plane, offset + column, g0 + g3);
            AddSample(plane, offset + stride + column, g1 + g2);
            AddSample(plane, offset + (stride << 1) + column, g1 - g2);
            AddSample(plane, offset + (stride * Column3) + column, g0 - g3);
        }
    }

    /// <summary>Scales the coefficients of an 8x8 block (clause 8.5.13.1).</summary>
    /// <param name="coefficients">The 64 levels by raster position; the scaled values replace them.</param>
    /// <param name="qp">The quantiser.</param>
    /// <param name="factors">The level scale factors of the list and remainder.</param>
    internal static void Scale8x8(Span<int> coefficients, int qp, ReadOnlySpan<int> factors)
    {
        var shift = (qp / QpPerOctave) - LargeScaleShift;
        if (shift >= 0)
        {
            for (var index = 0; index < LargeBlockSize; index++)
            {
                coefficients[index] = (coefficients[index] * factors[index]) << shift;
            }

            return;
        }

        var round = 1 << (-shift - 1);
        for (var index = 0; index < LargeBlockSize; index++)
        {
            coefficients[index] = ((coefficients[index] * factors[index]) + round) >> -shift;
        }
    }

    /// <summary>Runs the inverse 8x8 transform on scaled coefficients and adds the result to the prediction already in the picture (clause 8.5.13.2).</summary>
    /// <param name="coefficients">The 64 scaled coefficients by raster position; they are used up.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row of the plane.</param>
    internal static void AddInverse8x8(Span<int> coefficients, byte[] plane, int offset, int stride)
    {
        for (var row = 0; row < LargeBlockWidth; row++)
        {
            Butterfly8(coefficients, row * LargeBlockWidth, 1);
        }

        for (var column = 0; column < LargeBlockWidth; column++)
        {
            Butterfly8(coefficients, column, LargeBlockWidth);
        }

        for (var row = 0; row < LargeBlockWidth; row++)
        {
            for (var column = 0; column < LargeBlockWidth; column++)
            {
                AddSample(plane, offset + (row * stride) + column, coefficients[(row * LargeBlockWidth) + column]);
            }
        }
    }

    /// <summary>Runs the one-dimensional inverse 8-point transform along a row or column in place (8-358 to 8-381).</summary>
    /// <param name="values">The 64 values.</param>
    /// <param name="start">The index of the first value of the row or column.</param>
    /// <param name="step">The distance between the values: 1 along a row, 8 down a column.</param>
    private static void Butterfly8(Span<int> values, int start, int step)
    {
        var d0 = values[start];
        var d1 = values[start + step];
        var d2 = values[start + (Column2 * step)];
        var d3 = values[start + (Column3 * step)];
        var d4 = values[start + (Fourth * step)];
        var d5 = values[start + (Fifth * step)];
        var d6 = values[start + (Sixth * step)];
        var d7 = values[start + (Seventh * step)];
        var e0 = d0 + d4;
        var e1 = -d3 + d5 - d7 - (d7 >> 1);
        var e2 = d0 - d4;
        var e3 = d1 + d7 - d3 - (d3 >> 1);
        var e4 = (d2 >> 1) - d6;
        var e5 = -d1 + d7 + d5 + (d5 >> 1);
        var e6 = d2 + (d6 >> 1);
        var e7 = d3 + d5 + d1 + (d1 >> 1);
        var f0 = e0 + e6;
        var f1 = e1 + (e7 >> Column2);
        var f2 = e2 + e4;
        var f3 = e3 + (e5 >> Column2);
        var f4 = e2 - e4;
        var f5 = (e3 >> Column2) - e5;
        var f6 = e0 - e6;
        var f7 = e7 - (e1 >> Column2);
        values[start] = f0 + f7;
        values[start + step] = f2 + f5;
        values[start + (Column2 * step)] = f4 + f3;
        values[start + (Column3 * step)] = f6 + f1;
        values[start + (Fourth * step)] = f6 - f1;
        values[start + (Fifth * step)] = f4 - f3;
        values[start + (Sixth * step)] = f2 - f5;
        values[start + (Seventh * step)] = f0 - f7;
    }

    /// <summary>Adds a residual to a sample and clips the sum.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="index">The sample's index.</param>
    /// <param name="residual">The residual before the final shift.</param>
    private static void AddSample(byte[] plane, int index, int residual)
    {
        var value = plane[index] + ((residual + OutputRound) >> OutputShift);
        plane[index] = (byte)Math.Clamp(value, 0, MaxSample);
    }

    /// <summary>Applies the 4x4 Hadamard transform to a matrix in place.</summary>
    /// <param name="matrix">The 16 values by raster position.</param>
    private static void Hadamard4x4(Span<int> matrix)
    {
        for (var row = 0; row < BlockWidth; row++)
        {
            var at = row * BlockWidth;
            var a = matrix[at] + matrix[at + 1];
            var b = matrix[at] - matrix[at + 1];
            var c = matrix[at + Column2] + matrix[at + Column3];
            var d = matrix[at + Column2] - matrix[at + Column3];
            matrix[at] = a + c;
            matrix[at + 1] = a - c;
            matrix[at + Column2] = b - d;
            matrix[at + Column3] = b + d;
        }

        for (var column = 0; column < BlockWidth; column++)
        {
            var a = matrix[column] + matrix[column + Row1];
            var b = matrix[column] - matrix[column + Row1];
            var c = matrix[column + Row2] + matrix[column + Row3];
            var d = matrix[column + Row2] - matrix[column + Row3];
            matrix[column] = a + c;
            matrix[column + Row1] = a - c;
            matrix[column + Row2] = b - d;
            matrix[column + Row3] = b + d;
        }
    }
}
