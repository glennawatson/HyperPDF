// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Media;

namespace HyperPdf.Media.Tests;

/// <summary>A folder holding short clips made with the FFmpeg program, so no media file is stored with the tests.</summary>
internal sealed class ClipFolder : IDisposable
{
    /// <summary>The captions of the captioned clip.</summary>
    private const string Captions = "1\n00:00:00,000 --> 00:00:01,000\nHello\n\n2\n00:00:01,000 --> 00:00:03,000\nWorld\n";

    /// <summary>Initializes a new instance of the <see cref="ClipFolder"/> class.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="tools">The FFmpeg programs used to make the clips.</param>
    private ClipFolder(string directory, MediaTools tools)
    {
        Directory = directory;
        Tools = tools;
    }

    /// <summary>Gets the folder.</summary>
    internal string Directory { get; }

    /// <summary>Gets the FFmpeg programs.</summary>
    internal MediaTools Tools { get; }

    /// <inheritdoc />
    public void Dispose() => System.IO.Directory.Delete(Directory, true);

    /// <summary>Makes a folder, skipping the test when the FFmpeg program or the FFmpeg libraries are missing.</summary>
    /// <returns>The folder.</returns>
    internal static ClipFolder Create()
    {
        if (!FfmpegLibraries.IsAvailable)
        {
            Skip.Test($"The FFmpeg libraries were not found; put them in the folder named by {FfmpegLibraries.DirectoryVariable}.");
        }

        var tools = MediaToolLocator.Find();
        if (tools is null)
        {
            Skip.Test("The ffmpeg program is not installed, so the test clips cannot be made.");
        }

        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clips-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(directory);
        return new(directory, tools!);
    }

    /// <summary>Makes a clip of three seconds with a picture of 160 by 120 pixels at ten pictures a second, silence, and optionally captions.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="withSound">Whether to add sound.</param>
    /// <param name="withPicture">Whether to add a picture.</param>
    /// <param name="withCaptions">Whether to add a caption stream.</param>
    /// <returns>The full path of the clip.</returns>
    internal async Task<string> MakeAsync(string name, bool withSound, bool withPicture, bool withCaptions)
    {
        var path = System.IO.Path.Combine(Directory, name);
        var inputs = new List<string>();
        if (withPicture)
        {
            inputs.AddRange(["-f", "lavfi", "-i", "testsrc=duration=3:size=160x120:rate=10"]);
        }

        if (withSound)
        {
            inputs.AddRange(["-f", "lavfi", "-i", "sine=frequency=440:duration=3,volume=0"]);
        }

        var codecs = new List<string>();
        if (withPicture)
        {
            codecs.AddRange(["-c:v", "mpeg4"]);
        }

        if (withSound)
        {
            codecs.AddRange(["-c:a", "aac"]);
        }

        var plain = withCaptions ? System.IO.Path.Combine(Directory, $"plain-{name}") : path;
        await RunAsync([.. new[] { "-v", "error" }, .. inputs, .. codecs, "-y", plain]);
        if (withCaptions)
        {
            var srt = System.IO.Path.Combine(Directory, "captions.srt");
            await File.WriteAllTextAsync(srt, Captions);
            await RunAsync(["-v", "error", "-i", plain, "-i", srt, "-map", "0", "-map", "1", "-c", "copy", "-c:s", "mov_text", "-y", path]);
        }

        return path;
    }

    /// <summary>Runs the FFmpeg program to its end.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(string[] arguments)
    {
        var start = new ProcessStartInfo(Tools.Ffmpeg) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        _ = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
    }
}
