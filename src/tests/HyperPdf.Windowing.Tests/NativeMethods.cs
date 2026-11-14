// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace HyperPdf.Windowing.Tests;

/// <summary>Entry points of the UI Automation core library that the test client uses.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>The UI Automation core library.</summary>
    private const string UiaCore = "uiautomationcore";

    /// <summary>The user interface library.</summary>
    private const string User32 = "user32";

    /// <summary>The kernel library.</summary>
    private const string Kernel32 = "kernel32";

    /// <summary>Native <c>UiaNodeFromHandle</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="node">Receives the node.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaNodeFromHandle")]
    internal static partial int NodeFromHandle(nint window, out nint node);

    /// <summary>Native <c>UiaGetPropertyValue</c>.</summary>
    /// <param name="node">The node.</param>
    /// <param name="propertyId">The property id.</param>
    /// <param name="value">Receives the VARIANT.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaGetPropertyValue")]
    internal static partial int GetPropertyValue(nint node, int propertyId, byte* value);

    /// <summary>Native <c>UiaNodeRelease</c>.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Nonzero when released.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaNodeRelease")]
    internal static partial int NodeRelease(nint node);

    /// <summary>Native <c>PostMessageW</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message number.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "PostMessageW")]
    internal static partial int PostMessage(nint window, uint message, nuint primary, nint secondary);

    /// <summary>Native <c>OpenClipboard</c>.</summary>
    /// <param name="window">The window that opens the clipboard.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "OpenClipboard")]
    internal static partial int OpenClipboard(nint window);

    /// <summary>Native <c>GetClipboardData</c>.</summary>
    /// <param name="format">The clipboard format.</param>
    /// <returns>The memory handle of the data, or 0 when the clipboard has none in that format.</returns>
    [LibraryImport(User32, EntryPoint = "GetClipboardData")]
    internal static partial nint GetClipboardData(uint format);

    /// <summary>Native <c>CloseClipboard</c>.</summary>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "CloseClipboard")]
    internal static partial int CloseClipboard();

    /// <summary>Native <c>GlobalLock</c>.</summary>
    /// <param name="memory">The memory handle.</param>
    /// <returns>The address of the memory, or 0 on failure.</returns>
    [LibraryImport(Kernel32, EntryPoint = "GlobalLock")]
    internal static partial nint GlobalLock(nint memory);

    /// <summary>Native <c>GlobalUnlock</c>.</summary>
    /// <param name="memory">The memory handle.</param>
    /// <returns>Nonzero while the memory is still locked.</returns>
    [LibraryImport(Kernel32, EntryPoint = "GlobalUnlock")]
    internal static partial int GlobalUnlock(nint memory);
}
