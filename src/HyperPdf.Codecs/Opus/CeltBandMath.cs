// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Opus;

/// <summary>The small integer and vector helpers the CELT band decoding needs; the integer ones must give the same answers on every machine because they steer the bit allocation.</summary>
internal static class CeltBandMath
{
    /// <summary>The multiplier of the linear congruential noise generator.</summary>
    private const uint LcgMultiplier = 1_664_525;

    /// <summary>The increment of the linear congruential noise generator.</summary>
    private const uint LcgIncrement = 1_013_904_223;

    /// <summary>The square root of a half.</summary>
    private const float HalfRoot = 0.70710678F;

    /// <summary>Half of the scale of 15 fractional bits, added before the shift so products round.</summary>
    private const int RoundingHalf = 0x4000;

    /// <summary>The fractional bits of the integer cosine and sine.</summary>
    private const int AngleBits = 15;

    /// <summary>Half a unit of the cosine polynomial's input scale, added so its square rounds.</summary>
    private const int CosineRounding = 0x1000;

    /// <summary>The shift that brings the square of the angle to the scale of the cosine polynomial.</summary>
    private const int CosineShift = 13;

    /// <summary>The largest value of the cosine, one below a full scale.</summary>
    private const int CosinePeak = 0x7FFF;

    /// <summary>The first coefficient of the cosine polynomial.</summary>
    private const int CosineTerm1 = -7651;

    /// <summary>The second coefficient of the cosine polynomial.</summary>
    private const int CosineTerm2 = 8277;

    /// <summary>The third coefficient of the cosine polynomial.</summary>
    private const int CosineTerm3 = -626;

    /// <summary>The slope of the logarithm polynomial.</summary>
    private const int LogSlope = -2597;

    /// <summary>The offset of the logarithm polynomial.</summary>
    private const int LogOffset = 7932;

    /// <summary>The fractional bits of the logarithm.</summary>
    private const int LogBits = 11;

    /// <summary>The blocks, or entries, that a pair is made of.</summary>
    private const int Pair = 2;

    /// <summary>The most entries a band's reordering keeps on the stack.</summary>
    private const int StackLimit = 256;

    /// <summary>Gets the order in which Hadamard coefficients are interleaved: rows for 2, 4, 8 and 16 blocks.</summary>
    private static ReadOnlySpan<byte> HadamardOrder =>
    [
        0x01, 0x00,
        0x03, 0x00, 0x02, 0x01,
        0x07, 0x00, 0x04, 0x03, 0x06, 0x01, 0x05, 0x02,
        0x0F, 0x00, 0x08, 0x07, 0x0C, 0x03, 0x0B, 0x04, 0x0E, 0x01, 0x09, 0x06, 0x0D, 0x02, 0x0A, 0x05,
    ];

    /// <summary>Steps the noise generator.</summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The next seed.</returns>
    internal static uint NextRandom(uint seed) => unchecked((LcgMultiplier * seed) + LcgIncrement);

    /// <summary>Multiplies two numbers of 15 fractional bits, rounding.</summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The product.</returns>
    internal static int FractionMultiply(int a, int b) => (RoundingHalf + ((short)a * (short)b)) >> AngleBits;

    /// <summary>A cosine that gives the same result on every machine (RFC 6716 section 4.3.4.2).</summary>
    /// <param name="x">The angle, 0 to 16384 for a quarter turn.</param>
    /// <returns>The cosine scaled to 32768.</returns>
    internal static int BitExactCosine(int x)
    {
        var x2 = (CosineRounding + (x * x)) >> CosineShift;
        x2 = (CosinePeak - x2) + FractionMultiply(x2, CosineTerm1 + FractionMultiply(x2, CosineTerm2 + FractionMultiply(CosineTerm3, x2)));
        return 1 + x2;
    }

