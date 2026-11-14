// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HyperPdf.Head.Windows.Accessibility;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Direct3D;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>A native Win32 window that draws the page canvas through a render backend and reports its input as <see cref="WindowEvent"/> values.</summary>
[SupportedOSPlatform("windows")]
[DebuggerDisplay("Win32Window: {ClientSize}")]
public sealed unsafe class Win32Window : IPlatformWindow
{
    /// <summary>The window class name.</summary>
    private const string ClassName = "HyperPdfWindow";

    /// <summary>The WS_OVERLAPPEDWINDOW style.</summary>
    private const uint OverlappedWindow = 0x00CF0000;

    /// <summary>The CW_USEDEFAULT position and size.</summary>
    private const int UseDefault = unchecked((int)0x80000000);

    /// <summary>The SW_SHOW command.</summary>
    private const int ShowCommand = 5;

    /// <summary>The slot of the user data in the window, GWLP_USERDATA.</summary>
    private const int UserDataSlot = -21;

    /// <summary>The WM_NCCREATE message, which carries the creation parameter.</summary>
    private const uint NonClientCreate = 0x0081;

    /// <summary>The WM_GETOBJECT message, which assistive technologies use to ask for the window's accessible object.</summary>
    private const uint GetObjectMessage = 0x003D;

    /// <summary>The WM_ERASEBKGND message.</summary>
    private const uint EraseBackground = 0x0014;

    /// <summary>The WM_SETCURSOR message.</summary>
    private const uint SetCursorMessage = 0x0020;

    /// <summary>The size of a WNDCLASSEXW in bytes on 64-bit Windows.</summary>
    private const int WindowClassSize = 80;

    /// <summary>The size of a MSG in bytes on 64-bit Windows.</summary>
    private const int MessageSize = 48;

    /// <summary>The size of a BITMAPINFOHEADER in bytes.</summary>
    private const int BitmapHeaderSize = 40;

    /// <summary>The number of ints in a POINT.</summary>
    private const int PointSize = 2;

    /// <summary>The PM_REMOVE flag.</summary>
    private const uint RemoveMessage = 1;

    /// <summary>The QS_ALLINPUT wake mask.</summary>
    private const uint AllInput = 0x04FF;

    /// <summary>The window handle.</summary>
    private readonly long _window;

    /// <summary>The render backend, made once the window exists. It is still <see langword="null"/> while Windows sends the messages of window creation.</summary>
    private readonly IRenderBackend? _backend;

    /// <summary>The GCHandle that lets the window procedure find this instance.</summary>
    private GCHandle _self;

    /// <summary>The cursor shown over the client area.</summary>
    private long _cursor;

    /// <summary>The handler of the current <see cref="PumpEvents"/> call.</summary>
    private WindowEventHandler? _sink;

    /// <summary>The size of the drawing area.</summary>
    private SurfaceSize _size = new(Win32Cursors.InitialWidth, Win32Cursors.InitialHeight);

    /// <summary>One once the window is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="Win32Window"/> class.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to UI Automation.</param>
    /// <exception cref="PlatformNotSupportedException">The window could not be created.</exception>
    public Win32Window(RenderBackendPreference preference, IAccessibilityBridge accessibility)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;
        _self = GCHandle.Alloc(this);
        _cursor = NativeMethods.LoadCursor(0, Win32Cursors.Arrow);
        var instance = NativeMethods.GetModuleHandle(null);
        RegisterClass(instance);
        _window = NativeMethods.CreateWindow(
            0,
            ClassName,
            "HyperPDF",
            OverlappedWindow,
            UseDefault,
            UseDefault,
            Win32Cursors.InitialWidth,
            Win32Cursors.InitialHeight,
            0,
            0,
            instance,
            GCHandle.ToIntPtr(_self));
        if (_window == 0)
        {
            _self.Free();
            throw new PlatformNotSupportedException("Windows could not create the window.");
        }

        // The Direct3D swap chain belongs to the window, so the backend is made once the window exists.
        _size = ReadClientSize();
        _backend = RenderBackendSelector.Choose(
            preference,
            this,
            static window => new Direct3DRenderBackend((nint)window._window),
            static window => new CpuRenderBackend(window.Present)).Backend;
        _backend.Resize(_size);
        (accessibility as IWin32AccessibilityBridge)?.AttachWindow(_window);
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public double Scale => 1.0;

    /// <inheritdoc/>
    public long NativeHandle => _window;

    /// <inheritdoc/>
    public ScreenPoint ScreenOrigin
    {
        get
        {
            // The point starts as the corner of the client area, and Windows turns it into screen pixels in place.
            int* point = stackalloc int[PointSize];
            return Volatile.Read(ref _disposed) == 0 && NativeMethods.ClientToScreen((nint)_window, point) != 0 ? new(point[0], point[1]) : default;
        }
    }

