// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>The reference supported by the independent expectations.</summary>
internal enum ReferenceSelection
{
    /// <summary>No independent oracle exists; review remains required.</summary>
    ReviewRequired = 0,

    /// <summary>PDFium satisfies the oracle, including an equally correct tie.</summary>
    Pdfium = 1,

    /// <summary>The pdf.js raster satisfies the oracle while PDFium does not.</summary>
    PdfJs = 2,

    /// <summary>Neither renderer satisfies the oracle; retain the independent expectation.</summary>
    OwnStandardsExpectation = 3,
}
