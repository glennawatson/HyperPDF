// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Selecting, moving, resizing and nudging annotations with the Select tool, for any toolkit's canvas.</summary>
public static class CanvasEditMixins
{
    /// <summary>How far outside a handle a press still grabs it.</summary>
    private const double HandleReach = 4;

    /// <summary>Half, for the reach of a handle from its centre.</summary>
    private const double Half = 0.5;

    /// <summary>Twice, for growing a rectangle by its inset on both sides.</summary>
    private const double Both = 2;

    /// <summary>Adds the annotation editing helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Gets the outline of an annotation on the canvas, a little outside its bounds.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page.</param>
        /// <param name="bounds">The bounds in page space.</param>
        /// <returns>The outline, or an empty rectangle when the page is not laid out.</returns>
        public LayoutRect GetOutline(DocumentTabViewModel tab, int page, PageRect bounds)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if ((uint)page >= (uint)state.Sizes.Length || page >= state.Layout.PageCount)
            {
                return default;
            }

            var area = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale).MapToCanvas(bounds);
            return new(area.X - CanvasMetrics.OutlineInset, area.Y - CanvasMetrics.OutlineInset, area.Width + (CanvasMetrics.OutlineInset * Both), area.Height + (CanvasMetrics.OutlineInset * Both));
        }

        /// <summary>Finds whether a press is on one of the picked annotation's corner handles.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="picked">The picked annotation.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="anchor">The opposite corner in page space, which stays put while resizing.</param>
        /// <returns><see langword="true"/> when a handle was grabbed.</returns>
        public bool TryGrabHandle(DocumentTabViewModel tab, PageAnnotation picked, CanvasPoint position, out PagePoint anchor)
        {
            ArgumentNullException.ThrowIfNull(tab);
            ArgumentNullException.ThrowIfNull(picked);
            var transform = new PageTransform(state.Layout.GetPageBounds(picked.PageIndex), state.Sizes[picked.PageIndex], tab.Rotation, state.Layout.Options.Scale);
            if (ResizeHandles.TryGrab(state.GetOutline(tab, picked.PageIndex, picked.Bounds), position.X, position.Y, (CanvasMetrics.HandleSize * Half) + HandleReach, out var opposite))
            {
                anchor = transform.MapToPage(opposite.X, opposite.Y);
                return true;
            }

            anchor = default;
            return false;
        }

        /// <summary>Starts a resize when a press is on a corner handle of the picked annotation.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page the press landed on.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="session">The resize drag.</param>
        /// <returns><see langword="true"/> when a handle was grabbed.</returns>
        public bool TryStartResize(DocumentTabViewModel tab, int page, CanvasPoint position, out AnnotationEditSession session)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (tab.Annotations.Selected is { } picked && picked.PageIndex == page && AnnotationRules.CanResize(picked) && state.TryGrabHandle(tab, picked, position, out var anchor))
            {
                session = new(picked, anchor, position, picked.Bounds, true);
                return true;
            }

            session = default;
            return false;
        }

        /// <summary>Selects the annotation under a press (closing the bubble on blank page) and starts a move drag when it is movable.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page the press landed on.</param>
        /// <param name="pagePoint">The press in page space.</param>
        /// <param name="position">The press in canvas coordinates.</param>
        /// <returns>The move drag the press starts, and whether the press was used.</returns>
        public AnnotationPressOutcome SelectAndTryStartMove(DocumentTabViewModel tab, int page, PagePoint pagePoint, CanvasPoint position)
        {
            ArgumentNullException.ThrowIfNull(tab);
            var annotations = tab.Annotations;
            var hit = annotations.HitTest(page, pagePoint);
            annotations.Select(hit);
            if (hit is null)
            {
                annotations.CloseBubble();
            }

            state.Host.InvalidateVisual();
            if (hit is null)
            {
                return default;
            }

            var overText = state.TryHitTestCharacter(position, out _, out _);

            // An outline drawn around text leaves the text selectable; its handles and the arrow keys still move it.
            return !hit.IsMovable || (AnnotationRules.IsOutline(hit.Kind) && overText)
                ? new(null, !overText)
                : new(new AnnotationEditSession(hit, pagePoint, position, hit.Bounds, false), false);
        }

        /// <summary>Moves the preview of the active edit to where the pointer is.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="pointer">The pointer in canvas coordinates.</param>
        /// <returns><see langword="true"/> when an edit is active and its bounds changed.</returns>
        public bool MoveEdit(DocumentTabViewModel tab, CanvasPoint pointer)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (state.Tools.ActiveEdit is not { } edit)
            {
                return false;
            }

            var point = state.ToPage(tab, edit.Annotation.PageIndex, pointer);
            var nextBounds = AnnotationEdit.GetNextBounds(edit.Annotation.Bounds, edit.Resizing, edit.Anchor, point);
            state.Tools.ActiveEdit = edit with { Bounds = nextBounds };
            return nextBounds != edit.Bounds;
        }

        /// <summary>Finishes the active edit: commits the moved or resized bounds, or opens the comment bubble on a click.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="release">Where the pointer was released, in canvas coordinates.</param>
        /// <returns><see langword="true"/> when an edit was active.</returns>
        public bool EndEdit(DocumentTabViewModel tab, CanvasPoint release)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (state.Tools.ActiveEdit is not { } edit)
            {
                return false;
            }

            state.Tools.ActiveEdit = null;
            if (!CanvasMetrics.IsClick(edit.PressPosition, release))
            {
                _ = tab.Annotations.Move(edit.Annotation, edit.Bounds, false);
            }
            else if (!edit.Resizing && (edit.Annotation.Kind == AnnotationKind.Note || edit.Annotation.Contents.Length > 0))
            {
                _ = tab.Annotations.OpenBubble(edit.Annotation);
            }

            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Nudges the picked annotation with the arrow keys, in the direction they point on screen.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when the annotation moved.</returns>
        public bool NudgeSelected(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.Tab is not { Annotations.Selected: { IsMovable: true } picked } tab || (uint)picked.PageIndex >= (uint)state.Sizes.Length)
            {
                return false;
            }

            var step = ScreenStep.FromArrow(key, modifiers);
            if (step.IsEmpty)
            {
                return false;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(picked.PageIndex), state.Sizes[picked.PageIndex], tab.Rotation, state.Layout.Options.Scale);
            var origin = transform.MapToCanvas(new PagePoint(picked.Bounds.Left, picked.Bounds.Top));
            var start = transform.MapToPage(origin.X, origin.Y);
            var end = transform.MapToPage(origin.X + (step.X * transform.Scale), origin.Y + (step.Y * transform.Scale));
            var bounds = picked.Bounds with { Left = picked.Bounds.Left + (end.X - start.X), Top = picked.Bounds.Top + (end.Y - start.Y) };
            return tab.Annotations.Move(picked, bounds, true);
        }
    }
}
