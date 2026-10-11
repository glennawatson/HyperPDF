// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A system font standing in for a font the PDF does not embed. Faces are shared by documents using the same drawing
/// services. The service cache retains them while it is reachable; documents may keep a face after the cache is gone.
/// Backend calls run only while a glyph is first loaded.
/// </summary>
[DebuggerDisplay("SubstituteFace: {FamilyName}")]
internal sealed class SubstituteFace : GlyphSource, IDisposable
{
    /// <summary>The page of private-use code points Microsoft symbol fonts map their codes into.</summary>
    private const int SymbolPage = 0xF000;

    /// <summary>The largest single-byte code.</summary>
    private const int MaxByteCode = 0xFF;

    /// <summary>The backend font face, or null for a bundled face.</summary>
    private readonly IPdfFontFace? _font;

    /// <summary>The managed glyph source of a bundled face; null for a system face.</summary>
    private readonly ProgramGlyphSource? _bundled;

    /// <summary>Initializes a new instance of the <see cref="SubstituteFace"/> class from a system font.</summary>
    /// <param name="font">The backend face; this source owns it.</param>
    /// <param name="isRequestedFamily">Whether the typeface is the family the PDF asked for rather than a stand-in.</param>
    internal SubstituteFace(IPdfFontFace font, bool isRequestedFamily)
    {
        _font = font;
        IsRequestedFamily = isRequestedFamily;
        FamilyName = font.FamilyName;
        Ascent = font.Ascent;
        Descent = font.Descent;
        GlyphCount = font.GlyphCount;
    }

    /// <summary>Initializes a new instance of the <see cref="SubstituteFace"/> class from a bundled font program.</summary>
    /// <param name="program">The bundled program, decoded by the managed parser.</param>
    /// <param name="familyName">The face's name.</param>
    internal SubstituteFace(FontProgram program, string familyName)
    {
        _bundled = new(program);
        FamilyName = familyName;
        Ascent = _bundled.Ascent;
        Descent = _bundled.Descent;
        GlyphCount = program.GlyphCount;
    }

    /// <summary>Gets a value indicating whether the face is one of the fonts bundled with the library.</summary>
    internal bool IsBundled => _bundled is not null;

    /// <summary>Gets the family name of the typeface.</summary>
    internal string FamilyName { get; }

    /// <summary>Gets a value indicating whether the typeface is the family the PDF named, not a generic stand-in.</summary>
    internal bool IsRequestedFamily { get; }

    /// <inheritdoc/>
    internal override int GlyphCount { get; }

    /// <inheritdoc/>
    internal override float Ascent { get; }

    /// <inheritdoc/>
    internal override float Descent { get; }

    /// <inheritdoc/>
    internal override bool IsSubstitute => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _font?.Dispose();

    /// <summary>Finds the glyph of a Unicode code point.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetGlyph(int codePoint)
    {
        if (codePoint <= 0)
        {
            return 0;
        }

        return _bundled is null ? _font!.GetGlyph(codePoint) : Math.Max(_bundled.Program.GetGlyphByUnicode(codePoint), 0);
    }

    /// <summary>Finds a glyph by its PostScript name; only a bundled face keeps names.</summary>
    /// <param name="name">The glyph name's UTF-8 bytes.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetNamedGlyph(ReadOnlySpan<byte> name) => _bundled is null ? 0 : Math.Max(_bundled.Program.GetGlyphByName(name), 0);

    /// <summary>Finds the glyph of a symbol font code: the code itself, then the code in the F000 page.</summary>
    /// <param name="code">The single-byte code.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetSymbolGlyph(int code)
    {
        if ((uint)code > MaxByteCode)
        {
            return 0;
        }

        if (_bundled is not null)
        {
            return Math.Max(_bundled.Program.GetGlyphByCharCode(code), 0);
        }

        var glyph = GetGlyph(code);
        return glyph != 0 ? glyph : GetGlyph(SymbolPage | code);
    }

    /// <inheritdoc/>
    internal override PdfPath? BuildOutline(int glyph)
    {
        if (_bundled is not null)
        {
            return _bundled.BuildOutline(glyph);
        }

        return (uint)glyph >= (uint)GlyphCount ? null : _font!.BuildOutline(glyph);
    }

    /// <inheritdoc/>
    internal override float GetAdvance(int glyph)
    {
        if ((uint)glyph >= (uint)GlyphCount)
        {
            return 0;
        }

        return _bundled is null ? _font!.GetAdvance(glyph) : _bundled.GetAdvance(glyph);
    }
}
