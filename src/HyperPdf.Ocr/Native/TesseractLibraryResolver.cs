// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Native;

/// <summary>
/// Finds Tesseract. The copy shipped with the app comes first: beside the app when published, or in
/// <c>runtimes/&lt;rid&gt;/native</c> when run from a build. A system copy is used only when no shipped copy loads.
/// </summary>
internal static class TesseractLibraryResolver
{
    /// <summary>The file name the shipped library has for this operating system.</summary>
    private static readonly string BundledName = GetBundledName();

    /// <summary>The system file names tried, most specific first.</summary>
    private static readonly string[] SystemCandidates = GetSystemCandidates();

    /// <summary>The loaded library, or zero until it is found.</summary>
    private static nint _library;

    /// <summary>Gets the path or name the library was loaded from, or <see langword="null"/> before it loads.</summary>
    internal static string? LoadedFrom { get; private set; }

    /// <summary>Gets the folders the shipped library may be in, most likely first.</summary>
    internal static string[] BundledDirectories { get; } =
    [
        AppContext.BaseDirectory,
        Path.Combine(AppContext.BaseDirectory, "runtimes", PortableRuntimeIdentifier(), "native"),
    ];

    /// <summary>Installs the resolver for this assembly. Safe to call more than once.</summary>
    internal static void Install()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(TesseractLibraryResolver).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver is already installed for this assembly.
        }
    }

    /// <summary>Determines whether Tesseract can be loaded.</summary>
    /// <returns><see langword="true"/> when it was found.</returns>
    internal static bool TryLoad() => Load() != 0;

    /// <summary>Finds the shipped library without loading it.</summary>
    /// <returns>Its path, or <see langword="null"/> when the app ships none for this runtime.</returns>
    internal static string? FindBundled()
    {
        foreach (var directory in BundledDirectories)
        {
            var path = Path.Combine(directory, BundledName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>Gets the runtime identifier the shipped packages use, for example <c>linux-x64</c>.</summary>
    /// <returns>The runtime identifier.</returns>
    internal static string PortableRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        if (OperatingSystem.IsWindows())
        {
            return $"win-{architecture}";
        }

        return OperatingSystem.IsMacOS() ? $"osx-{architecture}" : $"linux-{architecture}";
    }

    /// <summary>Gets the shipped library's file name for this operating system, the plain name the packages also use.</summary>
    /// <returns>The name.</returns>
    private static string GetBundledName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "tesseract.dll";
        }

        return OperatingSystem.IsMacOS() ? "libtesseract.dylib" : "libtesseract.so";
    }

    /// <summary>Gets the system library names for this operating system.</summary>
    /// <returns>The names, most specific first.</returns>
    private static string[] GetSystemCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            // The UB Mannheim installer, the usual Windows build, puts libtesseract-5.dll in its program folder.
            return ["libtesseract-5.dll", "tesseract55.dll", "tesseract53.dll", "tesseract50.dll", @"C:\Program Files\Tesseract-OCR\libtesseract-5.dll"];
        }

        // Homebrew installs outside the default library path: /opt/homebrew on Apple silicon, /usr/local on Intel.
        // Tesseract's autotools build names the library libtesseract.so.5 (Debian, Ubuntu); its CMake build adds the
        // minor version, as in Fedora's libtesseract.so.5.5. The unversioned name needs the development package.
        return OperatingSystem.IsMacOS()
            ? ["libtesseract.5.dylib", "libtesseract.dylib", "/opt/homebrew/lib/libtesseract.5.dylib", "/usr/local/lib/libtesseract.5.dylib"]
            : ["libtesseract.so.5", "libtesseract.so.5.5", "libtesseract.so.5.4", "libtesseract.so.5.3", "libtesseract.so.5.2", "libtesseract.so.5.1", "libtesseract.so.5.0", "libtesseract.so"];
    }

    /// <summary>Resolves the Tesseract library.</summary>
    /// <param name="libraryName">The requested library.</param>
    /// <param name="assembly">The requesting assembly.</param>
    /// <param name="searchPath">The search path flags.</param>
    /// <returns>The library handle, or zero to fall back to the default probing.</returns>
    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == NativeMethods.Library ? Load() : 0;

    /// <summary>Loads the library, reusing it once loaded.</summary>
    /// <returns>The handle, or zero.</returns>
    private static nint Load()
    {
        var loaded = Volatile.Read(ref _library);
        if (loaded != 0)
        {
            return loaded;
        }

        // A missing library is looked for again next time rather than remembered as missing.
        loaded = LoadBundled();
        if (loaded == 0)
        {
            loaded = LoadSystem();
        }

        Volatile.Write(ref _library, loaded);
        return loaded;
    }

    /// <summary>Loads the shipped library.</summary>
    /// <returns>The handle, or zero when no shipped copy loads.</returns>
    private static nint LoadBundled()
    {
        if (FindBundled() is not { } path)
        {
            return 0;
        }

        if (!NativeLibrary.TryLoad(path, out var handle))
        {
            return 0;
        }

        LoadedFrom = path;
        return handle;
    }

    /// <summary>Loads a system copy of the library.</summary>
    /// <returns>The handle, or zero when none is installed.</returns>
    private static nint LoadSystem()
    {
        foreach (var candidate in SystemCandidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var handle))
            {
                continue;
            }

            LoadedFrom = candidate;
            return handle;
        }

        return 0;
    }
}
