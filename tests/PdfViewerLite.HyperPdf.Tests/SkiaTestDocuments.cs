// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Opens adapter test documents with their own drawing services.</summary>
internal static class SkiaTestDocuments
{
    /// <summary>Opens PDF bytes for rendering.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(byte[] bytes) => PdfDocumentReader.OpenWith(
        bytes,
        new PdfOpenOptions { DrawingServices = new(new PdfRenderBackendRegistration().UseSkia()) });
}
