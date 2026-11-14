// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux;
using HyperPdf.Head.Linux.Wayland;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Opens a real window on the running Wayland compositor. The tests skip when there is no Wayland session.</summary>
public sealed class WaylandWindowTests
{
    /// <summary>The white used to clear the frame.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>How long to wait for events in the test, in milliseconds.</summary>
    private const int WaitMilliseconds = 100;

    /// <summary>A window opens, shows a frame, handles events and closes cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowOpensPresentsAFrameAndCloses()
    {
        if (!WaylandWindow.IsAvailable())
        {
            Skip.Test("No Wayland compositor is available.");
        }

        var bridge = new RecordingAccessibilityBridge();
        using (var window = new WaylandWindow(RenderBackendPreference.Gpu, bridge))
        {
            window.SetTitle("HyperPDF window test");
            using (var pass = window.Backend.BeginFrame())
            {
                pass.Clear(White);
            }

            window.PumpEvents(WaitMilliseconds, static (in _) => { });

            await Assert.That(window.ClientSize.IsEmpty).IsFalse();
            await Assert.That(window.Backend.IsAlive).IsTrue();
        }

        await Assert.That(bridge.Disposed).IsTrue();
    }

    /// <summary>The Linux selector opens Wayland first when a compositor is reachable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectorPrefersWaylandWhenAvailable()
    {
        if (!WaylandWindow.IsAvailable())
        {
            Skip.Test("No Wayland compositor is available.");
        }

        using var window = LinuxWindows.Open(RenderBackendPreference.Gpu, new RecordingAccessibilityBridge(), out var system);

        await Assert.That(system).IsEqualTo(LinuxWindowSystem.Wayland);
    }
}
