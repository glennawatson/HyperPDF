// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.App.Controls;

/// <summary>Maps between unrotated page space (points) and canvas space for one laid out page.</summary>
/// <param name="Bounds">The page bounds on the canvas.</param>
/// <param name="Size">The unrotated page size.</param>
/// <param name="Rotation">The rotation.</param>
/// <param name="Scale">Canvas units per point.</param>
internal readonly record struct PageTransform(LayoutRect Bounds, PageSize Size, PageRotation Rotation, double Scale)
{
    /// <summary>Converts a page point to the canvas.</summary>
    /// <param name="point">The page point.</param>
    /// <returns>The canvas point.</returns>
    internal Point ToCanvas(PagePoint point)
    {
        var (x, y) = Rotation switch
        {
            PageRotation.Rotate90 => (Size.Height - point.Y, point.X),
            PageRotation.Rotate180 => (Size.Width - point.X, Size.Height - point.Y),
            PageRotation.Rotate270 => (point.Y, Size.Width - point.X),
            _ => (point.X, point.Y),
        };
        return new(Bounds.X + (x * Scale), Bounds.Y + (y * Scale));
    }

    /// <summary>Converts a page rectangle to the canvas.</summary>
    /// <param name="rect">The page rectangle.</param>
    /// <returns>The canvas rectangle.</returns>
    internal Rect ToCanvas(PageRect rect)
    {
        var a = ToCanvas(new PagePoint(rect.Left, rect.Top));
        var b = ToCanvas(new PagePoint(rect.Right, rect.Bottom));
        return new Rect(a, b).Normalize();
    }

    /// <summary>Converts a canvas point to page space.</summary>
    /// <param name="point">The canvas point.</param>
    /// <returns>The page point.</returns>
    internal PagePoint ToPage(Point point)
    {
        var x = (point.X - Bounds.X) / Scale;
        var y = (point.Y - Bounds.Y) / Scale;
        var (px, py) = Rotation switch
        {
            PageRotation.Rotate90 => (y, Size.Height - x),
            PageRotation.Rotate180 => (Size.Width - x, Size.Height - y),
            PageRotation.Rotate270 => (Size.Width - y, x),
            _ => (x, y),
        };
        return new((float)px, (float)py);
    }
}
