// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads the glyph offsets from a TrueType 'loca' table.</summary>
internal static class TrueTypeLocations
{
    /// <summary>The bytes of a long /loca entry.</summary>
    internal const int LongEntry = 4;

    /// <summary>The bytes of a short /loca entry.</summary>
    internal const int ShortEntry = 2;

    /// <summary>Reads the glyph offsets, checking they lie inside /glyf in order.</summary>
    /// <param name="loca">The /loca table.</param>
    /// <param name="glyfLength">The length of the /glyf table.</param>
    /// <param name="count">The glyph count.</param>
    /// <param name="longLoca">Whether entries are 32-bit offsets rather than 16-bit halves.</param>
    /// <param name="offsets">Receives the count plus one offsets into /glyf.</param>
    /// <returns><see langword="true"/> when the table is whole and consistent.</returns>
    /// <exception cref="PdfException">A glyph count or offset exceeds the available font data.</exception>
    internal static bool TryRead(ReadOnlySpan<byte> loca, int glyfLength, int count, bool longLoca, out int[] offsets)
    {
        ValidateCount(loca, count, longLoca);
        offsets = new int[count + 1];
        return TryRead(loca, glyfLength, count, longLoca, offsets.AsSpan());
    }

    /// <summary>Reads the glyph offsets into a caller-owned span, checking they lie inside /glyf in order.</summary>
    /// <param name="loca">The /loca table.</param>
    /// <param name="glyfLength">The length of the /glyf table.</param>
    /// <param name="count">The glyph count.</param>
    /// <param name="longLoca">Whether entries are 32-bit offsets rather than 16-bit halves.</param>
    /// <param name="offsets">Receives the count plus one offsets into /glyf; it must hold at least that many.</param>
    /// <returns><see langword="true"/> when the table is whole and consistent.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static bool TryRead(ReadOnlySpan<byte> loca, int glyfLength, int count, bool longLoca, Span<int> offsets)
    {
        var entry = longLoca ? LongEntry : ShortEntry;
        ValidateCount(loca, count, longLoca);
        if (count == 0)
        {
            return false;
        }

        if (offsets.Length <= count || glyfLength < 0)
        {
            throw new PdfException(PdfError.Format, "The TrueType glyph locations exceed their destination.");
        }

        for (var i = 0; i <= count; i++)
        {
            var at = loca[(i * entry)..];
            var offset = longLoca ? BinaryPrimitives.ReadUInt32BigEndian(at) : BinaryPrimitives.ReadUInt16BigEndian(at) * (long)ShortEntry;
            if (offset > glyfLength)
            {
                throw new PdfException(PdfError.Format, "The TrueType glyph offset exceeds its data.");
            }

            if (i > 0 && offset < offsets[i - 1])
            {
                return false;
            }

            offsets[i] = (int)offset;
        }

        return true;
    }

    /// <summary>Checks a glyph count before allocating or slicing its location entries.</summary>
    /// <param name="loca">The location bytes.</param>
    /// <param name="count">The glyph count.</param>
    /// <param name="longLoca">Whether entries have four bytes.</param>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static void ValidateCount(ReadOnlySpan<byte> loca, int count, bool longLoca)
    {
        if (count < 0 || (long)count + 1 > loca.Length / (longLoca ? LongEntry : ShortEntry))
        {
            throw new PdfException(PdfError.Format, "The TrueType glyph location count exceeds its data.");
        }
    }
}
