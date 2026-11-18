// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Names the coverage of one glyph as it is drawn: its outline, the scale and slant it is drawn at, where its origin sits inside
/// a pixel, and the table that reshapes its edges. Two glyphs with the same name cover the same pixels around their origins.
/// </summary>
/// <param name="Outline">The outline, which a font keeps one of for each glyph.</param>
/// <param name="A">The bits of the horizontal scale of the glyph's matrix.</param>
/// <param name="B">The bits of the matrix entry that moves a point down as it moves right.</param>
/// <param name="C">The bits of the matrix entry that moves a point right as it moves down.</param>
/// <param name="D">The bits of the vertical scale of the glyph's matrix.</param>
/// <param name="SubX">How far the glyph's origin is right of its pixel, in steps of a pixel.</param>
/// <param name="SubY">How far the glyph's origin is down its pixel, in steps of a pixel.</param>
/// <param name="Table">The table that reshapes the coverage, or <see langword="null"/> when it is used as measured.</param>
[DebuggerDisplay("GlyphCoverageKey: sub {SubX},{SubY}")]
internal readonly record struct GlyphCoverageKey(PdfPath Outline, int A, int B, int C, int D, byte SubX, byte SubY, byte[]? Table)
{
    /// <summary>The odd multiplier that spreads each field over the hash.</summary>
    private const uint Mix = 0x9E3779B9;

    /// <summary>The bits the steps across are moved up by in the hash, so they do not cancel the steps down.</summary>
    private const int SubShift = 8;

    /// <summary>Tells whether two names are the same. The outline and the table are compared as the objects they are, not by what they hold.</summary>
    /// <param name="other">The other name.</param>
    /// <returns><see langword="true"/> when they name the same coverage.</returns>
    public bool Equals(GlyphCoverageKey other) =>
        ReferenceEquals(Outline, other.Outline)
        && A == other.A
        && B == other.B
        && C == other.C
        && D == other.D
        && SubX == other.SubX
        && SubY == other.SubY
        && ReferenceEquals(Table, other.Table);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (uint)RuntimeHelpers.GetHashCode(Outline);
            hash = (hash * Mix) + (uint)A;
            hash = (hash * Mix) + (uint)B;
            hash = (hash * Mix) + (uint)C;
            hash = (hash * Mix) + (uint)D;
            hash = (hash * Mix) + (uint)((SubX << SubShift) | SubY);
            return (int)(hash * Mix);
        }
    }
}
