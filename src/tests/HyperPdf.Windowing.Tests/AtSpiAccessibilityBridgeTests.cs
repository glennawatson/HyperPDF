// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the AT-SPI bridge starts and stops cleanly. The test skips when the session has no accessibility bus.</summary>
public sealed class AtSpiAccessibilityBridgeTests
{
    /// <summary>The longest the test waits for the server to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How often the test checks whether the server has started.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>The bridge starts serving the tree, reports itself active, and stops on dispose.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BridgeServesTheTreeAndStops()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test("AT-SPI only runs on Linux.");
            return;
        }

        var bridge = new AtSpiAccessibilityBridge();
        bridge.Attach(new FakeAccessibleTree());
        using var timeout = new CancellationTokenSource(StartTimeout);
        using var timer = new PeriodicTimer(CheckInterval);
        try
        {
            while (!bridge.IsAssistiveTechnologyActive)
            {
                _ = await timer.WaitForNextTickAsync(timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            bridge.Dispose();
            Skip.Test("This session has no AT-SPI accessibility bus.");
            return;
        }

        await Assert.That(bridge.IsAssistiveTechnologyActive).IsTrue();
        bridge.Dispose();
        await Assert.That(bridge.IsAssistiveTechnologyActive).IsFalse();
    }
}
