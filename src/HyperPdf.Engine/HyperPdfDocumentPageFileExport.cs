// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Exporting;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reactive;
using HyperPdfLibrary.Export;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Implements <see cref="IPageFileExporter"/> over the document's owned state: pictures drawn from the rendered page, and SVG and PostScript written from the page's own drawing.</summary>
internal static class HyperPdfDocumentPageFileExport
{
    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The most pixels a picture may have, which keeps its memory near 200 MB.</summary>
    private const long MaximumPixels = 50_000_000;

    /// <summary>Writes pages as one file.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The zero based pages, in order.</param>
    /// <param name="format">The file format.</param>
    /// <param name="options">How to write them.</param>
    /// <param name="destination">The stream that receives the file.</param>
    /// <returns>A cold signal giving what the export did; unsubscribing cancels it. A bad request fails the signal with the argument exception.</returns>
    internal static IObservable<PageFileExportResult> Export(
        HyperPdfDocument self,
        int[] pages,
        PageFileFormat format,
        PageFileExportOptions options,
        Stream destination)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(destination);
        return Signal.Defer(() =>
        {
            Validate(self, pages, format, options);
            var flags = options.IncludeMarkup ? RenderFlags.Annotations : RenderFlags.None;
            return format switch
            {
                PageFileFormat.Svg => WriteSvg(self, pages[0], flags, destination),
                PageFileFormat.PostScript => WritePostScript(self, pages, flags, PdfPostScriptOptions.Document, destination),
                PageFileFormat.EncapsulatedPostScript => WritePostScript(self, pages, flags, PdfPostScriptOptions.Eps, destination),
                _ => WriteRaster(self, pages[0], ToRasterFormat(format), options.Dpi, flags, destination),
            };
        });
    }

    /// <summary>Checks that the request makes sense.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="format">The format.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentException">There are no pages, or the format takes one page and more were given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page is not in the document, or the resolution is outside the allowed range.</exception>
    private static void Validate(HyperPdfDocument self, int[] pages, PageFileFormat format, PageFileExportOptions options)
    {
        var info = PageFileFormats.Get(format);
        if (pages.Length == 0 || (pages.Length > 1 && !info.AllowsSeveralPages))
        {
            throw new ArgumentException($"{info.Name} files hold one page; give one page.", nameof(pages));
        }

        foreach (var page in pages)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(page);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(page, self.PageCount);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(options.Dpi, PageFileExportOptions.MinimumDpi);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Dpi, PageFileExportOptions.MaximumDpi);
    }

    /// <summary>Maps a picture format to the library's.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The library's format.</returns>
    private static PdfRasterFileFormat ToRasterFormat(PageFileFormat format) => format switch
    {
        PageFileFormat.Jpeg => PdfRasterFileFormat.Jpeg,
        PageFileFormat.Tiff => PdfRasterFileFormat.Tiff,
        PageFileFormat.Bmp => PdfRasterFileFormat.Bmp,
        PageFileFormat.Pnm => PdfRasterFileFormat.Pnm,
        PageFileFormat.Pam => PdfRasterFileFormat.Pam,
        PageFileFormat.Tga => PdfRasterFileFormat.Tga,
        PageFileFormat.Psd => PdfRasterFileFormat.Psd,
        _ => PdfRasterFileFormat.Png,
    };

    /// <summary>Draws a page to pixels and writes them as a picture file.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="format">The picture format.</param>
    /// <param name="dpi">The resolution.</param>
    /// <param name="flags">What to draw.</param>
    /// <param name="destination">The stream that receives the file.</param>
    /// <returns>A cold signal giving what the export did.</returns>
    private static IObservable<PageFileExportResult> WriteRaster(
        HyperPdfDocument self,
        int page,
        PdfRasterFileFormat format,
        int dpi,
        RenderFlags flags,
        Stream destination)
    {
        var size = self.GetPageSize(page);
        var scale = dpi / PointsPerInch;
        var width = Math.Max(1, (int)MathF.Ceiling(size.Width * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(size.Height * scale));
        return (long)width * height > MaximumPixels
            ? Signal.Return(PageFileExportResult.Failed("That picture would be too large. Choose a lower resolution."))
            : HyperPdfRendering.PreparePage(self, page)
                .SwitchMap(_ =>
                {
                    var pixels = new byte[width * height * BytesPerPixel];
                    var info = new PageRenderInfo(page, scale, PageRotation.None, 0, 0, flags);

                    // The library's own async encoder, awaited once here; unsubscribing cancels it.
                    return HyperPdfRendering.Render(self, in info, new(pixels, width, height, width * BytesPerPixel))
                        ? Signal.FromAsync(async token =>
                        {
                            await PdfRasterFiles.EncodeAsync(PdfRasterFiles.FromPremultipliedBgra(pixels, width, height), format, destination, token).ConfigureAwait(false);
                            return PageFileExportResult.Exact();
                        })
                        : Signal.Return(PageFileExportResult.Failed("The page could not be drawn."));
                });
    }

    /// <summary>Writes a page as an SVG drawing.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="flags">What to draw.</param>
    /// <param name="destination">The stream that receives the file.</param>
    /// <returns>A cold signal giving what the export did.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<PageFileExportResult> WriteSvg(HyperPdfDocument self, int page, RenderFlags flags, Stream destination) =>
        HyperPdfRendering.PreparePage(self, page)
            .SwitchMap(_ => BuildSvg(self, page, flags) is { } bytes
                ? WriteBytes(destination, bytes, PageFileExportResult.Exact())
                : Signal.Return(PageFileExportResult.Failed("The page could not be saved as an SVG drawing.")));

    /// <summary>Writes a built file to the destination stream.</summary>
    /// <param name="destination">The stream that receives the file.</param>
    /// <param name="bytes">The file.</param>
    /// <param name="result">What to report once it is written.</param>
    /// <returns>A cold signal giving the result once the bytes are written; unsubscribing cancels the write.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<PageFileExportResult> WriteBytes(Stream destination, byte[] bytes, PageFileExportResult result) =>
        Signal.FromAsync(async token =>
        {
            // The stream's own async write, awaited once here.
            await destination.WriteAsync(bytes, token).ConfigureAwait(false);
            return result;
        });

    /// <summary>Builds the SVG drawing of a page in memory.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="flags">What to draw.</param>
    /// <returns>The file, or <see langword="null"/> when the page could not be written.</returns>
    private static byte[]? BuildSvg(HyperPdfDocument self, int page, RenderFlags flags)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return null;
        }

        using var memory = new MemoryStream();
        return HyperPdfRendering.GetRenderer(self).WriteSvg(page, HyperPdfRendering.ToLibraryFlags(flags), memory) ? memory.ToArray() : null;
    }

    /// <summary>Writes pages as PostScript or Encapsulated PostScript.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="flags">What to draw.</param>
    /// <param name="postScript">Whether to write a document or one encapsulated page.</param>
    /// <param name="destination">The stream that receives the file.</param>
    /// <returns>A cold signal giving what the export did.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<PageFileExportResult> WritePostScript(
        HyperPdfDocument self,
        int[] pages,
        RenderFlags flags,
        PdfPostScriptOptions postScript,
        Stream destination) =>
        LazySequence.Concat(PreparePages(self, pages))
            .Count()
            .SwitchMap(_ => Signal.FromAsync(async token =>
            {
                // The library's own cancellable writer and the stream's own async write, awaited once here.
                if (BuildPostScript(self, pages, flags, postScript, token) is not { } output)
                {
                    return PageFileExportResult.Failed("The pages could not be saved as PostScript.");
                }

                await destination.WriteAsync(output.Bytes, token).ConfigureAwait(false);
                return new PageFileExportResult(true, output.Report.Skipped, output.Report.Approximated, null);
            }));

    /// <summary>Makes the preparation of each page, one at a time as the caller moves along.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <returns>A cold signal per page.</returns>
    private static IEnumerable<IObservable<RxVoid>> PreparePages(HyperPdfDocument self, int[] pages)
    {
        foreach (var page in pages)
        {
            yield return HyperPdfRendering.PreparePage(self, page);
        }
    }

    /// <summary>Builds a PostScript file in memory.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="flags">What to draw.</param>
    /// <param name="postScript">Whether to write a document or one encapsulated page.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>The file and its report, or <see langword="null"/> when the document is closed.</returns>
    private static PostScriptOutput? BuildPostScript(HyperPdfDocument self, int[] pages, RenderFlags flags, PdfPostScriptOptions postScript, CancellationToken cancellationToken)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return null;
        }

        using var memory = new MemoryStream();
        var report = HyperPdfRendering.GetRenderer(self).WritePostScript(pages, HyperPdfRendering.ToLibraryFlags(flags), postScript, memory, cancellationToken);
        return new(memory.ToArray(), report);
    }
}
