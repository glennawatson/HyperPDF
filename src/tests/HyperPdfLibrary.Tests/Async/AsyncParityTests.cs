// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Text;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>Checks that the async-first API returns what the synchronous API returns, over each kind of source.</summary>
[NotInParallel]
public sealed class AsyncParityTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 6;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pages rendered at once.</summary>
    private const int ParallelPages = 4;

    /// <summary>The word searched for.</summary>
    private const string Word = "fox";

    /// <summary>The bytes of the sample document.</summary>
    private static readonly byte[] Sample = TestPdf.Create(Pages);

    /// <summary>Gets the ways a document is opened from a file or stream.</summary>
    public static IEnumerable<string> Kinds { get; } = ["bytes", "mapped", "memory", "handle", "stream"];

    /// <summary>The async open gives the same pages, text, search results, outline and links as the sync open.</summary>
    /// <param name="kind">How the file is read.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Kinds))]
    public async Task OpenAsyncMatchesSync(string kind)
    {
        var directory = Directory.CreateTempSubdirectory("hyperpdf-async-");
        try
        {
            var path = Path.Combine(directory.FullName, "sample.pdf");
            await File.WriteAllBytesAsync(path, Sample);
            using var sync = SkiaTestRegistration.Open(Sample, null);
            using var opened = await OpenAsync(path, kind);
            var document = opened.Document;
            await Assert.That(document.PageCount).IsEqualTo(sync.PageCount);
            await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(PdfDocumentMetadata.GetInfo(sync).Title);
            await Assert.That((await PdfDocumentNavigation.GetOutlineAsync(document, CancellationToken.None)).Count).IsEqualTo(PdfDocumentNavigation.GetOutline(sync).Count);
            for (var i = 0; i < sync.PageCount; i++)
            {
                await Assert.That((await PdfDocumentLinks.GetLinksAsync(document, i, CancellationToken.None)).Count).IsEqualTo(PdfDocumentLinks.GetLinks(sync, i).Count);
                await Assert.That((await PdfDocumentText.GetTextPageAsync(document, i, CancellationToken.None)).Text).IsEqualTo(PdfDocumentText.GetTextPage(sync, i).Text);
                await Assert.That(await PdfDocumentContent.ScanAnnotationsAsync(document, i, CancellationToken.None)).IsEqualTo(PdfDocumentContent.ScanAnnotations(sync, i));
            }
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>The async walk of the text pages and the async search match the sync text and search.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWalkAndSearchMatchSync()
    {
        using var sync = SkiaTestRegistration.Open(Sample, null);
        using var document = await SkiaTestRegistration.OpenAsync(Sample, null, CancellationToken.None);
        var index = 0;
        await foreach (var page in PdfDocumentText.GetTextPagesAsync(document, CancellationToken.None))
        {
            await Assert.That(page.Text).IsEqualTo(PdfDocumentText.GetTextPage(sync, index).Text);
            index++;
        }

        await Assert.That(index).IsEqualTo(Pages);
        var expected = new List<PdfTextMatch>();
        var matchedPages = 0;
        for (var i = 0; i < Pages; i++)
        {
            expected.Clear();
            PdfDocumentText.GetTextPage(sync, i).Find(Word, PdfTextSearchOptions.None, expected);
            matchedPages += expected.Count > 0 ? 1 : 0;
        }

        var found = 0;
        await foreach (var match in PdfDocumentText.FindAsync(document, Word, PdfTextSearchOptions.None, CancellationToken.None))
        {
            found++;
            expected.Clear();
            PdfDocumentText.GetTextPage(sync, match.PageIndex).Find(Word, PdfTextSearchOptions.None, expected);
            await Assert.That(match.Matches).IsEquivalentTo(expected);
        }

        await Assert.That(found).IsEqualTo(matchedPages);
    }

    /// <summary>The async render draws the same pixels as the sync render.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderAsyncMatchesSync()
    {
        using var document = SkiaTestRegistration.Open(Sample, null);
        using var renderer = new PdfPageRenderer(document);
        using var asyncRenderer = new PdfPageRenderer(document);
        for (var i = 0; i < Pages; i++)
        {
            PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, i), 0, 1F, out var width, out var height);
            var expected = new byte[width * height * BytesPerPixel];
            var actual = new byte[expected.Length];
            var request = new PdfTileRequest(i, 1F, 0, 0, 0, PdfRenderFlags.Annotations);
            await Assert.That(renderer.Render(request, new(expected, width, height, width * BytesPerPixel))).IsTrue();
            await Assert.That(await asyncRenderer.RenderAsync(request, actual, width, height, width * BytesPerPixel, CancellationToken.None)).IsTrue();
            await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
        }
    }

    /// <summary>Rendering pages at once with the async API gives the pixels of rendering them one by one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentRendersMatchSync()
    {
        using var document = SkiaTestRegistration.Open(Sample, null);
        using var renderer = new PdfPageRenderer(document);
        var expected = new byte[ParallelPages][];
        var actual = new byte[ParallelPages][];
        var tasks = new Task<bool>[ParallelPages];
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1F, out var width, out var height);
        for (var i = 0; i < ParallelPages; i++)
        {
            expected[i] = new byte[width * height * BytesPerPixel];
            actual[i] = new byte[expected[i].Length];
            _ = renderer.Render(new(i, 1F, 0, 0, 0, PdfRenderFlags.Annotations), new(expected[i], width, height, width * BytesPerPixel));
        }

        using var fresh = new PdfPageRenderer(document);
        for (var i = 0; i < ParallelPages; i++)
        {
            var page = i;
            var request = new PdfTileRequest(page, 1F, 0, 0, 0, PdfRenderFlags.Annotations);
            tasks[i] = Task.Run(() => fresh.RenderAsync(request, actual[page], width, height, width * BytesPerPixel, CancellationToken.None).AsTask(), CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);
        for (var i = 0; i < ParallelPages; i++)
        {
            await Assert.That(results[i]).IsTrue();
            await Assert.That(actual[i].AsSpan().SequenceEqual(expected[i])).IsTrue();
        }
    }

    /// <summary>The async save writes the bytes of the sync save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveAsyncMatchesSync()
    {
        using var sync = SkiaTestRegistration.Open(Sample, null);
        using var document = await SkiaTestRegistration.OpenAsync(Sample, null, CancellationToken.None);
        PdfDocumentMetadataEditing.SetMetadata(sync, new() { Title = "Edited" });
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Edited" });
        await using var expected = new MemoryStream();
        PdfIncrementalWriter.Save(sync.Objects, expected);
        await using var actual = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, actual, CancellationToken.None);
        await Assert.That(actual.ToArray().AsSpan().SequenceEqual(expected.ToArray())).IsTrue();
    }

    /// <summary>Opens a file the async way for a kind of source.</summary>
    /// <param name="path">The file.</param>
    /// <param name="kind">How the file is read.</param>
    /// <returns>The document and the stream it reads, disposed together.</returns>
    private static async Task<OpenedAsync> OpenAsync(string path, string kind)
    {
        switch (kind)
        {
            case "bytes":
                {
                    return new(await SkiaTestRegistration.OpenAsync(await File.ReadAllBytesAsync(path), null, CancellationToken.None), null);
                }

            case "handle":
                {
                    return new(await PdfDocumentReader.OpenWithAsync(path, new() { Source = PdfSourceKind.Stream }, CancellationToken.None), null);
                }

            case "memory":
                {
                    return new(await PdfDocumentReader.OpenWithAsync(path, new() { Source = PdfSourceKind.Memory }, CancellationToken.None), null);
                }

            case "stream":
                {
                    var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous);
                    return new(await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None), stream);
                }

            default:
                {
                    return new(await PdfDocumentReader.OpenWithAsync(path, new() { Source = PdfSourceKind.Mapped }, CancellationToken.None), null);
                }
        }
    }
}
