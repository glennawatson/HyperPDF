// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Head.Linux;
using HyperPdf.Head.Linux.X11;
using HyperPdf.ViewModels.Services;
using HyperPdf.Windowing.Rendering;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>Files dragged from a file manager and dropped on the X11 window arrive as a list of paths, over the drag and drop protocol.</summary>
[NotInParallel]
public sealed class X11FileDropTests
{
    /// <summary>The longest the test waits for the server, in seconds.</summary>
    private const double TimeoutSeconds = 20;

    /// <summary>The wait between looks for the server's socket, in milliseconds.</summary>
    private const int LookMilliseconds = 20;

    /// <summary>The first display number tried for the private X server.</summary>
    private const int FirstDisplay = 90;

    /// <summary>The number of display numbers tried.</summary>
    private const int DisplayChoices = 10;

    /// <summary>The place where the X server makes its socket.</summary>
    private const string SocketFolder = "/tmp/.X11-unix";

    /// <summary>The first file dropped.</summary>
    private const string FirstPath = "/tmp/hyperpdf drop/first.pdf";

    /// <summary>The second file dropped.</summary>
    private const string SecondPath = "/tmp/hyperpdf drop/second.pdf";

    /// <summary>The number of files the source offers.</summary>
    private const int DropCount = 2;

    /// <summary>A list of addresses becomes the paths of the files in it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddressListBecomesPaths()
    {
        var paths = FileUris.PathsOf("# a comment\r\nfile:///tmp/a%20b.pdf\r\nhttps://example.com/c.pdf\r\n");
        await Assert.That(paths.Length).IsEqualTo(1);
        await Assert.That(paths[0]).IsEqualTo("/tmp/a b.pdf");
    }

    /// <summary>A drop over the window asks the source for the files, and the window publishes them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DroppedFilesArriveFromTheSource()
    {
        if (!OperatingSystem.IsLinux() || FindServer() is not { } program)
        {
            Skip.Test("No X server program is available.");
            return;
        }

        var display = Enumerable.Range(FirstDisplay, DisplayChoices).First(static number => !File.Exists($"{SocketFolder}/X{number}"));
        var name = $":{display}";
        using var server = Process.Start(new ProcessStartInfo(program, [name, "-screen", "0", "640x480x24", "-nolisten", "tcp"]) { UseShellExecute = false });
        string[] received = [];
        try
        {
            var started = Stopwatch.GetTimestamp();
            while (!File.Exists($"{SocketFolder}/X{display}") && Stopwatch.GetElapsedTime(started).TotalSeconds < TimeoutSeconds)
            {
                await Task.Delay(LookMilliseconds);
            }

            using var window = new X11Window(RenderBackendPreference.Cpu, new RecordingAccessibilityBridge(), null, name);
            using var following = window.FileDrops.SubscribeSafe(files => received = [.. files], ErrorTrace.Write);
            window.Show();
            X11DragSource.Drag(name, window, () => received.Length > 0);
        }
        finally
        {
            server?.Kill();
        }

        await Assert.That(received.Length).IsEqualTo(DropCount);
        await Assert.That(received[0]).IsEqualTo(FirstPath);
        await Assert.That(received[1]).IsEqualTo(SecondPath);
    }

    /// <summary>Finds an X server program on the path.</summary>
    /// <returns>The program, or <see langword="null"/> when there is none.</returns>
    private static string? FindServer() => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(':', StringSplitOptions.RemoveEmptyEntries)
        .Select(static folder => Path.Combine(folder, "Xvfb"))
        .FirstOrDefault(File.Exists);
}
