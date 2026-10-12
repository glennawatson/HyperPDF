// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Tests.Robustness;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Compares linearized page order and dimensions with the PDFium test reference.</summary>
public sealed class LinearizedParityTests
{
    /// <summary>Classic and stream cross-references expose the same two pages in both readers.</summary>
    /// <param name="xrefStreams">Whether the cross-reference sections are streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PageCountAndSizesMatchPdfium(bool xrefStreams)
    {
        var bytes = LinearizedDocuments.Create(xrefStreams);
        using var input = new HyperPdfTestDocument(bytes);
        var referenceCount = PdfiumReferenceRendering.PageCount(bytes);
        var sizes = input.Document.GetPageSizes();

        await Assert.That(referenceCount).IsEqualTo(input.Document.PageCount);
        for (var page = 0; page < referenceCount; page++)
        {
            var raster = PdfiumReferenceRendering.RenderPage(bytes, page, 1);
            await Assert.That(raster.Width).IsEqualTo((int)sizes[page].Width);
            await Assert.That(raster.Height).IsEqualTo((int)sizes[page].Height);
        }
    }

    /// <summary>A linearized form and outline book keeps both engines' page count and sizes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrittenFormBookMatchesPdfium()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        var bytes = PdfLinearizedWriter.Save(source);
        using var input = new HyperPdfTestDocument(bytes);
        var referenceCount = PdfiumReferenceRendering.PageCount(bytes);
        var sizes = input.Document.GetPageSizes();

        await Assert.That(referenceCount).IsEqualTo(input.Document.PageCount);
        for (var page = 0; page < referenceCount; page++)
        {
            var raster = PdfiumReferenceRendering.RenderPage(bytes, page, 1);
            await Assert.That(raster.Width).IsEqualTo((int)sizes[page].Width);
            await Assert.That(raster.Height).IsEqualTo((int)sizes[page].Height);
        }
    }
}
