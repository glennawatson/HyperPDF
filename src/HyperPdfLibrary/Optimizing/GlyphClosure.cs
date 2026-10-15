// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Finds every glyph a set of TrueType glyphs needs: the glyphs, glyph 0 and the parts of composite glyphs.</summary>
internal static class GlyphClosure
{
    /// <summary>The glyph batch between cancellation checks.</summary>
    private const int GlyphBatch = 256;

    /// <summary>Marks the glyphs to keep.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf, one more than the glyph count.</param>
    /// <param name="used">The glyphs the document shows.</param>
    /// <param name="keep">Receives whether each glyph is kept; it holds one entry per glyph.</param>
    /// <param name="cancellationToken">Stops composite-glyph expansion.</param>
    internal static void Find(ReadOnlySpan<byte> glyf, ReadOnlySpan<int> offsets, CodeBitSet used, Span<bool> keep, CancellationToken cancellationToken)
    {
        keep.Clear();
        var pending = default(RentedList<int>);
        try
        {
            pending.Add(0);
            for (var glyph = used.NextBit(0); glyph != CodeBitSet.NoBit; glyph = used.NextBit(glyph + 1))
            {
                pending.Add(glyph);
            }

            if (used.Overflow is { } overflow)
            {
                foreach (var glyph in overflow)
                {
                    pending.Add(glyph);
                }
            }

            Expand(glyf, offsets, keep, ref pending, cancellationToken);
        }
        finally
        {
            pending.Dispose();
        }
    }

    /// <summary>Keeps each pending glyph and queues the parts of the composite ones.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="keep">Receives whether each glyph is kept.</param>
    /// <param name="pending">The glyphs still to keep; drained.</param>
    /// <param name="cancellationToken">Stops composite-glyph expansion.</param>
    private static void Expand(ReadOnlySpan<byte> glyf, ReadOnlySpan<int> offsets, Span<bool> keep, ref RentedList<int> pending, CancellationToken cancellationToken)
    {
        var processed = 0;
        while (pending.Count > 0)
        {
            if ((processed & (GlyphBatch - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            processed++;
            var glyph = pending[pending.Count - 1];
            pending.Count--;
            if ((uint)glyph >= (uint)keep.Length || keep[glyph])
            {
                continue;
            }

            keep[glyph] = true;
            var walker = new CompositeWalker(glyf[offsets[glyph]..offsets[glyph + 1]]);
            while (walker.MoveNext())
            {
                pending.Add(walker.Glyph);
            }
        }
    }
}
