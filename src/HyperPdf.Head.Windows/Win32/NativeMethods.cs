// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Source generated entry points of user32, gdi32 and kernel32 for the window head.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The user32 library.</summary>
    private const string User32 = "user32";

    /// <summary>The gdi32 library.</summary>
    private const string Gdi32 = "gdi32";

    /// <summary>The kernel32 library.</summary>
    private const string Kernel32 = "kernel32";

    /// <summary>Native <c>GetKeyState</c>.</summary>
    /// <param name="key">The virtual key code.</param>
    /// <returns>The key state, with the top bit set when the key is down.</returns>
    [LibraryImport(User32, EntryPoint = "GetKeyState")]
    internal static partial short GetKeyState(int key);

    /// <summary>Native <c>GetModuleHandleW</c>.</summary>
    /// <param name="name">The module name, or null for the executable.</param>
    /// <returns>The module handle.</returns>
    [LibraryImport(Kernel32, EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? name);

    /// <summary>Native <c>RegisterClassExW</c>.</summary>
    /// <param name="windowClass">The WNDCLASSEXW.</param>
    /// <returns>The class atom, or 0 on failure.</returns>
    [LibraryImport(User32, EntryPoint = "RegisterClassExW")]
    internal static partial ushort RegisterClass(byte* windowClass);

    /// <summary>Native <c>CreateWindowExW</c>.</summary>
    /// <param name="exStyle">The extended style.</param>
    /// <param name="className">The class name.</param>
    /// <param name="title">The title.</param>
    /// <param name="style">The style.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="parent">The parent window.</param>
    /// <param name="menu">The menu.</param>
    /// <param name="instance">The module handle.</param>
    /// <param name="parameter">A value handed to the window procedure at creation.</param>
    /// <returns>The window handle, or 0 on failure.</returns>
    [LibraryImport(User32, EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindow(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    /// <summary>Native <c>DestroyWindow</c>.</summary>
    /// <param name="window">The window.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "DestroyWindow")]
    internal static partial int DestroyWindow(nint window);

    /// <summary>Native <c>ShowWindow</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="command">The show command.</param>
    /// <returns>Nonzero when it was shown before.</returns>
    [LibraryImport(User32, EntryPoint = "ShowWindow")]
    internal static partial int ShowWindow(nint window, int command);

    /// <summary>Native <c>SetWindowTextW</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="title">The title.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetWindowText(nint window, string title);

    /// <summary>Native <c>DefWindowProcW</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <returns>The result.</returns>
    [LibraryImport(User32, EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint window, uint message, nuint primary, nint secondary);

    /// <summary>Native <c>PeekMessageW</c>.</summary>
    /// <param name="message">Receives the MSG.</param>
    /// <param name="window">The window, or 0 for any of the thread.</param>
    /// <param name="min">The first message.</param>
    /// <param name="max">The last message.</param>
    /// <param name="remove">1 to remove the message.</param>
    /// <returns>Nonzero when a message was available.</returns>
    [LibraryImport(User32, EntryPoint = "PeekMessageW")]
    internal static partial int PeekMessage(byte* message, nint window, uint min, uint max, uint remove);

    /// <summary>Native <c>MsgWaitForMultipleObjectsEx</c> with no handles, to wait for a message.</summary>
    /// <param name="count">The handle count, 0.</param>
    /// <param name="handles">The handles, null.</param>
    /// <param name="milliseconds">The longest wait.</param>
    /// <param name="wakeMask">The message kinds that wake the wait.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The wait result.</returns>
    [LibraryImport(User32, EntryPoint = "MsgWaitForMultipleObjectsEx")]
    internal static partial uint WaitForMessage(uint count, nint* handles, uint milliseconds, uint wakeMask, uint flags);

    /// <summary>Native <c>TranslateMessage</c>.</summary>
    /// <param name="message">The MSG.</param>
    /// <returns>Nonzero when translated.</returns>
    [LibraryImport(User32, EntryPoint = "TranslateMessage")]
    internal static partial int TranslateMessage(byte* message);

    /// <summary>Native <c>DispatchMessageW</c>.</summary>
    /// <param name="message">The MSG.</param>
    /// <returns>The window procedure result.</returns>
    [LibraryImport(User32, EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(byte* message);

    /// <summary>Native <c>SetWindowLongPtrW</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="index">The slot, -21 for user data.</param>
    /// <param name="value">The value.</param>
    /// <returns>The old value.</returns>
    [LibraryImport(User32, EntryPoint = "SetWindowLongPtrW")]
    internal static partial nint SetWindowLongPtr(nint window, int index, nint value);

    /// <summary>Native <c>GetWindowLongPtrW</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="index">The slot.</param>
    /// <returns>The value.</returns>
    [LibraryImport(User32, EntryPoint = "GetWindowLongPtrW")]
    internal static partial nint GetWindowLongPtr(nint window, int index);

    /// <summary>Native <c>LoadCursorW</c>.</summary>
    /// <param name="instance">The module, 0 for system cursors.</param>
    /// <param name="cursor">The system cursor id.</param>
    /// <returns>The cursor.</returns>
    [LibraryImport(User32, EntryPoint = "LoadCursorW")]
    internal static partial nint LoadCursor(nint instance, nint cursor);

    /// <summary>Native <c>SetCursor</c>.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>The previous cursor.</returns>
    [LibraryImport(User32, EntryPoint = "SetCursor")]
    internal static partial nint SetCursor(nint cursor);

    /// <summary>Native <c>ClientToScreen</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">A POINT in client pixels on entry and in screen pixels on return.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "ClientToScreen")]
    internal static partial int ClientToScreen(nint window, int* point);

    /// <summary>Native <c>GetClientRect</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="rect">Receives left, top, right, bottom as four ints.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "GetClientRect")]
    internal static partial int GetClientRect(nint window, int* rect);

    /// <summary>Native <c>GetDC</c>.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The device context.</returns>
    [LibraryImport(User32, EntryPoint = "GetDC")]
    internal static partial nint GetDeviceContext(nint window);

    /// <summary>Native <c>ReleaseDC</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>1 when released.</returns>
    [LibraryImport(User32, EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDeviceContext(nint window, nint deviceContext);

    /// <summary>Native <c>InvalidateRect</c>, which may be called from any thread.</summary>
    /// <param name="window">The window.</param>
    /// <param name="rect">The area, or null for all.</param>
    /// <param name="erase">Nonzero to erase the background first.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "InvalidateRect")]
    internal static partial int InvalidateRect(nint window, int* rect, int erase);

    /// <summary>Native <c>PostMessageW</c>, which may be called from any thread.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message number.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "PostMessageW")]
    internal static partial int PostMessage(nint window, uint message, nuint primary, nint secondary);

    /// <summary>Native <c>ValidateRect</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="rect">The area, or null for all.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "ValidateRect")]
    internal static partial int ValidateRect(nint window, int* rect);

    /// <summary>Native <c>SetDIBitsToDevice</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <param name="x">The destination left edge.</param>
    /// <param name="y">The destination top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="sourceX">The source left edge.</param>
    /// <param name="sourceY">The source top edge.</param>
    /// <param name="startScan">The first scan line.</param>
    /// <param name="scanLines">The number of scan lines.</param>
    /// <param name="bits">The pixels.</param>
    /// <param name="info">The BITMAPINFO.</param>
    /// <param name="usage">0 for RGB colours.</param>
    /// <returns>The scan lines copied.</returns>
    [LibraryImport(Gdi32, EntryPoint = "SetDIBitsToDevice")]
    internal static partial int SetDIBitsToDevice(
        nint deviceContext,
        int x,
        int y,
        uint width,
        uint height,
        int sourceX,
        int sourceY,
        uint startScan,
        uint scanLines,
        byte* bits,
        byte* info,
        uint usage);
}