    /// <summary>The logarithm of the tangent of an angle, from its sine and cosine, in a form that is identical on every machine.</summary>
    /// <param name="sine">The sine, scaled to 32768.</param>
    /// <param name="cosine">The cosine, scaled to 32768.</param>
    /// <returns>The log2 of the tangent, in units of 1/2048.</returns>
    internal static int BitExactLog2Tangent(int sine, int cosine)
    {
        var lc = BitLength((uint)cosine);
        var ls = BitLength((uint)sine);
        cosine <<= AngleBits - lc;
        sine <<= AngleBits - ls;
        return ((ls - lc) * (1 << LogBits))
            + FractionMultiply(sine, FractionMultiply(sine, LogSlope) + LogOffset)
            - FractionMultiply(cosine, FractionMultiply(cosine, LogSlope) + LogOffset);
    }

    /// <summary>Gets the floor of a square root exactly.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The root.</returns>
    internal static uint SquareRoot(uint value)
    {
        if (value == 0)
        {
            return 0;
        }

        uint root = 0;
        var shift = (BitLength(value) - 1) >> 1;
        var bit = 1U << shift;
        do
        {
            var t = ((root << 1) + bit) << shift;
            if (t <= value)
            {
                root += bit;
                value -= t;
            }

            bit >>= 1;
            shift--;
        }
        while (shift >= 0);
        return root;
    }

    /// <summary>Turns pairs of entries into their sum and difference, scaled, a stride apart.</summary>
    /// <param name="values">The entries, changed in place.</param>
    /// <param name="length">The entries in each block.</param>
    /// <param name="stride">The distance between neighbouring entries of a block.</param>
    internal static void Haar(Span<float> values, int length, int stride)
    {
        var pairs = length >> 1;
        for (var i = 0; i < stride; i++)
        {
            for (var j = 0; j < pairs; j++)
            {
                var first = (stride * Pair * j) + i;
                var second = first + stride;
                var a = HalfRoot * values[first];
                var b = HalfRoot * values[second];
                values[first] = a + b;
                values[second] = a - b;
            }
        }
    }

    /// <summary>Reorders interleaved short blocks into time order, optionally in Hadamard order.</summary>
    /// <param name="values">The entries, changed in place.</param>
    /// <param name="blockSize">The entries in each block.</param>
    /// <param name="stride">The blocks.</param>
    /// <param name="hadamard">Whether to use the Hadamard order.</param>
    internal static void Deinterleave(Span<float> values, int blockSize, int stride, bool hadamard)
    {
        var length = blockSize * stride;
        Span<float> temp = length <= StackLimit ? stackalloc float[length] : new float[length];
        var order = HadamardOrder[(stride - Pair)..];
        for (var i = 0; i < stride; i++)
        {
            var target = hadamard ? order[i] : i;
            for (var j = 0; j < blockSize; j++)
            {
                temp[(target * blockSize) + j] = values[(j * stride) + i];
            }
        }

        temp.CopyTo(values);
    }

    /// <summary>Undoes <see cref="Deinterleave"/>.</summary>
    /// <param name="values">The entries, changed in place.</param>
    /// <param name="blockSize">The entries in each block.</param>
    /// <param name="stride">The blocks.</param>
    /// <param name="hadamard">Whether the Hadamard order was used.</param>
    internal static void Interleave(Span<float> values, int blockSize, int stride, bool hadamard)
    {
        var length = blockSize * stride;
        Span<float> temp = length <= StackLimit ? stackalloc float[length] : new float[length];
        var order = HadamardOrder[(stride - Pair)..];
        for (var i = 0; i < stride; i++)
        {
            var source = hadamard ? order[i] : i;
            for (var j = 0; j < blockSize; j++)
            {
                temp[(j * stride) + i] = values[(source * blockSize) + j];
            }
        }

        temp.CopyTo(values);
    }

    /// <summary>Gets the number of bits needed to write a positive number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The bits; zero for zero.</returns>
    private static int BitLength(uint value) => value == 0 ? 0 : BitOperations.Log2(value) + 1;
}
