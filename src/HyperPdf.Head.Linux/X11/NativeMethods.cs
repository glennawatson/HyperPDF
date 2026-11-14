// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Source generated entry points of Xlib (libX11, loaded at run time) for the window head.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name, resolved through <see cref="Core.Platform.NativeLibraries"/>.</summary>
    internal const string Library = "X11";

    /// <summary>Native <c>XOpenDisplay</c>.</summary>
    /// <param name="name">The UTF-8 display name, or null for the default.</param>
    /// <returns>The display, which is invalid when none can be opened.</returns>
    [LibraryImport(Library, EntryPoint = "XOpenDisplay")]
    internal static partial X11DisplayHandle XOpenDisplay(byte* name);

    /// <summary>Native <c>XCloseDisplay</c>, called by the display handle.</summary>
    /// <param name="display">The raw display.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XCloseDisplay")]
    internal static partial int XCloseDisplay(nint display);

    /// <summary>Native <c>XDefaultScreen</c>.</summary>
    /// <param name="display">The display.</param>
    /// <returns>The screen number.</returns>
    [LibraryImport(Library, EntryPoint = "XDefaultScreen")]
    internal static partial int XDefaultScreen(X11DisplayHandle display);

    /// <summary>Native <c>XRootWindow</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="screen">The screen.</param>
    /// <returns>The root window id.</returns>
    [LibraryImport(Library, EntryPoint = "XRootWindow")]
    internal static partial nint XRootWindow(X11DisplayHandle display, int screen);

    /// <summary>Native <c>XDefaultVisual</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="screen">The screen.</param>
    /// <returns>The visual.</returns>
    [LibraryImport(Library, EntryPoint = "XDefaultVisual")]
    internal static partial nint XDefaultVisual(X11DisplayHandle display, int screen);

    /// <summary>Native <c>XDefaultDepth</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="screen">The screen.</param>
    /// <returns>The depth in bits.</returns>
    [LibraryImport(Library, EntryPoint = "XDefaultDepth")]
    internal static partial int XDefaultDepth(X11DisplayHandle display, int screen);

    /// <summary>Native <c>XCreateSimpleWindow</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="parent">The parent window.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="borderWidth">The border width.</param>
    /// <param name="border">The border pixel.</param>
    /// <param name="background">The background pixel.</param>
    /// <returns>The window id.</returns>
    [LibraryImport(Library, EntryPoint = "XCreateSimpleWindow")]
    internal static partial nint XCreateSimpleWindow(X11DisplayHandle display, nint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);

    /// <summary>Native <c>XDestroyWindow</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XDestroyWindow")]
    internal static partial int XDestroyWindow(X11DisplayHandle display, nint window);

    /// <summary>Native <c>XMapWindow</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XMapWindow")]
    internal static partial int XMapWindow(X11DisplayHandle display, nint window);

    /// <summary>Native <c>XSelectInput</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="mask">The event mask.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XSelectInput")]
    internal static partial int XSelectInput(X11DisplayHandle display, nint window, nint mask);

    /// <summary>Native <c>XStoreName</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="name">The UTF-8 title with a terminator.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XStoreName")]
    internal static partial int XStoreName(X11DisplayHandle display, nint window, byte* name);

    /// <summary>Native <c>XInternAtom</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="name">The UTF-8 atom name with a terminator.</param>
    /// <param name="onlyIfExists">Whether to refuse making a new atom.</param>
    /// <returns>The atom.</returns>
    [LibraryImport(Library, EntryPoint = "XInternAtom")]
    internal static partial nint XInternAtom(X11DisplayHandle display, byte* name, int onlyIfExists);

    /// <summary>Native <c>XSetWMProtocols</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="protocols">The atoms.</param>
    /// <param name="count">The atom count.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XSetWMProtocols")]
    internal static partial int XSetWMProtocols(X11DisplayHandle display, nint window, nint* protocols, int count);

    /// <summary>Native <c>XPending</c>.</summary>
    /// <param name="display">The display.</param>
    /// <returns>The number of queued events.</returns>
    [LibraryImport(Library, EntryPoint = "XPending")]
    internal static partial int XPending(X11DisplayHandle display);

    /// <summary>Native <c>XNextEvent</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="xevent">Receives the event, at least 192 bytes.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XNextEvent")]
    internal static partial int XNextEvent(X11DisplayHandle display, byte* xevent);

    /// <summary>Native <c>XCreateGC</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="drawable">The drawable.</param>
    /// <param name="mask">The value mask.</param>
    /// <param name="values">The values, or null.</param>
    /// <returns>The graphics context.</returns>
    [LibraryImport(Library, EntryPoint = "XCreateGC")]
    internal static partial nint XCreateGC(X11DisplayHandle display, nint drawable, nuint mask, void* values);

    /// <summary>Native <c>XFreeGC</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="gc">The graphics context.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XFreeGC")]
    internal static partial int XFreeGC(X11DisplayHandle display, nint gc);

    /// <summary>Native <c>XCreateImage</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="visual">The visual.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="format">2 for ZPixmap.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="data">The pixel memory, which must come from malloc because XDestroyImage frees it.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bitmapPad">The scan line padding in bits.</param>
    /// <param name="bytesPerLine">The bytes per line.</param>
    /// <returns>The image.</returns>
    [LibraryImport(Library, EntryPoint = "XCreateImage")]
    internal static partial nint XCreateImage(X11DisplayHandle display, nint visual, uint depth, int format, int offset, byte* data, uint width, uint height, int bitmapPad, int bytesPerLine);

    /// <summary>Native <c>XPutImage</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="drawable">The drawable.</param>
    /// <param name="gc">The graphics context.</param>
    /// <param name="image">The image.</param>
    /// <param name="sourceX">The source left edge.</param>
    /// <param name="sourceY">The source top edge.</param>
    /// <param name="destinationX">The destination left edge.</param>
    /// <param name="destinationY">The destination top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XPutImage")]
    internal static partial int XPutImage(X11DisplayHandle display, nint drawable, nint gc, nint image, int sourceX, int sourceY, int destinationX, int destinationY, uint width, uint height);

    /// <summary>Native <c>XDestroyImage</c>, which frees the image and its pixel memory.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XDestroyImage")]
    internal static partial int XDestroyImage(nint image);

    /// <summary>Native <c>XFlush</c>.</summary>
    /// <param name="display">The display.</param>
    /// <returns>The status.</returns>
    [LibraryImport(Library, EntryPoint = "XFlush")]
    internal static partial int XFlush(X11DisplayHandle display);
}
