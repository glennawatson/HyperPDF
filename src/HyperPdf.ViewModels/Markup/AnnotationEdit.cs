// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Works out where an annotation goes while it is dragged or resized.</summary>
public static class AnnotationEdit
{
    /// <summary>Gets the bounds an annotation has while it is dragged or resized.</summary>
    /// <param name="bounds">The bounds the annotation had when the drag started.</param>
    /// <param name="resizing">Whether a corner handle is being dragged, and otherwise the whole annotation moves.</param>
    /// <param name="anchor">When resizing, the opposite corner that stays put; when moving, the point that was grabbed.</param>
    /// <param name="point">Where the pointer is now, in page space.</param>
    /// <returns>The new bounds.</returns>
    public static PageRect GetNextBounds(PageRect bounds, bool resizing, PagePoint anchor, PagePoint point) =>
        resizing
            ? PageRect.FromEdges(
        Math.Min(anchor.X, point.X),
        Math.Min(anchor.Y, point.Y),
        Math.Max(anchor.X, point.X),
        Math.Max(anchor.Y, point.Y))
            : bounds with { Left = bounds.Left + (point.X - anchor.X), Top = bounds.Top + (point.Y - anchor.Y) };
}
