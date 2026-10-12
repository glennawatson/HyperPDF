// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What an indirect object was visited as. veraPDF checks each indirect object once for each role it plays.</summary>
internal enum PdfAVisitKind
{
    /// <summary>A content stream: page content, a form, a pattern or a glyph.</summary>
    ContentStream = 0,

    /// <summary>An image XObject.</summary>
    Image = 1,

    /// <summary>An image used as a stencil <c>/Mask</c>.</summary>
    MaskImage = 2,

    /// <summary>An image used as a soft mask.</summary>
    SoftMaskImage = 3,

    /// <summary>A colour space array.</summary>
    ColourSpace = 4,

    /// <summary>A CMYK ICCBased colour space, also keyed by the overprint settings it was used with.</summary>
    CmykColourSpace = 5,

    /// <summary>A tiling or shading pattern.</summary>
    Pattern = 6,

    /// <summary>A shading.</summary>
    Shading = 7,

    /// <summary>A graphics state parameter dictionary.</summary>
    GraphicsState = 8,

    /// <summary>A halftone.</summary>
    Halftone = 9,

    /// <summary>A Type 3 font.</summary>
    Type3Font = 10,

    /// <summary>The 3D stream of a 3D annotation.</summary>
    ThreeDStream = 11,
}
