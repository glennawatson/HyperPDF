// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
namespace HyperPdf.Windowing.Tests;

/// <summary>A screen reader that moves the caret through the page text moves the viewer's own caret, and the tree then reports it where it was put.</summary>
[NotInParallel]
public sealed class ViewerCaretTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 800;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 10;

    /// <summary>The name of the test document.</summary>
    private const string DocumentFile = "caret.pdf";

    /// <summary>The offset in the page text the caret is asked to go to.</summary>
    private const int Wanted = 7;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The document element accepts a caret offset, the caret moves, and the tree reports the new offset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScreenReaderMovesTheCaret()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-caret-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, DocumentFile), TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        bool accepted;
        bool refused;
        bool moved;
        int reported;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([Path.Combine(folder, DocumentFile)]);
            _ = session.Accessibility.Get(session.Accessibility.RootId);
            _ = PumpUntil(session, () => session.Accessibility.Get(ViewerNodes.DocumentId) is { Text.Value.Length: > Wanted });
            refused = !session.Accessibility.SetCaret(ViewerNodes.WindowId, Wanted);
            accepted = session.Accessibility.SetCaret(ViewerNodes.DocumentId, Wanted);
            moved = PumpUntil(session, () => session.Accessibility.Get(ViewerNodes.DocumentId) is { Text.CaretOffset: >= 0 });
            reported = session.Accessibility.Get(ViewerNodes.DocumentId)?.Text?.CaretOffset ?? -1;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(refused).IsTrue();
        await Assert.That(accepted).IsTrue();
        await Assert.That(moved).IsTrue();
        await Assert.That(reported).IsEqualTo(Wanted);
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
}
