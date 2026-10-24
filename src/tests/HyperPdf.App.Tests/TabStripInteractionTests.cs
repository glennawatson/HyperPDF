// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks the tab strip and thumbnail list pointer input in a headless window.</summary>
public sealed class TabStripInteractionTests
{
    /// <summary>The name of the tab bar in the main window.</summary>
    private const string TabBarName = "TabBar";

    /// <summary>The name of the tab strip in the tab bar.</summary>
    private const string TabStripName = "TabStrip";

    /// <summary>The number of tabs in the small scenarios, and of thumbnails in the group.</summary>
    private const int Pair = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 700;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 600;

    /// <summary>The share of a control's size that is its middle.</summary>
    private const double Middle = 0.5;

    /// <summary>The number of tabs that overflow the tab strip.</summary>
    private const int ManyTabs = 24;

    /// <summary>The number of pages in the thumbnail scenario.</summary>
    private const int PageCount = 4;

    /// <summary>The distance a drag moves the pointer, beyond every threshold.</summary>
    private const double DragDistance = 40;

    /// <summary>Verifies a middle click on a tab closes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MiddleClickClosesTheTab()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", 1), test.CreateDocument("second.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var strip = window.FindControl<TabBarView>(TabBarName)!.FindControl<ListBox>(TabStripName)!;
            _ = await UiWait.UntilAsync(() => strip.GetRealizedContainers().Count() == Pair);
            var second = main.Tabs[1];
            var point = Centre(window, ContainerOf(strip, second));
            window.MouseDown(point, MouseButton.Middle);
            window.MouseUp(point, MouseButton.Middle);

            _ = await UiWait.UntilAsync(() => main.Tabs.Count == 1);
            await Assert.That(main.Tabs).DoesNotContain(second);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies dragging a tab onto another tab swaps their places.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DraggingATabReordersTheTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("one.pdf", 1), test.CreateDocument("two.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var strip = window.FindControl<TabBarView>(TabBarName)!.FindControl<ListBox>(TabStripName)!;
            _ = await UiWait.UntilAsync(() => strip.GetRealizedContainers().Count() == Pair);
            var first = main.Tabs[0];
            var second = main.Tabs[1];
            var from = Centre(window, ContainerOf(strip, first));
            var to = Centre(window, ContainerOf(strip, second));
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(new(from.X + DragDistance, from.Y));
            window.MouseMove(to);
            window.MouseUp(to, MouseButton.Left);

            _ = await UiWait.UntilAsync(() => ReferenceEquals(main.Tabs[0], second));
            await Assert.That(main.Tabs[1]).IsSameReferenceAs(first);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the wheel scrolls an overflowing tab strip sideways.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelScrollsTheTabStripSideways()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var documents = new List<string>();
        for (var tab = 0; tab < ManyTabs; tab++)
        {
            documents.Add(test.CreateDocument($"tab-{tab}.pdf", 1));
        }

        await TestServices.OpenAndWaitAsync(main, documents);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var strip = window.FindControl<TabBarView>(TabBarName)!.FindControl<ListBox>(TabStripName)!;
            var scroller = strip.FindDescendantOfType<ScrollViewer>()!;
            _ = await UiWait.UntilAsync(() => scroller.Extent.Width > scroller.Viewport.Width && scroller.Offset.X > 0);
            var before = scroller.Offset.X;
            var point = strip.TranslatePoint(new(strip.Bounds.Width * Middle, strip.Bounds.Height * Middle), window)!.Value;
            window.MouseWheel(point, new(0, 1));

            _ = await UiWait.UntilAsync(() => scroller.Offset.X < before);
            await Assert.That(scroller.Offset.X).IsLessThan(before);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies pressing a thumbnail in a group selection keeps the group, so the group can be dragged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressingAGroupedThumbnailKeepsTheSelection()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("grouped.pdf", PageCount)]);
        var tab = main.SelectedTab!;
        tab.SidebarMode = SidebarMode.Thumbnails;
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<ThumbnailListBox>().Any());
            var list = window.GetVisualDescendants().OfType<ThumbnailListBox>().Single();
            _ = list.SelectedItems!.Add(tab.Thumbnails[0]);
            _ = list.SelectedItems.Add(tab.Thumbnails[1]);
            _ = await UiWait.UntilAsync(() => tab.Pages.SelectedPages.Count == Pair);
            _ = await UiWait.UntilAsync(() => list.ContainerFromIndex(1) is { Bounds.Height: > 0 });

            var point = Centre(window, list.ContainerFromIndex(1)!);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(new(point.X, point.Y + DragDistance));
            window.MouseUp(point, MouseButton.Left);

            await Assert.That(tab.Pages.SelectedPages.Count).IsEqualTo(Pair);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Finds the container the tab strip made for a tab.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidOperationException">The tab has no container.</exception>
    private static Control ContainerOf(ListBox strip, DocumentTabViewModel tab)
    {
        foreach (var container in strip.GetRealizedContainers())
        {
            if (ReferenceEquals(container.DataContext, tab))
            {
                return container;
            }
        }

        throw new InvalidOperationException("The tab has no container.");
    }

    /// <summary>Gets the middle of a control in window coordinates.</summary>
    /// <param name="window">The window.</param>
    /// <param name="control">The control.</param>
    /// <returns>The point.</returns>
    private static Point Centre(MainWindow window, Control control) =>
        control.TranslatePoint(new(control.Bounds.Width * Middle, control.Bounds.Height * Middle), window)!.Value;
}
