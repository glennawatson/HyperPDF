// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>What a <see cref="PdfDiagnostic"/> reports.</summary>
public enum PdfDiagnosticCode
{
    /// <summary>No diagnostic.</summary>
    None = 0,

    /// <summary>The cross-reference table was missing or wrong and was rebuilt by scanning the file.</summary>
    XrefRebuilt = 1,

    /// <summary>The <c>%PDF-</c> header is not at the start of the file; offsets count from the header.</summary>
    HeaderOffset = 2,

    /// <summary>An object was not where the cross-reference table said it was.</summary>
    BrokenObject = 3,

    /// <summary>A stream's /Length was missing or wrong, so its end was found by searching for <c>endstream</c>.</summary>
    BadStreamLength = 4,

    /// <summary>A stream decoded to more than the size cap; the output was cut short.</summary>
    DecodeSizeCapped = 5,

    /// <summary>A stream names a filter this library does not know; its data passed through unchanged.</summary>
    UnknownFilter = 6,

    /// <summary>A nesting, reference chain or page tree limit was reached and the rest was ignored.</summary>
    RecursionLimit = 7,

    /// <summary>A stream's compressed data ended early or was damaged; the part that decoded is used.</summary>
    TruncatedStream = 8,

    /// <summary>A stream had no <c>endstream</c> keyword; its data runs to the end of the object.</summary>
    MissingEndStream = 9,

    /// <summary>The catalog was missing or unreadable and was made from the page tree found by scanning.</summary>
    CatalogRebuilt = 10,

    /// <summary>The trailer was missing and was made by scanning the file.</summary>
    TrailerRebuilt = 11,

    /// <summary>The page tree was missing or broken and the pages were found by scanning every object.</summary>
    PageTreeRebuilt = 12,

    /// <summary>A page box was swapped, empty, too large or not four numbers and was fixed or replaced.</summary>
    BadPageBox = 13,

    /// <summary>A font's /W or /W2 widths were malformed and the usable part was kept.</summary>
    BadFontWidths = 14,

    /// <summary>A font's encoding or /Differences were malformed and the usable part was kept.</summary>
    BadFontEncoding = 15,

    /// <summary>A font descriptor had entries of the wrong type, which were ignored.</summary>
    BadFontDescriptor = 16,

    /// <summary>A content stream has an operator or operand the parser could not read (found by a check).</summary>
    BadContentStream = 17,

    /// <summary>A required key is missing or has the wrong type (found by a check).</summary>
    BadStructure = 18,

    /// <summary>A structure was written in its conforming form when the file was saved.</summary>
    FixedOnSave = 19,

    /// <summary>A linearization header or hint table is inconsistent with the file (found by a check).</summary>
    BadLinearization = 20,
}
