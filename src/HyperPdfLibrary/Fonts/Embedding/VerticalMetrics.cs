// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// The vertical metrics of the glyphs a font shows, in the arrays that go into <c>/DW2</c> and <c>/W2</c> (ISO 32000-2,
/// 9.7.4.3). Lengths are thousandths of an em. <see cref="Advances"/> is the signed displacement <c>w1</c>, so it is
/// negative for the downward advance of vertical writing.
/// </summary>
/// <param name="Advances">The displacement <c>w1</c> of each CID.</param>
/// <param name="OriginsY">The height of each CID's vertical origin above its horizontal origin, the <c>vy</c> of its position vector.</param>
[DebuggerDisplay("VerticalMetrics: {Advances.Length} CIDs")]
internal sealed record VerticalMetrics(float[] Advances, float[] OriginsY)
{
    /// <summary>The offset of the glyph's top edge in a TrueType glyph header.</summary>
    private const int GlyphYMax = 8;

    /// <summary>The offset of the glyph-offset format in the 'head' table.</summary>
    private const int HeadLocaFormat = 50;

    /// <summary>The bytes of the 'head' table up to and including the glyph-offset format.</summary>
    private const int HeadSize = 54;

    /// <summary>The bytes of a TrueType glyph header.</summary>
    private const int GlyphHeaderSize = 10;

    /// <summary>Thousandths in one em.</summary>
    private const float Thousand = 1000F;

    /// <summary>Works out the vertical metrics of some glyphs.</summary>
    /// <param name="source">The font.</param>
    /// <param name="glyphs">The glyph of each CID.</param>
    /// <returns>The metrics, one entry per glyph.</returns>
    /// <remarks>
    /// A glyph's vertical origin is the top of its outline plus its 'vmtx' top side bearing, which puts the outline where the
    /// font designer placed it for vertical writing. Without those, or for CFF outlines, the origin is the font's ascent,
    /// as the OpenType default is.
    /// </remarks>
    internal static VerticalMetrics Create(PdfFontSource source, ReadOnlySpan<int> glyphs)
    {
        var advances = new float[glyphs.Length];
        var origins = new float[glyphs.Length];
        var scale = Thousand / source.UnitsPerEm;
        var tops = TopEdges(source);
        for (var i = 0; i < glyphs.Length; i++)
        {
            advances[i] = -MathF.Round(source.GetAdvanceAlong(glyphs[i], PdfWritingMode.Vertical));
            var origin = tops is null || glyphs[i] >= tops.Length ? source.Facts.Ascent : tops[glyphs[i]] + source.GetTopSideBearing(glyphs[i]);
            origins[i] = MathF.Round(origin * scale);
        }

        return new(advances, origins);
    }

    /// <summary>Reads the top edge of every glyph's outline.</summary>
    /// <param name="source">The font.</param>
    /// <returns>The top of each glyph in design units; <see langword="null"/> when the font has no vertical metrics or no TrueType outlines to read them from.</returns>
    private static int[]? TopEdges(PdfFontSource source)
    {
        if (!source.HasVerticalMetrics || source.Outlines != PdfFontOutlines.TrueType)
        {
            return null;
        }

        var font = source.Font;
        var glyf = font.Table(SfntTags.Glyf);
        var head = font.Table(SfntTags.Head);
        var longLoca = head.Length >= HeadSize && BinaryPrimitives.ReadInt16BigEndian(head[HeadLocaFormat..]) == 1;
        if (!TrueTypeLocations.TryRead(font.Table(SfntTags.Loca), glyf.Length, source.GlyphCount, longLoca, out var offsets))
        {
            return null;
        }

        var tops = new int[source.GlyphCount];
        for (var i = 0; i < tops.Length; i++)
        {
            if (offsets[i + 1] - offsets[i] >= GlyphHeaderSize)
            {
                tops[i] = BinaryPrimitives.ReadInt16BigEndian(glyf[(offsets[i] + GlyphYMax)..]);
            }
        }

        return tops;
    }
}
