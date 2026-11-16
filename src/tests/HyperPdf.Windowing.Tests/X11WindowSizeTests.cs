// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Head.Linux.X11;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>A window opens at the size the reader had it when it was last closed.</summary>
[NotInParallel]
public sealed class X11WindowSizeTests
{
    /// <summary>The longest the test waits for the server, in seconds.</summary>
    private const double TimeoutSeconds = 20;

    /// <summary>The longest one wait for window events lasts, in milliseconds.</summary>
    private const int PumpMilliseconds = 50;

    /// <summary>The width asked for, in logical units.</summary>
    private const int WantedWidth = 700;

    /// <summary>The height asked for, in logical units.</summary>
    private const int WantedHeight = 500;

    /// <summary>A window asked for a size takes it, once the server reports the change back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowTakesTheSizeItIsAskedFor()
    {
        if (!OperatingSystem.IsLinux() || FindServer() is not { } program)
        {
            Skip.Test("No X server program is available.");
            return;
        }

        var start = new ProcessStartInfo(program, ["-displayfd", "1", "-screen", "0", "1280x800x24", "-nolisten", "tcp"]) { UseShellExecute = false, RedirectStandardOutput = true };
        using var server = Process.Start(start);
        var width = 0;
        var height = 0;
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var line = await server!.StandardOutput.ReadLineAsync(limit.Token);
            using var window = new X11Window(RenderBackendPreference.Cpu, new RecordingAccessibilityBridge(), null, $":{line?.Trim()}");
            window.Show();
            window.SetSize(WantedWidth, WantedHeight);

            // The server answers with a configure event, which the window takes while it pumps its events.
            var deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * TimeoutSeconds);
            while (window.ClientSize.Width != (int)Math.Round(WantedWidth * window.Scale) && Stopwatch.GetTimestamp() < deadline)
            {
                window.PumpEvents(PumpMilliseconds);
            }

            width = (int)Math.Round(window.ClientSize.Width / window.Scale);
            height = (int)Math.Round(window.ClientSize.Height / window.Scale);
        }
        finally
        {
            server?.Kill();
        }

        await Assert.That(width).IsEqualTo(WantedWidth);
        await Assert.That(height).IsEqualTo(WantedHeight);
    }

    /// <summary>Finds an X server program on the path.</summary>
    /// <returns>The program, or <see langword="null"/> when there is none.</returns>
    private static string? FindServer() => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(':', StringSplitOptions.RemoveEmptyEntries)
        .Select(static folder => Path.Combine(folder, "Xvfb"))
        .FirstOrDefault(File.Exists);
}
