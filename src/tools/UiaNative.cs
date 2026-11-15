// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Scripts;

/// <summary>The operating system calls the UI Automation test tool makes: finding a process's top-level window.</summary>
internal static partial class UiaNative
{
    /// <summary>Calls back for each top-level window.</summary>
    /// <param name="handle">The window.</param>
    /// <param name="parameter">The caller's value.</param>
    /// <returns><see langword="true"/> to go on with the next window.</returns>
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool WindowCallback(IntPtr handle, IntPtr parameter);

    /// <summary>Finds a visible top-level window of a process; the hidden helper windows a process makes before its own are skipped.</summary>
    /// <param name="processId">The process.</param>
    /// <returns>The window handle, or zero when the process has none.</returns>
    internal static IntPtr FindWindowOf(int processId)
    {
        var found = IntPtr.Zero;
        _ = EnumWindows(
            (handle, _) =>
            {
                if (GetWindowThreadProcessId(handle, out var owner) == 0 || owner != processId || !IsWindowVisible(handle))
                {
                    return true;
                }

                found = handle;
                return false;
            },
            IntPtr.Zero);
        return found;
    }

    /// <summary>Calls back for each top-level window.</summary>
    /// <param name="callback">What to call.</param>
    /// <param name="parameter">A value to hand back.</param>
    /// <returns><see langword="true"/> when the list was walked.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(WindowCallback callback, IntPtr parameter);

    /// <summary>Gets the process a window belongs to.</summary>
    /// <param name="window">The window.</param>
    /// <param name="processId">Receives the process.</param>
    /// <returns>The thread that made the window, or zero when the window is gone.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out int processId);

    /// <summary>Tells whether a window is shown.</summary>
    /// <param name="window">The window.</param>
    /// <returns><see langword="true"/> when it is visible.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr window);
}
