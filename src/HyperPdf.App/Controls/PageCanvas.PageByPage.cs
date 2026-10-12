// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;

namespace HyperPdf.App.Controls;

/// <summary>
/// Page by page viewing: the layout gives each row a slot at least a viewport tall, and scrolling moves within a slot
/// until its edge, then steps to the whole next or previous slot, so a page never shows half of its neighbour.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>How far one wheel notch or arrow key scrolls within a slot.</summary>
    private const double LineStep = 48;

    /// <summary>How close to a slot edge counts as at the edge.</summary>
    private const double EdgeTolerance = 0.5;

    /// <summary>Gets the vertical offset that shows a position on a page, kept inside the page's slot when page by page.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
    /// <returns>The offset.</returns>
    private double GetPageScrollTop(int pageIndex, double offsetFraction)
    {
        var bounds = _layout.GetPageBounds(pageIndex);
        return ClampToSlot(pageIndex, bounds.Y + (bounds.Height * offsetFraction) - ContentMargin);
    }

    /// <summary>Keeps an offset inside a page's slot when page by page.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The offset, clamped when page by page.</returns>
    private double ClampToSlot(int pageIndex, double offset)
    {
        if (!_layout.Options.PageByPage || _scroller is null)
        {
            return offset;
        }

        _layout.GetRowExtent(pageIndex, out var top, out var bottom);
        return Math.Clamp(offset, top, Math.Max(top, bottom - _scroller.Viewport.Height));
    }

    /// <summary>Scrolls page by page: within the current slot, or to the neighbouring slot from its edge.</summary>
    /// <param name="delta">How far to move; positive is down.</param>
    /// <returns><see langword="true"/> when page by page handled the movement.</returns>
    private bool StepPageByPage(double delta)
    {
        if (_scroller is null || !_layout.Options.PageByPage || _layout.PageCount == 0)
        {
            return false;
        }

        var y = _scroller.Offset.Y;
        var viewport = _scroller.Viewport.Height;
        _layout.GetRowExtent(_layout.GetPageNearest(y + EdgeTolerance), out var top, out var bottom);
        var last = Math.Max(top, bottom - viewport);
        double target;
        if (delta > 0)
        {
            target = y < last - EdgeTolerance ? Math.Min(y + delta, last) : bottom;
            if (target >= _layout.ExtentHeight - EdgeTolerance)
            {
                return true;
            }
        }
        else if (y > top + EdgeTolerance)
        {
            target = Math.Max(y + delta, top);
        }
        else if (top > 0)
        {
            // Show the end of the previous slot, which for a page that fits is the whole page.
            _layout.GetRowExtent(_layout.GetPageNearest(top - EdgeTolerance), out var previousTop, out var previousBottom);
            target = Math.Max(previousTop, previousBottom - viewport);
        }
        else
        {
            return true;
        }

        _scroller.Offset = new(_scroller.Offset.X, target);
        ReportPosition();
        return true;
    }

    /// <summary>Handles the scrolling keys when page by page.</summary>
    /// <param name="e">The key event.</param>
    /// <returns><see langword="true"/> when handled.</returns>
    private bool HandlePageByPageKey(KeyEventArgs e)
    {
        if (!_layout.Options.PageByPage || _scroller is null || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
        {
            return false;
        }

        var page = _scroller.Viewport.Height;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        return e.Key switch
        {
            Key.Down => StepPageByPage(LineStep),
            Key.Up => StepPageByPage(-LineStep),
            Key.PageDown or Key.Right => StepPageByPage(page),
            Key.PageUp or Key.Left => StepPageByPage(-page),
            Key.Space => StepPageByPage(shift ? -page : page),
            _ => false,
        };
    }
}
