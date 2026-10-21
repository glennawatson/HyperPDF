// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>Where the pointer is on the page canvas: the page under it and the point on that page, found once per move.</summary>
/// <param name="Page">The page under the pointer, or -1 when it is over no page.</param>
/// <param name="Point">The point in page space; meaningless when <paramref name="Page"/> is -1.</param>
internal readonly record struct PointerHover(int Page, PagePoint Point)
{
    /// <summary>Gets a value indicating whether the pointer is over a page.</summary>
    internal bool IsOverPage => Page >= 0;

    /// <summary>Finds the page and page point under a canvas point.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns>The hover; its page is -1 when the pointer is over no page or the canvas has no tab.</returns>
    internal static PointerHover At(PageCanvas canvas, Point position)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var page = canvas.Layout.HitTest(position.X, position.Y);
        return page < 0 || canvas.Tab is not { } tab ? new(-1, default) : new(page, canvas.ToPage(tab, page, position));
    }
}
