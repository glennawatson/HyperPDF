// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Controls;

/// <summary>
/// Moving the pages by hand: with the Hand tool, a left drag pushes the pages like paper on a desk. Whatever tool is
/// chosen, holding Space or the middle mouse button while dragging does the same for that one drag.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The pointer while the pages are being moved.</summary>
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

    /// <summary>Whether a drag is moving the pages.</summary>
    private bool _panning;

    /// <summary>Whether Space is held, so a left drag moves the pages.</summary>
    private bool _spaceHeld;

    /// <summary>Whether the pages were moved while Space was held, so letting go of Space does not also turn the page.</summary>
    private bool _spacePanned;

    /// <summary>Where the drag started, in the scroll viewer's coordinates, which stay still while the pages move.</summary>
    private Point _panStart;

    /// <summary>The scroll offset when the drag started.</summary>
    private Vector _panOffset;

    /// <summary>Starts moving the pages for a middle button press, a press while Space is held, or any press with the Hand tool.</summary>
    /// <param name="e">The press.</param>
    /// <param name="point">The pointer state.</param>
    /// <returns><see langword="true"/> when the press moves the pages.</returns>
    private bool BeginPan(PointerPressedEventArgs e, PointerPoint point)
    {
        var left = point.Properties.IsLeftButtonPressed;
        if (_scroller is null || Tab is not { } tab || !(point.Properties.IsMiddleButtonPressed || (left && (_spaceHeld || tab.PageTool == PageTool.Hand))))
        {
            return false;
        }

        _ = Focus();
        _pressPoint = point.Position;

        // A click with the Hand tool still follows a link, as in other readers.
        _pressedLink = left && !_spaceHeld ? HitTestLink(point.Position, out _) : null;
        _panning = true;
        _spacePanned |= _spaceHeld;
        _panStart = e.GetPosition(_scroller);
        _panOffset = _scroller.Offset;
        Cursor = MoveCursor;
        e.Pointer.Capture(this);
        e.Handled = true;
        return true;
    }

    /// <summary>Moves the pages with the pointer.</summary>
    /// <param name="e">The pointer movement.</param>
    /// <returns><see langword="true"/> while the pages are being moved.</returns>
    private bool ContinuePan(PointerEventArgs e)
    {
        if (!_panning || _scroller is null)
        {
            return false;
        }

        var moved = e.GetPosition(_scroller) - _panStart;
        var y = _panOffset.Y - moved.Y;
        if (_layout.Options.PageByPage && _layout.PageCount > 0)
        {
            // Page by page, a drag moves within the page shown and never shows half of the next one.
            y = ClampToSlot(_layout.GetPageNearest(_panOffset.Y + EdgeTolerance), y);
        }

        _scroller.Offset = new(_panOffset.X - moved.X, y);
        return true;
    }

    /// <summary>Ends moving the pages, following a link when the Hand tool clicked one without moving.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a drag was moving the pages.</returns>
    private bool EndPan(Point position)
    {
        if (!_panning)
        {
            return false;
        }

        _panning = false;
        Cursor = ToolCursor();
        ReportPosition();
        var link = _pressedLink;
        _pressedLink = null;
        var travel = position - _pressPoint;
        if (link is { } pressed && Math.Abs(travel.X) < DragThreshold && Math.Abs(travel.Y) < DragThreshold)
        {
            Tab?.Navigate(pressed.Target);
        }

        return true;
    }

    /// <summary>Notes that Space is held, so a drag moves the pages.</summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was Space.</returns>
    private bool PressSpace(KeyEventArgs e)
    {
        if (e.Key != Key.Space || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
        {
            return false;
        }

        // Held keys repeat; only the first press starts a hold.
        if (!_spaceHeld)
        {
            _spaceHeld = true;
            _spacePanned = false;
            Cursor = HandCursor;
        }

        return true;
    }

    /// <summary>Handles letting go of Space: page by page turns the page unless the pages were moved while it was held.</summary>
    /// <param name="e">The key release.</param>
    private void HandleKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key != Key.Space || !_spaceHeld)
        {
            return;
        }

        _spaceHeld = false;
        if (!_spacePanned && !_panning)
        {
            _ = HandlePageByPageKey(e);
        }

        if (!_panning)
        {
            Cursor = ToolCursor();
        }

        e.Handled = true;
    }

    /// <summary>Forgets a held Space when the pages lose the keyboard, as its release will not arrive.</summary>
    private void ReleaseSpace()
    {
        _spaceHeld = false;
        _spacePanned = false;
    }

    /// <summary>Gets the pointer for the chosen tool when it is not over anything special.</summary>
    /// <returns>The cursor, or <see langword="null"/> for select text, whose pointer follows what is under it.</returns>
    private Cursor? ToolCursor() =>
        _spaceHeld ? HandCursor : Tab?.PageTool switch
        {
            PageTool.Hand => HandCursor,
            PageTool.ZoomArea or PageTool.Snapshot => CrossCursor,
            _ => null,
        };

    /// <summary>Moves the pages or the area with the pointer, or shows the chosen tool's pointer while hovering.</summary>
    /// <param name="e">The pointer movement.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when the movement was used.</returns>
    private bool ContinuePageTool(PointerEventArgs e, Point position)
    {
        if (ContinuePan(e) || ContinueArea(position))
        {
            return true;
        }

        // Only a hover shows the tool's pointer; a drag that is selecting, drawing or measuring keeps its own.
        if (_selecting || Tab is null or { Measure.IsOn: true } || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || ToolCursor() is not { } cursor)
        {
            return false;
        }

        Cursor = cursor;
        return true;
    }
}
