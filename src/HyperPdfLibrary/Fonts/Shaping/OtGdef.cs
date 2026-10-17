// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The glyph classes, mark attachment classes and mark glyph sets of a font's GDEF table (OpenType, "GDEF").</summary>
/// <param name="Data">The GDEF table bytes.</param>
/// <param name="GlyphClasses">The glyph class table offset.</param>
/// <param name="MarkAttachClasses">The mark attachment class table offset.</param>
/// <param name="MarkSets">The mark glyph set table offset.</param>
internal sealed record OtGdef(ReadOnlyMemory<byte> Data, int GlyphClasses, int MarkAttachClasses, int MarkSets)
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

    /// <summary>Initializes a new instance of the <see cref="OtGdef"/> class.</summary>
    /// <param name="data">The GDEF table, or empty when the font has none.</param>
    internal OtGdef(ReadOnlyMemory<byte> data)
        : this(
            data,
            data.Length < Version10Bytes ? 0 : FontBytes.U16(data.Span, GlyphClassOffset),
            data.Length < Version10Bytes ? 0 : FontBytes.U16(data.Span, MarkAttachClassOffset),
            ReadMarkSets(data.Span))
    {
    }

    /// <summary>Gets a value indicating whether the table classifies glyphs.</summary>
    internal bool HasGlyphClasses => GlyphClasses != 0;

    /// <summary>Gets a glyph's class.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class: 1 base, 2 ligature, 3 mark, 4 component, 0 when the glyph has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GlyphClass(int glyph) => OtClassDef.ClassOf(Data.Span, GlyphClasses, glyph);

    /// <summary>Gets a mark glyph's attachment class.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class; zero when the glyph has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int MarkAttachClass(int glyph) => OtClassDef.ClassOf(Data.Span, MarkAttachClasses, glyph);

    /// <summary>Determines whether a glyph belongs to a mark glyph set.</summary>
    /// <param name="setIndex">The set's index.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns><see langword="true"/> when the glyph is in the set.</returns>
    internal bool IsInMarkSet(int setIndex, int glyph)
    {
        var table = Data.Span;
        if (MarkSets == 0 || (uint)setIndex >= (uint)FontBytes.U16(table, MarkSets + SetCountOffset))
        {
            return false;
        }

        var coverage = FontBytes.U32(table, MarkSets + SetOffsetsOffset + (setIndex * FontBytes.U32Size));
        return OtCoverage.IndexOf(table, FontBytes.AddOffset(table, MarkSets, coverage), glyph) >= 0;
    }

    /// <summary>Reads the optional mark set offset from a version 1.2 or later header.</summary>
    /// <param name="table">The GDEF table.</param>
    /// <returns>The mark set offset, or zero when absent.</returns>
    private static int ReadMarkSets(ReadOnlySpan<byte> table) =>
        FontBytes.U16(table, MinorVersionOffset) >= MarkSetsMinorVersion && table.Length >= MarkGlyphSetsOffset + FontBytes.U16Size
            ? FontBytes.U16(table, MarkGlyphSetsOffset)
            : 0;
}
