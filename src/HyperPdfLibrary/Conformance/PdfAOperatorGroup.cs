// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What the PDF/A colour walk does for a content operator.</summary>
internal enum PdfAOperatorGroup
{
    /// <summary>An operator the colour walk ignores.</summary>
    None = 0,

    /// <summary>Saves the graphics state with <c>q</c>.</summary>
    Save = 1,

    /// <summary>Restores the graphics state with <c>Q</c>.</summary>
    Restore = 2,

    /// <summary>Applies a parameter dictionary with <c>gs</c>.</summary>
    GraphicsState = 3,

    /// <summary>Sets the rendering intent with <c>ri</c>.</summary>
    Intent = 4,

    /// <summary>Sets the text rendering mode with <c>Tr</c>.</summary>
    RenderMode = 5,

    /// <summary>Sets the font with <c>Tf</c>.</summary>
    Font = 6,

    /// <summary>Starts a shape-only Type 3 glyph with <c>d1</c>, after which no colour is set.</summary>
    ShapeGlyph = 7,

    /// <summary>Sets a grey fill with <c>g</c>.</summary>
    FillGray = 8,

    /// <summary>Sets a grey stroke with <c>G</c>.</summary>
    StrokeGray = 9,

    /// <summary>Sets an RGB fill with <c>rg</c>.</summary>
    FillRgb = 10,

    /// <summary>Sets an RGB stroke with <c>RG</c>.</summary>
    StrokeRgb = 11,

    /// <summary>Sets a CMYK fill with <c>k</c>.</summary>
    FillCmyk = 12,

    /// <summary>Sets a CMYK stroke with <c>K</c>.</summary>
    StrokeCmyk = 13,

    /// <summary>Selects the fill colour space with <c>cs</c>.</summary>
    FillSpace = 14,

    /// <summary>Selects the stroke colour space with <c>CS</c>.</summary>
    StrokeSpace = 15,

    /// <summary>Sets the fill colour or pattern with <c>scn</c>.</summary>
    FillPattern = 16,

    /// <summary>Sets the stroke colour or pattern with <c>SCN</c>.</summary>
    StrokePattern = 17,

    /// <summary>Fills a path with <c>f</c>, <c>F</c> or <c>f*</c>.</summary>
    PaintFill = 18,

    /// <summary>Strokes a path with <c>S</c> or <c>s</c>.</summary>
    PaintStroke = 19,

    /// <summary>Fills and strokes a path with <c>B</c>, <c>B*</c>, <c>b</c> or <c>b*</c>.</summary>
    PaintBoth = 20,

    /// <summary>Shows text with <c>Tj</c>, <c>TJ</c>, <c>'</c> or <c>"</c>.</summary>
    ShowText = 21,

    /// <summary>Paints an inline image with <c>BI</c>.</summary>
    InlineImage = 22,

    /// <summary>Paints an XObject with <c>Do</c>.</summary>
    XObject = 23,

    /// <summary>Paints a shading with <c>sh</c>.</summary>
    Shading = 24,
}
