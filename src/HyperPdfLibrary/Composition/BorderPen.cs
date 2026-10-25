// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Makes the pen that draws a border edge.</summary>
internal static class BorderPen
{
    /// <summary>The length of a dash, as a multiple of the line width.</summary>
    private const float DashLength = 3F;

    /// <summary>The length of the gap after a dash or dot, as a multiple of the line width.</summary>
    private const float GapLength = 2F;

    /// <summary>The length of a dot, as a multiple of the line width.</summary>
    private const float DotLength = 1F;

    /// <summary>Makes a pen for a border edge.</summary>
    /// <param name="side">The edge.</param>
    /// <returns>The pen.</returns>
    internal static PdfPen Create(PdfBorderSide side)
    {
        var width = side.Width;
        return side.Style switch
        {
            PdfLineStyle.Dashed => new PdfPen(side.Color, width) { Dash = new[] { width * DashLength, width * GapLength } },
            PdfLineStyle.Dotted => new PdfPen(side.Color, width) { Dash = new[] { width * DotLength, width * GapLength }, Cap = PdfLineCap.Round },
            _ => new PdfPen(side.Color, width),
        };
    }
}
