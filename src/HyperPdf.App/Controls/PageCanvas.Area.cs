// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Layout;

namespace HyperPdf.App.Controls;

/// <summary>
/// The area tools: with Zoom to Area or Snapshot, a drag draws a rectangle on the pages. On release, Zoom to Area
/// zooms until the rectangle fills the view, centred on it, and Snapshot copies a picture of it. Escape cancels a
/// drag; with no drag, it goes back to selecting text.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>Whether an area is being dragged.</summary>
    private bool _areaDragging;

    /// <summary>Where the area drag started, in canvas coordinates.</summary>
    private Point _areaStart;

    /// <summary>Where the area drag is now, in canvas coordinates.</summary>
    private Point _areaEnd;

    /// <summary>Gets the rectangle between two corners, whichever way it was dragged.</summary>
    /// <param name="first">One corner.</param>
    /// <param name="second">The opposite corner.</param>
    /// <returns>The rectangle.</returns>
    private static Rect Between(Point first, Point second) =>
        new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));

    /// <summary>Starts dragging an area when Zoom to Area or Snapshot is chosen.</summary>
    /// <param name="e">The press.</param>
    /// <param name="point">The pointer state.</param>
    /// <returns><see langword="true"/> when the press starts an area.</returns>
    private bool BeginArea(PointerPressedEventArgs e, PointerPoint point)
    {
        if (Tab is not { PageTool: PageTool.ZoomArea or PageTool.Snapshot } || !point.Properties.IsLeftButtonPressed)
        {
            return false;
        }

        _ = Focus();
        _areaStart = point.Position;
        _areaEnd = point.Position;
        _areaDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
        return true;
    }

    /// <summary>Moves the far corner of the area with the pointer.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> while an area is being dragged.</returns>
    private bool ContinueArea(Point position)
    {
        if (!_areaDragging)
        {
            return false;
        }

        _areaEnd = position;
        InvalidateVisual();
        return true;
    }

    /// <summary>Finishes the area: zooms to it or copies a picture of it.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when an area was being dragged.</returns>
    private bool EndArea(Point position)
    {
        if (!_areaDragging)
        {
            return false;
        }

        _areaDragging = false;
        InvalidateVisual();
        if (Tab is not { } tab)
        {
            return true;
        }

        var area = Between(_areaStart, position);
        if (tab.PageTool == PageTool.ZoomArea)
        {
            ZoomToArea(tab, area);
        }
        else
        {
            _ = SnapshotAsync(tab, area);
        }

        return true;
    }

    /// <summary>Handles Escape for the moving and area tools: cancels a drag, or goes back to selecting text.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns><see langword="true"/> when Escape was used.</returns>
    private bool EscapePageTool(DocumentTabViewModel tab)
    {
        if (_areaDragging)
        {
            _areaDragging = false;
            InvalidateVisual();
            return true;
        }

        if (tab.PageTool == PageTool.SelectText)
        {
            return false;
        }

        tab.PageTool = PageTool.SelectText;
        Cursor = null;
        return true;
    }

    /// <summary>Zooms until an area fills the view, with the area's middle in the middle of the view.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="area">The area, in canvas coordinates.</param>
    private void ZoomToArea(DocumentTabViewModel tab, in Rect area)
    {
        if (_scroller is null)
        {
            return;
        }

        var viewport = _scroller.Viewport;
        var centre = area.Center;
        var zoom = AreaZoom.GetZoom(tab.Zoom, area.Width, area.Height, viewport.Width, viewport.Height);
        var page = _layout.HitTest(centre.X, centre.Y);
        if (page < 0)
        {
            page = _layout.GetPageNearest(centre.Y);
        }

        if (page < 0)
        {
            tab.SetZoom(zoom);
            return;
        }

        var pagePoint = ToPage(tab, page, centre);
        _anchoring = true;
        try
        {
            tab.SetZoom(zoom);

            // Measured now so the offset follows the new size in the same frame.
            _scroller.UpdateLayout();
            var target = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToCanvas(pagePoint);
            viewport = _scroller.Viewport;
            var extent = _scroller.Extent;
            var y = ClampToSlot(page, AreaZoom.GetCentredOffset(target.Y, viewport.Height, extent.Height));
            _scroller.Offset = new(AreaZoom.GetCentredOffset(target.X, viewport.Width, extent.Width), y);
        }
        finally
        {
            _anchoring = false;
        }

        ReportPosition();
    }

    /// <summary>Draws the outline of the area being dragged.</summary>
    /// <param name="context">The drawing context.</param>
    private void DrawArea(DrawingContext context)
    {
        if (!_areaDragging || _currentHitPen is not { } pen)
        {
            return;
        }

        context.DrawRectangle(null, pen, Between(_areaStart, _areaEnd));
    }
}
