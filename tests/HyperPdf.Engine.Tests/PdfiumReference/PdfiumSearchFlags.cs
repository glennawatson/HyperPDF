// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>PDFium's <c>FPDFText_FindStart</c> flags.</summary>
[Flags]
internal enum PdfiumSearchFlags
{
    /// <summary>Case-insensitive, partial word matching.</summary>
    None = 0,

    /// <summary><c>FPDF_MATCHCASE</c>: match letter case.</summary>
    MatchCase = 1 << 0,

    /// <summary><c>FPDF_MATCHWHOLEWORD</c>: match whole words only.</summary>
    WholeWord = 1 << 1,

    /// <summary><c>FPDF_CONSECUTIVE</c>: let matches overlap.</summary>
    Consecutive = 1 << 2,
}
