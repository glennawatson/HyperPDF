// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Windows.Win32;
using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Opens a real Win32 window. The tests skip on other operating systems.</summary>
public sealed class Win32WindowTests
{
    /// <summary>The white used to clear the frame.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>How long to wait for events in the test, in milliseconds.</summary>
    private const int WaitMilliseconds = 100;

    /// <summary>A window opens, shows a frame, handles messages and closes cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowOpensPresentsAFrameAndCloses()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Win32 windows only run on Windows.");
            return;
        }

        var bridge = new RecordingAccessibilityBridge();
        using (var window = new Win32Window(RenderBackendPreference.Gpu, bridge))
        {
            window.SetTitle("HyperPDF window test");
            window.SetCursor(ViewerCursor.Hand);
            window.Show();
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
}
