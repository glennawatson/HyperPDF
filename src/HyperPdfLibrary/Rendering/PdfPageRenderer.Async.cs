// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <content>Async rendering.</content>
public sealed partial class PdfPageRenderer
{
    /// <summary>
    /// Renders part of a page into BGRA premultiplied pixels. The page's objects are loaded first with async I/O; then the
    /// content is recorded in slices of operators, with the token checked between slices and inside long operators such as
    /// image and stream decoding, so a cancelled render stops its CPU work and frees its buffers promptly.
    /// </summary>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="pixels">The pixel buffer to fill; it must stay untouched until the task completes.</param>
    /// <param name="width">The width of the target in pixels.</param>
    /// <param name="height">The height of the target in pixels.</param>
    /// <param name="stride">The bytes from one row to the next.</param>
    /// <param name="cancellationToken">Cancels the loading and the render.</param>
    /// <returns><see langword="false"/> when the page does not exist or the target is invalid.</returns>
    /// <exception cref="ObjectDisposedException">The renderer has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<bool> RenderAsync(PdfTileRequest request, Memory<byte> pixels, int width, int height, int stride, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var load = (uint)request.PageIndex < (uint)_document.PageCount
            ? HyperPdfLibrary.Document.PdfDocumentPages.PrefetchPageAsync(_document, request.PageIndex, cancellationToken)
            : ValueTask.CompletedTask;
        return load.IsCompletedSuccessfully
            ? RenderReady(request, pixels, width, height, stride, cancellationToken)
            : RenderAfterAsync(load, request, pixels, width, height, stride, cancellationToken);
    }

    /// <summary>Renders once the page's objects are loaded, reporting failures through the task.</summary>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="pixels">The pixel buffer.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The row stride in bytes.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed result, or a faulted task.</returns>
    private ValueTask<bool> RenderReady(PdfTileRequest request, Memory<byte> pixels, int width, int height, int stride, CancellationToken cancellationToken)
    {
        try
        {
            return new(RenderScoped(request, pixels, width, height, stride, cancellationToken));
        }
        catch (Exception ex) when (ex is OperationCanceledException or PdfException)
        {
            return ValueTask.FromException<bool>(ex);
        }
    }

    /// <summary>Waits for the page's objects to load, then renders.</summary>
    /// <param name="load">The load.</param>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="pixels">The pixel buffer.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The row stride in bytes.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>Whether the tile was drawn.</returns>
    private async ValueTask<bool> RenderAfterAsync(ValueTask load, PdfTileRequest request, Memory<byte> pixels, int width, int height, int stride, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return RenderScoped(request, pixels, width, height, stride, cancellationToken);
    }

    /// <summary>Renders with the token in force for the synchronous core.</summary>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="pixels">The pixel buffer.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The row stride in bytes.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>Whether the tile was drawn.</returns>
    private bool RenderScoped(PdfTileRequest request, Memory<byte> pixels, int width, int height, int stride, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = RenderProgressive(request, new(pixels.Span, width, height, stride), null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return status == PdfRenderStatus.Done;
    }
}
