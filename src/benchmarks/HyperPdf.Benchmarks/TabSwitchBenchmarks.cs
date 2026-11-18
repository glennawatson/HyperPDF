// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Tests;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures moving between open tabs that have each been shown before: selecting the next tab alone, which is the window and
/// the canvas letting go of one tab and following the other, and selecting it and drawing the frame that shows it.
/// </summary>
public class TabSwitchBenchmarks
{
    /// <summary>The window's width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window's height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The open documents.</summary>
    private const int Documents = 6;

    /// <summary>The pages of each document.</summary>
    private const int Pages = 40;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

    /// <summary>How long one turn of the window loop waits for work.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How long each tab is shown before the measurements, so its tiles and page pictures are made.</summary>
    private static readonly TimeSpan ShownFor = TimeSpan.FromSeconds(1);

    /// <summary>The folder holding the documents and the settings.</summary>
    private string _folder = string.Empty;

    /// <summary>The synchronisation context before the window thread's was installed.</summary>
    private SynchronizationContext? _before;

    /// <summary>The window with no display.</summary>
    private HeadlessWindow _window = null!;

    /// <summary>The application services.</summary>
    private AppServices _services = null!;

    /// <summary>The viewer.</summary>
    private ViewerSession _session = null!;

    /// <summary>The tab selected by the last run.</summary>
    private int _selected;

    /// <summary>Opens the documents and shows each tab once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-tab-switch-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var bytes = TestPdf.CreateTextFilled(Pages, Lines);
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
        for (var i = 0; i < Documents; i++)
        {
            var path = Path.Combine(_folder, $"document-{i}.pdf");
            File.WriteAllBytes(path, bytes);
            _session.Open([path]);
            PumpFor(ShownFor);
        }

        // Every tab is shown a second time, so what a tab makes the first time it is shown is not part of a measured switch.
        var tabs = _session.Main.Tabs;
        for (var i = 0; i < tabs.Count; i++)
        {
            _session.Main.SelectedTab = tabs[i];
            PumpFor(ShownFor);
        }
    }

    /// <summary>Closes the viewer and removes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
        _services.Dispose();
        _window.Dispose();
        SynchronizationContext.SetSynchronizationContext(_before);
        Directory.Delete(_folder, true);
    }

    /// <summary>Selects the next tab: the window and the canvas leave one tab and follow the other.</summary>
    [Benchmark(Baseline = true)]
    public void SelectNextTab() => SelectNext();

    /// <summary>Selects the next tab and draws the frame that shows it.</summary>
    [Benchmark]
    public void SelectNextTabAndDraw()
    {
        SelectNext();
        _ = _session.PumpOnce(0);
        _session.Canvas.DrawFrame();
    }

    /// <summary>Selects the tab after the one selected.</summary>
    private void SelectNext()
    {
        var tabs = _session.Main.Tabs;
        _selected = _selected + 1 < tabs.Count ? _selected + 1 : 0;
        _session.Main.SelectedTab = tabs[_selected];
    }

    /// <summary>Turns the window loop for a while.</summary>
    /// <param name="duration">How long.</param>
    private void PumpFor(TimeSpan duration)
    {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < duration)
        {
            _ = _session.PumpOnce(PumpMilliseconds);
        }
    }
}
