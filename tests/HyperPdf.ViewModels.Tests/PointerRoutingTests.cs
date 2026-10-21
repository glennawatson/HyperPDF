// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks what pointer presses and hovers mean in the document view, with no toolkit to press them.</summary>
public sealed class PointerRoutingTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The page the presses land on.</summary>
    private const int OnPage = 0;

    /// <summary>The page number that means no page.</summary>
    private const int NoPage = -1;

    /// <summary>Only a left press that is not also a right press is plain.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="expected">Whether the press is plain left.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ViewerButtons.Left, true)]
    [Arguments(ViewerButtons.Left | ViewerButtons.Middle, true)]
    [Arguments(ViewerButtons.Left | ViewerButtons.Right, false)]
    [Arguments(ViewerButtons.Right, false)]
    [Arguments(ViewerButtons.None, false)]
    public async Task OnlyPlainLeftIsPlain(ViewerButtons buttons, bool expected) =>
        await Assert.That(PointerRouting.IsPlainLeft(buttons)).IsEqualTo(expected);

    /// <summary>The right button opens the context menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightButtonOpensTheMenu()
    {
        await Assert.That(PointerRouting.IsContextPress(ViewerButtons.Right)).IsTrue();
        await Assert.That(PointerRouting.IsContextPress(ViewerButtons.Left)).IsFalse();
    }

    /// <summary>Any press stops auto-scrolling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnyPressStopsAutoScroll()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-auto.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.PageTools.IsAutoScrolling = true;

        var kind = PointerRouting.GetPageToolPress(tab, ViewerButtons.Right, true, false);

        await Assert.That(kind).IsEqualTo(PageToolPressKind.StopAutoScroll);
    }

    /// <summary>The middle button, the space bar or the hand tool pan, but only when the view can scroll.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PanNeedsAScrollerAndAPanningGesture()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-pan.pdf", Pages)]);
        var tab = main.SelectedTab!;

        var middle = PointerRouting.GetPageToolPress(tab, ViewerButtons.Middle, true, false);
        var space = PointerRouting.GetPageToolPress(tab, ViewerButtons.Left, true, true);
        var noScroller = PointerRouting.GetPageToolPress(tab, ViewerButtons.Middle, false, false);
        tab.PageTools.PageTool = PageTool.Hand;
        var hand = PointerRouting.GetPageToolPress(tab, ViewerButtons.Left, true, false);

        await Assert.That(middle).IsEqualTo(PageToolPressKind.Pan);
        await Assert.That(space).IsEqualTo(PageToolPressKind.Pan);
        await Assert.That(noScroller).IsEqualTo(PageToolPressKind.None);
        await Assert.That(hand).IsEqualTo(PageToolPressKind.Pan);
    }

    /// <summary>The area tools choose an area, measuring adds a point, and a plain press only focuses.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AreaMeasureAndFocusPresses()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-area.pdf", Pages)]);
        var tab = main.SelectedTab!;

        var focus = PointerRouting.GetPageToolPress(tab, ViewerButtons.Left, true, false);
        var right = PointerRouting.GetPageToolPress(tab, ViewerButtons.Right, true, false);
        tab.Measure.IsOn = true;
        var measure = PointerRouting.GetPageToolPress(tab, ViewerButtons.Left, true, false);
        tab.Measure.IsOn = false;
        tab.PageTools.PageTool = PageTool.ZoomArea;
        var area = PointerRouting.GetPageToolPress(tab, ViewerButtons.Left, true, false);

        await Assert.That(focus).IsEqualTo(PageToolPressKind.Focus);
        await Assert.That(right).IsEqualTo(PageToolPressKind.None);
        await Assert.That(measure).IsEqualTo(PageToolPressKind.Measure);
        await Assert.That(area).IsEqualTo(PageToolPressKind.Area);
    }

    /// <summary>Each annotation tool sends a left press to the right kind of drawing.</summary>
    /// <param name="tool">The tool.</param>
    /// <param name="page">The page pressed.</param>
    /// <param name="expected">What the press does.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(AnnotationTool.Draw, OnPage, DrawingPressKind.Ink)]
    [Arguments(AnnotationTool.FreehandHighlight, OnPage, DrawingPressKind.Ink)]
    [Arguments(AnnotationTool.Rectangle, OnPage, DrawingPressKind.Shape)]
    [Arguments(AnnotationTool.Rectangle, NoPage, DrawingPressKind.None)]
    [Arguments(AnnotationTool.Polygon, OnPage, DrawingPressKind.Polygon)]
    [Arguments(AnnotationTool.Polygon, NoPage, DrawingPressKind.Polygon)]
    [Arguments(AnnotationTool.Note, OnPage, DrawingPressKind.ClickTool)]
    [Arguments(AnnotationTool.Stamp, OnPage, DrawingPressKind.ClickTool)]
    [Arguments(AnnotationTool.Highlight, OnPage, DrawingPressKind.None)]
    [Arguments(AnnotationTool.Select, OnPage, DrawingPressKind.None)]
    public async Task ToolsRoutePresses(AnnotationTool tool, int page, DrawingPressKind expected)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-tools.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = tool;

        var kind = PointerRouting.GetDrawingPress(tab, ViewerButtons.Left, page, default);

        await Assert.That(kind).IsEqualTo(expected);
    }

    /// <summary>A press with the right button, or none, is never a drawing press.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyLeftPressesDraw()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-right.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Draw;

        await Assert.That(PointerRouting.GetDrawingPress(tab, ViewerButtons.Right, OnPage, default)).IsEqualTo(DrawingPressKind.None);
        await Assert.That(PointerRouting.GetDrawingPress(tab, ViewerButtons.None, OnPage, default)).IsEqualTo(DrawingPressKind.None);
    }

    /// <summary>The Select tool owns plain left presses unless measuring or placing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectToolOwnsPlainPresses()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("route-select.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Select;

        var plain = PointerRouting.IsSelectToolPress(tab, ViewerButtons.Left);
        var right = PointerRouting.IsSelectToolPress(tab, ViewerButtons.Right);
        tab.Measure.IsOn = true;
        var measuring = PointerRouting.IsSelectToolPress(tab, ViewerButtons.Left);

        await Assert.That(plain).IsTrue();
        await Assert.That(right).IsFalse();
        await Assert.That(measuring).IsFalse();
    }

    /// <summary>The tool's pointer is a hand while space is held or the hand tool is chosen, and a cross for area tools.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToolCursorFollowsTheTool()
    {
        await Assert.That(PointerRouting.GetToolCursor(true, PageTool.SelectText)).IsEqualTo(ViewerCursor.Hand);
        await Assert.That(PointerRouting.GetToolCursor(false, PageTool.Hand)).IsEqualTo(ViewerCursor.Hand);
        await Assert.That(PointerRouting.GetToolCursor(false, PageTool.ZoomArea)).IsEqualTo(ViewerCursor.Cross);
        await Assert.That(PointerRouting.GetToolCursor(false, PageTool.Snapshot)).IsEqualTo(ViewerCursor.Cross);
        await Assert.That(PointerRouting.GetToolCursor(false, PageTool.SelectText)).IsEqualTo(ViewerCursor.Inherit);
        await Assert.That(PointerRouting.GetToolCursor(false, null)).IsEqualTo(ViewerCursor.Inherit);
    }

    /// <summary>Hovering shows a hand over something clickable, the text beam over text, and the arrow otherwise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoverCursorFollowsWhatIsUnder()
    {
        await Assert.That(PointerRouting.GetHoverCursor(true, true)).IsEqualTo(ViewerCursor.Hand);
        await Assert.That(PointerRouting.GetHoverCursor(true, false)).IsEqualTo(ViewerCursor.Text);
        await Assert.That(PointerRouting.GetHoverCursor(false, false)).IsEqualTo(ViewerCursor.Arrow);
    }

    /// <summary>A missing tab is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingTabIsRefused() =>
        await Assert.That(static () => PointerRouting.GetPageToolPress(null!, ViewerButtons.Left, true, false)).Throws<ArgumentNullException>();
}
