// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that every stream mode and page content normalisation of the compact writer gives a file both HyperPDF and the PDFium test reference open and draw as the original.</summary>
public sealed class WriterModesPdfiumTests
{
    /// <summary>The pixels per PDF point of the comparison renders.</summary>
    private const float Scale = 1;

    /// <summary>The pages in the samples.</summary>
    private const int PageCount = 1;

    /// <summary>A page with split content and large inline images draws the same in PDFium and opens in HyperPDF after each mode with content normalised.</summary>
    /// <param name="mode">The stream mode.</param>
    /// <param name="useObjectStreams">Whether to pack objects into object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamMode.Compress, false)]
    [Arguments(PdfStreamMode.Compress, true)]
    [Arguments(PdfStreamMode.Preserve, true)]
    [Arguments(PdfStreamMode.Recompress, false)]
    [Arguments(PdfStreamMode.Recompress, true)]
    [Arguments(PdfStreamMode.Uncompressed, false)]
    [Arguments(PdfStreamMode.Uncompressed, true)]
    public async Task NormalisedContentDrawsTheSameInPdfium(PdfStreamMode mode, bool useObjectStreams)
    {
        var source = WriterControlSamples.ContentPage();
        var options = PdfCompactOptions.Default with { UseObjectStreams = useObjectStreams, StreamMode = mode, Normalisation = PdfContentNormalisation.All };

        await AssertSameInBothEngines(source, options);
    }

    /// <summary>A page with filtered streams draws the same in PDFium after each mode and when unreferenced objects are kept.</summary>
    /// <param name="mode">The stream mode.</param>
    /// <param name="keepUnreferenced">Whether to keep objects no reference reaches.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamMode.Compress, false)]
    [Arguments(PdfStreamMode.Preserve, true)]
    [Arguments(PdfStreamMode.Recompress, true)]
    [Arguments(PdfStreamMode.Uncompressed, false)]
    public async Task FilteredStreamsDrawTheSameInPdfium(PdfStreamMode mode, bool keepUnreferenced)
    {
        var source = WriterControlSamples.FilteredStreams(false);
        var options = PdfCompactOptions.Default with { StreamMode = mode, KeepUnreferenced = keepUnreferenced };

        await AssertSameInBothEngines(source, options);
    }

    /// <summary>Saves a file and checks that PDFium draws it as it drew the original and that both engines open it.</summary>
    /// <param name="source">The original file.</param>
    /// <param name="options">The layout.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameInBothEngines(byte[] source, PdfCompactOptions options)
    {
        byte[] saved;
        using (var store = StoreOpening.Open(source, null))
        {
            saved = PdfCompactWriter.Save(store, options);
        }

        using var pair = new EnginePair(saved);
        var before = PdfiumReferenceRendering.RenderPage(source, 0, Scale);
        var after = PdfiumReferenceRendering.RenderPage(saved, 0, Scale);

        await Assert.That(pair.Library.PageCount).IsEqualTo(PageCount);
        await Assert.That(PdfiumReferenceRendering.PageCount(saved)).IsEqualTo(PageCount);
        await Assert.That(after.Width).IsEqualTo(before.Width);
        await Assert.That(after.Pixels.Span.SequenceEqual(before.Pixels.Span)).IsTrue();
    }
}
