// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A PDF/A part and conformance level (ISO 19005-1 to 4).</summary>
public enum PdfALevel
{
    /// <summary>PDF/A-1b (ISO 19005-1, basic): reliable visual appearance, based on PDF 1.4.</summary>
    PdfA1B = 0,

    /// <summary>PDF/A-1a (ISO 19005-1, accessible): PDF/A-1b plus tagged structure and Unicode text.</summary>
    PdfA1A = 1,

    /// <summary>PDF/A-2b (ISO 19005-2, basic), based on PDF 1.7.</summary>
    PdfA2B = 2,

    /// <summary>PDF/A-2u (ISO 19005-2, Unicode): PDF/A-2b plus Unicode text for every glyph.</summary>
    PdfA2U = 3,

    /// <summary>PDF/A-2a (ISO 19005-2, accessible): PDF/A-2u plus tagged structure.</summary>
    PdfA2A = 4,

    /// <summary>PDF/A-3b (ISO 19005-3, basic): PDF/A-2b that may embed files of any type.</summary>
    PdfA3B = 5,

    /// <summary>PDF/A-3u (ISO 19005-3, Unicode).</summary>
    PdfA3U = 6,

    /// <summary>PDF/A-3a (ISO 19005-3, accessible).</summary>
    PdfA3A = 7,

    /// <summary>PDF/A-4 (ISO 19005-4), based on PDF 2.0.</summary>
    PdfA4 = 8,

    /// <summary>PDF/A-4e (ISO 19005-4, engineering): PDF/A-4 that may hold 3D and rich media.</summary>
    PdfA4E = 9,

    /// <summary>PDF/A-4f (ISO 19005-4): PDF/A-4 that embeds at least one file of any type.</summary>
    PdfA4F = 10,
}
