// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>Maps between unrotated page space (points) and canvas space for one laid out page.</summary>
/// <param name="Bounds">The page bounds on the canvas.</param>
/// <param name="Size">The unrotated page size.</param>
/// <param name="Rotation">The rotation.</param>
/// <param name="Scale">Canvas units per point.</param>
[DebuggerDisplay("PageTransform: {Bounds}, {Rotation}, scale {Scale}")]
public readonly record struct PageTransform(LayoutRect Bounds, PageSize Size, PageRotation Rotation, double Scale)
{
    /// <summary>Converts a page point to the canvas.</summary>
    /// <param name="point">The page point.</param>
    /// <returns>The canvas point.</returns>
    public CanvasPoint MapToCanvas(PagePoint point)
    {
        var rotated = Rotation switch
        {
            PageRotation.Rotate90 => new CanvasPoint(Size.Height - point.Y, point.X),
            PageRotation.Rotate180 => new CanvasPoint(Size.Width - point.X, Size.Height - point.Y),
            PageRotation.Rotate270 => new CanvasPoint(point.Y, Size.Width - point.X),
            _ => new CanvasPoint(point.X, point.Y),
        };
        return new(Bounds.X + (rotated.X * Scale), Bounds.Y + (rotated.Y * Scale));
    }

    /// <summary>Converts a page rectangle to the canvas.</summary>
    /// <param name="rect">The page rectangle.</param>
    /// <returns>The canvas rectangle, with a positive width and height.</returns>
    public LayoutRect MapToCanvas(PageRect rect)
    {
        var a = MapToCanvas(new PagePoint(rect.Left, rect.Top));
        var b = MapToCanvas(new PagePoint(rect.Right, rect.Bottom));
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new(left, top, Math.Max(a.X, b.X) - left, Math.Max(a.Y, b.Y) - top);
    }

    /// <summary>Converts a canvas point to page space.</summary>
    /// <param name="x">The canvas x.</param>
    /// <param name="y">The canvas y.</param>
    /// <returns>The page point.</returns>
    public PagePoint MapToPage(double x, double y)
    {
        var px = (x - Bounds.X) / Scale;
        var py = (y - Bounds.Y) / Scale;
        return Rotation switch
        {
            PageRotation.Rotate90 => new((float)py, (float)(Size.Height - px)),
            PageRotation.Rotate180 => new((float)(Size.Width - px), (float)(Size.Height - py)),
            PageRotation.Rotate270 => new((float)(Size.Width - py), (float)px),
            _ => new((float)px, (float)py),
        };
    }
}
