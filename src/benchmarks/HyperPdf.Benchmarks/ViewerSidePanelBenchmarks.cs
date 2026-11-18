// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Tests;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures whole frames of the native viewer with the side panel open on its list of pages, over a long document, on a window
/// with no display. The panel draws a small picture of each page it lists and has a row for every page of the document, so this
/// is what the panel adds to a repaint and to a page turn once the pictures are made.
/// </summary>
public class ViewerSidePanelBenchmarks
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 2000;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

    /// <summary>The pages from the first whose pictures must be made before measuring, which is more than the side panel lists at this window height.</summary>
    private const int ListedPages = 6;

    /// <summary>How many of the pauses before the neighbouring pages are got ready the warm up waits, which leaves time for the work itself.</summary>
    private const int PreparePauses = 8;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The frames with nothing new to ask for that end the warm up.</summary>
    private const int SettledFrames = 5;

    /// <summary>The longest the warm up may take, in seconds.</summary>
    private const double WarmUpSeconds = 120;

    /// <summary>The folder for the settings and the document.</summary>
    private string _folder = string.Empty;

    /// <summary>The synchronisation context before the benchmark took the thread.</summary>
    private SynchronizationContext? _before;

    /// <summary>The window.</summary>
    private HeadlessWindow _window = null!;

    /// <summary>The application services.</summary>
    private AppServices _services = null!;

    /// <summary>The viewer session.</summary>
    private ViewerSession _session = null!;

    /// <summary>Whether the next page turn goes to the second page.</summary>
    private bool _forward = true;

    /// <summary>Opens the document with the side panel shown and draws both pages the benchmark turns between until every tile and page picture they show is cached.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-side-panel-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "long.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
        _session.Open([path]);
        while (_session.Main.SelectedTab is not { IsLoaded: true })
        {
            _ = _session.PumpOnce(PumpMilliseconds);
        }

        _session.Main.SelectedTab.SidebarVisible = true;
        PumpUntilSettled();
        _session.Main.SelectedTab!.GoToPage(1);
        PumpUntilSettled();
        _session.Main.SelectedTab!.GoToPage(0);
        PumpUntilSettled();

        // A page turn is followed, after a pause, by getting the pages next to it ready. That is let finish, so a repaint is measured alone.
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < NeighbourPagePreparer.DefaultDebounce * PreparePauses)
        {
            _ = _session.PumpOnce(PumpMilliseconds);
        }

        PumpUntilSettled();
    }

    /// <summary>Closes the viewer and removes its files.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
        _services.Dispose();
        _window.Dispose();
        SynchronizationContext.SetSynchronizationContext(_before);
        Directory.Delete(_folder, true);
    }

    /// <summary>Draws the same view again with the side panel open, as a repaint with nothing new does.</summary>
    [Benchmark(Baseline = true)]
    public void RepaintWithSidePanel() => _session.Canvas.DrawFrame();

    /// <summary>Turns to the next page and back in turn and draws the frame, so the side panel marks another page as the one being read each time.</summary>
    [Benchmark]
    public void TurnPageWithSidePanel()
    {
        _session.Main.SelectedTab!.GoToPage(_forward ? 1 : 0);
        _forward = !_forward;
        _ = _session.PumpOnce(0);
        _session.Canvas.DrawFrame();
    }

    /// <summary>Pumps the session until several frames in a row ask for no new tile.</summary>
    /// <exception cref="InvalidOperationException">The tiles did not arrive in time.</exception>
    private void PumpUntilSettled()
    {
        var started = Stopwatch.GetTimestamp();
        var settled = 0;
        while (settled < SettledFrames)
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > WarmUpSeconds)
            {
                throw new InvalidOperationException("The viewer did not finish rendering its tiles in time.");
            }

            _ = _session.PumpOnce(PumpMilliseconds);
            _session.Canvas.DrawFrame();
            var loaded = _session.Main.SelectedTab is { IsLoaded: true } && _session.Canvas.State.Layout.PageCount > 0;
            settled = loaded && _session.Canvas.State.Render.RequestsThisFrame == 0 && HasPagePictures() ? settled + 1 : 0;
        }
    }

    /// <summary>
    /// Says whether the pictures of the pages the side panel lists are made. They are made at the lowest priority, after the tiles of
    /// the pages, so a frame that asks for no tile can still be waiting for them.
    /// </summary>
    /// <returns><see langword="true"/> when every listed page has its picture.</returns>
    private bool HasPagePictures()
    {
        if (_session.Main.SelectedTab is not { SidebarVisible: true } tab)
        {
            return false;
        }

        for (var page = 0; page < ListedPages; page++)
        {
            if (!tab.RenderHub.Cache.TryGet(TileKey.Preview(tab.TileId, page, PageRotation.None, tab.PageTone.Id), out _))
            {
                return false;
            }
        }

        return true;
    }
}
