// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A /Type0 font: codes read through a CMap, CIDs measured by /W and /W2, glyphs from an embedded CIDFontType0 or
/// CIDFontType2 program or a system font. Lookups allocate nothing; outlines are built once per glyph and cached.
/// </summary>
[DebuggerDisplay("PdfCompositeFont: {_data.Source}")]
internal sealed class PdfCompositeFont : PdfFont
{
    /// <summary>The glyph space units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Half, used to centre a glyph on its vertical origin.</summary>
    private const float Half = 0.5F;

    /// <summary>The font's loaded data.</summary>
    private readonly CompositeFontData _data;

    /// <summary>The cached outlines: by glyph id for embedded programs, by code for system fonts.</summary>
    private readonly GlyphOutlineCache _outlines = new();

    /// <summary>Initializes a new instance of the <see cref="PdfCompositeFont"/> class.</summary>
    /// <param name="dictionary">The /Type0 font dictionary.</param>
    /// <param name="data">The loaded data.</param>
    internal PdfCompositeFont(PdfDictionary dictionary, CompositeFontData data)
        : base(dictionary) => _data = data;

    /// <inheritdoc/>
    public override bool IsVertical => _data.CMap.IsVertical;

    /// <inheritdoc/>
    public override bool IsBold => _data.Metrics.IsBold;

    /// <inheritdoc/>
    public override float Ascent => _data.Metrics.Ascent;

    /// <inheritdoc/>
    public override float Descent => _data.Metrics.Descent;

    /// <summary>Gets where the glyphs come from.</summary>
    internal GlyphSource Source => _data.Source;

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code) => _data.CMap.Map.ReadCode(bytes, out code);

    /// <inheritdoc/>
    public override float GetWidth(int code) => GetGlyphUnitWidth(code) / GlyphUnits;

    /// <inheritdoc/>
    public override void GetVerticalMetrics(int code, out float advance, out float originX, out float originY)
    {
        var cid = _data.CMap.ToCid(code);
        if (_data.Vertical.TryGetVertical(cid, out var metric))
        {
            advance = metric.Advance / GlyphUnits;
            originX = metric.OriginX / GlyphUnits;
            originY = metric.OriginY / GlyphUnits;
            return;
        }

        advance = _data.DefaultVertical.Advance / GlyphUnits;
        originX = GetGlyphUnitWidth(code) * Half / GlyphUnits;
        originY = _data.DefaultVertical.OriginY / GlyphUnits;
    }

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code)
    {
        if (_data.Source.IsSubstitute)
        {
            return _outlines.GetOrBuild(code, this, static (font, slot) => font.BuildSubstituteOutline(slot));
        }

        var glyph = GetGlyph(code);
        return glyph < 0 ? null : _outlines.GetOrBuild(glyph, _data.Source, static (source, slot) => source.BuildOutline(slot));
    }

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        if (_data.ToUnicode is { } map && map.TryGetUnicode(code, destination, out var written) && written > 0)
        {
            return written;
        }

        return Rune.TryCreate(_data.CMap.ToUnicode(code, _data.CidToUnicode), out var rune) && rune.Value > 0 && rune.TryEncodeToUtf16(destination, out written) ? written : 0;
    }

    /// <summary>Gets a code's glyph id in the embedded program.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetGlyph(int code) => _data.GlyphRoute.GlyphOf(_data.CMap.ToCid(code));

    /// <summary>Gets a code's width in glyph units: its CID's /W entry, else /DW.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The width.</returns>
    private float GetGlyphUnitWidth(int code) =>
        _data.Widths.TryGetAdvance(_data.CMap.ToCid(code), out var width) ? width : _data.DefaultWidth;

    /// <summary>
    /// Gets the code point a code stands for when drawn in a system font, as PDFium's CPDF_CIDFont::GlyphFromCharCode
    /// finds it: the CID's Unicode value, then the CMap's own meaning, then /ToUnicode.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The code point, or zero.</returns>
    private int GetCodePoint(int code)
    {
        var cmap = _data.CMap;
        var cid = cmap.ToCid(code);
        var codePoint = cid != 0 && _data.CidToUnicode is { } table ? table.Lookup(cid) : 0;
        if (codePoint == 0)
        {
            codePoint = cmap.ToUnicode(code, _data.CidToUnicode);
        }

        if (codePoint == 0 && _data.ToUnicode is { } map)
        {
            _ = map.TryGetCodePoint(code, out codePoint);
        }

        return codePoint;
    }

    /// <summary>Builds the outline of a code in a system font, falling back to another system font for a missing character.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The outline, or <see langword="null"/>.</returns>
    private PdfPath? BuildSubstituteOutline(int code)
    {
        var codePoint = GetCodePoint(code);
        if (codePoint == 0 || _data.Source is not SubstituteFace face)
        {
            return null;
        }

        var glyph = face.GetGlyph(codePoint);
        if (glyph == 0 && SystemFontMatcher.For(Dictionary).MatchCharacter(codePoint) is { } fallback)
        {
            face = fallback;
            glyph = fallback.GetGlyph(codePoint);
        }

        return glyph == 0 ? null : face.BuildOutline(glyph);
    }
}
