// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;

namespace HyperPdf.Windowing.Tests;

/// <summary>A file dropped on the window opens as a tab.</summary>
[NotInParallel]
public sealed class ViewerFileDropTests
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

    /// <summary>The name of the dropped document.</summary>
    private const string DroppedFile = "dropped.pdf";

    /// <summary>A file dropped on the window is opened, and its tab is the one shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DroppedFileOpensAsATab()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-drop-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, DroppedFile);
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        bool opened;
        string title;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            window.Drop([path]);
            opened = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            title = session.Main.SelectedTab?.FileName ?? string.Empty;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(opened).IsTrue();
        await Assert.That(title).IsEqualTo(DroppedFile);
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
