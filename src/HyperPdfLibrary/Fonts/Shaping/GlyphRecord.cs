// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>One glyph of the run being shaped, with the state lookups need.</summary>
/// <param name="Glyph">The glyph index in the font.</param>
/// <param name="Cluster">The UTF-16 index of the first character the glyph shows.</param>
/// <param name="CodePoint">The Unicode character the glyph began as.</param>
[DebuggerDisplay("GlyphRecord: {Glyph} cluster {Cluster}")]
internal record struct GlyphRecord(int Glyph, int Cluster, int CodePoint)
{
    /// <summary>Gets or sets the features that apply to the glyph, one bit for each (see <see cref="ShapeMasks"/>).</summary>
    internal uint Mask { get; set; } = ShapeMasks.Global;

    /// <summary>Gets or sets the GDEF class of the glyph: 1 base, 2 ligature, 3 mark, 4 component, 0 none.</summary>
    internal int GlyphClass { get; set; }

    /// <summary>Gets or sets what the shaper knows about the glyph.</summary>
    internal GlyphFlags Flags { get; set; }

    /// <summary>Gets or sets the identifier of the ligature the glyph is part of or became, or zero.</summary>
    internal int LigatureId { get; set; }

    /// <summary>Gets or sets the one-based component of its ligature a mark follows, or zero.</summary>
    internal int LigatureComponent { get; set; }

    /// <summary>Gets or sets the number of characters a ligature glyph stands for.</summary>
    internal int LigatureComponents { get; set; }

    /// <summary>Gets or sets the identifier of the Indic syllable the glyph is in.</summary>
    internal int Syllable { get; set; }
}
