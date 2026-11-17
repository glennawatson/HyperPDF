// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>The window opens at the size it had when it was last closed, and remembers its size when it closes.</summary>
[NotInParallel]
public sealed class ViewerCloseTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 800;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The width the settings hold from an earlier run.</summary>
    private const int SavedWidth = 900;

    /// <summary>The height the settings hold from an earlier run.</summary>
    private const int SavedHeight = 700;

    /// <summary>The code of the close request in a window event.</summary>
    private const uint NoCode = 0;

    /// <summary>The saved size is asked of the window when the run starts, and the size the window has is saved when it closes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedSizeIsUsedAndTheCurrentSizeIsSaved()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-close-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        SurfaceSize? requested;
        double savedWidth;
        double savedHeight;
        try
        {
            var path = Path.Combine(folder, "settings.json");
            var store = new SettingsStore(path);
            var settings = store.Load();
            settings.WindowWidth = SavedWidth;
            settings.WindowHeight = SavedHeight;
            store.Save(settings);

            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);

            // The close is already waiting, so the run ends after its first turn.
            window.Send(new(WindowEventKind.CloseRequested, 0, 0, NoCode, 0));
            session.Run();
            requested = window.RequestedSize;
            savedWidth = services.Settings.WindowWidth;
            savedHeight = services.Settings.WindowHeight;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(requested).IsEqualTo(new SurfaceSize(SavedWidth, SavedHeight));
        await Assert.That(savedWidth).IsEqualTo(Width);
        await Assert.That(savedHeight).IsEqualTo(Height);
    }

    /// <summary>A window saved as maximised is maximised when the run starts, and closing it maximised keeps the size from before and the state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MaximisedStateIsUsedAndSaved()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-max-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        bool maximized;
        bool savedMaximized;
        double savedWidth;
        try
        {
            var path = Path.Combine(folder, "settings.json");
            var store = new SettingsStore(path);
            var settings = store.Load();
            settings.WindowWidth = SavedWidth;
            settings.WindowHeight = SavedHeight;
            settings.WindowMaximized = true;
            store.Save(settings);

            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            window.Send(new(WindowEventKind.CloseRequested, 0, 0, NoCode, 0));
            session.Run();
            maximized = window.IsMaximized;
            savedMaximized = services.Settings.WindowMaximized;
            savedWidth = services.Settings.WindowWidth;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(maximized).IsTrue();
        await Assert.That(savedMaximized).IsTrue();
        await Assert.That(savedWidth).IsEqualTo(SavedWidth);
    }
}
