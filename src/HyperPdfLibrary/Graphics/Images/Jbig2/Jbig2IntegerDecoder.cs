// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The arithmetic integer and symbol ID decoding procedures of T.88 annex A.</summary>
internal static class Jbig2IntegerDecoder
{
    /// <summary>The contexts one integer decoder needs.</summary>
    internal const int ContextCount = 512;

    /// <summary>The value of PREV that wraps the context index once it passes eight bits.</summary>
    private const int WrapLimit = 256;

    /// <summary>The mask that keeps the low nine bits of PREV.</summary>
    private const int WrapMask = 511;

    /// <summary>The number of prefix ranges after the first.</summary>
    private const int LastRange = 5;

    /// <summary>
    /// The first value of each prefix range. A static array rather than a span property, because spans of multi-byte
    /// constants allocate on every access in unoptimised builds.
    /// </summary>
    private static readonly int[] RangeOffsets = [0x0000, 0x0004, 0x0014, 0x0054, 0x0154, 0x1154];

    /// <summary>Gets the value bits of each prefix range.</summary>
    private static ReadOnlySpan<byte> RangeBits => [0x02, 0x04, 0x06, 0x08, 0x0C, 0x20];

    /// <summary>Decodes one integer (T.88 A.2).</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The decoder's <see cref="ContextCount"/> contexts.</param>
    /// <param name="value">The value, or zero for out-of-band.</param>
    /// <returns><see langword="false"/> for the out-of-band value, or a value that does not fit 32 bits.</returns>
    internal static bool Decode(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, out int value)
    {
        var previous = 1;
        var sign = decoder.Decode(ref contexts[previous]);
        previous = (previous << 1) | sign;
        var range = 0;
        while (range < LastRange)
        {
            var bit = decoder.Decode(ref contexts[previous]);
            previous = (previous << 1) | bit;
            if (bit == 0)
            {
                break;
            }

            range++;
        }

        var bits = 0;
        for (var i = 0; i < RangeBits[range]; i++)
        {
            var bit = decoder.Decode(ref contexts[previous]);
            previous = (previous << 1) | bit;
            if (previous >= WrapLimit)
            {
                previous = (previous & WrapMask) | WrapLimit;
            }

            bits = (bits << 1) | bit;
        }

        return Finish(RangeOffsets[range] + (long)bits, sign, out value);
    }

    /// <summary>Decodes a symbol ID (T.88 A.3).</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The <c>1 &lt;&lt; codeLength</c> contexts.</param>
    /// <param name="codeLength">The bits in a symbol ID.</param>
    /// <returns>The symbol ID.</returns>
    internal static int DecodeId(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, int codeLength)
    {
        var previous = 1;
        for (var i = 0; i < codeLength; i++)
        {
            previous = (previous << 1) | decoder.Decode(ref contexts[previous]);
        }

        return previous - (1 << codeLength);
    }

    /// <summary>Applies the sign and checks the range of a decoded magnitude (T.88 section A.2): a result that does not fit 32 bits is rejected.</summary>
    /// <param name="magnitude">The magnitude.</param>
    /// <param name="sign">The sign bit.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> for out-of-band or overflow.</returns>
    private static bool Finish(long magnitude, int sign, out int value)
    {
        if (magnitude > int.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (int)magnitude;
        if (sign == 1 && value > 0)
        {
            value = -value;
        }

        return sign != 1 || value != 0;
    }
}
