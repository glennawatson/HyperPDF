// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>Reading helpers for the AAC bit stream.</summary>
internal static class AacBits
{
    /// <summary>The bits in a byte.</summary>
    internal const int ByteBits = 8;

    /// <summary>The most bits skipped in one step.</summary>
    private const int SkipStep = 24;

    /// <summary>Skips bits of any number, stopping at the end of the data.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="bits">The number of bits.</param>
    internal static void Skip(ref MsbBitReader reader, long bits)
    {
        while (bits > 0 && !reader.IsOverrun)
        {
            var step = (int)Math.Min(bits, SkipStep);
            reader.Skip(step);
            bits -= step;
        }
    }
}
