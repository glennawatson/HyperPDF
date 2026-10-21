// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Tests;

/// <summary>Checks a blank thumbnail still loads once its request is kept, moved on, or its tile is dropped.</summary>
public sealed class ThumbnailRepaintTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The thumbnail width.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>The thumbnail height.</summary>
    private const double ThumbnailHeight = 160;

    /// <summary>A repaint after the generation moved, as a scroll step causes, asks for the preview again and it is drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewIsDrawnAfterTheGenerationMovesOn()
    {
        var engine = new PausingEngine();
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("repaint.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var gate = engine.Arm();
        var thumbnail = new PageThumbnail { Tab = tab, PageIndex = 0, Width = ThumbnailWidth, Height = ThumbnailHeight };
        var window = new Window { Content = thumbnail, Width = ThumbnailWidth, Height = ThumbnailHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => gate.Reached.Task.IsCompleted, static () => "No preview was requested.");

            // The queued request is now stale, so only a new request can bring the preview.
            _ = tab.ThumbnailClient.Advance();
            thumbnail.InvalidateVisual();
            engine.Disarm();
            gate.Release();

            var cache = test.Services.RenderHub.Cache;
            var preview = TileKey.Preview(tab.TileId, 0, PageRotation.None, tab.PageTone.Id);
            _ = await UiWait.UntilAsync(() => cache.Contains(preview), () => $"{cache.Count} tiles cached.");
            await Assert.That(thumbnail.NeedsPreview).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A thumbnail shown after its tile was dropped from the cache requests the preview again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewIsDrawnAgainAfterTheCacheDroppedIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("evicted.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var cache = test.Services.RenderHub.Cache;
        var preview = TileKey.Preview(tab.TileId, 0, PageRotation.None, tab.PageTone.Id);
        var first = new PageThumbnail { Tab = tab, PageIndex = 0, Width = ThumbnailWidth, Height = ThumbnailHeight };
        var window = new Window { Content = first, Width = ThumbnailWidth, Height = ThumbnailHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => cache.Contains(preview), () => $"{cache.Count} tiles cached.");
            test.Services.RenderHub.RemoveDocumentTiles(tab.TileId);
            await Assert.That(cache.Contains(preview)).IsFalse();

            var second = new PageThumbnail { Tab = tab, PageIndex = 0, Width = ThumbnailWidth, Height = ThumbnailHeight };
            window.Content = second;
            _ = await UiWait.UntilAsync(() => cache.Contains(preview), () => $"{cache.Count} tiles cached.");
        }
        finally
        {
            window.Close();
        }
    }
}
