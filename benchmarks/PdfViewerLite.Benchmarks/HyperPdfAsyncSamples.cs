// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>The documents and open helpers shared by the sync versus async HyperPdfLibrary benchmarks.</summary>
internal static class HyperPdfAsyncSamples
{
    /// <summary>The pages of the generated text report.</summary>
    internal const int ReportPages = 50;

    /// <summary>The render scale: one pixel per point.</summary>
    internal const float Scale = 1F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The pages rendered by the multi-page benchmarks.</summary>
    internal const int RenderedPages = 10;

    /// <summary>The widest page, in pixels, the shared pixel buffers hold.</summary>
    internal const int MaxWidth = 1100;

    /// <summary>The tallest page, in pixels, the shared pixel buffers hold.</summary>
    internal const int MaxHeight = 1500;

    /// <summary>The word the search benchmarks look for.</summary>
    internal const string Word = "the";

    /// <summary>The cached corpus folder.</summary>
    private static readonly string CorpusFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "reference-pdfs");

    /// <summary>Gets the path of a sample document, writing generated ones to the temp folder.</summary>
    /// <param name="name">Report, Scan, Form or Book.</param>
    /// <returns>The path.</returns>
    internal static string PathFor(string name)
    {
        switch (name)
        {
            case "Scan":
                {
                    return Path.Combine(CorpusFolder, "loc-marbury-v-madison.pdf");
                }

            case "Book":
                {
                    return Path.Combine(CorpusFolder, "ia-us-reports-341.pdf");
                }

            default:
                {
                    var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-async-{name}.pdf");
                    File.WriteAllBytes(path, name == "Form" ? TestPdf.CreateForm() : TestPdf.Create(ReportPages));
                    return path;
                }
        }
    }

    /// <summary>Opens a document the synchronous way for a source kind.</summary>
    /// <param name="path">The file.</param>
    /// <param name="source">Mapped, FileStream, Memory or UserStream.</param>
    /// <returns>The opened document.</returns>
    internal static OpenedDocument OpenSync(string path, string source)
    {
        if (source == "UserStream")
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess);
            return new(PdfDocumentReader.Open(stream, null), stream);
        }

        return new(PdfDocumentReader.OpenWith(path, new() { Source = KindOf(source) }), null);
    }

    /// <summary>Opens a document the async way for a source kind.</summary>
    /// <param name="path">The file.</param>
    /// <param name="source">Mapped, FileStream, Memory or UserStream.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The opened document.</returns>
    internal static async ValueTask<OpenedDocument> OpenAsync(string path, string source, CancellationToken cancellationToken)
    {
        if (source == "UserStream")
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess | FileOptions.Asynchronous);
            return new(await PdfDocumentReader.OpenAsync(stream, null, cancellationToken).ConfigureAwait(false), stream);
        }

        return new(await PdfDocumentReader.OpenWithAsync(path, new() { Source = KindOf(source) }, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>Renders a page at one pixel per point into a shared buffer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="pixels">The pixels.</param>
    /// <returns>Whether the page was drawn.</returns>
    internal static bool RenderSync(PdfPageRenderer renderer, PdfDocument document, int pageIndex, byte[] pixels)
    {
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, pageIndex), 0, Scale, out var width, out var height);
        return renderer.Render(new(pageIndex, Scale, 0, 0, 0, PdfRenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
    }

    /// <summary>Renders a page at one pixel per point into a shared buffer with the async API.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="pixels">The pixels.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>Whether the page was drawn.</returns>
    internal static ValueTask<bool> RenderAsync(PdfPageRenderer renderer, PdfDocument document, int pageIndex, byte[] pixels, CancellationToken cancellationToken)
    {
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, pageIndex), 0, Scale, out var width, out var height);
        return renderer.RenderAsync(new(pageIndex, Scale, 0, 0, 0, PdfRenderFlags.Annotations), pixels, width, height, width * BytesPerPixel, cancellationToken);
    }

    /// <summary>Gets the source kind a name stands for.</summary>
    /// <param name="source">The name.</param>
    /// <returns>The kind.</returns>
    private static PdfSourceKind KindOf(string source) => source switch
    {
        "Memory" => PdfSourceKind.Memory,
        "FileStream" => PdfSourceKind.Stream,
        _ => PdfSourceKind.Mapped,
    };
}
