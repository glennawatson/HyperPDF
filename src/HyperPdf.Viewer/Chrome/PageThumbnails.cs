// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The small pictures of the pages in the pages panel. Each is drawn by the renderer at low priority and kept in the tab's cache, so a
/// frame only looks for it there. A picture is asked for once, and again only when a picture arriving or the cache dropping one could
/// change the answer.
/// </summary>
internal static class PageThumbnails
{
    /// <summary>Gets the picture of a page, and asks for it when it is not ready.</summary>
    /// <param name="asks">What was asked so far, which starts again when another tab is shown.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The picture, or <see langword="null"/> while it is being made.</returns>
    internal static IRenderSurface? Get(ref ThumbnailAsks asks, DocumentTabViewModel tab, int page)
    {
        if (!ReferenceEquals(tab, asks.Tab))
        {
            asks = new(tab, []);
        }

        if ((uint)page >= (uint)tab.Source.PageCount)
        {
            return null;
        }

        var key = TileKey.Preview(tab.TileId, page, PageRotation.None, tab.PageTone.Id);
        if (tab.RenderHub.Cache.TryGet(key, out var surface))
        {
            return surface;
        }

        if (tab.TryGetDocument() is { } document)
        {
            Request(asks, tab, document, key);
        }

        return null;
    }

    /// <summary>Asks the renderer for a picture, unless the same thing was already asked.</summary>
    /// <param name="asks">What was asked so far.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="document">The document.</param>
    /// <param name="key">The picture wanted.</param>
    private static void Request(ThumbnailAsks asks, DocumentTabViewModel tab, IDocument document, TileKey key)
    {
        var page = key.PageIndex;
        var client = tab.ThumbnailClient;
        var ask = new ThumbnailAsk(client.Generation, tab.RenderHub.PreviewVersion);

        // Any picture arriving or the cache dropping one moves the version, which is when asking again can change the answer.
        if (asks.Asked.TryGetValue(page, out var before) && before == ask)
        {
            return;
        }

        asks.Asked[page] = ask;
        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, PageRotation.None);
        TileGrid.GetPagePixelSize(size, PageRotation.None, scale, out var width, out var height);
        var info = new PageRenderInfo(page, scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var request = new RenderRequest(key, document, info, width, height, RenderPriority.Thumbnail, client, ask.Generation, tab.PageTone) { CancellationToken = tab.SelectedWorkToken };
        _ = tab.RenderHub.Scheduler.Request(request);
    }
}
