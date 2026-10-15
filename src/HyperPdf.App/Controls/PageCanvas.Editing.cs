// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>
/// Moving and resizing a picked annotation with the select tool: dragging the annotation moves it, dragging one of the
/// square handles at its corners resizes it, and the arrow keys nudge it (Shift for small steps) in the direction they
/// point on screen. The new place shows as an outline while dragging and is kept on release, as one undoable change.
/// Text markup follows its text and cannot be moved.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The side of a resize handle, in device independent pixels.</summary>
    private const double HandleSize = 8;

    /// <summary>How far outside a handle a press still grabs it.</summary>
    private const double HandleReach = 4;

    /// <summary>The annotation being moved or resized, or <see langword="null"/>.</summary>
    private PageAnnotation? _editing;

    /// <summary>Whether a corner is being dragged (resizing) rather than the whole annotation (moving).</summary>
    private bool _resizing;

    /// <summary>Where the drag started, in page space; for a resize, the corner opposite the dragged one.</summary>
    private PagePoint _editAnchor;

    /// <summary>Where the drag started, in canvas space, to tell a drag from a click.</summary>
    private Point _editPress;

    /// <summary>The bounds shown while dragging, in page space.</summary>
    private PageRect _editBounds;

    /// <summary>Converts a page point to a point on the canvas, as the page is shown now.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The canvas point, or the origin when the page is not laid out.</returns>
    internal Point PageToCanvas(int page, PagePoint point) =>
        Tab is { } tab && (uint)page < (uint)_sizes.Length
            ? new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToCanvas(point)
            : default;

    /// <summary>Gets the four corners of a canvas rectangle, clockwise from the top-left.</summary>
    /// <param name="area">The rectangle.</param>
    /// <returns>The corners.</returns>
    private static (Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft) Corners(in Rect area) =>
        (area.TopLeft, area.TopRight, area.BottomRight, area.BottomLeft);

    /// <summary>Determines whether an annotation shows resize handles: movable kinds other than notes, which keep their icon size.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it can be resized.</returns>
    private static bool CanResize(PageAnnotation annotation) => annotation.IsMovable && annotation.Kind != AnnotationKind.Note;

    /// <summary>Determines whether a kind is drawn as lines only, so the page shows through it.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for drawings, lines and unfilled shapes.</returns>
    private static bool IsOutline(AnnotationKind kind) =>
        kind is AnnotationKind.Ink or AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Arrow or AnnotationKind.Line
            or AnnotationKind.Polygon or AnnotationKind.Cloud or AnnotationKind.PolyLine;

    /// <summary>Starts moving or resizing the picked annotation when the press is on it or on one of its handles.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page pressed.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when a drag started or a comment bubble opened.</returns>
    private bool BeginEdit(DocumentTabViewModel tab, int page, Point position, PointerPressedEventArgs e)
    {
        var annotations = tab.Annotations;
        if (annotations.Selected is { } picked && picked.PageIndex == page && CanResize(picked) && TryGrabHandle(tab, picked, position, out var anchor))
        {
            StartEdit(picked, anchor, position, true, e);
            return true;
        }

        var pagePoint = ToPage(tab, page, position);
        if (TryOpenClickedBubble(annotations, page, pagePoint, e.ClickCount, out var hit))
        {
            return true;
        }

        InvalidateVisual();

        // An outline drawn around text leaves the text selectable; its handles and the arrow keys still move it.
        if (hit is not { IsMovable: true } || (IsOutline(hit.Kind) && TryHitTestCharacter(position, out _, out _)))
        {
            return false;
        }

        StartEdit(hit, pagePoint, position, false, e);
        return true;
    }

    /// <summary>Opens the on-page comment bubble when a comment badge is clicked or an annotation is double-clicked, or closes it on blank page.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="page">The page index.</param>
    /// <param name="pagePoint">The point in page space.</param>
    /// <param name="clickCount">The pointer click count.</param>
    /// <param name="hit">Receives the annotation under the pointer, if any.</param>
    /// <returns><see langword="true"/> when a comment bubble was opened.</returns>
    private bool TryOpenClickedBubble(AnnotationsViewModel annotations, int page, PagePoint pagePoint, int clickCount, out PageAnnotation? hit)
    {
        if (annotations.HitTestCommentBadge(page, pagePoint) is { } badgeHit)
        {
            hit = badgeHit;
            _ = annotations.OpenBubble(badgeHit);
            InvalidateVisual();
            return true;
        }

        hit = annotations.HitTest(page, pagePoint);
        annotations.Select(hit);
        if (hit is null)
        {
            annotations.CloseBubble();
            return false;
        }

        if (clickCount >= DoubleClick && AnnotationsViewModel.CanShowBubble(hit))
        {
            _ = annotations.OpenBubble(hit);
            InvalidateVisual();
            return true;
        }

        return false;
    }

    /// <summary>Determines whether the pointer is over a sticky note or a comment indicator badge, which opens a bubble when clicked.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when over a note or comment badge.</returns>
    private bool IsOverCommentTrigger(Point position)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        var page = _layout.HitTest(position.X, position.Y);
        if (page < 0)
        {
            return false;
        }

        var point = ToPage(tab, page, position);
        return tab.Annotations.HitTestCommentBadge(page, point) is not null
            || tab.Annotations.HitTest(page, point) is { Kind: AnnotationKind.Note };
    }

    /// <summary>Finds whether a press is on one of the picked annotation's corner handles.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="picked">The picked annotation.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="anchor">The opposite corner in page space, which stays put while resizing.</param>
    /// <returns><see langword="true"/> when a handle was grabbed.</returns>
    private bool TryGrabHandle(DocumentTabViewModel tab, PageAnnotation picked, Point position, out PagePoint anchor)
    {
        var transform = new PageTransform(_layout.GetPageBounds(picked.PageIndex), _sizes[picked.PageIndex], tab.Rotation, _layout.Options.Scale);
        var (topLeft, topRight, bottomRight, bottomLeft) = Corners(transform.ToCanvas(picked.Bounds).Inflate(OutlineInset));
        ReadOnlySpan<(Point Handle, Point Opposite)> corners = [(topLeft, bottomRight), (topRight, bottomLeft), (bottomRight, topLeft), (bottomLeft, topRight)];
        foreach (var (handle, opposite) in corners)
        {
            if (Math.Abs(position.X - handle.X) > (HandleSize * Half) + HandleReach || Math.Abs(position.Y - handle.Y) > (HandleSize * Half) + HandleReach)
            {
                continue;
            }

            anchor = transform.ToPage(opposite);
            return true;
        }

        anchor = default;
        return false;
    }

    /// <summary>Remembers the drag and captures the pointer.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="anchor">The page point the drag is measured from.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="resizing">Whether a corner is dragged.</param>
    /// <param name="e">The event.</param>
    private void StartEdit(PageAnnotation annotation, PagePoint anchor, Point position, bool resizing, PointerPressedEventArgs e)
    {
        _editing = annotation;
        _resizing = resizing;
        _editAnchor = anchor;
        _editPress = position;
        _editBounds = annotation.Bounds;
        e.Pointer.Capture(this);
    }

    /// <summary>Follows the pointer with the outline of the moved or resized annotation.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> while dragging.</returns>
    private bool ContinueEdit(Point position)
    {
        if (_editing is not { } editing || Tab is not { } tab)
        {
            return false;
        }

        var point = ToPage(tab, editing.PageIndex, position);
        var previous = _editBounds;
        if (_resizing)
        {
            _editBounds = PageRect.FromEdges(
                Math.Min(_editAnchor.X, point.X),
                Math.Min(_editAnchor.Y, point.Y),
                Math.Max(_editAnchor.X, point.X),
                Math.Max(_editAnchor.Y, point.Y));
        }
        else
        {
            var bounds = editing.Bounds;
            _editBounds = bounds with { Left = bounds.Left + (point.X - _editAnchor.X), Top = bounds.Top + (point.Y - _editAnchor.Y) };
        }

        // A move inside the same page point leaves the outline where it is, so there is nothing to redraw.
        if (_editBounds != previous)
        {
            InvalidateVisual();
        }

        return true;
    }

    /// <summary>Keeps the moved or resized annotation on release; a click without a drag opens the bubble for a note or commented annotation.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a drag was in progress.</returns>
    private bool EndEdit(DocumentTabViewModel tab, Point position)
    {
        if (_editing is not { } editing)
        {
            return false;
        }

        _editing = null;
        var travel = position - _editPress;
        if (Math.Abs(travel.X) >= DragThreshold || Math.Abs(travel.Y) >= DragThreshold)
        {
            _ = tab.Annotations.Move(editing, _editBounds, false);
        }
        else if (!_resizing && (editing.Kind == AnnotationKind.Note || editing.Contents.Length > 0))
        {
            _ = tab.Annotations.OpenBubble(editing);
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Nudges the picked annotation with the arrow keys, in the direction they point on screen.</summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the annotation moved.</returns>
    private bool NudgeSelected(KeyEventArgs e)
    {
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != 0 || Tab is not { Annotations.Selected: { IsMovable: true } picked } tab || (uint)picked.PageIndex >= (uint)_sizes.Length)
        {
            return false;
        }

        var step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? SignatureMarkLayout.FineMoveStep : SignatureMarkLayout.MoveStep;
        var (dx, dy) = e.Key switch
        {
            Key.Left => (-step, 0F),
            Key.Right => (step, 0F),
            Key.Up => (0F, -step),
            Key.Down => (0F, step),
            _ => (0F, 0F),
        };
        if (dx == 0 && dy == 0)
        {
            return false;
        }

        var transform = new PageTransform(_layout.GetPageBounds(picked.PageIndex), _sizes[picked.PageIndex], tab.Rotation, _layout.Options.Scale);
        var origin = transform.ToCanvas(new PagePoint(picked.Bounds.Left, picked.Bounds.Top));
        var start = transform.ToPage(origin);
        var end = transform.ToPage(origin + new Vector(dx * transform.Scale, dy * transform.Scale));
        var bounds = picked.Bounds with { Left = picked.Bounds.Left + (end.X - start.X), Top = picked.Bounds.Top + (end.Y - start.Y) };
        return tab.Annotations.Move(picked, bounds, true);
    }

    /// <summary>Draws the outline of an annotation being dragged, and the resize handles of the picked one.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawEditing(DrawingContext context, DocumentTabViewModel tab)
    {
        var pen = _currentHitPen ?? StrokePen;
        if (_editing is { } editing && (uint)editing.PageIndex < (uint)_sizes.Length)
        {
            var transform = new PageTransform(_layout.GetPageBounds(editing.PageIndex), _sizes[editing.PageIndex], tab.Rotation, _layout.Options.Scale);
            context.DrawRectangle(null, pen, transform.ToCanvas(_editBounds).Inflate(OutlineInset));
            return;
        }

        if (tab.Annotations.Selected is not { } picked || !CanResize(picked) || (uint)picked.PageIndex >= (uint)_sizes.Length)
        {
            return;
        }

        var pickedTransform = new PageTransform(_layout.GetPageBounds(picked.PageIndex), _sizes[picked.PageIndex], tab.Rotation, _layout.Options.Scale);
        var (topLeft, topRight, bottomRight, bottomLeft) = Corners(pickedTransform.ToCanvas(picked.Bounds).Inflate(OutlineInset));
        foreach (var corner in (ReadOnlySpan<Point>)[topLeft, topRight, bottomRight, bottomLeft])
        {
            context.DrawRectangle(pen.Brush, null, new(corner.X - (HandleSize * Half), corner.Y - (HandleSize * Half), HandleSize, HandleSize));
        }
    }
}
