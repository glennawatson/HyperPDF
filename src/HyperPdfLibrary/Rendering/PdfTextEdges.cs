// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>How the edges of text are drawn.</summary>
public enum PdfTextEdges
{
    /// <summary>Edges are smoothed with grey levels (grayscale anti-aliasing).</summary>
    Smooth = 0,

    /// <summary>Edges are not smoothed: every pixel is either fully covered or not covered.</summary>
    Sharp = 1,

    /// <summary>
    /// Edges are smoothed separately for the red, green and blue parts of each pixel (LCD sub-pixel text), which makes small
    /// text look sharper on a screen with that pixel layout. It applies to small text drawn from glyph masks on a plain page.
    /// </summary>
    Lcd = 2,
}
