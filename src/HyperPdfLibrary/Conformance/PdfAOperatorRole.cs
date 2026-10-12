// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What a content operator means to the PDF/A content rules.</summary>
internal enum PdfAOperatorRole
{
    /// <summary>The rules ignore the operator.</summary>
    None = 0,

    /// <summary><c>q</c>: saves the graphics state.</summary>
    Save = 1,

    /// <summary><c>Q</c>: restores the graphics state.</summary>
    Restore = 2,

    /// <summary><c>Tf</c>: selects a font.</summary>
    Font = 3,

    /// <summary><c>Tj</c>, <c>TJ</c>, <c>'</c> and <c>"</c>: show text.</summary>
    Text = 4,

    /// <summary><c>d1</c>: a Type 3 glyph that sets no colour.</summary>
    GlyphBounds = 5,

    /// <summary><c>g</c>, <c>rg</c>, <c>k</c> and their stroke forms: select a device colour space.</summary>
    DeviceColor = 6,

    /// <summary><c>cs</c> and <c>CS</c>: select a named colour space.</summary>
    ColorSpace = 7,

    /// <summary><c>scn</c> and <c>SCN</c>: set a colour, or a pattern in a pattern space.</summary>
    PatternColor = 8,

    /// <summary><c>sh</c>: paints a named shading.</summary>
    Shading = 9,

    /// <summary><c>gs</c>: sets a named graphics state.</summary>
    GraphicsState = 10,

    /// <summary><c>Do</c>: paints a named XObject.</summary>
    XObject = 11,

    /// <summary><c>BDC</c> and <c>DP</c>: name a property list.</summary>
    Properties = 12,

    /// <summary>An operator ISO 32000 does not define.</summary>
    Undefined = 13,
}
