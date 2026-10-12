// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>How a character code of a font is shown somewhere in the document.</summary>
[Flags]
internal enum PdfAGlyphUse
{
    /// <summary>Not shown.</summary>
    None = 0,

    /// <summary>Shown with a rendering mode other than 3, so the glyph is drawn or clipped.</summary>
    Rendered = 1 << 0,

    /// <summary>Shown with rendering mode 3, invisible.</summary>
    Invisible = 1 << 1,

    /// <summary>Shown outside any marked content or structure element that carries <c>/ActualText</c>.</summary>
    WithoutActualText = 1 << 2,
}
