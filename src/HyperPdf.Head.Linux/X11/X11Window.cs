// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Linux.X11;

/// <summary>A native X11 window that draws the page canvas through a render backend and reports its input as <see cref="WindowEvent"/> values.</summary>
[DebuggerDisplay("X11Window: {ClientSize}")]
public sealed unsafe class X11Window : IPlatformWindow
{
    /// <summary>The Xlib mask for key press events.</summary>
    private const nint KeyPressMask = 1;

    /// <summary>The Xlib mask for key release events.</summary>
    private const nint KeyReleaseMask = 1 << 1;

    /// <summary>The Xlib mask for button press events.</summary>
    private const nint ButtonPressMask = 1 << 2;

    /// <summary>The Xlib mask for button release events.</summary>
    private const nint ButtonReleaseMask = 1 << 3;

    /// <summary>The Xlib mask for pointer motion events.</summary>
    private const nint PointerMotionMask = 1 << 6;

    /// <summary>The Xlib mask for exposure events.</summary>
    private const nint ExposureMask = 1 << 15;

    /// <summary>The Xlib mask for structure change events.</summary>
    private const nint StructureNotifyMask = 1 << 17;

    /// <summary>The size of an XEvent union in bytes.</summary>
    private const int XEventSize = 192;

    /// <summary>The largest atom name the head interns, including its terminator.</summary>
    private const int AtomNameBuffer = 64;

    /// <summary>The Xlib image format for packed pixels.</summary>
    private const int ZPixmap = 2;

    /// <summary>The bits per pixel and scan line pad of the BGRA frame.</summary>
    private const int BitsPerPixel = 32;

    /// <summary>The first window width in pixels.</summary>
    private const int InitialWidth = 1024;

    /// <summary>The first window height in pixels.</summary>
    private const int InitialHeight = 768;

    /// <summary>The X11 display connection.</summary>
    private readonly X11DisplayHandle _display;

    /// <summary>The server side ids of the window.</summary>
    private readonly X11Ids _ids;

    /// <summary>The depth of the screen.</summary>
    private readonly uint _depth;

    /// <summary>The size the head last reported or was created with.</summary>
    private SurfaceSize _size = new(InitialWidth, InitialHeight);

    /// <summary>One once the window is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="X11Window"/> class.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to AT-SPI.</param>
    /// <exception cref="PlatformNotSupportedException">No X server could be reached.</exception>
    public X11Window(RenderBackendPreference preference, IAccessibilityBridge accessibility)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;
        Core.Platform.NativeLibraries.Register(typeof(NativeMethods).Assembly, NativeMethods.Library, ["libX11.so.6", "libX11.so"]);
        _display = NativeMethods.XOpenDisplay(null);
        if (_display.IsInvalid)
        {
            throw new PlatformNotSupportedException("No X server could be reached.");
        }

        var screen = NativeMethods.XDefaultScreen(_display);
        _depth = (uint)NativeMethods.XDefaultDepth(_display, screen);
        var window = NativeMethods.XCreateSimpleWindow(_display, NativeMethods.XRootWindow(_display, screen), 0, 0, InitialWidth, InitialHeight, 0, 0, 0);
        _ids = new(window, NativeMethods.XCreateGC(_display, window, 0, null), NativeMethods.XDefaultVisual(_display, screen), Intern("WM_DELETE_WINDOW"u8));
        nint* protocols = stackalloc nint[] { (nint)_ids.CloseAtom };
        _ = NativeMethods.XSetWMProtocols(_display, window, protocols, 1);
        _ = NativeMethods.XSelectInput(
            _display,
            window,
            ExposureMask | StructureNotifyMask | KeyPressMask | KeyReleaseMask | ButtonPressMask | ButtonReleaseMask | PointerMotionMask);

        Backend = RenderBackendSelector.Choose(preference, this, static _ => null, static window => new CpuRenderBackend(window.Present)).Backend;
        Backend.Resize(_size);
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public double Scale => 1.0;

    /// <inheritdoc/>
    public IRenderBackend Backend { get; }

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var bytes = Encoding.UTF8.GetBytes($"{title}\0");
        fixed (byte* name = bytes)
        {
            _ = NativeMethods.XStoreName(_display, (nint)_ids.Window, name);
        }
    }

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor)
    {
        // Cursor shapes need the X cursor font, which arrives with the input work; the default arrow stays until then.
        _ = cursor;
    }

    /// <inheritdoc/>
    public void RequestFrame() => _ = NativeMethods.XFlush(_display);

    /// <inheritdoc/>
    public void Show()
    {
        _ = NativeMethods.XMapWindow(_display, (nint)_ids.Window);
        _ = NativeMethods.XFlush(_display);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Dispose();

    /// <summary>Handles every queued event without waiting.</summary>
    /// <param name="handler">Receives each event.</param>
    public void PumpEvents(WindowEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        byte* buffer = stackalloc byte[XEventSize];
        while (Volatile.Read(ref _disposed) == 0 && NativeMethods.XPending(_display) > 0)
        {
            _ = NativeMethods.XNextEvent(_display, buffer);
            if (!X11EventReader.TryRead(buffer, (nint)_ids.CloseAtom, out var windowEvent))
            {
                continue;
            }

            if (windowEvent.Kind == WindowEventKind.Resized)
            {
                _size = new(windowEvent.X, windowEvent.Y);
                Backend.Resize(_size);
            }

            handler(windowEvent);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Backend.Dispose();
        Accessibility.Dispose();
        _ = NativeMethods.XFreeGC(_display, (nint)_ids.Gc);
        _ = NativeMethods.XDestroyWindow(_display, (nint)_ids.Window);
        _display.Dispose();
    }

    /// <summary>Copies a finished frame to the window.</summary>
    /// <param name="pixels">The premultiplied BGRA frame.</param>
    /// <param name="size">The frame size.</param>
    /// <param name="stride">The bytes per line.</param>
    private void Present(ReadOnlySpan<byte> pixels, SurfaceSize size, int stride)
    {
        if (Volatile.Read(ref _disposed) != 0 || size.IsEmpty)
        {
            return;
        }

        // XDestroyImage frees the pixel memory with free(), so it must come from malloc.
        var length = stride * size.Height;
        var data = (byte*)NativeMemory.Alloc((nuint)length);
        pixels[..length].CopyTo(new(data, length));
        var image = NativeMethods.XCreateImage(_display, (nint)_ids.Visual, _depth, ZPixmap, 0, data, (uint)size.Width, (uint)size.Height, BitsPerPixel, stride);
        if (image == 0)
        {
            NativeMemory.Free(data);
            return;
        }

        _ = NativeMethods.XPutImage(_display, (nint)_ids.Window, (nint)_ids.Gc, image, 0, 0, 0, 0, (uint)size.Width, (uint)size.Height);
        _ = NativeMethods.XDestroyImage(image);
        _ = NativeMethods.XFlush(_display);
    }

    /// <summary>Gets an atom by name.</summary>
    /// <param name="name">The ASCII name without a terminator, shorter than 64 bytes.</param>
    /// <returns>The atom.</returns>
    private nint Intern(ReadOnlySpan<byte> name)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(name.Length, AtomNameBuffer);
        Span<byte> terminated = stackalloc byte[AtomNameBuffer];
        terminated.Clear();
        name.CopyTo(terminated);
        fixed (byte* pointer = terminated)
        {
            return NativeMethods.XInternAtom(_display, pointer, 0);
        }
    }
}
