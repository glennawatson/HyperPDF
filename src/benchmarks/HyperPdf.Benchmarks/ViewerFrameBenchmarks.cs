// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Windowing.Tests;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures whole frames of the native viewer on a window with no display: the canvas state plans the frame, the painter turns it
/// into backend calls and the CPU backend draws it. The document's tiles are cached first, so this is what a repaint, a scroll step
/// and a screen reader's refresh cost once the pages are rendered.
/// </summary>
public class ViewerFrameBenchmarks
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The x of the pointer over the page, in pixels.</summary>
    private const int PointerX = 960;

    /// <summary>The y of the pointer over the page, in pixels.</summary>
    private const int PointerY = 540;

    /// <summary>The x of the pointer over the first toolbar button, in pixels.</summary>
    private const int FirstButtonX = 30;

    /// <summary>The x of the pointer over the second toolbar button, in pixels.</summary>
    private const int SecondButtonX = 95;

    /// <summary>The y of the pointer over the toolbar, in pixels.</summary>
    private const int ToolbarY = 56;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 40;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 40;

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

    /// <summary>Whether the next scroll step goes down.</summary>
    private bool _down = true;

    /// <summary>Whether the pointer is over the first of the two toolbar buttons it moves between.</summary>
    private bool _overFirst;

    /// <summary>Opens the document and draws both scroll positions until every tile they show is cached.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-viewer-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "viewer.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
        _session.Open([path]);
        PumpUntilSettled();
        _window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch, 0, 0));
        PumpUntilSettled();
        _window.Send(new(WindowEventKind.Scrolled, 0, WindowEvent.ScrollNotch, 0, 0));
        PumpUntilSettled();

        // A screen reader's first question makes the viewer keep its tree, which the refresh benchmark then rebuilds.
        _ = _session.Accessibility.Get(_session.Accessibility.RootId);
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

    /// <summary>Draws the same view again, as a repaint with nothing new does.</summary>
    [Benchmark(Baseline = true)]
    public void RepaintUnchangedView() => _session.Canvas.DrawFrame();

    /// <summary>Scrolls one wheel notch and draws the frame, going down and up in turn so every tile stays cached.</summary>
    [Benchmark]
    public void ScrollOneNotch()
    {
        _session.Canvas.Handle(new(WindowEventKind.Scrolled, 0, _down ? -WindowEvent.ScrollNotch : WindowEvent.ScrollNotch, 0, 0));
        _down = !_down;
        _session.Canvas.DrawFrame();
    }

    /// <summary>Handles a pointer move over the page, as every mouse movement does.</summary>
    [Benchmark]
    public void MovePointer() => _session.Canvas.Handle(new(WindowEventKind.PointerMoved, PointerX, PointerY, 0, 0));

    /// <summary>Handles a pointer move along the toolbar, from one button to another in turn, which changes the control marked under the pointer.</summary>
    /// <returns>Whether the controls took the move.</returns>
    [Benchmark]
    public bool MovePointerOverControls()
    {
        _overFirst = !_overFirst;
        return _session.Chrome.Handle(new(WindowEventKind.PointerMoved, _overFirst ? FirstButtonX : SecondButtonX, ToolbarY, 0, 0));
    }

    /// <summary>Draws the tab strip, the toolbar and the status bar alone, which every frame does after the pages.</summary>
    [Benchmark]
    public void DrawControls()
    {
        using var pass = _window.Backend.BeginFrame();
        _session.Chrome.Draw(pass);
    }

    /// <summary>Turns one loop of the viewer with nothing waiting while a screen reader is attached, which checks whether the tree it reads is still right.</summary>
    /// <returns>Whether the window is still open.</returns>
    [Benchmark]
    public bool IdleLoopWithScreenReader() => _session.PumpOnce(0);

    /// <summary>Reads the document element the way a screen reader does.</summary>
    /// <returns>The length of the page text.</returns>
    [Benchmark]
    public int ReadDocumentElement() => _session.Accessibility.Get(ViewerNodes.DocumentId)?.Text?.Value.Length ?? 0;

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
            settled = loaded && _session.Canvas.State.Render.RequestsThisFrame == 0 ? settled + 1 : 0;
        }
    }
}
