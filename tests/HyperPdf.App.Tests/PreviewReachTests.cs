// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Tests;

/// <summary>Checks that low resolution previews are made for the pages around the view, so a quick scroll shows something instead of blank paper.</summary>
public sealed class PreviewReachTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The pages in the long test document.</summary>
    private const int Pages = 90;

    /// <summary>The page the reader jumps to when scrolling forwards.</summary>
    private const int ForwardPage = 40;

    /// <summary>How many pages ahead of the view a preview must appear.</summary>
    private const int Ahead = 12;

    /// <summary>How many pages behind the view a preview must appear.</summary>
    private const int Behind = 4;

    /// <summary>The page the reader starts from before scrolling backwards.</summary>
    private const int BackwardStart = 70;

    /// <summary>The page the reader jumps to when scrolling backwards.</summary>
    private const int BackwardPage = 60;

    /// <summary>How many pages behind the view a preview must appear after scrolling backwards.</summary>
    private const int BehindWhenGoingBack = 16;

    /// <summary>Scrolling towards the end of a long document makes previews well ahead of the view and some behind it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsAreMadeAheadWhenScrollingForwards()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("long.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => HasPreview(test, tab, 0));
            tab.GoToPage(ForwardPage);
            _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= ForwardPage - 1);

            var made = await UiWait.UntilAsync(() => HasPreview(test, tab, ForwardPage + Ahead) && HasPreview(test, tab, ForwardPage - Behind));

            await Assert.That(made).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Scrolling back towards the start makes previews well behind the view, which is now the side the reader is heading to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsAreMadeAheadWhenScrollingBackwards()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("long.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => HasPreview(test, tab, 0));
            tab.GoToPage(BackwardStart);
            _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= BackwardStart - 1);
            tab.GoToPage(BackwardPage);
            _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex <= BackwardPage + 1);

            var made = await UiWait.UntilAsync(() => HasPreview(test, tab, BackwardPage - BehindWhenGoingBack));

            await Assert.That(made).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Determines whether a page's preview is cached.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool HasPreview(TestServices test, DocumentTabViewModel tab, int page) =>
        test.Services.RenderHub.Cache.Contains(TileKey.Preview(tab.TileId, page, tab.Rotation, tab.PageTone.Id));
}
