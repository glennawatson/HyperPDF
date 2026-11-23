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
/// Measures opening a document in a new tab, waiting until it is loaded, and closing the tab again. The viewer ends each
/// run as it started, so memory that grows from run to run is memory a closed tab leaves behind.
/// </summary>
[InvocationCount(1, 1)]
public class TabOpenCloseBenchmarks
{
    /// <summary>The window's width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window's height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The pages of the document.</summary>
    private const int Pages = 12;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

    /// <summary>How long one turn of the window loop waits for work.</summary>
    private const int PumpMilliseconds = 1;

    /// <summary>The longest one open may take, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The folder holding the document and the settings.</summary>
    private string _folder = string.Empty;

    /// <summary>The document opened by every run.</summary>
    private string _path = string.Empty;

    /// <summary>The synchronisation context before the window thread's was installed.</summary>
    private SynchronizationContext? _before;

    /// <summary>The window with no display.</summary>
    private HeadlessWindow _window = null!;

    /// <summary>The application services.</summary>
    private AppServices _services = null!;

    /// <summary>The viewer.</summary>
    private ViewerSession _session = null!;

    /// <summary>Starts the viewer and writes the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-tab-open-close-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "document.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateTextFilled(Pages, Lines));
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
    }

    /// <summary>Closes the viewer and removes the files.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
        _services.Dispose();
        _window.Dispose();
        SynchronizationContext.SetSynchronizationContext(_before);
        Directory.Delete(_folder, true);
    }

    /// <summary>Opens the document, waits until its tab is loaded, then closes the tab.</summary>
    /// <returns>The tabs left open, which is none.</returns>
    /// <exception cref="InvalidOperationException">The tab did not load in time.</exception>
    [Benchmark]
    public int OpenAndCloseTab()
    {
        _session.Open([_path]);
        var started = Stopwatch.GetTimestamp();
        while (_session.Main.SelectedTab is not { IsLoaded: true })
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                throw new InvalidOperationException("The tab did not load in time.");
            }

            _ = _session.PumpOnce(PumpMilliseconds);
        }

        _session.Main.CloseTabWithoutAsking(_session.Main.SelectedTab);
        _ = _session.PumpOnce(PumpMilliseconds);
        return _session.Main.Tabs.Count;
    }
}
