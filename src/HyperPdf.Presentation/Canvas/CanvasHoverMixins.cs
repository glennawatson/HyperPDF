// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What is under the pointer on the page canvas: the page, a link, text, a field or a comment, for any toolkit.</summary>
public static class CanvasHoverMixins
{
    /// <summary>Adds the hover queries to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Finds the page and page point under a canvas point.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns>The hover; its page is -1 when the pointer is over no page or the canvas has no tab.</returns>
        public PointerHover GetHover(CanvasPoint position)
        {
            var page = state.Layout.HitTest(position.X, position.Y);
            return page < 0 || state.Tab is not { } tab ? new(-1, default) : new(page, state.ToPage(tab, page, position));
        }

        /// <summary>Finds the link under the pointer.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns>The link, if any.</returns>
        public PageLink? HitTestLink(PointerHover hover)
        {
            if (!hover.IsOverPage || state.Tab is not { } tab)
            {
                return null;
            }

            ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Links, hover.Page, out _);
            links ??= tab.TryGetDocument()?.GetLinks(hover.Page) ?? [];
            foreach (var link in links)
            {
                if (link.Bounds.Contains(hover.Point))
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>Finds the link under a canvas point.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns>The link, if any.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PageLink? HitTestLink(CanvasPoint position) => state.HitTestLink(state.GetHover(position));

        /// <summary>Determines whether the pointer is over text.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over text.</returns>
        public bool IsOverText(PointerHover hover) =>
            hover.IsOverPage && state.Tab?.TryGetDocument() is { } document && document.GetCharacterIndexAt(hover.Page, hover.Point, CanvasMetrics.TextTolerance) >= 0;

        /// <summary>Determines whether the pointer is over a fillable field.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over a field.</returns>
        public bool IsOverField(PointerHover hover) =>
            hover.IsOverPage && state.Tab is { } tab && tab.Forms.HitTest(hover.Page, hover.Point) is not null;

        /// <summary>Determines whether the pointer is over a sticky note or a comment indicator badge, which opens a bubble when clicked.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over a note or comment badge.</returns>
        public bool IsOverCommentTrigger(PointerHover hover) =>
            hover.IsOverPage && state.Tab is { } tab && tab.Annotations.IsOverCommentTrigger(hover.Page, hover.Point);

        /// <summary>Gets the pointer shape for a point where the select tool hovers: a hand over something clickable, an I-beam over text, else an arrow.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns>The shape.</returns>
        public ViewerCursor GetHoverCursor(PointerHover hover)
        {
            // The text probe takes the page lock and scans characters, and a hand cursor wins whatever it finds, so it runs only when nothing is clickable.
            var clickable = state.HitTestLink(hover) is not null || state.IsOverField(hover) || state.IsOverCommentTrigger(hover);
            return PointerRouting.GetHoverCursor(!clickable && state.IsOverText(hover), clickable);
        }

        /// <summary>Gets the pointer for the chosen tool when it is not over anything special.</summary>
        /// <returns>The shape, or <see cref="ViewerCursor.Inherit"/> for select text, whose pointer follows what is under it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ViewerCursor GetToolCursor() => PointerRouting.GetToolCursor(state.Tools.SpaceHeld, state.Tab?.PageTools.PageTool);
    }
}
