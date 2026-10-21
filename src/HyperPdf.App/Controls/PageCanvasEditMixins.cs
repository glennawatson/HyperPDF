// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams and extension blocks for selecting, editing, moving, resizing and opening comment bubbles on
/// annotations with the Select tool on <see cref="PageCanvas"/>.
/// </summary>
internal static class PageCanvasEditMixins
{
    /// <summary>The side of a resize handle, in device independent pixels.</summary>
    private const double HandleSize = 8;

    /// <summary>How far outside a handle a press still grabs it.</summary>
    private const double HandleReach = 4;

    /// <summary>
    /// Adds the conditional select-tool streams for double-clicking text boxes, grabbing resize handles, clicking comment
    /// badges, double-clicking bubbles, and dragging movable annotations.
    /// </summary>
    /// <param name="presses">The unhandled select-tool presses on a page.</param>
    extension(IObservable<SelectToolPress> presses)
    {
        /// <summary>Gets the double-clicks on a free-text annotation that start inline text editing.</summary>
        internal IObservable<AnnotationHit> TextBoxDoubleClicks => presses
            .Where(static p => !p.Args.Handled && p.Args.ClickCount >= PageCanvasToolMixins.DoubleClick)
            .Pick(TryHitDoubleClickedTextBox);

        /// <summary>Gets the presses on a corner resize handle of the currently picked annotation.</summary>
        internal IObservable<AnnotationEditStart> ResizeHandleStarts => presses
            .Where(static p => !p.Args.Handled)
            .Pick(TryMatchResizeHandle);

        /// <summary>Gets the clicks on an on-page comment indicator badge that open the comment bubble.</summary>
        internal IObservable<AnnotationHit> CommentBadgeClicks => presses
            .Where(static p => !p.Args.Handled)
            .Pick(TryHitCommentBadge);

        /// <summary>Gets the double-clicks on an annotation that open its on-page comment bubble.</summary>
        internal IObservable<AnnotationHit> BubbleDoubleClicks => presses
            .Where(static p => !p.Args.Handled && p.Args.ClickCount >= PageCanvasToolMixins.DoubleClick)
            .Pick(TryHitDoubleClickedBubble);

        /// <summary>
        /// Gets the presses that pick an annotation (or clear the pick and close the bubble on blank page) and start a
        /// move drag when the annotation is movable and not an outline over selectable text.
        /// </summary>
        internal IObservable<AnnotationEditStart> AnnotationMoveStarts => presses
            .Where(static p => !p.Args.Handled)
            .Pick(SelectAndTryStartMove);
    }

    /// <summary>Adds the select-tool press, edit move and edit release streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the unhandled left pointer presses on a laid-out page while the Select annotation tool is active.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The select-tool presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<SelectToolPress> SelectToolPresses(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryCreateSelectToolPress);

        /// <summary>Gets the pointer movements that move or resize the active annotation preview.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The edit preview updates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<AnnotationEditMove> EditMoves(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerMoves
                .Inject(tabs)
                .Pick(canvas.TryCreateEditMove);

        /// <summary>Gets the pointer releases that finish moving or resizing an annotation, or open its comment bubble on a click.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The edit completion steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<AnnotationEditEnd> EditReleases(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerReleases
                .Inject(tabs)
                .Pick(canvas.TryCreateEditEnd);

        /// <summary>Determines whether the pointer is over a sticky note or a comment indicator badge, which opens a bubble when clicked.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over a note or comment badge.</returns>
        internal bool IsOverCommentTrigger(PointerHover hover) =>
            hover.IsOverPage && canvas.Tab is { } tab && tab.Annotations.IsOverCommentTrigger(hover.Page, hover.Point);

