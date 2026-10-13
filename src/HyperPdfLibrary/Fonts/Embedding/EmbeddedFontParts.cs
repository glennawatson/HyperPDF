// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Everything the PDF objects of one embedded composite font are written from. Lengths are in 1/1000 em.</summary>
/// <param name="BaseFont">The PostScript name, with a subset tag such as <c>ABCDEF+</c> for a subset.</param>
/// <param name="Kind">How the font program is stored.</param>
/// <param name="Program">The font program.</param>
/// <param name="FontBox">The font's bounding box.</param>
/// <param name="Cids">The CIDs the widths are for, in ascending order.</param>
/// <param name="Widths">The advance of each CID in <paramref name="Cids"/>.</param>
[DebuggerDisplay("EmbeddedFontParts: {BaseFont}")]
internal sealed record EmbeddedFontParts(string BaseFont, EmbeddedProgramKind Kind, byte[] Program, PdfRectangle FontBox, int[] Cids, float[] Widths)
{
    /// <summary>Gets the font descriptor flags.</summary>
    internal int Flags { get; init; }

    /// <summary>Gets the slant in degrees anticlockwise from vertical; negative for italic.</summary>
    internal float ItalicAngle { get; init; }

    /// <summary>Gets the height above the baseline.</summary>
    internal float Ascent { get; init; }

    /// <summary>Gets the depth below the baseline, negative.</summary>
    internal float Descent { get; init; }

    /// <summary>Gets the height of capital letters.</summary>
    internal float CapHeight { get; init; }

    /// <summary>Gets the dominant stem width; readers use it only to substitute the font.</summary>
    internal int StemV { get; init; }

    /// <summary>Gets the <c>/ToUnicode</c> CMap program, or <see langword="null"/> for none.</summary>
    internal byte[]? ToUnicode { get; init; }

    /// <summary>Gets the <c>/CIDSet</c> bitmap, or <see langword="null"/> for none.</summary>
    internal byte[]? CidSet { get; init; }

    /// <summary>Gets the <c>/CIDToGIDMap</c> stream's data, two bytes of glyph per CID; or <see langword="null"/> for the identity map.</summary>
    internal byte[]? CidToGidMap { get; init; }

    /// <summary>Gets the vertical metrics of each CID in <c>Cids</c>, or <see langword="null"/> for horizontal writing.</summary>
    internal VerticalMetrics? Vertical { get; init; }

    /// <summary>Gets the CID collection's registry.</summary>
    internal string Registry { get; init; } = "Adobe";

    /// <summary>Gets the CID collection's ordering.</summary>
    internal string Ordering { get; init; } = "Identity";

    /// <summary>Gets the CID collection's supplement.</summary>
    internal int Supplement { get; init; }
}
