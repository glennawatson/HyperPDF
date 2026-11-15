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
/// Measures what a reader waits for: from asking the native viewer to open a document until the first view is drawn with every
/// tile it shows. It takes in loading, layout, rendering the tiles on the worker threads and the frames drawn as they arrive.
/// </summary>
public class ViewerOpenBenchmarks
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 40;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

    /// <summary>The longest one wait for the window, in milliseconds. It is short so the frames that confirm the view is complete add little to the measurement.</summary>
    private const int PumpMilliseconds = 1;

    /// <summary>The frames with nothing new to ask for that end the wait for the first view.</summary>
    private const int SettledFrames = 3;

    /// <summary>The longest the first view may take, in seconds.</summary>
    private const double TimeoutSeconds = 120;

    /// <summary>The folder for the settings and the document.</summary>
    private string _folder = string.Empty;

    /// <summary>The document.</summary>
    private string _path = string.Empty;

    /// <summary>The synchronisation context before the benchmark took the thread.</summary>
    private SynchronizationContext? _before;

    /// <summary>Writes the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-viewer-open-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "viewer.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateTextFilled(Pages, Lines));
        _before = SynchronizationContext.Current;
    }

    /// <summary>Removes the files and gives the thread back.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        SynchronizationContext.SetSynchronizationContext(_before);
        Directory.Delete(_folder, true);
    }

    /// <summary>Opens the document in a new viewer and draws until the first view is complete.</summary>
    /// <returns>The frames presented.</returns>
    /// <exception cref="InvalidOperationException">The first view did not complete in time.</exception>
    [Benchmark]
    public int OpenAndDrawFirstView()
    {
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        session.Open([_path]);
        var started = Stopwatch.GetTimestamp();
        var settled = 0;
        while (settled < SettledFrames)
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                throw new InvalidOperationException("The viewer did not finish its first view in time.");
            }

            _ = session.PumpOnce(PumpMilliseconds);
            var loaded = session.Main.SelectedTab is { IsLoaded: true } && session.Canvas.State.Layout.PageCount > 0;
            settled = loaded && window.FramesPresented > 0 && session.Canvas.State.Render.RequestsThisFrame == 0 ? settled + 1 : 0;
        }

        return window.FramesPresented;
    }
}
