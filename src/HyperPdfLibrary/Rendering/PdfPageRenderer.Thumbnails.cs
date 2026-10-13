// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Rendering;

/// <content>Page thumbnails.</content>
public sealed partial class PdfPageRenderer
{
    /// <summary>Gets the size of a thumbnail: the page, upright as the viewer shows it, fitted into a square of <paramref name="maxEdge"/> pixels.</summary>
    /// <param name="page">The page.</param>
    /// <param name="maxEdge">The longest side in pixels.</param>
    /// <param name="width">Receives the width in pixels.</param>
    /// <param name="height">Receives the height in pixels.</param>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEdge"/> is not positive.</exception>
    public static void GetThumbnailSize(PdfPage page, int maxEdge, out int width, out int height)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEdge);
        GetPixelSize(page, 0, ThumbnailScale(page, maxEdge), out width, out height);
        width = Math.Min(width, maxEdge);
        height = Math.Min(height, maxEdge);
    }

    /// <summary>Decodes a page's embedded thumbnail image (<c>/Thumb</c>), (ISO 32000-2 12.3.4); most files have none.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The thumbnail as premultiplied BGRA, which the caller owns; <see langword="null"/> when the page has none or it is damaged.</returns>
    /// <exception cref="ObjectDisposedException">The renderer has been disposed.</exception>
    public IPdfRenderImage? GetEmbeddedThumbnail(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)pageIndex >= (uint)_document.PageCount)
        {
            return null;
        }

        var page = PdfDocumentPages.GetPage(_document, pageIndex);
        if (page.Dictionary.Get(Cache.Thumb).AsStream() is not { } thumb)
        {
            return null;
        }

        var data = PdfImageDecoder.Decode(thumb, page.Resources);
        return data is null || data.IsStencilMask || data.UnsupportedCodec != PdfImageCodec.None ? null : Cache.ToRenderImage(data);
    }

    /// <summary>
    /// Renders a thumbnail of a page into a target sized by <see cref="GetThumbnailSize"/>. The embedded thumbnail is
    /// stretched to the target when the page has one; otherwise the page content alone is drawn at the small scale,
    /// without annotations, reusing the page's recorded picture.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="maxEdge">The longest side in pixels, as given to <see cref="GetThumbnailSize"/>.</param>
    /// <param name="target">The pixel buffer to fill.</param>
    /// <returns><see langword="false"/> when the page does not exist or the target is invalid.</returns>
    /// <exception cref="ObjectDisposedException">The renderer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEdge"/> is not positive.</exception>
    /// <remarks>The target is drawn directly; a drawing exception can leave partial pixels.</remarks>
    public bool RenderThumbnail(int pageIndex, int maxEdge, PdfTileTarget target)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEdge);
        using var embedded = GetEmbeddedThumbnail(pageIndex);
        if (embedded is null)
        {
            var page = (uint)pageIndex < (uint)_document.PageCount ? PdfDocumentPages.GetPage(_document, pageIndex) : null;
            return page is not null && Render(new(pageIndex, ThumbnailScale(page, maxEdge), 0, 0, 0, PdfRenderFlags.None), target);
        }

        return target.IsValid && _document.DrawingServices.Backend.GetDrawingSession().DrawImage(embedded, target);
    }

    /// <summary>Gets the scale that fits a page into a square.</summary>
    /// <param name="page">The page.</param>
    /// <param name="maxEdge">The square's side in pixels.</param>
    /// <returns>Device pixels per point.</returns>
    private static float ThumbnailScale(PdfPage page, int maxEdge) => maxEdge / Math.Max(1, Math.Max(page.PhysicalWidth, page.PhysicalHeight));
}
