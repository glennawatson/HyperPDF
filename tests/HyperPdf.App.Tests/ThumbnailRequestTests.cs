// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.Controls;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks a thumbnail stops asking for the preview of a page it no longer shows.</summary>
public sealed class ThumbnailRequestTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The page the second thumbnail moves on to.</summary>
    private const int LastPage = 2;

    /// <summary>The thumbnails in the window.</summary>
    private const int Thumbnails = 2;

    /// <summary>The thumbnail width.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>The thumbnail height.</summary>
    private const double ThumbnailHeight = 160;

    /// <summary>A preview that was queued but whose thumbnail moved on is dropped instead of drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewOfAPageThatScrolledAwayIsNotDrawn()
    {
        var engine = new PausingEngine();
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("thumbnails.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var gate = engine.ArmRender();
        var first = new PageThumbnail { Tab = tab, PageIndex = 0, Width = ThumbnailWidth, Height = ThumbnailHeight };
        var second = new PageThumbnail { Tab = tab, PageIndex = 1, Width = ThumbnailWidth, Height = ThumbnailHeight };
        var panel = new StackPanel();
        panel.Children.Add(first);
        panel.Children.Add(second);
        var window = new Window { Content = panel, Width = ThumbnailWidth * Thumbnails, Height = ThumbnailHeight * Pages };
        window.Show();
        try
        {
            // The first preview holds the render thread, so the second preview waits in the queue.
            _ = await UiWait.UntilAsync(() => gate.Reached.Task.IsCompleted, static () => "No preview started drawing.");
            var cache = test.Services.RenderHub.Cache;
            var pageOne = TileKey.Preview(tab.TileId, 1, PageRotation.None, tab.PageTone.Id);
            var pageTwo = TileKey.Preview(tab.TileId, LastPage, PageRotation.None, tab.PageTone.Id);

            // The second preview is queued before its thumbnail moves on, whichever frame drew it.
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.IsPending(pageOne), static () => "The second preview was never queued.");
            second.PageIndex = LastPage;
            engine.Disarm();
            gate.Release();

            _ = await UiWait.UntilAsync(() => cache.Contains(pageTwo), () => $"{cache.Count} tiles cached.");
            await Assert.That(cache.Contains(pageOne)).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A preview requested before the thumbnail activates is still drawn once it does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewRequestedBeforeActivationIsStillDrawn()
    {
        var engine = new PausingEngine();
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("thumbnails.pdf", Pages)]);
        var tab = main.SelectedTab!;

        // The gate holds the page in preparation, so the request is still live when the thumbnail activates.
        var gate = engine.Arm();
        var thumbnail = new PageThumbnail { Tab = tab, PageIndex = 0, Width = ThumbnailWidth, Height = ThumbnailHeight };
        var window = new Window { Content = thumbnail, Width = ThumbnailWidth, Height = ThumbnailHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => gate.Reached.Task.IsCompleted, static () => "No preview was requested.");
            _ = await UiWait.UntilAsync(() => thumbnail.IsLoaded, static () => "The thumbnail never activated.");
            engine.Disarm();
            gate.Release();

            var cache = test.Services.RenderHub.Cache;
            var preview = TileKey.Preview(tab.TileId, 0, PageRotation.None, tab.PageTone.Id);
            _ = await UiWait.UntilAsync(() => cache.Contains(preview), () => $"{cache.Count} tiles cached.");
        }
        finally
        {
            window.Close();
        }
    }
}
