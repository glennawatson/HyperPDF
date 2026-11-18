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
/// Measures opening one more document in a viewer that already has tabs: the work on the window thread that makes the tab,
/// selects it and has the window and the canvas follow it. Each run opens a new file, so the tabs grow as in a long session.
/// </summary>
[InvocationCount(1, 1)]
public class TabOpenBenchmarks
{
    /// <summary>The window's width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window's height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The pages of each document.</summary>
    private const int Pages = 12;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

    /// <summary>How long one turn of the window loop waits for work.</summary>
    private const int PumpMilliseconds = 1;

    /// <summary>The longest one open may take, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>How long the viewer is left to finish the pictures of a new tab, so that work is not part of the next open.</summary>
    private static readonly TimeSpan SettleFor = TimeSpan.FromMilliseconds(150);

    /// <summary>The folder holding the documents and the settings.</summary>
    private string _folder = string.Empty;

    /// <summary>The bytes of every document.</summary>
    private byte[] _bytes = [];

    /// <summary>The file the next run opens.</summary>
    private string _path = string.Empty;

    /// <summary>The files written so far.</summary>
    private int _written;

    /// <summary>The synchronisation context before the window thread's was installed.</summary>
    private SynchronizationContext? _before;

    /// <summary>The window with no display.</summary>
    private HeadlessWindow _window = null!;

    /// <summary>The application services.</summary>
    private AppServices _services = null!;

    /// <summary>The viewer.</summary>
    private ViewerSession _session = null!;

    /// <summary>Starts the viewer.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-tab-open-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _bytes = TestPdf.CreateTextFilled(Pages, Lines);
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
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

    /// <summary>Writes the file the next run opens.</summary>
    [IterationSetup]
    public void WriteNextDocument()
    {
        _written++;
        _path = Path.Combine(_folder, $"document-{_written}.pdf");
        File.WriteAllBytes(_path, _bytes);
    }

    /// <summary>Lets the viewer load the tab just opened and finish its pictures, so that work is not part of the next open.</summary>
    /// <exception cref="InvalidOperationException">The tab did not load in time.</exception>
    [IterationCleanup]
    public void Settle()
    {
        var started = Stopwatch.GetTimestamp();
        while (_session.Main.SelectedTab is not { IsLoaded: true })
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                throw new InvalidOperationException("The tab did not load in time.");
            }

            _ = _session.PumpOnce(PumpMilliseconds);
        }

        started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < SettleFor)
        {
            _ = _session.PumpOnce(PumpMilliseconds);
        }
    }

    /// <summary>Opens one more document: the tab is made, selected and followed by the window and the canvas.</summary>
    /// <returns>The open tabs.</returns>
    [Benchmark]
    public int OpenAnotherDocument()
    {
        _session.Open([_path]);
        return _session.Main.Tabs.Count;
    }
}
