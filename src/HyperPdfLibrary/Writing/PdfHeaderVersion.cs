// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>The version a new file's <c>%PDF-</c> header names.</summary>
internal enum PdfHeaderVersion
{
    /// <summary>PDF 1.4, the base of PDF/A-1.</summary>
    Pdf14 = 14,

    /// <summary>PDF 1.7 (ISO 32000-1), the base of PDF/A-2 and PDF/A-3.</summary>
    Pdf17 = 17,

    /// <summary>PDF 2.0 (ISO 32000-2), the base of PDF/A-4.</summary>
    Pdf20 = 20,
}
