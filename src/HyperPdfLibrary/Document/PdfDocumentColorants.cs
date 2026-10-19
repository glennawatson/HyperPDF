// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <summary>Lists the colorants a page paints, for a separation view that shows or hides each one.</summary>
public static class PdfDocumentColorants
{
    /// <summary>
    /// Lists the colorants the page's content paints: the Separation and DeviceN colorant names, and the process
    /// colorants (Cyan, Magenta, Yellow, Black) that DeviceCMYK and four-component ICC colours put ink in, with Black for
    /// DeviceGray and one-component ICC colours. A colorant is listed when a fill, stroke, glyph, stencil mask or shading
    /// paints it with some ink; an image or shading lists every colorant of its colour space, because the pixels are not
    /// read. RGB, Lab and Indexed colours list nothing, and annotation appearances are not read. Pass the names to
    /// <see cref="PdfRenderOptions.HiddenColorants"/> to hide them.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The process colorants in Cyan, Magenta, Yellow, Black order, then the others in the order they are first painted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfColorant[] GetPageColorants(PdfDocument document, int pageIndex) =>
        PdfDocumentColorants.Collect(document, pageIndex, CancellationToken.None);

    /// <summary>Lists the colorants a page paints, loading the page's objects first. See <see cref="GetPageColorants"/>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading and the walk of the content.</param>
    /// <returns>The colorants.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfColorant[]> GetPageColorantsAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        await PdfDocumentPages.PrefetchPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        return PdfDocumentColorants.Collect(document, pageIndex, cancellationToken);
    }

    /// <summary>Runs a page's content into a collector.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The colorants.</returns>
    private static PdfColorant[] Collect(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        cancellationToken.ThrowIfCancellationRequested();
        var collector = new PageColorantCollector();

        // A private cache, so pattern cells and soft masks recorded for drawing are walked again instead of reused.
        using var interpreter = ContentInterpreter.Rent(new(document), collector, 0, cancellationToken);
        interpreter.Colorants = collector;
        try
        {
            ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, pageIndex));
        }
        catch (Exception ex) when (ex is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException)
        {
            // The colorants painted before damaged content stops the walk are still listed.
        }

        return collector.Result();
    }
}
