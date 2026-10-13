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
    internal static bool TryRead(ReadOnlySpan<byte> loca, int glyfLength, int count, bool longLoca, out int[] offsets)
    {
        offsets = new int[count + 1];
        var entry = longLoca ? LongEntry : ShortEntry;
        if (count == 0 || loca.Length < (count + 1) * entry)
        {
            return false;
        }

        for (var i = 0; i <= count; i++)
        {
            var at = loca[(i * entry)..];
            var offset = longLoca ? BinaryPrimitives.ReadUInt32BigEndian(at) : BinaryPrimitives.ReadUInt16BigEndian(at) * (long)ShortEntry;
            if (offset > glyfLength || (i > 0 && offset < offsets[i - 1]))
            {
                return false;
            }

            offsets[i] = (int)offset;
        }

        return true;
    }
}