        /// <summary>Finds whether a press is on one of the picked annotation's corner handles.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="picked">The picked annotation.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="anchor">The opposite corner in page space, which stays put while resizing.</param>
        /// <returns><see langword="true"/> when a handle was grabbed.</returns>
        internal bool TryGrabHandle(DocumentTabViewModel tab, PageAnnotation picked, Point position, out PagePoint anchor)
        {
            var transform = new PageTransform(canvas.Layout.GetPageBounds(picked.PageIndex), canvas.Sizes[picked.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
            var (topLeft, topRight, bottomRight, bottomLeft) = Corners(transform.ToCanvas(picked.Bounds).Inflate(PageCanvasAnnotationMixins.OutlineInset));
            ReadOnlySpan<HandleCorner> corners = [new(topLeft, bottomRight), new(topRight, bottomLeft), new(bottomRight, topLeft), new(bottomLeft, topRight)];
            foreach (var (handle, opposite) in corners)
            {
                if (Math.Abs(position.X - handle.X) > (HandleSize * PageCanvas.Half) + HandleReach || Math.Abs(position.Y - handle.Y) > (HandleSize * PageCanvas.Half) + HandleReach)
                {
                    continue;
                }

                anchor = transform.ToPage(opposite);
                return true;
            }

            anchor = default;
            return false;
        }

        /// <summary>Nudges the picked annotation with the arrow keys, in the direction they point on screen.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the annotation moved.</returns>
        internal bool NudgeSelected(KeyEventArgs e)
        {
            if (canvas.Tab is not { Annotations.Selected: { IsMovable: true } picked } tab || (uint)picked.PageIndex >= (uint)canvas.Sizes.Length)
            {
                return false;
            }

            var (dx, dy) = ScreenStep.FromArrow(e.ViewerKey, e.ViewerModifiers);
            if (dx == 0 && dy == 0)
            {
                return false;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(picked.PageIndex), canvas.Sizes[picked.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
            var origin = transform.ToCanvas(new PagePoint(picked.Bounds.Left, picked.Bounds.Top));
            var start = transform.ToPage(origin);
            var end = transform.ToPage(origin + new Vector(dx * transform.Scale, dy * transform.Scale));
            var bounds = picked.Bounds with { Left = picked.Bounds.Left + (end.X - start.X), Top = picked.Bounds.Top + (end.Y - start.Y) };
            return tab.Annotations.Move(picked, bounds, true);
        }

        /// <summary>Draws the outline of an annotation being dragged, and the resize handles of the picked one.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        internal void DrawEditing(DrawingContext context, DocumentTabViewModel tab)
        {
            var pen = canvas.CurrentHitPen ?? PageCanvasAnnotationMixins.StrokePen;
            if (canvas.ActiveEdit is { } edit && (uint)edit.Annotation.PageIndex < (uint)canvas.Sizes.Length)
            {
                var transform = new PageTransform(canvas.Layout.GetPageBounds(edit.Annotation.PageIndex), canvas.Sizes[edit.Annotation.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
                context.DrawRectangle(null, pen, transform.ToCanvas(edit.Bounds).Inflate(PageCanvasAnnotationMixins.OutlineInset));
                return;
            }

            if (tab.Annotations.Selected is not { } picked || !AnnotationRules.CanResize(picked) || (uint)picked.PageIndex >= (uint)canvas.Sizes.Length)
            {
                return;
            }

            var pickedTransform = new PageTransform(canvas.Layout.GetPageBounds(picked.PageIndex), canvas.Sizes[picked.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
            var (topLeft, topRight, bottomRight, bottomLeft) = Corners(pickedTransform.ToCanvas(picked.Bounds).Inflate(PageCanvasAnnotationMixins.OutlineInset));
            foreach (var corner in (ReadOnlySpan<Point>)[topLeft, topRight, bottomRight, bottomLeft])
            {
                context.DrawRectangle(pen.Brush, null, new(corner.X - (HandleSize * PageCanvas.Half), corner.Y - (HandleSize * PageCanvas.Half), HandleSize, HandleSize));
            }
        }

        /// <summary>Creates a <see cref="SelectToolPress"/> when an unhandled left press lands on a page while the Select tool is active.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The select-tool press, or <see langword="null"/> when the press is outside a page or uses another tool.</returns>
        private SelectToolPress? TryCreateSelectToolPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            if (e.Handled || tab.Annotations.Tool != AnnotationTool.Select || tab.Measure.IsOn || tab.FillAndSign.Placement is not null)
            {
                return null;
            }

            var point = e.GetCurrentPoint(canvas);
            if (!point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
            {
                return null;
            }

            var page = canvas.Layout.HitTest(point.Position.X, point.Position.Y);
            return page >= 0 ? new(canvas, tab, e, page, point.Position, canvas.ToPage(tab, page, point.Position)) : null;
        }

        /// <summary>Creates an <see cref="AnnotationEditMove"/> while an annotation is being moved or resized.</summary>
        /// <param name="item">The pointer move and tab.</param>
        /// <returns>The edit move step, or <see langword="null"/> when no edit drag is active.</returns>
        private AnnotationEditMove? TryCreateEditMove(Injected<PointerEventArgs, DocumentTabViewModel> item)
        {
            if (canvas.ActiveEdit is not { } edit)
            {
                return null;
            }

            var (e, tab) = item;
            var point = canvas.ToPage(tab, edit.Annotation.PageIndex, e.GetPosition(canvas));
            PageRect nextBounds;
            if (edit.Resizing)
            {
                nextBounds = PageRect.FromEdges(
                    Math.Min(edit.Anchor.X, point.X),
                    Math.Min(edit.Anchor.Y, point.Y),
                    Math.Max(edit.Anchor.X, point.X),
                    Math.Max(edit.Anchor.Y, point.Y));
            }
            else
            {
                var bounds = edit.Annotation.Bounds;
                nextBounds = bounds with { Left = bounds.Left + (point.X - edit.Anchor.X), Top = bounds.Top + (point.Y - edit.Anchor.Y) };
            }

            return new(canvas, edit with { Bounds = nextBounds }, nextBounds != edit.Bounds);
        }

        /// <summary>Creates an <see cref="AnnotationEditEnd"/> when the pointer is released during an annotation edit drag.</summary>
        /// <param name="item">The pointer release and tab.</param>
        /// <returns>The edit end step, or <see langword="null"/> when no edit drag is active.</returns>
        private AnnotationEditEnd? TryCreateEditEnd(Injected<PointerReleasedEventArgs, DocumentTabViewModel> item)
        {
            if (canvas.ActiveEdit is not { } edit)
            {
                return null;
            }

            var (e, tab) = item;
            return new(canvas, tab, e, edit, e.GetPosition(canvas));
        }
    }

    /// <summary>Gets the four corners of a canvas rectangle, clockwise from the top-left.</summary>
    /// <param name="area">The rectangle.</param>
    /// <returns>The corners.</returns>
    private static CanvasCorners Corners(in Rect area) =>
        new(area.TopLeft, area.TopRight, area.BottomRight, area.BottomLeft);

    /// <summary>Matches a double-click on a free-text annotation.</summary>
    /// <param name="press">The select-tool press.</param>
    /// <returns>The hit when a text box was double-clicked.</returns>
    private static AnnotationHit? TryHitDoubleClickedTextBox(SelectToolPress press) =>
        press.Tab.Annotations.HitTest(press.Page, press.PagePoint) is { Kind: AnnotationKind.TextBox } box
            ? new(press.Canvas, press.Args, press.Tab.Annotations, box)
            : null;

    /// <summary>Matches a press on a corner resize handle of the currently picked annotation.</summary>
    /// <param name="press">The select-tool press.</param>
    /// <returns>The resize edit start when a corner handle was grabbed.</returns>
    private static AnnotationEditStart? TryMatchResizeHandle(SelectToolPress press) =>
        press.Tab.Annotations.Selected is { } picked
        && picked.PageIndex == press.Page
        && AnnotationRules.CanResize(picked)
        && press.Canvas.TryGrabHandle(press.Tab, picked, press.Position, out var anchor)
            ? new(press.Canvas, press.Args, new(picked, anchor, press.Position, picked.Bounds, true))
            : null;

    /// <summary>Matches a click on an on-page comment indicator badge.</summary>
    /// <param name="press">The select-tool press.</param>
    /// <returns>The hit when a comment badge was clicked.</returns>
    private static AnnotationHit? TryHitCommentBadge(SelectToolPress press) =>
        press.Tab.Annotations.HitTestCommentBadge(press.Page, press.PagePoint) is { } badge
            ? new(press.Canvas, press.Args, press.Tab.Annotations, badge)
            : null;

    /// <summary>Matches a double-click on an annotation that supports an on-page comment bubble.</summary>
    /// <param name="press">The select-tool press.</param>
    /// <returns>The hit when a bubble-capable annotation was double-clicked.</returns>
    private static AnnotationHit? TryHitDoubleClickedBubble(SelectToolPress press) =>
        press.Tab.Annotations.HitTest(press.Page, press.PagePoint) is { } hit && AnnotationsViewModel.CanShowBubble(hit)
            ? new(press.Canvas, press.Args, press.Tab.Annotations, hit)
            : null;

    /// <summary>Selects the annotation under the press (closing the bubble on blank page) and starts a move drag when movable.</summary>
    /// <param name="press">The select-tool press.</param>
    /// <returns>The move edit start when a movable annotation was grabbed.</returns>
    private static AnnotationEditStart? SelectAndTryStartMove(SelectToolPress press)
    {
        var annotations = press.Tab.Annotations;
        var hit = annotations.HitTest(press.Page, press.PagePoint);
        annotations.Select(hit);
        if (hit is null)
        {
            annotations.CloseBubble();
        }

        press.Canvas.InvalidateVisual();
        if (hit is null)
        {
            return null;
        }

        var overText = press.Canvas.TryHitTestCharacter(press.Position, out _, out _);

        // An outline drawn around text leaves the text selectable; its handles and the arrow keys still move it.
        if (!hit.IsMovable || (AnnotationRules.IsOutline(hit.Kind) && overText))
        {
            if (!overText)
            {
                press.Args.Handled = true;
            }

            return null;
        }

        return new(press.Canvas, press.Args, new(hit, press.PagePoint, press.Position, hit.Bounds, false));
    }

    /// <summary>An unhandled left pointer press on a page while the Select tool is active.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Page">The page index.</param>
    /// <param name="Position">The point in canvas coordinates.</param>
    /// <param name="PagePoint">The point in page coordinates.</param>
    internal readonly record struct SelectToolPress(
        PageCanvas Canvas,
        DocumentTabViewModel Tab,
        PointerPressedEventArgs Args,
        int Page,
        Point Position,
        PagePoint PagePoint);

    /// <summary>An annotation hit by a click or double-click with the Select tool.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Annotations">The annotations view model.</param>
    /// <param name="Annotation">The hit annotation.</param>
    internal readonly record struct AnnotationHit(
        PageCanvas Canvas,
        PointerPressedEventArgs Args,
        AnnotationsViewModel Annotations,
        PageAnnotation Annotation)
    {
        /// <summary>Starts editing the text box on the page and marks the press handled when editing begins.</summary>
        internal void EditText()
        {
            if (Annotations.EditText(Annotation))
            {
                Args.Handled = true;
            }
        }

        /// <summary>Opens the on-page comment bubble for the hit annotation and marks the press handled.</summary>
        internal void OpenBubble()
        {
            _ = Annotations.OpenBubble(Annotation);
            Canvas.InvalidateVisual();
            Args.Handled = true;
        }

        /// <summary>Selects the hit annotation, opens its on-page comment bubble, and marks the press handled.</summary>
        internal void SelectAndOpenBubble()
        {
            Annotations.Select(Annotation);
            _ = Annotations.OpenBubble(Annotation);
            Canvas.InvalidateVisual();
            Args.Handled = true;
        }
    }

    /// <summary>The state of an active annotation move or resize drag on the canvas.</summary>
    /// <param name="Annotation">The annotation being moved or resized.</param>
    /// <param name="Anchor">Where the drag started in page space, or the opposite corner when resizing.</param>
    /// <param name="PressPosition">Where the drag started in canvas coordinates.</param>
    /// <param name="Bounds">The preview bounds in page space.</param>
    /// <param name="Resizing">Whether a corner resize handle is being dragged.</param>
    internal readonly record struct AnnotationEditSession(
        PageAnnotation Annotation,
        PagePoint Anchor,
        Point PressPosition,
        PageRect Bounds,
        bool Resizing);

    /// <summary>Starts an annotation move or resize drag.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Session">The initial edit session.</param>
    internal readonly record struct AnnotationEditStart(PageCanvas Canvas, PointerPressedEventArgs Args, AnnotationEditSession Session)
    {
        /// <summary>Records the active edit session, captures the pointer and marks the press handled.</summary>
        internal void Begin()
        {
            Canvas.ActiveEdit = Session;
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }
    }

    /// <summary>Updates the preview bounds of an active annotation move or resize drag.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Session">The updated edit session.</param>
    /// <param name="Changed">Whether the page bounds changed.</param>
    internal readonly record struct AnnotationEditMove(PageCanvas Canvas, AnnotationEditSession Session, bool Changed)
    {
        /// <summary>Applies the updated preview bounds and redraws when they changed.</summary>
        internal void UpdatePreview()
        {
            Canvas.ActiveEdit = Session;
            if (Changed)
            {
                Canvas.InvalidateVisual();
            }
        }
    }

    /// <summary>Finishes an active annotation move or resize drag on pointer release.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer release arguments.</param>
    /// <param name="Session">The finished edit session.</param>
    /// <param name="Position">The release position in canvas coordinates.</param>
    internal readonly record struct AnnotationEditEnd(
        PageCanvas Canvas,
        DocumentTabViewModel Tab,
        PointerReleasedEventArgs Args,
        AnnotationEditSession Session,
        Point Position)
    {
        /// <summary>Commits the moved or resized bounds, or opens the comment bubble when clicked without dragging.</summary>
        internal void CommitOrOpenBubble()
        {
            Canvas.ActiveEdit = null;
            Args.Pointer.Capture(null);
            Args.Handled = true;
            var travel = Position - Session.PressPosition;
            if (Math.Abs(travel.X) >= PageCanvas.DragThreshold || Math.Abs(travel.Y) >= PageCanvas.DragThreshold)
            {
                _ = Tab.Annotations.Move(Session.Annotation, Session.Bounds, false);
            }
            else if (!Session.Resizing && (Session.Annotation.Kind == AnnotationKind.Note || Session.Annotation.Contents.Length > 0))
            {
                _ = Tab.Annotations.OpenBubble(Session.Annotation);
            }

            Canvas.InvalidateVisual();
        }
    }

    /// <summary>The four corners of a canvas rectangle, clockwise from the top-left.</summary>
    /// <param name="TopLeft">The top-left corner.</param>
    /// <param name="TopRight">The top-right corner.</param>
    /// <param name="BottomRight">The bottom-right corner.</param>
    /// <param name="BottomLeft">The bottom-left corner.</param>
    private readonly record struct CanvasCorners(Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft);

    /// <summary>A resize handle corner and the opposite corner that stays anchored.</summary>
    /// <param name="Handle">The handle corner in canvas coordinates.</param>
    /// <param name="Opposite">The opposite corner in canvas coordinates.</param>
    private readonly record struct HandleCorner(Point Handle, Point Opposite);
}
