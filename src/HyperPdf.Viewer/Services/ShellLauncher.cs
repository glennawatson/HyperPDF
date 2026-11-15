// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Services;

namespace HyperPdf.Viewer.Services;

/// <summary>
/// Hands things to the desktop: a web or mail address to the app that opens it, a file to the app it belongs to, and documents to
/// a new viewer window. The desktop's own launcher does the work on every system, through the shell start of a process.
/// </summary>
public static class ShellLauncher
{
    /// <summary>Opens a web or mail address in the app the desktop uses for it. Other kinds of address are not opened, as a document could name any program.</summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> when the desktop took the address.</returns>
    public static bool OpenLink(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsAbsoluteUri && address.Scheme is "http" or "https" or "mailto" && Launch(address.AbsoluteUri);
    }

    /// <summary>Opens a file in the app the desktop uses for it.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when the file exists and the desktop took it.</returns>
    public static bool OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return File.Exists(path) && Launch(Path.GetFullPath(path));
    }

    /// <summary>Starts another viewer window on some documents. Each window is its own process, so one window cannot take another down with it.</summary>
    /// <param name="paths">The documents the new window opens, or none for an empty window.</param>
    /// <returns><see langword="true"/> when the new window was started.</returns>
    public static bool OpenWindow(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (Environment.ProcessPath is not { Length: > 0 } viewer)
        {
            return false;
        }

        var start = new ProcessStartInfo(viewer) { UseShellExecute = false };
        foreach (var path in paths)
        {
            start.ArgumentList.Add(path);
        }

        return Start(start);
    }

    /// <summary>Hands a target to the desktop's launcher.</summary>
    /// <param name="target">The address or file path.</param>
    /// <returns><see langword="true"/> when the desktop took it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Launch(string target) => Start(new(target) { UseShellExecute = true });

    /// <summary>Starts a process and lets it go.</summary>
    /// <param name="start">The program and its arguments.</param>
    /// <returns><see langword="true"/> when the process started.</returns>
    private static bool Start(ProcessStartInfo start)
    {
        try
        {
            using var process = Process.Start(start);
            return true;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // The desktop has no app for the target, or no launcher at all. The viewer carries on without it.
            ErrorTrace.Write(error);
            return false;
        }
    }
}
