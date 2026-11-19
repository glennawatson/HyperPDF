// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks the side panel's list of pages across page turns. The rows of the list are kept from one layout to the next, so the page
/// being read has to be marked from the tab each time and not from the rows.
/// </summary>
public sealed class SidebarPageRowsTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest a step may take, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The page being read is the one marked in the list, before a page turn, after it, and after turning back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageBeingReadIsMarkedAfterEachTurn()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-page-rows-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        MarkedPages marked;
        try
        {
            marked = RunTurns(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(marked.AtStart).IsEqualTo(0);
        await Assert.That(marked.AfterTurn).IsEqualTo(1);
        await Assert.That(marked.AfterTurnBack).IsEqualTo(0);
    }

    /// <summary>Opens a document with the side panel on its pages and turns a page forward and back.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>The page marked in the list at each step.</returns>
    private static MarkedPages RunTurns(string folder)
    {
        var path = Path.Combine(folder, "pages.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
        var tab = session.Main.SelectedTab!;
        tab.SidebarVisible = true;
        var atStart = MarkedAfter(session, 0);
        tab.GoToPage(1);
        var afterTurn = MarkedAfter(session, 1);
        tab.GoToPage(0);
        return new(atStart, afterTurn, MarkedAfter(session, 0));
    }

    /// <summary>Turns the loop until the list marks a page, and says which page the list marks then.</summary>
    /// <param name="session">The viewer session.</param>
    /// <param name="page">The page the list should come to mark.</param>
    /// <returns>The row marked, or -1 when the list marks none or more than one.</returns>
    private static int MarkedAfter(ViewerSession session, int page)
    {
        _ = PumpUntil(session, () => Marked(session.Chrome) == page);
        return Marked(session.Chrome);
    }

    /// <summary>Finds the one page whose picture is marked in the list.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>The row marked, or -1 when the list marks none or more than one.</returns>
    private static int Marked(ViewerChrome chrome)
    {
        var marked = -1;
        foreach (var item in chrome.Bars)
        {
            if (item is not { Kind: ChromeItemKind.Thumbnail, Selected: true })
            {
                continue;
            }

            var tick = chrome.Bars.Find(control => control.Index == item.Index && control is { Kind: ChromeItemKind.Button, Icon: ChromeIcon.Done, Selected: true });
            var highlight = chrome.Bars.Find(control => control.Index == item.Index && control is { Kind: ChromeItemKind.Panel, Ground: not 0 });
            if (marked >= 0 || tick is null || highlight is null || highlight.Bounds.Width < tick.Bounds.Width || highlight.Bounds.Bottom < tick.Bounds.Bottom)
            {
                return -1;
            }

            marked = item.Index;
        }

        return marked;
    }

    /// <summary>Turns the viewer's loop until a condition holds or the time runs out.</summary>
    /// <param name="session">The viewer session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when the condition held in time.</returns>
    private static bool PumpUntil(ViewerSession session, Func<bool> done)
    {
        var started = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
        }

        return true;
    }

    /// <summary>The page marked in the list of pages at each step of the test.</summary>
    /// <param name="AtStart">The page marked when the document opened.</param>
    /// <param name="AfterTurn">The page marked after turning to the second page.</param>
    /// <param name="AfterTurnBack">The page marked after turning back to the first.</param>
    private readonly record struct MarkedPages(int AtStart, int AfterTurn, int AfterTurnBack);
}
