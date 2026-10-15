// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>
/// Whether the engine may fetch font data that is not inside the library. The faces that stand in for the 14 standard
/// fonts are embedded in the library, so they never need the network in either mode. Only the predefined CJK CMaps and the
/// CID to Unicode tables are external: a PDF needs them when a composite font names a predefined CMap such as
/// <c>90ms-RKSJ-H</c> or <c>UniJIS-UCS2-H</c> instead of embedding it, or when a non-embedded CJK font needs text.
/// </summary>
public enum PdfFontDataMode
{
    /// <summary>
    /// Missing CMaps and Unicode tables are downloaded from their pinned upstream revisions the first time a page needs
    /// them, then kept in the font data folder (<c>HYPERPDF_FONT_DATA</c> or the user's local application data). A
    /// failed download throws from the asynchronous font and page methods.
    /// </summary>
    OnDemand = 0,

    /// <summary>
    /// Nothing is downloaded. Data already in the font data folder is used; data that is missing is skipped and listed by
    /// <see cref="PdfDrawingServices.GetMissingFontData"/>. A composite font whose predefined CMap is missing reads its
    /// two-byte codes as CIDs and has no extracted text of its own, and a non-embedded CJK font without its Unicode table
    /// has no text; nothing else changes, and the same inputs always give the same result.
    /// </summary>
    Offline = 1,
}
