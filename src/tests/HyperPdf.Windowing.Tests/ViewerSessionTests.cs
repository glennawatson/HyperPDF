// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Runs the native viewer end to end on a window with no display: it opens a document, draws it with the CPU backend and scrolls it.</summary>
[NotInParallel]
public sealed class ViewerSessionTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 800;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for the document to appear, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The divisor that finds the middle of the window.</summary>
    private const int Half = 2;

    /// <summary>The fewest text pixels a drawn page of text has. The page shadow alone is far fewer than the text.</summary>
    private const int FewestInkPixels = 2000;

    /// <summary>The viewer shows the pages of an opened document and a wheel notch scrolls them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenedDocumentIsDrawnAndScrolls()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-viewer-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        ViewerOutcome outcome;
        try
        {
            outcome = RunViewer(folder);
        }
        finally
        {
            // The assertions await, so the window thread context must be gone before them or their continuations would wait for a pump.
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.Loaded).IsTrue();
        await Assert.That(outcome.PaperPixels).IsGreaterThan(0);
        await Assert.That(outcome.InkPixels).IsGreaterThan(0);
        await Assert.That(outcome.ScrolledBy).IsGreaterThan(0);
    }

    /// <summary>Opens a document in a viewer session, pumps it until the pages are drawn, then scrolls one notch.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the viewer did.</returns>
    private static ViewerOutcome RunViewer(string folder)
    {
        var path = Path.Combine(folder, "viewer.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        session.Open([path]);
        var paper = 0;
        var ink = 0;
        var loaded = PumpUntil(session, () =>
        {
            Count(window, out paper, out ink);
            return session.Main.SelectedTab is { IsLoaded: true } && ink > FewestInkPixels;
        });
        var start = session.Canvas.Offset.Y;
        window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch, 0, 0));
        _ = PumpUntil(session, () => session.Canvas.Offset.Y > start);
        return new(loaded, paper, ink, session.Canvas.Offset.Y - start);
    }

    /// <summary>Pumps the session until a condition holds or the time runs out.</summary>
    /// <param name="session">The session.</param>
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

    /// <summary>Counts the page pixels and the text pixels in the last frame. The corner of the window shows the canvas behind the pages, and the middle shows paper.</summary>
    /// <param name="window">The window.</param>
    /// <param name="paper">Receives the number of pixels that are not the canvas colour.</param>
    /// <param name="ink">Receives the number of pixels that are neither the canvas colour nor the paper colour, which is text and shadow.</param>
    private static void Count(HeadlessWindow window, out int paper, out int ink)
    {
        paper = 0;
        ink = 0;
        var pixels = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels);
        if (pixels.IsEmpty)
        {
            return;
        }

        var canvas = pixels[0];
        var paperColor = pixels[(Width * (Height / Half)) + (Width / Half)];
        foreach (var pixel in pixels)
        {
            if (pixel == canvas)
            {
                continue;
            }

            paper++;
            if (pixel != paperColor)
            {
                ink++;
            }
        }
    }

    /// <summary>What a viewer run did.</summary>
    /// <param name="Loaded">Whether the document loaded and was drawn in time.</param>
    /// <param name="PaperPixels">The paper pixels in the frame.</param>
    /// <param name="InkPixels">The text pixels in the frame.</param>
    /// <param name="ScrolledBy">How far one wheel notch down scrolled, in canvas units.</param>
    private readonly record struct ViewerOutcome(bool Loaded, int PaperPixels, int InkPixels, double ScrolledBy);
}
