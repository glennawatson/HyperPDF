// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Decides what each pointer press and hover means in the document view, which every render head follows alike.</summary>
public static class PointerRouting
{
    /// <summary>Determines whether a press is the plain primary button: the left button without the right.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <returns><see langword="true"/> for a left press that is not also a right press.</returns>
    public static bool IsPlainLeft(ViewerButtons buttons) =>
        (buttons & ViewerButtons.Left) != 0 && (buttons & ViewerButtons.Right) == 0;

    /// <summary>Determines whether a press opens the context menu.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <returns><see langword="true"/> when the right button is held.</returns>
    public static bool IsContextPress(ViewerButtons buttons) => (buttons & ViewerButtons.Right) != 0;

    /// <summary>Determines whether a press starts panning the pages.</summary>
    /// <param name="tab">The active tab.</param>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="hasScroller">Whether the view can scroll.</param>
    /// <param name="spaceHeld">Whether the space bar is held, which turns the pointer into a hand.</param>
    /// <returns><see langword="true"/> when the press pans.</returns>
    public static bool IsPanPress(DocumentTabViewModel tab, ViewerButtons buttons, bool hasScroller, bool spaceHeld)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return hasScroller
            && ((buttons & ViewerButtons.Middle) != 0
                || ((buttons & ViewerButtons.Left) != 0 && (spaceHeld || tab.PageTools.PageTool == PageTool.Hand)));
    }

    /// <summary>Works out what a press does when a page tool or measuring is active.</summary>
    /// <param name="tab">The active tab.</param>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="hasScroller">Whether the view can scroll.</param>
    /// <param name="spaceHeld">Whether the space bar is held.</param>
    /// <returns>What the press does, or <see cref="PageToolPressKind.None"/> when it is not for a page tool.</returns>
    public static PageToolPressKind GetPageToolPress(DocumentTabViewModel tab, ViewerButtons buttons, bool hasScroller, bool spaceHeld)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.PageTools.IsAutoScrolling)
        {
            return PageToolPressKind.StopAutoScroll;
        }

        if (IsPanPress(tab, buttons, hasScroller, spaceHeld))
        {
            return PageToolPressKind.Pan;
        }

        if (!IsPlainLeft(buttons))
        {
            return PageToolPressKind.None;
        }

        if (tab.PageTools.PageTool is PageTool.ZoomArea or PageTool.Snapshot)
        {
            return PageToolPressKind.Area;
        }

        return tab.Measure.IsOn ? PageToolPressKind.Measure : PageToolPressKind.Focus;
    }

    /// <summary>Works out what a left press does while a drawing tool, a mark being placed or a form is in play.</summary>
    /// <param name="tab">The active tab.</param>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="page">The page under the pointer, or -1.</param>
    /// <param name="pagePoint">The point under the pointer in page space, when <paramref name="page"/> is not -1.</param>
    /// <returns>What the press does, or <see cref="DrawingPressKind.None"/> when it belongs to selecting or another tool.</returns>
    public static DrawingPressKind GetDrawingPress(DocumentTabViewModel tab, ViewerButtons buttons, int page, PagePoint pagePoint)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!IsPlainLeft(buttons))
        {
            return DrawingPressKind.None;
        }

        if (tab.FillAndSign.Placement is not null)
        {
            return DrawingPressKind.Placement;
        }

        var tool = tab.Annotations.Tool;
        if (page >= 0 && AnnotationsViewModel.GetShapeKind(tool) is not null)
        {
            return DrawingPressKind.Shape;
        }

        if (AnnotationsViewModel.GetPolygonKind(tool) is not null)
        {
            return DrawingPressKind.Polygon;
        }

        return page >= 0 ? GetPageDrawingPress(tab, tool, page, pagePoint) : DrawingPressKind.None;
    }

    /// <summary>Determines whether a press belongs to the Select tool.</summary>
    /// <param name="tab">The active tab.</param>
    /// <param name="buttons">The buttons held.</param>
    /// <returns><see langword="true"/> when the Select tool is active, nothing is being measured or placed, and the press is a plain left press.</returns>
    public static bool IsSelectToolPress(DocumentTabViewModel tab, ViewerButtons buttons)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return tab.Annotations.Tool == AnnotationTool.Select
            && !tab.Measure.IsOn
            && tab.FillAndSign.Placement is null
            && IsPlainLeft(buttons);
    }

    /// <summary>Gets the pointer shape for the chosen page tool when it is not over anything special.</summary>
    /// <param name="spaceHeld">Whether the space bar is held.</param>
    /// <param name="tool">The chosen page tool, or <see langword="null"/> without a tab.</param>
    /// <returns>The shape, or <see cref="ViewerCursor.Inherit"/> for select text, whose pointer follows what is under it.</returns>
    public static ViewerCursor GetToolCursor(bool spaceHeld, PageTool? tool) =>
        spaceHeld
            ? ViewerCursor.Hand
            : tool switch
            {
                PageTool.Hand => ViewerCursor.Hand,
                PageTool.ZoomArea or PageTool.Snapshot => ViewerCursor.Cross,
                _ => ViewerCursor.Inherit,
            };

    /// <summary>Gets the pointer shape while hovering a page with no tool in the way.</summary>
    /// <param name="overText">Whether the pointer is over text.</param>
    /// <param name="overClickable">Whether the pointer is over a link, a fillable field or a comment badge.</param>
    /// <returns>The shape.</returns>
    public static ViewerCursor GetHoverCursor(bool overText, bool overClickable)
    {
        if (overClickable)
        {
            return ViewerCursor.Hand;
        }

        return overText ? ViewerCursor.Text : ViewerCursor.Arrow;
    }

    /// <summary>Works out what a left press on a laid-out page does for the ink, form and click tools.</summary>
    /// <param name="tab">The active tab.</param>
    /// <param name="tool">The annotation tool.</param>
    /// <param name="page">The page pressed.</param>
    /// <param name="pagePoint">The point pressed in page space.</param>
    /// <returns>What the press does.</returns>
    private static DrawingPressKind GetPageDrawingPress(DocumentTabViewModel tab, AnnotationTool tool, int page, PagePoint pagePoint) => tool switch
    {
        AnnotationTool.Draw or AnnotationTool.FreehandHighlight => DrawingPressKind.Ink,
        AnnotationTool.Text or AnnotationTool.Select when tab.Forms.HitTest(page, pagePoint) is not null => DrawingPressKind.Field,
        AnnotationTool.Note or AnnotationTool.Text or AnnotationTool.Stamp or AnnotationTool.Caret => DrawingPressKind.ClickTool,
        _ => DrawingPressKind.None,
    };
}
