// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>The arithmetic integer and symbol ID encoding procedures of T.88 annex A, the inverse of <see cref="Jbig2IntegerDecoder"/>.</summary>
internal static class Jbig2IntegerEncoder
{
    /// <summary>The value of PREV that wraps the context index once it passes eight bits.</summary>
    private const int WrapLimit = 256;

    /// <summary>The mask that keeps the low nine bits of PREV.</summary>
    private const int WrapMask = 511;

    /// <summary>The index of the last prefix range, whose prefix has no terminating zero.</summary>
    private const int LastRange = 5;

    /// <summary>The first value of each prefix range (T.88 table A.1).</summary>
    private static readonly int[] RangeOffsets = [0x0000, 0x0004, 0x0014, 0x0054, 0x0154, 0x1154];

    /// <summary>Gets the value bits of each prefix range.</summary>
    private static ReadOnlySpan<byte> RangeBits => [0x02, 0x04, 0x06, 0x08, 0x0C, 0x20];

    /// <summary>Encodes one integer (T.88 A.2).</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The encoder's <see cref="Jbig2IntegerDecoder.ContextCount"/> contexts.</param>
    /// <param name="value">The value.</param>
    internal static void Encode(ref Jbig2MqEncoder encoder, Span<byte> contexts, int value)
    {
        var sign = value < 0 ? 1 : 0;
        var magnitude = Math.Abs((long)value);
        var range = 0;
        while (range < LastRange && magnitude >= RangeOffsets[range + 1])
        {
            range++;
        }

        Write(ref encoder, contexts, sign, range, magnitude - RangeOffsets[range]);
    }

    /// <summary>Encodes the out-of-band value.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The encoder's contexts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EncodeOutOfBand(ref Jbig2MqEncoder encoder, Span<byte> contexts) => Write(ref encoder, contexts, 1, 0, 0);

    /// <summary>Encodes a symbol ID (T.88 A.3).</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The <c>1 &lt;&lt; codeLength</c> contexts.</param>
    /// <param name="codeLength">The bits in a symbol ID.</param>
    /// <param name="id">The symbol ID.</param>
    internal static void EncodeId(ref Jbig2MqEncoder encoder, Span<byte> contexts, int codeLength, int id)
    {
        var previous = 1;
        for (var i = codeLength - 1; i >= 0; i--)
        {
            var bit = (id >> i) & 1;
            encoder.Encode(ref contexts[previous], bit);
            previous = (previous << 1) | bit;
        }
    }

    /// <summary>Writes the sign, the prefix of a range and the offset within it.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="sign">The sign bit.</param>
    /// <param name="range">The prefix range.</param>
    /// <param name="offset">The magnitude minus the range's first value.</param>
    private static void Write(ref Jbig2MqEncoder encoder, Span<byte> contexts, int sign, int range, long offset)
    {
        var previous = 1;
        Put(ref encoder, contexts, ref previous, sign);
        for (var i = 0; i < range; i++)
        {
            Put(ref encoder, contexts, ref previous, 1);
        }

        if (range < LastRange)
        {
            Put(ref encoder, contexts, ref previous, 0);
        }

        for (var i = RangeBits[range] - 1; i >= 0; i--)
        {
            Put(ref encoder, contexts, ref previous, (int)((offset >> i) & 1));
        }
    }

    /// <summary>Encodes one bit and advances PREV.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="previous">PREV.</param>
    /// <param name="bit">The bit.</param>
    private static void Put(ref Jbig2MqEncoder encoder, Span<byte> contexts, ref int previous, int bit)
    {
        encoder.Encode(ref contexts[previous], bit);
        previous = (previous << 1) | bit;
        if (previous >= WrapLimit)
        {
            previous = (previous & WrapMask) | WrapLimit;
        }
    }
}
