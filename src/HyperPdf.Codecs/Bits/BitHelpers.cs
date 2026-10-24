// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Bits;

/// <summary>Reading helpers that the bit reader does not give directly.</summary>
internal static class BitHelpers
{
    /// <summary>The most bits the reader skips at once.</summary>
    private const int SkipStep = 32;

    /// <summary>The bits in a 32 bit word.</summary>
    private const int WordBits = 32;

    /// <summary>Skips any number of bits.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="count">The number of bits to skip.</param>
    internal static void Skip(ref MsbBitReader reader, int count)
    {
        while (count > SkipStep)
        {
            reader.Skip(SkipStep);
            count -= SkipStep;
        }

        reader.Skip(count);
    }

    /// <summary>Finds the number of bits needed to code values from 0 up to a count, Ceil(Log2(count)).</summary>
    /// <param name="count">The number of values.</param>
    /// <returns>The bits; 0 when there is one value.</returns>
    internal static int CeilLog2(int count) => count <= 1 ? 0 : WordBits - System.Numerics.BitOperations.LeadingZeroCount((uint)(count - 1));
}
