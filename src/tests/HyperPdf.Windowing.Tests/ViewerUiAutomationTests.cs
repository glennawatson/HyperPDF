// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Head.Windows.Accessibility;
using HyperPdf.Head.Windows.Win32;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Runs the native viewer in a real Win32 window and reads it through the operating system's UI Automation client, the way a screen reader does.</summary>
[NotInParallel]
public sealed class ViewerUiAutomationTests
{
    /// <summary>The UI Automation property id of an element's name.</summary>
    private const int NameProperty = 30_005;

    /// <summary>The UI Automation property id of an element's control type.</summary>
    private const int ControlTypeProperty = 30_003;

    /// <summary>The UI Automation control type of a window.</summary>
    private const int WindowControlType = 50_032;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 10;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for the viewer, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>UI Automation finds the viewer's window by its title once a document is open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UiAutomationReadsTheViewerWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("UI Automation only runs on Windows.");
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-viewer-uia-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        ViewerReading reading;
        try
        {
            reading = ReadViewer(folder);
        }
        finally
        {
            // The assertions await, so the window thread context must be gone before them or their continuations would wait for a pump.
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(reading.Title).Contains("viewer.pdf");
        await Assert.That(reading.Name).IsEqualTo(reading.Title);
        await Assert.That(reading.ControlType).IsEqualTo(WindowControlType);
    }

    /// <summary>Opens a document in a viewer window and reads the window's name and control type through UI Automation.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What UI Automation reported, beside the title the viewer set.</returns>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static ViewerReading ReadViewer(string folder)
    {
        var path = Path.Combine(folder, "viewer.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        using var bridge = new UiaAccessibilityBridge();
        using var window = new Win32Window(RenderBackendPreference.Gpu, bridge);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        window.Show();
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });

        // The first question makes the viewer build its tree; the answer comes once the window has pumped.
        _ = UiaClient.ReadString(window.WindowHandle, NameProperty);
        string? name = null;
        _ = PumpUntil(session, () =>
        {
            name = UiaClient.ReadString(window.WindowHandle, NameProperty);
            return string.Equals(name, session.Main.WindowTitle, StringComparison.Ordinal);
        });
        return new(session.Main.WindowTitle, name, UiaClient.ReadInt32(window.WindowHandle, ControlTypeProperty));
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

    /// <summary>What UI Automation reported about the viewer window.</summary>
    /// <param name="Title">The title the viewer gave the window.</param>
    /// <param name="Name">The name UI Automation read.</param>
    /// <param name="ControlType">The control type UI Automation read.</param>
    private readonly record struct ViewerReading(string Title, string? Name, int? ControlType);
}
