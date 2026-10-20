// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>Finds the FFmpeg programs on the computer.</summary>
public static class MediaToolLocator
{
    /// <summary>The name of the decoder program.</summary>
    private const string FfmpegName = "ffmpeg";

    /// <summary>The name of the sound player program.</summary>
    private const string FfplayName = "ffplay";

    /// <summary>The name of the probe program.</summary>
    private const string FfprobeName = "ffprobe";

    /// <summary>The ending of a program's file name on Windows.</summary>
    private const string WindowsEnding = ".exe";

    /// <summary>Gets the name of the environment variable that names a folder to look in before the search path.</summary>
    public static string DirectoryVariable => "HYPERPDF_FFMPEG";

    /// <summary>Finds the programs using the <c>PATH</c> variable and the <c>HYPERPDF_FFMPEG</c> folder.</summary>
    /// <returns>The programs, or <see langword="null"/> when one of them is missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MediaTools? Find() =>
        Find(Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable(DirectoryVariable));

    /// <summary>Finds the programs in a folder and a search path.</summary>
    /// <param name="searchPath">A list of folders separated as the operating system does, or <see langword="null"/>.</param>
    /// <param name="preferredFolder">A folder searched first, or <see langword="null"/>.</param>
    /// <returns>The programs, or <see langword="null"/> when one of them is missing.</returns>
    public static MediaTools? Find(string? searchPath, string? preferredFolder)
    {
        var folders = new List<string>();
        if (!string.IsNullOrEmpty(preferredFolder))
        {
            folders.Add(preferredFolder);
        }

        if (!string.IsNullOrEmpty(searchPath))
        {
            folders.AddRange(searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        return Locate(folders, FfmpegName) is { } ffmpeg && Locate(folders, FfplayName) is { } ffplay && Locate(folders, FfprobeName) is { } ffprobe
            ? new MediaTools(ffmpeg, ffplay, ffprobe)
            : null;
    }

    /// <summary>Finds one program in the folders.</summary>
    /// <param name="folders">The folders to look in, in order.</param>
    /// <param name="name">The program's name without an ending.</param>
    /// <returns>The path of the program, or <see langword="null"/> when no folder holds it.</returns>
    private static string? Locate(List<string> folders, string name)
    {
        var file = OperatingSystem.IsWindows() ? name + WindowsEnding : name;
        foreach (var folder in folders)
        {
            var candidate = Path.Combine(folder, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
