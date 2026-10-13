// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Builds the <c>/CIDSet</c> bitmap of a CIDFont (ISO 32000-2, 9.8.3): bit zero of the first byte is the most significant.</summary>
internal static class CidSets
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bit of the first CID in a byte.</summary>
    private const int FirstBit = 0x80;

    /// <summary>Marks the CIDs from zero up to a count.</summary>
    /// <param name="count">The number of CIDs, which are 0 up to one less than this.</param>
    /// <returns>The bitmap.</returns>
    internal static byte[] FromCount(int count)
    {
        var bitmap = new byte[(count + ByteBits - 1) / ByteBits];
        var whole = count / ByteBits;
        bitmap.AsSpan(0, whole).Fill(byte.MaxValue);
        if (count % ByteBits != 0)
        {
            bitmap[whole] = (byte)(byte.MaxValue << (ByteBits - (count % ByteBits)));
        }

        return bitmap;
    }

    /// <summary>Marks the given CIDs and CID zero.</summary>
    /// <param name="cids">The CIDs, ascending.</param>
    /// <returns>The bitmap.</returns>
    internal static byte[] FromCids(ReadOnlySpan<int> cids)
    {
        var last = cids.IsEmpty ? 0 : cids[^1];
        var bitmap = new byte[(last / ByteBits) + 1];
        bitmap[0] = FirstBit;
        foreach (var cid in cids)
        {
            bitmap[cid / ByteBits] |= (byte)(FirstBit >> (cid % ByteBits));
        }

        return bitmap;
    }
}
