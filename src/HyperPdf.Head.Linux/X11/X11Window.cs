// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Vulkan;
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

    /// <summary>The offset of the pixel pointer in an XImage.</summary>
    private const int ImageDataOffset = 16;

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

    /// <summary>The wake-up that ends a wait for events.</summary>
    private readonly EventWake _wake;

    /// <summary>The window's side of the clipboard.</summary>
    private readonly X11Clipboard _clipboard;

    /// <summary>The device pixels of one layout unit, from the desktop's text scaling setting.</summary>
    private readonly double _scale;

    /// <summary>The cursors made so far, by <see cref="ViewerCursor"/> value. They are freed with the display connection.</summary>
    private readonly nint[] _cursors = new nint[CursorShapes.Length];

    /// <summary>The size the head last reported or was created with.</summary>
    private SurfaceSize _size = new(InitialWidth, InitialHeight);

    /// <summary>Where the drawing area is on the screen. Asking the server waits for its answer, so it is asked only when the server reports a move or a new size.</summary>
    private ScreenPoint _origin;

    /// <summary>One when a frame was asked for and not yet reported.</summary>
    private int _frameWanted;

    /// <summary>A Vulkan context handed to the constructor, until the backend takes it.</summary>
    private VulkanContext? _gpu;

    /// <summary>One once the window is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="X11Window"/> class.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to AT-SPI.</param>
    /// <exception cref="PlatformNotSupportedException">No X server could be reached.</exception>
    public X11Window(RenderBackendPreference preference, IAccessibilityBridge accessibility)
        : this(preference, accessibility, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="X11Window"/> class.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to AT-SPI.</param>
    /// <param name="gpu">A Vulkan context already made for a window, which the window owns from here on, or <see langword="null"/> to make one when the GPU is asked for.</param>
    /// <exception cref="PlatformNotSupportedException">No X server could be reached.</exception>
    public X11Window(RenderBackendPreference preference, IAccessibilityBridge accessibility, VulkanContext? gpu)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;
        _gpu = gpu;
        Core.Platform.NativeLibraries.Register(typeof(NativeMethods).Assembly, NativeMethods.Library, ["libX11.so.6", "libX11.so"]);
        Core.Platform.NativeLibraries.Register(typeof(NativeMethods).Assembly, Wayland.NativeMethods.Library, ["libc.so.6"]);
        _wake = new();
        _display = NativeMethods.XOpenDisplay(null);
        if (_display.IsInvalid)
        {
            throw new PlatformNotSupportedException("No X server could be reached.");
        }

        var screen = NativeMethods.XDefaultScreen(_display);
        _depth = (uint)NativeMethods.XDefaultDepth(_display, screen);
        _scale = ReadScale();

        // The first size is the same on screen at any scale, so it grows in pixels with the scale.
        _size = new((int)Math.Round(InitialWidth * _scale), (int)Math.Round(InitialHeight * _scale));
        var window = NativeMethods.XCreateSimpleWindow(_display, NativeMethods.XRootWindow(_display, screen), 0, 0, (uint)_size.Width, (uint)_size.Height, 0, 0, 0);
        _ids = new(window, NativeMethods.XCreateGC(_display, window, 0, null), NativeMethods.XDefaultVisual(_display, screen), Intern("WM_DELETE_WINDOW"u8));
        _clipboard = new(_display, window, new(Intern("CLIPBOARD"u8), Intern("TARGETS"u8), Intern("UTF8_STRING"u8)));
        nint* protocols = stackalloc nint[] { (nint)_ids.CloseAtom };
        _ = NativeMethods.XSetWMProtocols(_display, window, protocols, 1);
        _ = NativeMethods.XSelectInput(
            _display,
            window,
            ExposureMask | StructureNotifyMask | KeyPressMask | KeyReleaseMask | ButtonPressMask | ButtonReleaseMask | PointerMotionMask);

        Backend = RenderBackendSelector.Choose(preference, this, static window => window.StartVulkan(), static window => new CpuRenderBackend(window.Present)).Backend;
        Backend.Resize(_size);
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public double Scale => _scale;

    /// <inheritdoc/>
    public long NativeHandle => _ids.Window;

    /// <inheritdoc/>
    public ScreenPoint ScreenOrigin => _origin;

    /// <inheritdoc/>
    public IRenderBackend Backend { get; }

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <summary>Gets the shape numbers in the standard X cursor font, by <see cref="ViewerCursor"/> value: none, left pointer, hand, crosshair, text beam and four arrows.</summary>
    private static ReadOnlySpan<uint> CursorShapes => [0x00, 0x44, 0x3C, 0x22, 0x98, 0x34];

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
        _ = NativeMethods.XDefineCursor(_display, (nint)_ids.Window, CursorFor(cursor));
        _ = NativeMethods.XFlush(_display);
    }

    /// <inheritdoc/>
    public void RequestFrame()
    {
        Volatile.Write(ref _frameWanted, 1);
        _wake.Signal();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wake() => _wake.Signal();

    /// <inheritdoc/>
    public void Show()
    {
        _ = NativeMethods.XMapWindow(_display, (nint)_ids.Window);
        _ = NativeMethods.XFlush(_display);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Dispose();

    /// <summary>Handles every queued event. X11 events are read without waiting, so the timeout is not used.</summary>
    /// <param name="timeoutMilliseconds">Not used.</param>
    /// <param name="handler">Receives each event.</param>
    public void PumpEvents(int timeoutMilliseconds, WindowEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (timeoutMilliseconds != 0 && Volatile.Read(ref _frameWanted) == 0 && NativeMethods.XPending(_display) == 0)
        {
            // XPending flushed the requests, so the wait is only for the server or a wake-up.
            _ = _wake.Wait(NativeMethods.XConnectionNumber(_display), timeoutMilliseconds);
        }

        if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
        {
            handler(new(WindowEventKind.Exposed, 0, 0, 0, 0));
        }

        byte* buffer = stackalloc byte[XEventSize];
        while (Volatile.Read(ref _disposed) == 0 && NativeMethods.XPending(_display) > 0)
        {
            _ = NativeMethods.XNextEvent(_display, buffer);
            Dispatch(buffer, handler);
        }
    }

    /// <inheritdoc/>
    public void SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _clipboard.SetText(text);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Backend.Dispose();
        Interlocked.Exchange(ref _gpu, null)?.Dispose();
        Accessibility.Dispose();
        _wake.Dispose();
        _ = NativeMethods.XFreeGC(_display, (nint)_ids.Gc);
        _ = NativeMethods.XDestroyWindow(_display, (nint)_ids.Window);
        _display.Dispose();
    }

    /// <summary>Handles one event from the server.</summary>
    /// <param name="raw">The XEvent bytes.</param>
    /// <param name="handler">Receives the window events it makes.</param>
    private void Dispatch(byte* raw, WindowEventHandler handler)
    {
        var type = *(int*)raw;
        if (type == X11Clipboard.SelectionRequestType)
        {
            _clipboard.Answer(raw);
            return;
        }

        if (type == X11Clipboard.SelectionClearType)
        {
            _clipboard.Clear();
            return;
        }

        if (!X11EventReader.TryRead(raw, (nint)_ids.CloseAtom, out var windowEvent))
        {
            return;
        }

        if (windowEvent.Kind == WindowEventKind.Resized)
        {
            // The server reports a move with the same event as a new size.
            _origin = ReadOrigin();
            _size = new(windowEvent.X, windowEvent.Y);
            Backend.Resize(_size);
        }

        handler(windowEvent);
        if (windowEvent.Kind == WindowEventKind.KeyDown && X11EventReader.TryReadText(raw, out var typed))
        {
            handler(typed);
        }
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

        fixed (byte* data = pixels)
        {
            // The image only wraps the frame for the length of the call, so the frame is sent without being copied first.
            var image = NativeMethods.XCreateImage(_display, (nint)_ids.Visual, _depth, ZPixmap, 0, data, (uint)size.Width, (uint)size.Height, BitsPerPixel, stride);
            if (image == 0)
            {
                return;
            }

            _ = NativeMethods.XPutImage(_display, (nint)_ids.Window, (nint)_ids.Gc, image, 0, 0, 0, 0, (uint)size.Width, (uint)size.Height);

            // XDestroyImage frees the pixel memory it finds, and this memory belongs to the backend, so the image lets go of it first.
            *(byte**)(image + ImageDataOffset) = null;
            _ = NativeMethods.XDestroyImage(image);
        }

        _ = NativeMethods.XFlush(_display);
    }

    /// <summary>Reads the display scale from the desktop's Xft.dpi setting, which desktops set for scaled and high resolution displays.</summary>
    /// <returns>The scale, 1 when the desktop sets none.</returns>
    private double ReadScale()
    {
        fixed (byte* program = "Xft\0"u8)
        {
            fixed (byte* option = "dpi\0"u8)
            {
                var text = NativeMethods.XGetDefault(_display, program, option);
                var dpi = text is not null && double.TryParse(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
                return DisplayScale.FromDpi(dpi);
            }
        }
    }

    /// <summary>Asks the server where the top left of the window is on the screen.</summary>
    /// <returns>The place, or the screen's corner when the server cannot say.</returns>
    private ScreenPoint ReadOrigin()
    {
        int x;
        int y;
        nint child;
        var root = NativeMethods.XRootWindow(_display, NativeMethods.XDefaultScreen(_display));
        return NativeMethods.XTranslateCoordinates(_display, (nint)_ids.Window, root, 0, 0, &x, &y, &child) != 0 ? new(x, y) : default;
    }

    /// <summary>Starts the Vulkan backend and attaches it to this window.</summary>
    /// <returns>The backend, or <see langword="null"/> when the machine has no GPU that can present to an X11 window.</returns>
    private VulkanRenderBackend? StartVulkan()
    {
        var context = Interlocked.Exchange(ref _gpu, null) ?? VulkanContext.TryCreate(forWindow: true);
        if (context is null)
        {
            return null;
        }

        var backend = new VulkanRenderBackend(context);

        // Xlib hands back the display pointer of the default screen, which is the pointer Vulkan needs. The surface keeps it after the
        // call, and the window disposes the backend before it closes the display, so the pointer outlives the surface.
        var display = NativeMethods.XDisplayOfScreen(NativeMethods.XDefaultScreenOfDisplay(_display));
        if (backend.TryPresentToXlibWindow(display, (nuint)_ids.Window))
        {
            return backend;
        }

        backend.Dispose();
        return null;
    }

    /// <summary>Gets the cursor of a shape from the standard cursor font, making it on first use.</summary>
    /// <param name="cursor">The shape.</param>
    /// <returns>The cursor, or 0 for the window manager's own.</returns>
    private nint CursorFor(ViewerCursor cursor)
    {
        var index = (int)cursor;
        if ((uint)index >= (uint)_cursors.Length || CursorShapes[index] == 0)
        {
            return 0;
        }

        if (_cursors[index] == 0)
        {
            _cursors[index] = NativeMethods.XCreateFontCursor(_display, CursorShapes[index]);
        }

        return _cursors[index];
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
