// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
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
        await Assert.That(outcome.TreeAttached).IsTrue();
        await Assert.That(outcome.DocumentRole).IsEqualTo(AccessibleRole.Document);
        await Assert.That(outcome.DocumentTextLength).IsGreaterThan(0);
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
            // The side panel appears once the document has loaded, which moves the page area.
            Count(window, session.Chrome.CanvasBounds, out paper, out ink);
            return session.Main.SelectedTab is { IsLoaded: true } && ink > FewestInkPixels;
        });
        var start = session.Canvas.Offset.Y;
        window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch, 0, 0));
        _ = PumpUntil(session, () => session.Canvas.Offset.Y > start);

        // The first question from a screen reader makes the viewer build its tree, which the next pump does.
        _ = session.Accessibility.Get(session.Accessibility.RootId);
        _ = PumpUntil(session, () => session.Accessibility.Get(ViewerNodes.DocumentId) is { Text.Value.Length: > 0 });
        var document = session.Accessibility.Get(ViewerNodes.DocumentId);
        return new(loaded, paper, ink, session.Canvas.Offset.Y - start, document?.Role ?? AccessibleRole.Unknown, document?.Text?.Value.Length ?? 0, window.Attached);
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
    /// <param name="area">The part of the window the pages are shown in. The window's own controls are around it and are not counted.</param>
    /// <param name="paper">Receives the number of pixels that are not the canvas colour.</param>
    /// <param name="ink">Receives the number of pixels that are neither the canvas colour nor the paper colour, which is text and shadow.</param>
    private static void Count(HeadlessWindow window, in LayoutRect area, out int paper, out int ink)
    {
        paper = 0;
        ink = 0;
        var pixels = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels);
        if (pixels.IsEmpty || area.Width < 1 || area.Height < 1)
        {
            return;
        }

        var left = (int)area.X;
        var top = (int)area.Y;
        var width = (int)area.Width;
        var canvas = pixels[(Width * top) + left];
        var paperColor = pixels[(Width * (top + ((int)area.Height / Half))) + left + (width / Half)];
        for (var row = top; row < top + (int)area.Height; row++)
        {
            foreach (var pixel in pixels.Slice((Width * row) + left, width))
            {
                if (pixel == canvas)
                {
                    continue;
                }

                paper++;
                ink += pixel != paperColor ? 1 : 0;
            }
        }
    }

    /// <summary>What a viewer run did.</summary>
    /// <param name="Loaded">Whether the document loaded and was drawn in time.</param>
    /// <param name="PaperPixels">The paper pixels in the frame.</param>
    /// <param name="InkPixels">The text pixels in the frame.</param>
    /// <param name="ScrolledBy">How far one wheel notch down scrolled, in canvas units.</param>
    /// <param name="DocumentRole">The role of the document element a screen reader finds.</param>
    /// <param name="DocumentTextLength">The length of the page text a screen reader can read.</param>
    /// <param name="TreeAttached">Whether the viewer gave its tree to the window's accessibility bridge.</param>
    private readonly record struct ViewerOutcome(bool Loaded, int PaperPixels, int InkPixels, double ScrolledBy, AccessibleRole DocumentRole, int DocumentTextLength, bool TreeAttached);
}
