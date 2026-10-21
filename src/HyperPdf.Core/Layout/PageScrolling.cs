// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Layout;

/// <summary>Works out where a view scrolls to so a place on a page is shown, whichever toolkit draws the view.</summary>
public static class PageScrolling
{
    /// <summary>Keeps a vertical offset inside a page's slot when scrolling page by page.</summary>
    /// <param name="layout">The document layout.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="offset">The vertical offset.</param>
    /// <param name="viewportHeight">The height of the viewport, or <see langword="null"/> before the view has one.</param>
    /// <returns>The offset, clamped to the slot when page by page and the viewport is known, and unchanged otherwise.</returns>
    public static double ClampToSlot(DocumentLayout layout, int pageIndex, double offset, double? viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!layout.Options.PageByPage || viewportHeight is not { } height)
        {
            return offset;
        }

        layout.GetRowExtent(pageIndex, out var top, out var bottom);
        return Math.Clamp(offset, top, Math.Max(top, bottom - height));
    }

    /// <summary>Gets the vertical offset that shows a position on a page, kept inside the page's slot when page by page.</summary>
    /// <param name="layout">The document layout.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
    /// <param name="contentMargin">The space kept above the position.</param>
    /// <param name="viewportHeight">The height of the viewport, or <see langword="null"/> before the view has one.</param>
    /// <returns>The vertical offset.</returns>
    public static double GetTop(DocumentLayout layout, int pageIndex, double offsetFraction, double contentMargin, double? viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var bounds = layout.GetPageBounds(pageIndex);
        return ClampToSlot(layout, pageIndex, bounds.Y + (bounds.Height * offsetFraction) - contentMargin, viewportHeight);
    }

    /// <summary>Gets the horizontal offset that shows a page: its left side in a horizontal layout, and otherwise the offset the reader has.</summary>
    /// <param name="layout">The document layout.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="current">The current horizontal offset.</param>
    /// <param name="contentMargin">The space kept to the left of the page.</param>
    /// <returns>The horizontal offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetLeft(DocumentLayout layout, int pageIndex, double current, double contentMargin)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.Options.Scroll == ScrollMode.Horizontal ? Math.Max(0, layout.GetPageBounds(pageIndex).X - contentMargin) : current;
    }

    /// <summary>Gets the offset along one axis that scrolls just enough to show a span, leaving the offset alone when the span is in view.</summary>
    /// <param name="offset">The current offset.</param>
    /// <param name="viewport">The size of the viewport along the axis.</param>
    /// <param name="near">Where the span starts along the axis.</param>
    /// <param name="far">Where the span ends along the axis.</param>
    /// <param name="contentMargin">The space kept beyond the span after scrolling.</param>
    /// <returns>The offset.</returns>
    public static double GetOffsetToShow(double offset, double viewport, double near, double far, double contentMargin)
    {
        if (near < offset)
        {
            return near - contentMargin;
        }

        return far > offset + viewport ? far - viewport + contentMargin : offset;
    }

    /// <summary>Gets the vertical offset that shows a position on a page: the current one in a horizontal layout, which only moves sideways.</summary>
    /// <param name="layout">The document layout.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
    /// <param name="current">The current vertical offset.</param>
    /// <param name="contentMargin">The space kept above the position.</param>
    /// <param name="viewportHeight">The height of the viewport, or <see langword="null"/> before the view has one.</param>
    /// <returns>The vertical offset.</returns>
    public static double GetScrollTop(DocumentLayout layout, int pageIndex, double offsetFraction, double current, double contentMargin, double? viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.Options.Scroll == ScrollMode.Horizontal ? current : GetTop(layout, pageIndex, offsetFraction, contentMargin, viewportHeight);
    }
}
