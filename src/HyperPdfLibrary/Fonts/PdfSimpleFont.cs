// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A /Type1, /MMType1 or /TrueType font: one-byte codes, each with a precomputed glyph, width and text. Lookups read
/// arrays and allocate nothing; each code's outline is built once and cached.
/// </summary>
[DebuggerDisplay("PdfSimpleFont: {_data.Source}")]
internal sealed class PdfSimpleFont : PdfFont
{
    /// <summary>The glyph space units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Half, used to centre a narrow substituted glyph.</summary>
    private const float Half = 0.5F;

    /// <summary>The share of the PDF width a narrow substituted glyph may leave empty before it is centred.</summary>
    private const float CentreThreshold = 0.02F;

    /// <summary>The font's loaded data.</summary>
    private readonly SimpleFontData _data;

    /// <summary>The font resources reused by this font.</summary>
    private readonly PdfFontResources _resources;

    /// <summary>The cached outlines by code.</summary>
    private readonly GlyphOutlineCache _outlines = new();

    /// <summary>Initializes a new instance of the <see cref="PdfSimpleFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="data">The loaded data.</param>
    /// <param name="resources">The font resources.</param>
    internal PdfSimpleFont(PdfDictionary dictionary, SimpleFontData data, PdfFontResources resources)
        : base(dictionary)
    {
        _data = data;
        _resources = resources;
    }

    /// <inheritdoc/>
    public override bool IsBold => _data.Metrics.IsBold;

    /// <inheritdoc/>
    public override float Ascent => _data.Metrics.Ascent;

    /// <inheritdoc/>
    public override float Descent => _data.Metrics.Descent;

    /// <summary>Gets where the glyphs come from.</summary>
    internal GlyphSource Source => _data.Source;

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        if (bytes.IsEmpty)
        {
            code = 0;
            return 0;
        }

        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => _data.Widths[(uint)code < FontEncodings.CodeCount ? code : 0];

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code) =>
        (uint)code < FontEncodings.CodeCount ? _outlines.GetOrBuild(code, this, static (font, slot) => font.BuildOutline(slot)) : null;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        if (_data.ToUnicode is { } map && map.TryGetUnicode(code, destination, out var written) && written > 0)
        {
            return PuaSymbols.Apply(_resources, _data.Remap, destination, written);
        }

        var text = (uint)code < FontEncodings.CodeCount ? _data.Texts[code] : null;
        return text is not null && text.AsSpan().TryCopyTo(destination) ? PuaSymbols.Apply(_resources, _data.Remap, destination, text.Length) : 0;
    }

    /// <summary>Gets a code's glyph id.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or -1 when the code has none.</returns>
    internal int GetGlyph(int code) => (uint)code < FontEncodings.CodeCount ? _data.Glyphs[code] : SimpleGlyphMap.NoGlyph;

    /// <summary>
    /// Fits a substituted glyph to the advance the PDF gives it: a wider glyph is compressed horizontally and a
    /// noticeably narrower one is centred, so text keeps the spacing its author laid out (ISO 32000-2, 9.2.4).
    /// </summary>
    /// <param name="path">The outline.</param>
    /// <param name="fontWidth">The glyph's own advance in glyph units.</param>
    /// <param name="pdfWidth">The PDF width in glyph units.</param>
    /// <returns>The adjusted outline.</returns>
    private static PdfPath FitToWidth(PdfPath path, float fontWidth, float pdfWidth) => pdfWidth switch
    {
        _ when fontWidth <= 0 || pdfWidth <= 0 => path,
        _ when pdfWidth < fontWidth => path.Transform(Matrix3x2.CreateScale(pdfWidth / fontWidth, 1)),
        _ when pdfWidth - fontWidth > pdfWidth * CentreThreshold => path.Transform(Matrix3x2.CreateTranslation((pdfWidth - fontWidth) * Half, 0)),
        _ => path,
    };

    /// <summary>Builds a code's outline: its glyph, or a system fallback for a substituted font's missing character.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The outline, or <see langword="null"/>.</returns>
    private PdfPath? BuildOutline(int code)
    {
        var glyph = _data.Glyphs[code];
        var source = _data.Source;
        if (glyph < 0)
        {
            var codePoint = GlyphNameText.FirstCodePoint(_data.Texts[code]);
            if (!source.IsSubstitute || codePoint == 0 || SystemFontMatcher.For(Dictionary, _resources).MatchCharacter(codePoint) is not { } fallback)
            {
                return null;
            }

            source = fallback;
            glyph = fallback.GetGlyph(codePoint);
        }

        var path = source.BuildOutline(glyph);
        if (path is not null && _data.Metrics.AdjustSpacing)
        {
            path = FitToWidth(path, source.GetAdvance(glyph), _data.Widths[code] * GlyphUnits);
        }

        return path;
    }
}
