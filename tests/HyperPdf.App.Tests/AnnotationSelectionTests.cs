// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks which annotation a click on the page picks when annotations overlap.</summary>
public sealed class AnnotationSelectionTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Halves a size to find a middle.</summary>
    private const float Half = 0.5F;

    /// <summary>Where a large rectangle starts, in points.</summary>
    private static readonly PagePoint RectangleStart = new(100, 100);

    /// <summary>Where a large rectangle ends, in points.</summary>
    private static readonly PagePoint RectangleEnd = new(400, 500);

    /// <summary>Where a stamp goes inside the large rectangle.</summary>
    private static readonly PagePoint StampAt = new(200, 300);

    /// <summary>A point in the middle of the rectangle, away from every edge and from the stamp.</summary>
    private static readonly PagePoint EmptyInside = new(330, 150);

    /// <summary>A point on the rectangle's left edge.</summary>
    private static readonly PagePoint OnEdge = new(100, 300);

    /// <summary>A small annotation drawn under a later, larger outline is still the one picked where the two overlap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallAnnotationUnderLaterOutlineIsPicked()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("selection.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            _ = annotations.AddStamp(0, StampAt);
            _ = annotations.AddShape(0, AnnotationKind.Rectangle, RectangleStart, RectangleEnd);

            var stamp = annotations.Items.Select(static i => i.Annotation).Single(static a => a.Kind == AnnotationKind.Stamp);
            var centre = new PagePoint(stamp.Bounds.Left + (stamp.Bounds.Width * Half), stamp.Bounds.Top + (stamp.Bounds.Height * Half));

            await Assert.That(annotations.HitTest(0, centre)?.Kind).IsEqualTo(AnnotationKind.Stamp);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The empty inside of an outlined shape picks nothing, so the text and page under it stay reachable, while its edge picks it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlinedShapeIsPickedOnlyByItsEdge()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("hollow.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            _ = annotations.AddShape(0, AnnotationKind.Rectangle, RectangleStart, RectangleEnd);

            await Assert.That(annotations.HitTest(0, EmptyInside)).IsNull();
            await Assert.That(annotations.HitTest(0, OnEdge)?.Kind).IsEqualTo(AnnotationKind.Rectangle);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Picking an annotation is said to assistive technology as help text on the page, and clearing the pick clears it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickedAnnotationIsDescribedToScreenReaders()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("describe.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
            _ = annotations.AddStamp(0, StampAt);
            annotations.Select(annotations.Items.Single().Annotation);
            var picked = peer.GetHelpText();
            annotations.Select(null);
            var cleared = peer.GetHelpText();

            await Assert.That(picked).Contains("Selected: Stamp");
            await Assert.That(picked).Contains("page 1");
            await Assert.That(cleared).DoesNotContain("Selected");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits until the document is shown and the annotation tools are out.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(Window window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        _ = await tab.Annotations.StartCommand.Execute().ToTask();
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }
}
