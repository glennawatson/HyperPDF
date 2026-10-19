// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// How glyphs are fitted to the pixel grid when small text is drawn from cached glyph masks. Glyph outlines are drawn from the
/// font's own shapes, so this fits where each glyph sits, not the shape of its strokes.
/// </summary>
public enum PdfTextHinting
{
    /// <summary>Glyphs sit where the page puts them, to a quarter of a pixel, which keeps spacing even.</summary>
    None = 0,

    /// <summary>Glyphs sit on whole pixel rows, so the bottom of every line of text has the same crisp edge, and keep their quarter pixel spacing along the line.</summary>
    Slight = 1,

    /// <summary>Glyphs sit on whole pixels both ways, which gives the crispest stems and uneven spacing.</summary>
    Full = 2,
}
