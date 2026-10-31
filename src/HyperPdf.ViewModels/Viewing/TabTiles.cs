// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// The identity a tab's tiles carry. The two views of a split document share one open document but render for different
/// clients, and the render scheduler restarts a running tile that another client asks for. So the second view keys its
/// tiles apart from the first, and a change to the document drops the tiles of both.
/// </summary>
internal static class TabTiles
{
    /// <summary>Gets the identity a view's tiles carry.</summary>
    /// <param name="sourceId">The document source's identity.</param>
    /// <param name="isSecondaryView">Whether the view is the second view of a split document.</param>
    /// <returns>The source identity for the first view, and its bitwise complement for the second.</returns>
    internal static int IdFor(int sourceId, bool isSecondaryView) => isSecondaryView ? ~sourceId : sourceId;

    /// <summary>Drops queued and running renders of both views of a document, so none of them is cached.</summary>
    /// <param name="hub">The render hub.</param>
    /// <param name="sourceId">The document source's identity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void InvalidateBoth(RenderHub hub, int sourceId) => hub.Scheduler.Invalidate(sourceId, ~sourceId);

    /// <summary>Drops the renders and the cached tiles of both views of a document.</summary>
    /// <param name="hub">The render hub.</param>
    /// <param name="sourceId">The document source's identity.</param>
    internal static void DropBoth(RenderHub hub, int sourceId)
    {
        InvalidateBoth(hub, sourceId);
        hub.RemoveDocumentTiles(sourceId);
        hub.RemoveDocumentTiles(~sourceId);
    }

    /// <summary>Drops the tiles a change made stale: every tile for a change to all pages, otherwise those of the page changed.</summary>
    /// <param name="hub">The render hub.</param>
    /// <param name="sourceId">The document source's identity.</param>
    /// <param name="pageIndex">The page changed, or <see cref="PageChange.AllPages"/>.</param>
    internal static void DropChanged(RenderHub hub, int sourceId, int pageIndex)
    {
        if (pageIndex == PageChange.AllPages)
        {
            DropBoth(hub, sourceId);
            return;
        }

        // Tiles rendered before the edit may still be on their way; the scheduler drops them so they cannot be cached.
        InvalidateBoth(hub, sourceId);
        hub.Cache.RemovePage(sourceId, ~sourceId, pageIndex);
    }
}
