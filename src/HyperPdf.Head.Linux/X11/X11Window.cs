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
using ReactiveUI.Primitives.Signals;

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

    /// <summary>The X11 event mask for changes to a window's properties.</summary>
    private const nint PropertyChangeMask = 1 << 22;

    /// <summary>The X11 event type for a changed property.</summary>
    private const int PropertyNotifyType = 28;

    /// <summary>The offset of the window in an XPropertyEvent.</summary>
    private const int PropertyWindowOffset = 32;

    /// <summary>The offset of the atom in an XPropertyEvent.</summary>
    private const int PropertyAtomOffset = 40;

    /// <summary>The most of the resource text read, in 32-bit units.</summary>
    private const nint MaxResourceUnits = 1 << 16;

    /// <summary>The bits of one element of a property that holds text.</summary>
    private const int ByteFormat = 8;

    /// <summary>The smallest change of scale that is followed.</summary>
    private const double ScaleStep = 0.01;

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
    private readonly nint _display;

    /// <summary>The server side ids of the window.</summary>
    private readonly X11Ids _ids;

    /// <summary>The depth of the screen.</summary>
    private readonly uint _depth;

    /// <summary>Carries the window's events to whoever follows the window.</summary>
    private readonly Signal<WindowEvent> _events = new();

    /// <summary>Carries the files dropped on the window, on the thread that pumps it.</summary>
    private readonly Signal<IReadOnlyList<string>> _drops = new();

    /// <summary>The atoms of the drag and drop protocol.</summary>
    private readonly X11DropAtoms _dropAtoms;

    /// <summary>The wake-up that ends a wait for events.</summary>
    private readonly EventWake _wake;

    /// <summary>The window's side of the clipboard.</summary>
    private readonly X11Clipboard _clipboard;

    /// <summary>The root window of the screen, where the desktop keeps its resource text.</summary>
    private readonly nint _root;

    /// <summary>The atom of the root window property that holds the desktop's resource text.</summary>
    private readonly nint _resourcesAtom;

    /// <summary>The cursors made so far, by <see cref="ViewerCursor"/> value. They are freed with the display connection.</summary>
    private readonly nint[] _cursors = new nint[CursorShapes.Length];

    /// <summary>The device pixels of one layout unit, from the desktop's text scaling setting, which is followed while the window is open.</summary>
    private double _scale;

    /// <summary>The size the head last reported or was created with.</summary>
    private SurfaceSize _size = new(InitialWidth, InitialHeight);

    /// <summary>Where the drawing area is on the screen. Asking the server waits for its answer, so it is asked only when the server reports a move or a new size.</summary>
    private ScreenPoint _origin;

    /// <summary>The drag that is over the window now, or none.</summary>
    private X11DropOffer _offer;

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
        : this(preference, accessibility, gpu, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="X11Window"/> class on a named X server.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to AT-SPI.</param>
    /// <param name="gpu">A Vulkan context already made for a window, or <see langword="null"/>.</param>
    /// <param name="displayName">The name of the X server, such as <c>:0</c>, or <see langword="null"/> for the one the environment names.</param>
    /// <exception cref="PlatformNotSupportedException">No X server could be reached.</exception>
    internal X11Window(RenderBackendPreference preference, IAccessibilityBridge accessibility, VulkanContext? gpu, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;
        _gpu = gpu;
        NativeMethods.Register();
        Core.Platform.NativeLibraries.Register(typeof(NativeMethods).Assembly, Wayland.NativeMethods.Library, ["libc.so.6"]);
        _wake = new();
        X11ErrorHandler.Install();
        _display = Open(displayName);
        if (_display == 0)
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
        _clipboard = new(_display, window, new(Intern("CLIPBOARD"u8), Intern("TARGETS"u8), Intern("UTF8_STRING"u8), Intern("HYPERPDF_PASTE"u8)));
        _dropAtoms = new(
            Intern("XdndAware"u8),
            Intern("XdndEnter"u8),
            Intern("XdndPosition"u8),
            Intern("XdndStatus"u8),
            Intern("XdndLeave"u8),
            Intern("XdndDrop"u8),
            Intern("XdndFinished"u8),
            Intern("XdndTypeList"u8),
            Intern("XdndSelection"u8),
            Intern("XdndActionCopy"u8),
            Intern("text/uri-list"u8),
            Intern("HYPERPDF_DROP"u8));
        X11Drops.Declare(_display, window, _dropAtoms);
        nint* protocols = stackalloc nint[] { (nint)_ids.CloseAtom };
        _ = NativeMethods.XSetWMProtocols(_display, window, protocols, 1);
        _ = NativeMethods.XSelectInput(
            _display,
            window,
            ExposureMask | StructureNotifyMask | KeyPressMask | KeyReleaseMask | ButtonPressMask | ButtonReleaseMask | PointerMotionMask);

        // The desktop rewrites its resource text on the root window when the user changes the display scale.
        _root = NativeMethods.XRootWindow(_display, screen);
        _resourcesAtom = Intern("RESOURCE_MANAGER"u8);
        _ = NativeMethods.XSelectInput(_display, _root, PropertyChangeMask);

        Backend = RenderBackendSelector.Choose(preference, this, static window => window.StartVulkan(), static window => new CpuRenderBackend(window.Present)).Backend;
        Backend.Resize(_size);
    }

    /// <summary>Finalizes an instance of the <see cref="X11Window"/> class. A window that was never let go still closes its connection to the X server, which frees everything made on it.</summary>
    ~X11Window()
    {
        if (_display != 0 && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = NativeMethods.XCloseDisplay(_display);
        }
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

    /// <inheritdoc/>
    public IObservable<WindowEvent> Events => _events;

    /// <inheritdoc/>
    public IObservable<IReadOnlyList<string>> FileDrops => _drops;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Raise() => X11WindowState.Activate(_display, _root, (nint)_ids.Window, Intern("_NET_ACTIVE_WINDOW"u8));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFullScreen(bool on) => X11WindowState.Change(_display, _root, (nint)_ids.Window, Intern("_NET_WM_STATE"u8), Intern("_NET_WM_STATE_FULLSCREEN"u8), on);

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
    public void PumpEvents(int timeoutMilliseconds)
    {
        if (timeoutMilliseconds != 0 && Volatile.Read(ref _frameWanted) == 0 && NativeMethods.XPending(_display) == 0)
        {
            // XPending flushed the requests, so the wait is only for the server or a wake-up.
            _ = _wake.Wait(NativeMethods.XConnectionNumber(_display), timeoutMilliseconds);
        }

        if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
        {
            _events.OnNext(new(WindowEventKind.Exposed, 0, 0, 0, 0));
        }

        byte* buffer = stackalloc byte[XEventSize];
        while (Volatile.Read(ref _disposed) == 0 && NativeMethods.XPending(_display) > 0)
        {
            _ = NativeMethods.XNextEvent(_display, buffer);
            Dispatch(buffer);
        }
    }

    /// <inheritdoc/>
    public void SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _clipboard.SetText(text);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetClipboardText() => _clipboard.GetText();

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
        _events.Dispose();
        _drops.Dispose();
        _wake.Dispose();
        _ = NativeMethods.XFreeGC(_display, (nint)_ids.Gc);
        _ = NativeMethods.XDestroyWindow(_display, (nint)_ids.Window);
        _ = NativeMethods.XCloseDisplay(_display);
        GC.SuppressFinalize(this);
    }

    /// <summary>Connects to an X server.</summary>
    /// <param name="displayName">The name of the server, or <see langword="null"/> for the one the environment names.</param>
    /// <returns>The connection, or 0 when the server cannot be reached.</returns>
    private static nint Open(string? displayName)
    {
        if (displayName is null)
        {
            return NativeMethods.XOpenDisplay(null);
        }

        var bytes = Encoding.ASCII.GetBytes($"{displayName}\0");
        fixed (byte* name = bytes)
        {
            return NativeMethods.XOpenDisplay(name);
        }
    }

    /// <summary>Handles one event from the server.</summary>
    /// <param name="raw">The XEvent bytes.</param>
    private void Dispatch(byte* raw)
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

        if (type == PropertyNotifyType)
        {
            FollowScale(raw);
            return;
        }

        if (X11Drops.Handle(_display, (nint)_ids.Window, _dropAtoms, ref _offer, raw, out var dropped))
        {
            if (dropped.Length > 0)
            {
                _drops.OnNext(dropped);
            }

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

        _events.OnNext(windowEvent);
        if (windowEvent.Kind == WindowEventKind.KeyDown && X11EventReader.TryReadText(raw, out var typed))
        {
            _events.OnNext(typed);
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

    /// <summary>Takes the new display scale when the desktop rewrote its resource text, and has the window laid out again.</summary>
    /// <param name="raw">The XPropertyEvent bytes.</param>
    private void FollowScale(byte* raw)
    {
        if (*(nint*)(raw + PropertyWindowOffset) != _root || *(nint*)(raw + PropertyAtomOffset) != _resourcesAtom)
        {
            return;
        }

        nint type;
        int format;
        nuint count;
        nuint remaining;
        byte* data;
        if (NativeMethods.XGetWindowProperty(_display, _root, _resourcesAtom, 0, MaxResourceUnits, 0, 0, &type, &format, &count, &remaining, &data) != 0 || data is null)
        {
            return;
        }

        var dpi = format == ByteFormat ? X11Resources.ReadDpi(new(data, (int)count)) : 0;
        _ = NativeMethods.XFree(data);
        var scale = DisplayScale.FromDpi(dpi);
        if (dpi <= 0 || Math.Abs(scale - _scale) < ScaleStep)
        {
            return;
        }

        // The window keeps its size in pixels; the viewer reads the new scale when it lays out again.
        _scale = scale;
        _events.OnNext(new(WindowEventKind.Resized, _size.Width, _size.Height, 0, 0));
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
