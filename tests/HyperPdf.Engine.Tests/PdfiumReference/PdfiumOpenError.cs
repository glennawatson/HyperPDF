// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>PDFium's <c>FPDF_ERR_*</c> reasons for failing to open a document.</summary>
internal enum PdfiumOpenError
{
    /// <summary>The document opened.</summary>
    Success = 0,

    /// <summary>An unknown error.</summary>
    Unknown = 1,

    /// <summary>The file could not be read.</summary>
    File = 2,

    /// <summary>The bytes are not a PDF or are too damaged to recover.</summary>
    Format = 3,

    /// <summary>A password is needed, or the one given is wrong.</summary>
    Password = 4,

    /// <summary>The security handler is not supported.</summary>
    Security = 5,

    /// <summary>A page was not found or its content failed.</summary>
    Page = 6,
}
