// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The glyph classes, mark attachment classes and mark glyph sets of a font's GDEF table (OpenType, "GDEF").</summary>
internal sealed class OtGdef
{
    /// <summary>The offset of the minor version in the header.</summary>
    private const int MinorVersionOffset = 2;

    /// <summary>The first minor version whose header has mark glyph sets.</summary>
    private const int MarkSetsMinorVersion = 2;

    /// <summary>The offset of the glyph class definitions in the header.</summary>
    private const int GlyphClassOffset = 4;

    /// <summary>The offset of the mark attachment class definitions in the header.</summary>
    private const int MarkAttachClassOffset = 10;

    /// <summary>The offset of the mark glyph sets in a version 1.2 header.</summary>
    private const int MarkGlyphSetsOffset = 12;

    /// <summary>The bytes of a version 1.0 header.</summary>
    private const int Version10Bytes = 12;

    /// <summary>The offset of the set count in the mark glyph sets table.</summary>
    private const int SetCountOffset = 2;

    /// <summary>The offset of the first set offset in the mark glyph sets table.</summary>
    private const int SetOffsetsOffset = 4;

    /// <summary>The GDEF table, or empty.</summary>
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>The offset of the glyph class definitions, or zero.</summary>
    private readonly int _glyphClasses;

    /// <summary>The offset of the mark attachment class definitions, or zero.</summary>
    private readonly int _markAttachClasses;

    /// <summary>The offset of the mark glyph sets, or zero.</summary>
    private readonly int _markSets;

    /// <summary>Initializes a new instance of the <see cref="OtGdef"/> class.</summary>
    /// <param name="data">The GDEF table, or empty when the font has none.</param>
    internal OtGdef(ReadOnlyMemory<byte> data)
    {
        _data = data;
        var table = data.Span;
        if (table.Length < Version10Bytes)
        {
            return;
        }

        _glyphClasses = FontBytes.U16(table, GlyphClassOffset);
        _markAttachClasses = FontBytes.U16(table, MarkAttachClassOffset);
        var hasSets = FontBytes.U16(table, MinorVersionOffset) >= MarkSetsMinorVersion && table.Length >= MarkGlyphSetsOffset + FontBytes.U16Size;
        _markSets = hasSets ? FontBytes.U16(table, MarkGlyphSetsOffset) : 0;
    }

    /// <summary>Gets a value indicating whether the table classifies glyphs.</summary>
    internal bool HasGlyphClasses => _glyphClasses != 0;

    /// <summary>Gets a glyph's class.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class: 1 base, 2 ligature, 3 mark, 4 component, 0 when the glyph has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GlyphClass(int glyph) => OtClassDef.ClassOf(_data.Span, _glyphClasses, glyph);

    /// <summary>Gets a mark glyph's attachment class.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class; zero when the glyph has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int MarkAttachClass(int glyph) => OtClassDef.ClassOf(_data.Span, _markAttachClasses, glyph);

    /// <summary>Determines whether a glyph belongs to a mark glyph set.</summary>
    /// <param name="setIndex">The set's index.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns><see langword="true"/> when the glyph is in the set.</returns>
    internal bool IsInMarkSet(int setIndex, int glyph)
    {
        var table = _data.Span;
        if (_markSets == 0 || (uint)setIndex >= (uint)FontBytes.U16(table, _markSets + SetCountOffset))
        {
            return false;
        }

        var coverage = FontBytes.Offset32(table, _markSets + SetOffsetsOffset + (setIndex * FontBytes.U32Size));
        return OtCoverage.IndexOf(table, _markSets + coverage, glyph) >= 0;
    }
}
