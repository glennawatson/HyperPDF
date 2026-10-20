// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using HyperPdf.Core.Platform;

namespace HyperPdf.Media;

/// <summary>
/// Finds and loads the FFmpeg libraries that ship with the app, in a folder named <c>ffmpeg</c> beside it. A folder named by the
/// <c>HYPERPDF_FFMPEG_LIBS</c> variable is tried first, then the system. A library is used only when its major version is the one
/// the bindings were made for, because the layout of FFmpeg's structures changes between major versions.
/// </summary>
public static class FfmpegLibraries
{
    /// <summary>The folder beside the app that holds the libraries.</summary>
    private const string BundledFolder = "ffmpeg";

    /// <summary>The bits that hold the major number of a library version.</summary>
    private const int MajorShift = 16;

    /// <summary>The libraries, in the order they must load: each one's dependencies come before it.</summary>
    private static readonly LibraryName[] Libraries =
    [
        new("avutil-60", "avutil", 60),
        new("swresample-6", "swresample", 6),
        new("avcodec-62", "avcodec", 62),
        new("avformat-62", "avformat", 62),
        new("swscale-9", "swscale", 9),
    ];

    /// <summary>Whether the libraries loaded, worked out on first use.</summary>
    private static readonly Lazy<bool> Loaded = new(Load);

    /// <summary>Gets the name of the environment variable that names a folder of FFmpeg libraries to try first.</summary>
    public static string DirectoryVariable => "HYPERPDF_FFMPEG_LIBS";

    /// <summary>
    /// Gets a value indicating whether the libraries are present beside the app or in the folder the variable names. It looks for the files
    /// only; <see cref="IsAvailable"/> loads them.
    /// </summary>
    public static bool IsPresent => Array.TrueForAll(Libraries, Exists);

    /// <summary>Gets a value indicating whether the FFmpeg libraries were found, loaded and have the major versions the bindings need.</summary>
    public static bool IsAvailable => Loaded.Value;

    /// <summary>Gets the file name a library has on this operating system.</summary>
    /// <param name="library">The library.</param>
    /// <returns>The file name.</returns>
    private static string FileName(LibraryName library)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{library.Import}.dll";
        }

        return OperatingSystem.IsMacOS() ? $"lib{library.Stem}.{library.Major}.dylib" : $"lib{library.Stem}.so.{library.Major}";
    }

    /// <summary>Tests whether a library is beside the app or in the folder the variable names.</summary>
    /// <param name="library">The library.</param>
    /// <returns><see langword="true"/> when the file is there.</returns>
    private static bool Exists(LibraryName library) =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, BundledFolder, FileName(library)))
        || (Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } folder && File.Exists(Path.Combine(folder, FileName(library))));

    /// <summary>Lists the places a library may be, best first.</summary>
    /// <param name="library">The library.</param>
    /// <returns>The paths and bare names to try.</returns>
    private static string[] Candidates(LibraryName library)
    {
        var name = FileName(library);
        var candidates = new List<string>();
        if (Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } folder)
        {
            candidates.Add(Path.Combine(folder, name));
        }

        candidates.Add(Path.Combine(BundledFolder, name));
        candidates.Add(name);
        return [.. candidates];
    }

    /// <summary>Registers, loads and version-checks the libraries, then binds the functions.</summary>
    /// <returns><see langword="true"/> when everything loaded.</returns>
    private static bool Load()
    {
        var assembly = typeof(DynamicallyLinkedBindings).Assembly;
        foreach (var library in Libraries)
        {
            NativeLibraries.Register(assembly, library.Import, Candidates(library));
            if (!NativeLibraries.TryLoad(assembly, library.Import))
            {
                return false;
            }
        }

        try
        {
            DynamicallyLinkedBindings.Initialize();
            return VersionsMatch();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            // A library that loads but lacks a function the bindings need is not the version they were made for.
            return false;
        }
    }

    /// <summary>Checks the major version of each library.</summary>
    /// <returns><see langword="true"/> when all of them match.</returns>
    private static bool VersionsMatch() =>
        ffmpeg.avutil_version() >> MajorShift == ffmpeg.LIBAVUTIL_VERSION_MAJOR
        && ffmpeg.swresample_version() >> MajorShift == ffmpeg.LIBSWRESAMPLE_VERSION_MAJOR
        && ffmpeg.avcodec_version() >> MajorShift == ffmpeg.LIBAVCODEC_VERSION_MAJOR
        && ffmpeg.avformat_version() >> MajorShift == ffmpeg.LIBAVFORMAT_VERSION_MAJOR
        && ffmpeg.swscale_version() >> MajorShift == ffmpeg.LIBSWSCALE_VERSION_MAJOR;

    /// <summary>A library the bindings import.</summary>
    /// <param name="Import">The name in the bindings' imports.</param>
    /// <param name="Stem">The name without a prefix or version.</param>
    /// <param name="Major">The major version in the file name.</param>
    private readonly record struct LibraryName(string Import, string Stem, int Major);
}
