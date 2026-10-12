// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>The tables of a TrueType program the PDF/A font rules read.</summary>
[DebuggerDisplay("PdfATrueTypeTables: {GlyphCount} glyphs, {Cmaps.Length} cmaps")]
internal sealed class PdfATrueTypeTables
{
    /// <summary>The design units veraPDF assumes when there is no 'head' table.</summary>
    internal const int DefaultUnitsPerEm = 2048;

    /// <summary>The units per em glyph widths are compared in.</summary>
    private const float TextUnits = 1000F;

    /// <summary>Initializes a new instance of the <see cref="PdfATrueTypeTables"/> class.</summary>
    /// <param name="unitsPerEm">The design units per em.</param>
    /// <param name="advances">The advance widths of the long metrics, in design units.</param>
    /// <param name="glyphCount">The glyph count.</param>
    /// <param name="cmaps">The 'cmap' subtables in table order, or null without a 'cmap' table.</param>
    /// <param name="postGlyphs">The glyph of each 'post' table name.</param>
    internal PdfATrueTypeTables(int unitsPerEm, int[] advances, int glyphCount, PdfATrueTypeCmap[]? cmaps, Dictionary<string, int> postGlyphs)
    {
        UnitsPerEm = unitsPerEm;
        Advances = advances;
        GlyphCount = glyphCount;
        HasCmapTable = cmaps is not null;
        Cmaps = cmaps ?? [];
        PostGlyphs = postGlyphs;
    }

    /// <summary>Gets a value indicating whether the program has a 'cmap' table.</summary>
    internal bool HasCmapTable { get; }

    /// <summary>Gets the design units per em.</summary>
    internal int UnitsPerEm { get; }

    /// <summary>Gets the advance widths of the 'hmtx' long metrics, in design units.</summary>
    internal int[] Advances { get; }

    /// <summary>Gets the glyph count from 'maxp', or the number of long metrics without one.</summary>
    internal int GlyphCount { get; }

    /// <summary>Gets the 'cmap' subtables in table order.</summary>
    internal PdfATrueTypeCmap[] Cmaps { get; }

    /// <summary>Gets the glyph of each 'post' table name; empty without names.</summary>
    internal Dictionary<string, int> PostGlyphs { get; }

    /// <summary>Finds the first subtable for a platform and encoding.</summary>
    /// <param name="platformId">The platform id.</param>
    /// <param name="encodingId">The encoding id.</param>
    /// <returns>The subtable, or null.</returns>
    internal PdfATrueTypeCmap? Cmap(int platformId, int encodingId)
    {
        foreach (var cmap in Cmaps)
        {
            if (cmap.PlatformId == platformId && cmap.EncodingId == encodingId)
            {
                return cmap;
            }
        }

        return null;
    }

    /// <summary>Gets a glyph's advance width scaled to 1000 units per em, as veraPDF scales it.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The width.</returns>
    internal float Width(int glyph)
    {
        if (Advances.Length == 0)
        {
            return 0;
        }

        if (glyph < Advances.Length)
        {
            return Scale(Advances[glyph]);
        }

        // Glyphs past the long metrics share the last advance, as in monospaced fonts; ids past the font read glyph 0.
        return Scale(glyph < GlyphCount ? Advances[^1] : Advances[0]);
    }

    /// <summary>Scales design units to text units with veraPDF's single-precision factor.</summary>
    /// <param name="units">The design units.</param>
    /// <returns>The width in thousandths of an em.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Scale(int units) => units * (TextUnits / UnitsPerEm);
}
