// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks the search controls use the width they are given.</summary>
public sealed class SidebarSearchLayoutTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Pages in the sample document.</summary>
    private const int Pages = 3;

    /// <summary>The query that matches every page.</summary>
    private const string Words = "lazy dog";

    /// <summary>The room left for a scroll bar.</summary>
    private const double ScrollBarAllowance = 20;

    /// <summary>The margins either side of the sidebar's search box.</summary>
    private const double SidebarMargins = 16;

    /// <summary>The widest the find box was before it grew with the window.</summary>
    private const double OldFixedWidth = 280;

    /// <summary>A result row is as wide as the list that holds it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchResultsFillTheSidebarWidth()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("search.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            tab.SidebarVisible = true;
            tab.Search.Query = Words;
            _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count == Pages);
            tab.IsSearchMode = true;

            ListBox? list = null;
            _ = await UiWait.UntilAsync(() =>
            {
                list = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(static l => l.Name == "SearchResultList");
                return list?.ContainerFromIndex(0) is not null;
            });
            var item = (ListBoxItem)list!.ContainerFromIndex(0)!;
            var content = item.GetVisualDescendants().OfType<SearchResultView>().First();

            var box = window.GetVisualDescendants().OfType<TextBox>().First(static t => t.Name == "SidebarSearchBox");
            await Assert.That(box.IsEffectivelyVisible).IsTrue();
            await Assert.That(box.Text).IsEqualTo(Words);
            await Assert.That(box.Bounds.Width).IsGreaterThanOrEqualTo(list.Bounds.Width - SidebarMargins);
            await Assert.That(item.Bounds.Width).IsGreaterThanOrEqualTo(list.Bounds.Width - ScrollBarAllowance);
            await Assert.That(content.Bounds.Width).IsGreaterThanOrEqualTo(item.Bounds.Width - item.Padding.Left - item.Padding.Right - 1);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The find box grows to fill the find bar instead of stopping at a fixed width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindBoxFillsTheFindBar()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("find.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            main.SelectedTab!.Search.IsOpen = true;
            TextBox? box = null;
            _ = await UiWait.UntilAsync(() =>
            {
                box = window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(static t => t.Name == "SearchBox");
                return box is { IsEffectivelyVisible: true } && box.Bounds.Width > 0;
            });

            await Assert.That(box!.Bounds.Width).IsGreaterThan(OldFixedWidth);
        }
        finally
        {
            window.Close();
        }
    }
}
