// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Platform;

/// <summary>
/// Finds optional native libraries, such as libcups or libpulse-simple, by their file names on each operating system.
/// .NET allows one import resolver per assembly, so every library an assembly imports is registered here and resolved
/// by a single resolver; libraries not registered use the default probing.
/// </summary>
public static class NativeLibraries
{
    /// <summary>The registered libraries of each assembly.</summary>
    private static readonly Dictionary<Assembly, Dictionary<string, string[]>> Registered = [];

    /// <summary>Guards <see cref="Registered"/>.</summary>
    private static readonly Lock Gate = new();

    /// <summary>Registers the file names to try for a library an assembly imports. Safe to call more than once.</summary>
    /// <param name="assembly">The importing assembly.</param>
    /// <param name="library">The library name used in its imports.</param>
    /// <param name="candidates">The file names to try, most specific first; bare names use the system search path.</param>
    public static void Register(Assembly assembly, string library, string[] candidates)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(candidates);
        lock (Gate)
        {
            var install = !Registered.TryGetValue(assembly, out var libraries);
            if (install)
            {
                libraries = [];
                Registered[assembly] = libraries;
            }

            libraries![library] = candidates;

            // A second caller waits for the lock, so it returns only after the resolver is installed and its first import resolves.
            if (install)
            {
                InstallResolver(assembly);
            }
        }
    }

    /// <summary>Determines whether a registered library can be loaded.</summary>
    /// <param name="assembly">The importing assembly.</param>
    /// <param name="library">The library name.</param>
    /// <returns><see langword="true"/> when one of its candidates loaded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryLoad(Assembly assembly, string library) => Load(assembly, library) != 0;

    /// <summary>Installs the single resolver for an assembly.</summary>
    /// <param name="assembly">The importing assembly.</param>
    private static void InstallResolver(Assembly assembly)
    {
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // The assembly installed its own resolver.
        }
    }

    /// <summary>Resolves a library for an assembly.</summary>
    /// <param name="library">The requested library.</param>
    /// <param name="assembly">The requesting assembly.</param>
    /// <param name="searchPath">The search path flags.</param>
    /// <returns>The handle, or zero for the default probing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Resolve(string library, Assembly assembly, DllImportSearchPath? searchPath) => Load(assembly, library);

    /// <summary>Loads the first candidate found, beside the application first, then from the system.</summary>
    /// <param name="assembly">The importing assembly.</param>
    /// <param name="library">The library name.</param>
    /// <returns>The handle, or zero.</returns>
    private static nint Load(Assembly assembly, string library)
    {
        string[]? candidates;
        lock (Gate)
        {
            candidates = Registered.TryGetValue(assembly, out var libraries) && libraries.TryGetValue(library, out var found) ? found : null;
        }

        if (candidates is null)
        {
            return 0;
        }

        foreach (var candidate in candidates)
        {
            if (!Path.IsPathRooted(candidate) && NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, candidate), out var local))
            {
                return local;
            }

            if (NativeLibrary.TryLoad(candidate, out var system))
            {
                return system;
            }
        }

        return 0;
    }
}
