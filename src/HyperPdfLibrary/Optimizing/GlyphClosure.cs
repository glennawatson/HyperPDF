// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Finds every glyph a set of TrueType glyphs needs: the glyphs, glyph 0 and the parts of composite glyphs.</summary>
internal static class GlyphClosure
{
    /// <summary>Marks the glyphs to keep.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf, one more than the glyph count.</param>
    /// <param name="used">The glyphs the document shows.</param>
    /// <returns>Whether each glyph is kept.</returns>
    internal static bool[] Find(ReadOnlySpan<byte> glyf, int[] offsets, CodeBitSet used)
    {
        var keep = new bool[offsets.Length - 1];
        var pending = new Stack<int>();
        pending.Push(0);
        for (var glyph = used.NextBit(0); glyph != CodeBitSet.NoBit; glyph = used.NextBit(glyph + 1))
        {
            pending.Push(glyph);
        }

        if (used.Overflow is { } overflow)
        {
            foreach (var glyph in overflow)
            {
                pending.Push(glyph);
            }
        }

        while (pending.TryPop(out var glyph))
        {
            if ((uint)glyph >= (uint)keep.Length || keep[glyph])
            {
                continue;
            }

            keep[glyph] = true;
            var walker = new CompositeWalker(glyf[offsets[glyph]..offsets[glyph + 1]]);
            while (walker.MoveNext())
            {
                pending.Push(walker.Glyph);
            }
        }

        return keep;
    }
}
