// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Tests;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures opening and closing the window's menus as a person does: F10 for File, Alt and a letter for another menu, the arrow
/// keys along the menu bar, and a right press on a page. Each opens the menu, draws the frame, closes it and draws again, on a
/// window with no display.
/// </summary>
public class ViewerMenuBenchmarks
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1920;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 1080;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 20;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 30;

    /// <summary>The menus on the bar, which Right steps through to come back to the first.</summary>
    private const int MenuCount = 9;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The frames with nothing new to ask for that end the warm up.</summary>
    private const int SettledFrames = 5;

    /// <summary>The longest the warm up may take, in seconds.</summary>
    private const double WarmUpSeconds = 120;

    /// <summary>The share of the window's width and height where the right press lands, on the page.</summary>
    private const double PressShare = 0.6;

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

    /// <summary>Opens a document and draws until nothing new is asked for.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-menu-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "menus.pdf");
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

        PumpUntilSettled();
        _ = OpenFileMenuWithF10();
        _ = OpenDocumentMenuWithAlt();
        _ = MoveAlongTheMenus();
        _ = RightPressOnPage();
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

    /// <summary>Draws the same view again with the menu bar shown, as a repaint with nothing new does.</summary>
    [Benchmark]
    public void RepaintWithMenuBar() => _session.Canvas.DrawFrame();

    /// <summary>Opens the File menu with F10, draws it, closes it with Escape and draws again.</summary>
    /// <returns>Whether the menu was open.</returns>
    [Benchmark(Baseline = true)]
    public bool OpenFileMenuWithF10() => OpenAndClose(ViewerKey.F10, ViewerModifiers.None);

    /// <summary>Opens the Document menu with Alt and D, draws it, closes it with Escape and draws again.</summary>
    /// <returns>Whether the menu was open.</returns>
    [Benchmark]
    public bool OpenDocumentMenuWithAlt() => OpenAndClose(ViewerKey.D, ViewerModifiers.Alt);

    /// <summary>Opens File, presses Right through every menu on the bar back to File, drawing each, then closes it.</summary>
    /// <returns>Whether a menu was open at the end.</returns>
    [Benchmark]
    public bool MoveAlongTheMenus()
    {
        Key(ViewerKey.F10, ViewerModifiers.None);
        for (var step = 0; step < MenuCount; step++)
        {
            Key(ViewerKey.Right, ViewerModifiers.None);
        }

        var open = _session.Chrome.HasOverlay;
        Key(ViewerKey.Escape, ViewerModifiers.None);
        return open;
    }

    /// <summary>Presses the right button on the page, draws the menu it opens, closes it with Escape and draws again.</summary>
    /// <returns>Whether the menu was open.</returns>
    [Benchmark]
    public bool RightPressOnPage()
    {
        const int x = (int)(Width * PressShare);
        const int y = (int)(Height * PressShare);
        _window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        _window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Right, 0));
        _window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Right, 0));
        _ = _session.PumpOnce(0);
        _session.Canvas.DrawFrame();
        var open = _session.Chrome.HasOverlay;
        Key(ViewerKey.Escape, ViewerModifiers.None);
        return open;
    }

    /// <summary>Opens a menu with a key, draws it, closes it with Escape and draws again.</summary>
    /// <param name="key">The key that opens the menu.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns>Whether the menu was open.</returns>
    private bool OpenAndClose(ViewerKey key, ViewerModifiers modifiers)
    {
        Key(key, modifiers);
        var open = _session.Chrome.HasOverlay;
        Key(ViewerKey.Escape, ViewerModifiers.None);
        return open;
    }

    /// <summary>Sends a key press, lets the window take it and draws the frame.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    private void Key(ViewerKey key, ViewerModifiers modifiers)
    {
        _window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)key, (uint)modifiers));
        _ = _session.PumpOnce(0);
        _session.Canvas.DrawFrame();
    }

    /// <summary>Pumps and draws until the frames stop asking for page pictures.</summary>
    /// <exception cref="InvalidOperationException">The pages did not finish in time.</exception>
    private void PumpUntilSettled()
    {
        var started = Stopwatch.GetTimestamp();
        var settled = 0;
        while (settled < SettledFrames)
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > WarmUpSeconds)
            {
                throw new InvalidOperationException("The viewer did not finish drawing its pages in time.");
            }

            _ = _session.PumpOnce(PumpMilliseconds);
            _session.Canvas.DrawFrame();
            settled = _session.Canvas.State.Render.RequestsThisFrame == 0 ? settled + 1 : 0;
        }
    }
}
