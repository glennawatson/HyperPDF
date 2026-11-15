// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Tests;

/// <summary>Presenting a document fills the screen with the window, and stopping gives the window back.</summary>
[NotInParallel]
public sealed class ViewerFullScreenTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 800;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 5;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The name of the test document.</summary>
    private const string DocumentFile = "present.pdf";

    /// <summary>The window is asked to fill the screen while a document is presented, and to go back when the presenting stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresentingFillsTheScreen()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-present-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, DocumentFile), TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        bool filled;
        bool restored;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([Path.Combine(folder, DocumentFile)]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;
            Signal.Return(RxVoid.Default).InvokeCommand(tab.PresentCommand).Dispose();
            filled = PumpUntil(session, () => window.IsFullScreen);
            Signal.Return(RxVoid.Default).InvokeCommand(tab.PresentCommand).Dispose();
            restored = PumpUntil(session, () => !window.IsFullScreen);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(filled).IsTrue();
        await Assert.That(restored).IsTrue();
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