    /// <inheritdoc/>
    public IRenderBackend Backend => _backend ?? throw new InvalidOperationException("The window is not open yet.");

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <summary>Gets the native window handle, for tests and for code that talks to the shell about this window.</summary>
    internal long WindowHandle => _window;

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        _ = NativeMethods.SetWindowText((nint)_window, title);
    }

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor)
    {
        _cursor = NativeMethods.LoadCursor(0, Win32Cursors.For(cursor));
        _ = NativeMethods.SetCursor((nint)_cursor);
    }

    /// <inheritdoc/>
    public void RequestFrame()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            // Windows answers with one paint message however many requests arrive before it, which is the frame.
            _ = NativeMethods.InvalidateRect((nint)_window, null, 0);
        }
    }

    /// <inheritdoc/>
    public void Wake()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            // An empty message is enough to end the wait in PumpEvents.
            _ = NativeMethods.PostMessage((nint)_window, 0, 0, 0);
        }
    }

    /// <inheritdoc/>
    public void Show() => _ = NativeMethods.ShowWindow((nint)_window, ShowCommand);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Dispose();

    /// <inheritdoc/>
    public void PumpEvents(int timeoutMilliseconds, WindowEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _sink = handler;
        try
        {
            // A negative time waits without limit, which is what the same bits mean to Windows.
            if (timeoutMilliseconds != 0)
            {
                _ = NativeMethods.WaitForMessage(0, null, (uint)timeoutMilliseconds, AllInput, 0);
            }

            byte* message = stackalloc byte[MessageSize];
            while (NativeMethods.PeekMessage(message, 0, 0, 0, RemoveMessage) != 0)
            {
                _ = NativeMethods.TranslateMessage(message);
                _ = NativeMethods.DispatchMessage(message);
            }
        }
        finally
        {
            _sink = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _backend?.Dispose();
        Accessibility.Dispose();
        _ = NativeMethods.SetWindowLongPtr((nint)_window, UserDataSlot, 0);
        _ = NativeMethods.DestroyWindow((nint)_window);
        _self.Free();
    }

    /// <summary>The window procedure shared by every window of the class.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <returns>The result for the message.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint window, uint message, nuint primary, nint secondary)
    {
        if (message == NonClientCreate)
        {
            _ = NativeMethods.SetWindowLongPtr(window, UserDataSlot, *(nint*)secondary);
        }

        var target = NativeMethods.GetWindowLongPtr(window, UserDataSlot);
        return target == 0
            ? NativeMethods.DefWindowProc(window, message, primary, secondary)
            : ((Win32Window)GCHandle.FromIntPtr(target).Target!).Handle(window, message, primary, secondary);
    }

    /// <summary>Registers the window class. A class that already exists from an earlier window is reused.</summary>
    /// <param name="instance">The module handle.</param>
    private static void RegisterClass(nint instance)
    {
        byte* windowClass = stackalloc byte[WindowClassSize];
        new Span<byte>(windowClass, WindowClassSize).Clear();
        fixed (char* name = ClassName)
        {
            *(uint*)windowClass = WindowClassSize;
            *(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>*)(windowClass + Win32Cursors.ProcedureOffset) = &WindowProcedure;
            *(nint*)(windowClass + Win32Cursors.InstanceOffset) = instance;
            *(char**)(windowClass + Win32Cursors.ClassNameOffset) = name;
            _ = NativeMethods.RegisterClass(windowClass);
        }
    }

    /// <summary>Handles a message for this window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <returns>The result.</returns>
    private nint Handle(nint window, uint message, nuint primary, nint secondary)
    {
        if (message == EraseBackground)
        {
            return 1;
        }

        if (message == GetObjectMessage && Accessibility is IWin32AccessibilityBridge bridge && bridge.TryAnswerGetObject(window, primary, secondary, out var answer))
        {
            return answer;
        }

        if (message == SetCursorMessage && (secondary & Win32Cursors.HitTestMask) == Win32Cursors.ClientHit)
        {
            _ = NativeMethods.SetCursor((nint)_cursor);
            return 1;
        }

        var windowEvent = Win32MessageReader.Read(message, primary, secondary, Win32Cursors.Modifiers());
        if (windowEvent.Kind == WindowEventKind.None)
        {
            return NativeMethods.DefWindowProc(window, message, primary, secondary);
        }

        Apply(window, windowEvent);
        _sink?.Invoke(windowEvent);

        // A close request is for the viewer to answer, so the window is not destroyed here.
        return 0;
    }

    /// <summary>Keeps the window's own state in step with an event.</summary>
    /// <param name="window">The window.</param>
    /// <param name="windowEvent">The event.</param>
    private void Apply(nint window, in WindowEvent windowEvent)
    {
        if (windowEvent.Kind == WindowEventKind.Resized)
        {
            // Windows sends the first size while the window is being created, before there is a backend to resize.
            _size = new(windowEvent.X, windowEvent.Y);
            _backend?.Resize(_size);
        }
        else if (windowEvent.Kind == WindowEventKind.Exposed)
        {
            _ = NativeMethods.ValidateRect(window, null);
        }
    }

    /// <summary>Reads the client area size.</summary>
    /// <returns>The size.</returns>
    private SurfaceSize ReadClientSize()
    {
        int* rect = stackalloc int[4];
        _ = NativeMethods.GetClientRect((nint)_window, rect);
        return new(rect[2] - rect[0], rect[3] - rect[1]);
    }

    /// <summary>Copies a finished frame to the window.</summary>
    /// <param name="pixels">The premultiplied BGRA frame.</param>
    /// <param name="size">The frame size.</param>
    /// <param name="stride">The bytes per line.</param>
    private void Present(ReadOnlySpan<byte> pixels, SurfaceSize size, int stride)
    {
        if (Volatile.Read(ref _disposed) != 0 || size.IsEmpty || _window == 0)
        {
            return;
        }

        byte* info = stackalloc byte[BitmapHeaderSize];
        Win32Cursors.WriteBitmapHeader(info, size);
        var deviceContext = NativeMethods.GetDeviceContext((nint)_window);
        fixed (byte* bits = pixels)
        {
            _ = NativeMethods.SetDIBitsToDevice(deviceContext, 0, 0, (uint)size.Width, (uint)size.Height, 0, 0, 0, (uint)size.Height, bits, info, 0);
        }

        _ = NativeMethods.ReleaseDeviceContext((nint)_window, deviceContext);
        _ = stride;
    }
}
