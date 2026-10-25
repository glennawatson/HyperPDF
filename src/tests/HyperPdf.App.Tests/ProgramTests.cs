// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;

namespace HyperPdf.App.Tests;

/// <summary>Tests the desktop backend configured by the application entry point.</summary>
public sealed class ProgramTests
{
    /// <summary>The compositor socket used by the test sessions.</summary>
    private const string WaylandDisplay = "wayland-0";

    /// <summary>Verifies that a Linux session without X11 configures native Wayland.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_WaylandOnly_ConfiguresWayland()
    {
        var expected = AppBuilder.Configure<App>().UseWayland().WindowingSubsystemInitializer;
        await Assert.That(Program.BuildAvaloniaApp(true, null, WaylandDisplay).WindowingSubsystemInitializer?.Method).IsEqualTo(expected?.Method);
    }

    /// <summary>Verifies that sessions with X11 keep the platform's default backend.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_BothDisplays_KeepsDefaultBackend()
    {
        var expected = AppBuilder.Configure<App>().UseX11().WindowingSubsystemInitializer;
        await Assert.That(Program.BuildAvaloniaApp(true, ":0", WaylandDisplay).WindowingSubsystemInitializer?.Method).IsEqualTo(expected?.Method);
    }

    /// <summary>Verifies that Wayland environment values do not override other operating systems.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_NonLinux_KeepsDefaultBackend()
    {
        var expected = (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()) switch
        {
            (true, _) => AppBuilder.Configure<App>().UseWin32(),
            (_, true) => AppBuilder.Configure<App>().UseAvaloniaNative(),
            _ => AppBuilder.Configure<App>().UseX11(),
        };

        await Assert.That(Program.BuildAvaloniaApp(false, null, WaylandDisplay).WindowingSubsystemInitializer?.Method).IsEqualTo(expected.WindowingSubsystemInitializer?.Method);
    }
}
