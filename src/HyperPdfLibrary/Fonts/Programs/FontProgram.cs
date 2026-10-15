// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// A parsed font program: TrueType, OpenType, CFF or Type 1. A program is immutable after construction and safe to
/// read from many threads. Glyph lookups return -1 when the font has no such glyph. Decoding a glyph does not allocate.
/// </summary>
[DebuggerDisplay("{GetType().Name}: {GlyphCount} glyphs")]
public abstract class FontProgram
{
    /// <summary>The value lookups return when there is no glyph.</summary>
    internal const int NotFound = -1;

    /// <summary>The space a constructed <c>uniXXXX</c> or <c>uXXXXXX</c> name needs.</summary>
    private const int ConstructedNameLength = 8;

    /// <summary>The largest code point the four-digit <c>uni</c> form covers.</summary>
    private const int MaxBmp = 0xFFFF;

    /// <summary>The glyph ids sorted by name, built on the first name lookup.</summary>
    private int[]? _nameOrder;

    /// <summary>Initializes a new instance of the <see cref="FontProgram"/> class.</summary>
    private protected FontProgram()
    {
    }

    /// <summary>Gets the number of glyphs.</summary>
    public abstract int GlyphCount { get; }

    /// <summary>Gets the transform from font units to text space.</summary>
    public abstract FontMatrix FontMatrix { get; }

    /// <summary>Gets the number of font units per em: 1000 for most Type 1 and CFF fonts, 2048 or 1000 for TrueType.</summary>
    public abstract float UnitsPerEm { get; }

    /// <summary>Gets the font's bounding box in font units, or an empty box when the font does not give one.</summary>
    public abstract PdfRectangle BoundingBox { get; }

    /// <summary>Gets the ascent in font units; the top of the bounding box when the font does not give one.</summary>
    public virtual float Ascent => BoundingBox.Top;

    /// <summary>Gets the descent in font units, a negative number; the bottom of the bounding box when the font does not give one.</summary>
    public virtual float Descent => BoundingBox.Bottom;

    /// <summary>Decodes a glyph outline into a sink, in font units with y up. An unknown glyph produces nothing.</summary>
    /// <typeparam name="TSink">The sink type; a struct or ref struct avoids virtual calls.</typeparam>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="sink">The sink.</param>
    public abstract void DecodeGlyph<TSink>(int glyph, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct;

    /// <summary>Gets a glyph's name.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The name's bytes, or empty when the font does not name its glyphs.</returns>
    public abstract ReadOnlySpan<byte> GetGlyphName(int glyph);

    /// <summary>Finds a glyph through the font's built-in encoding, or its symbolic cmap for TrueType.</summary>
    /// <param name="code">The character code.</param>
    /// <returns>The glyph id, or -1.</returns>
    public abstract int GetGlyphByCharCode(int code);

    /// <summary>Gets a glyph's advance width in font units.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The width, or zero for an unknown glyph.</returns>
    public abstract float GetAdvanceWidth(int glyph);

    /// <summary>Finds a glyph by name.</summary>
    /// <param name="name">The glyph name's UTF-8 bytes.</param>
    /// <returns>The glyph id, or -1.</returns>
    public virtual int GetGlyphByName(ReadOnlySpan<byte> name) => FindGlyphByName(name);

    /// <summary>Finds a glyph for a Unicode code point. Fonts without a Unicode cmap look up the Adobe Glyph List name.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The glyph id, or -1.</returns>
    public virtual int GetGlyphByUnicode(int codePoint)
    {
        if (GlyphList.TryGetName(codePoint, out var listed))
        {
            var glyph = FindGlyphByName(listed);
            if (glyph >= 0)
            {
                return glyph;
            }
        }

        Span<byte> name = stackalloc byte[ConstructedNameLength];
        var prefix = codePoint <= MaxBmp ? "uni"u8 : "u"u8;
        prefix.CopyTo(name);
        return codePoint >= 0 && codePoint.TryFormat(name[prefix.Length..], out var written, codePoint <= MaxBmp ? "X4" : "X5", CultureInfo.InvariantCulture)
            ? FindGlyphByName(name[..(prefix.Length + written)])
            : NotFound;
    }

    /// <summary>Finds a glyph by the names <see cref="GetGlyphName"/> reports, using an index built on first use.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The lowest glyph id with the name, or -1.</returns>
    private protected int FindGlyphByName(ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty)
        {
            return NotFound;
        }

        var order = Volatile.Read(ref _nameOrder) ?? BuildNameOrder();
        var low = 0;
        var high = order.Length;
        while (low < high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            if (GetGlyphName(order[middle]).SequenceCompareTo(name) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < order.Length && GetGlyphName(order[low]).SequenceEqual(name) ? order[low] : FindNumberedGlyph(name);
    }

    /// <summary>Finds the glyph of a CID named <c>cidNNN</c>; fonts without a CID mapping use the number as the glyph id.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The glyph id, or -1.</returns>
    private protected virtual int GlyphOfCid(int cid) => GlyphOfId(cid);

    /// <summary>Finds a glyph named only by its number (<c>g23</c>, <c>cid23</c>), which subsetting tools write for fonts without names.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The glyph id, or -1.</returns>
    private int FindNumberedGlyph(ReadOnlySpan<byte> name)
    {
        if (!NumberedGlyphName.TryParse(name, out var isCid, out var number))
        {
            return NotFound;
        }

        return isCid ? GlyphOfCid(number) : GlyphOfId(number);
    }

    /// <summary>Checks a glyph id named <c>gNN</c>.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The glyph id, or -1 when the font has no such glyph.</returns>
    private int GlyphOfId(int glyph) => glyph < GlyphCount ? glyph : NotFound;

    /// <summary>Sorts the glyph ids by name, once.</summary>
    /// <returns>The sorted ids.</returns>
    private int[] BuildNameOrder()
    {
        var order = new int[GlyphCount];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        GlyphNameOrder.Sort(this, order);
        return Interlocked.CompareExchange(ref _nameOrder, order, null) ?? order;
    }
}
