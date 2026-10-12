// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>One installed font face: a family in one weight and slant, and the file holding it.</summary>
/// <param name="Family">The family, such as "DejaVu Sans".</param>
/// <param name="Style">The style name, such as "Bold Oblique".</param>
/// <param name="Path">The font file.</param>
/// <param name="FaceIndex">The face's index in a collection file, or 0.</param>
[DebuggerDisplay("FontFace: {Family} {Style}")]
public sealed record FontFace(string Family, string Style, string Path, int FaceIndex)
{
    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The embedding level bits of the OS/2 <c>fsType</c>.</summary>
    private const int LevelMask = 0x000F;

    /// <summary>The restricted licence embedding level: the font must not be embedded.</summary>
    private const int Restricted = 0x0002;

    /// <summary>The <c>fsType</c> bit forbidding subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The <c>fsType</c> bit allowing only bitmaps to be embedded.</summary>
    private const int BitmapOnly = 0x0200;

    /// <summary>Gets the weight, from 100 (thin) to 900 (black); 400 is regular.</summary>
    public int Weight { get; init; } = 400;

    /// <summary>Gets a value indicating whether the face is italic or oblique.</summary>
    public bool IsItalic { get; init; }

    /// <summary>Gets a value indicating whether every character has the same width.</summary>
    public bool IsMonospace { get; init; }

    /// <summary>Gets a value indicating whether the face's classification says it has serifs.</summary>
    public bool IsSerif { get; init; }

    /// <summary>Gets the licence's embedding flags (OS/2 <c>fsType</c>).</summary>
    public int EmbeddingFlags { get; init; }

    /// <summary>Gets a value indicating whether the face has TrueType outlines, which PDF can embed as a CID font.</summary>
    public bool HasTrueTypeOutlines { get; init; }

    /// <summary>Gets a value indicating whether the face is bold.</summary>
    public bool IsBold => Weight >= BoldWeight;

    /// <summary>Gets a value indicating whether text in this face can be embedded in a PDF, by its outlines and licence.</summary>
    public bool CanEmbed => HasTrueTypeOutlines && (EmbeddingFlags & LevelMask) != Restricted && (EmbeddingFlags & BitmapOnly) == 0;

    /// <summary>Gets a value indicating whether the licence lets the embedded font keep only the glyphs used.</summary>
    public bool CanSubset => (EmbeddingFlags & NoSubsetting) == 0;
}
