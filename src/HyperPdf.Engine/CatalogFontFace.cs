// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Engine;

/// <summary>An installed font read by the library's own font parser, for text whose font is not embedded in the document.</summary>
[DebuggerDisplay("CatalogFontFace: {FamilyName}")]
internal sealed class CatalogFontFace : IPdfFontFace
{
    /// <summary>The font's outlines and metrics, in the thousandths of an em the library draws glyphs in.</summary>
    private readonly ProgramGlyphSource _glyphs;

    /// <summary>Initializes a new instance of the <see cref="CatalogFontFace"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="weight">The weight, where 400 is regular and 700 bold.</param>
    /// <param name="italic">Whether the face is italic or oblique.</param>
    /// <param name="program">The parsed font.</param>
    internal CatalogFontFace(string familyName, int weight, bool italic, FontProgram program)
    {
        FamilyName = familyName;
        Weight = weight;
        Italic = italic;
        _glyphs = new(program);
    }

    /// <inheritdoc/>
    public string FamilyName { get; }

    /// <inheritdoc/>
    public int Weight { get; }

    /// <inheritdoc/>
    public bool Italic { get; }

    /// <inheritdoc/>
    public int GlyphCount => _glyphs.GlyphCount;

    /// <inheritdoc/>
    public float Ascent => _glyphs.Ascent;

    /// <inheritdoc/>
    public float Descent => _glyphs.Descent;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetGlyph(int unicode) => unicode <= 0 ? 0 : Math.Max(_glyphs.Program.GetGlyphByUnicode(unicode), 0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetAdvance(int glyph) => (uint)glyph < (uint)GlyphCount ? _glyphs.GetAdvance(glyph) : 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfPath? BuildOutline(int glyph) => (uint)glyph < (uint)GlyphCount ? _glyphs.BuildOutline(glyph, Matrix3x2.Identity) : null;

    /// <inheritdoc/>
    public void Dispose()
    {
        // The parsed font is shared by every face made from the same file and is freed by the collector.
    }
}
