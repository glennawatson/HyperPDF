// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies one glyph mask: a glyph of a font, or an image mask, at a size and position within a pixel, as grey coverage or as per-channel coverage in one colour.</summary>
/// <param name="Owner">What the mask is made from: the font whose glyph it is, or the image mask itself.</param>
/// <param name="Code">The glyph's character code; 0 for an image mask.</param>
/// <param name="A">The horizontal scale of the glyph's matrix, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="B">The matrix entry that moves a point down as it moves right, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="C">The matrix entry that moves a point right as it moves down, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="D">The vertical scale of the glyph's matrix, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="Shift">The power of two the matrix entries are scaled by.</param>
/// <param name="SubX">How far the glyph's origin is right of its pixel, in quarter pixels.</param>
/// <param name="SubY">How far the glyph's origin is down its pixel, in quarter pixels.</param>
/// <param name="Edges">How the edges are drawn.</param>
/// <param name="Contrast">The contrast level, 0 to 3.</param>
/// <param name="Color">The text colour as 0xRRGGBB for a per-channel mask, and 0 for grey coverage, which any colour can tint.</param>
internal readonly record struct GlyphMaskKey(object Owner, int Code, int A, int B, int C, int D, int Shift, byte SubX, byte SubY, GlyphMaskEdges Edges, byte Contrast, uint Color)
{
    /// <summary>The odd multiplier that spreads each field over the hash.</summary>
    private const ulong Mix = 0x9E3779B97F4A7C15UL;

    /// <summary>The bits the mixed hash is folded down by.</summary>
    private const int Fold = 32;

    /// <summary>Where the horizontal quarter pixel sits in the low word of the packed position.</summary>
    private const int SubXShift = 24;

    /// <summary>Where the vertical quarter pixel sits in the low word of the packed position.</summary>
    private const int SubYShift = 16;

    /// <summary>Where the edge style sits in the low word of the packed position.</summary>
    private const int EdgesShift = 8;

    /// <summary>Compares two keys field by field.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when every field matches.</returns>
    public bool Equals(GlyphMaskKey other) =>
        SameGlyph(in other) && SameMatrix(in other) && SamePlace(in other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // The fields are packed into a few words and multiplied once each, which is cheaper than combining twelve separate hashes.
        var packed = ((ulong)(uint)Code << Fold) | (uint)A;
        var matrix = ((ulong)(uint)B << Fold) | (uint)C;
        var rest = ((ulong)(uint)D << Fold) | (uint)Shift;
        var place = ((ulong)Color << Fold) | ((uint)SubX << SubXShift) | ((uint)SubY << SubYShift) | ((uint)(byte)Edges << EdgesShift) | Contrast;
        var hash = (ulong)Owner.GetHashCode();
        hash = (hash ^ packed) * Mix;
        hash = (hash ^ matrix) * Mix;
        hash = (hash ^ rest) * Mix;
        hash = (hash ^ place) * Mix;
        return (int)(hash >> Fold);
    }

    /// <summary>Compares what the glyph is made from.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when the owner and code match.</returns>
    private bool SameGlyph(in GlyphMaskKey other) => Code == other.Code && (ReferenceEquals(Owner, other.Owner) || Owner.Equals(other.Owner));

    /// <summary>Compares the matrix.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when the matrix entries and shift match.</returns>
    private bool SameMatrix(in GlyphMaskKey other) => A == other.A && B == other.B && C == other.C && D == other.D && Shift == other.Shift;

    /// <summary>Compares the position, edges, contrast and colour.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private bool SamePlace(in GlyphMaskKey other) =>
        SubX == other.SubX && SubY == other.SubY && Edges == other.Edges && Contrast == other.Contrast && Color == other.Color;
}
