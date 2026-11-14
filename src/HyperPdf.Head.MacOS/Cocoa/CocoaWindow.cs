// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Metal;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>
/// A native Cocoa window that draws the page canvas through a render backend and reports its input as <see cref="WindowEvent"/>
/// values. It pulls events from the application's queue itself, so no subclass or delegate is registered with the Objective-C
/// runtime. Apart from <see cref="Wake"/> and <see cref="RequestFrame"/>, every member runs on the thread that made the window.
/// </summary>
[DebuggerDisplay("CocoaWindow: {ClientSize}")]
public sealed unsafe class CocoaWindow : IPlatformWindow
{
    /// <summary>The first window width in points.</summary>
    private const double InitialWidth = 1024;

    /// <summary>The first window height in points.</summary>
    private const double InitialHeight = 768;

    /// <summary>The style of a window with a title, a close button, a minimise button and a resizable frame.</summary>
    private const nuint WindowStyle = 15;

    /// <summary>The backing store type every modern window uses.</summary>
    private const nuint BufferedBacking = 2;

    /// <summary>The milliseconds in a second.</summary>
    private const double MillisecondsPerSecond = 1000;

    /// <summary>The event type of an application defined event, which <see cref="Wake"/> posts.</summary>
    private const nuint ApplicationDefined = 15;

    /// <summary>The event type of the left button going down.</summary>
    private const nuint LeftDown = 1;

    /// <summary>The event type of the left button coming up.</summary>
    private const nuint LeftUp = 2;

    /// <summary>The event type of the right button going down.</summary>
    private const nuint RightDown = 3;

    /// <summary>The event type of the right button coming up.</summary>
    private const nuint RightUp = 4;

    /// <summary>The event type of the pointer moving with no button held.</summary>
    private const nuint MouseMoved = 5;

    /// <summary>The event type of the pointer moving with the left button held.</summary>
    private const nuint LeftDragged = 6;

    /// <summary>The event type of the pointer moving with the right button held.</summary>
    private const nuint RightDragged = 7;

    /// <summary>The pasteboard type of plain text.</summary>
    private const string PlainTextType = "public.utf8-plain-text";

    /// <summary>The event type of a key going down.</summary>
    private const nuint KeyDown = 10;

    /// <summary>The event type of a key coming up.</summary>
    private const nuint KeyUp = 11;

    /// <summary>The event type of the wheel or a two finger scroll.</summary>
    private const nuint ScrollWheel = 22;

    /// <summary>The event type of another button going down.</summary>
    private const nuint OtherDown = 25;

    /// <summary>The event type of another button coming up.</summary>
    private const nuint OtherUp = 26;

    /// <summary>The event type of the pointer moving with another button held.</summary>
    private const nuint OtherDragged = 27;

    /// <summary>The button number of the middle button.</summary>
    private const nint MiddleButtonNumber = 2;

    /// <summary>The key code of the Q key, which closes the window with Command.</summary>
    private const ushort QuitKey = 12;

    /// <summary>The scroll units of one point of a trackpad scroll.</summary>
    private const double UnitsPerPoint = 3;

    /// <summary>The bits per colour channel of a frame.</summary>
    private const nuint BitsPerChannel = 8;

    /// <summary>The bits per pixel of a frame.</summary>
    private const nuint BitsPerPixel = 32;

    /// <summary>The Core Graphics bitmap layout of premultiplied BGRA: alpha first in a little endian 32 bit pixel.</summary>
    private const uint PremultipliedBgra = 0x2002;

    /// <summary>The application object.</summary>
    private readonly ulong _application;

    /// <summary>The window.</summary>
    private readonly ulong _window;

    /// <summary>The window's content view.</summary>
    private readonly ulong _view;

    /// <summary>The run loop mode events are taken in.</summary>
    private readonly ulong _mode;

    /// <summary>The layer frames are shown in.</summary>
    private ulong _layer;

    /// <summary>The size of the drawing area in device pixels.</summary>
    private SurfaceSize _size;

    /// <summary>The height of the drawing area in points, which turns Cocoa's bottom up y into a top down y.</summary>
    private double _heightInPoints;

    /// <summary>The pointer buttons held.</summary>
    private ViewerButtons _held;

    /// <summary>One when a frame was asked for and not yet reported.</summary>
    private int _frameWanted;

    /// <summary>Whether the window has been shown, after which becoming hidden means the user closed it.</summary>
    private bool _shown;

    /// <summary>Whether the close was reported.</summary>
    private bool _closeReported;

    /// <summary>One once the window is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="CocoaWindow"/> class on the main thread.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to VoiceOver.</param>
    public CocoaWindow(RenderBackendPreference preference, IAccessibilityBridge accessibility)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;

        // The window and layer classes exist only once their frameworks are loaded.
        _ = NativeLibrary.Load(NativeMethods.AppKit);
        _ = NativeLibrary.Load(NativeMethods.QuartzCore);
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            var application = NativeMethods.Send(NativeMethods.GetClass("NSApplication"), Selectors.SharedApplication);
            _application = (ulong)application;
            NativeMethods.SendVoid(application, Selectors.SetActivationPolicy, 0);
            NativeMethods.SendVoid(application, Selectors.FinishLaunching);
            var window = NativeMethods.SendInitWindow(
                NativeMethods.Send(NativeMethods.GetClass("NSWindow"), Selectors.Alloc),
                Selectors.InitWindow,
                new(0, 0, InitialWidth, InitialHeight),
                WindowStyle,
                BufferedBacking,
                0);
            _window = (ulong)window;

            // Closing hides the window instead of freeing it, so the head can see the close and release the window itself.
            NativeMethods.SendVoidFlag(window, Selectors.SetReleasedWhenClosed, 0);
            NativeMethods.SendVoidFlag(window, Selectors.SetAcceptsMouseMoved, 1);
            NativeMethods.SendVoid(window, Selectors.Center);
            var view = NativeMethods.Send(window, Selectors.ContentView);
            _view = (ulong)view;
            NativeMethods.SendVoidFlag(view, Selectors.SetWantsLayer, 1);

            // Run loop modes are compared as text, and this is the text of the default mode.
            _mode = (ulong)NativeMethods.Send(Cocoa.Text("kCFRunLoopDefaultMode"), NativeMethods.Selector("retain"));
            Backend = RenderBackendSelector.Choose(preference, this, static window => window.StartMetal(), static window => window.StartCpu()).Backend;
            ReadSize();
            Backend.Resize(_size);
            (accessibility as MacAccessibilityBridge)?.AttachView(view);
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public ScreenPoint ScreenOrigin
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return default;
            }

            // Cocoa measures the screen in points from its bottom left corner, and the viewer measures in pixels from the top left.
            var window = (nint)_window;
            var content = Cocoa.Rect(window, Selectors.ContentRectForFrameRect, Cocoa.Rect(window, Selectors.Frame));
            var screen = NativeMethods.Send(window, Selectors.Screen);
            var screenHeight = screen != 0 ? Cocoa.Rect(screen, Selectors.Frame).Height : content.Y + content.Height;
            var scale = Scale;
            return new((int)Math.Round(content.X * scale), (int)Math.Round((screenHeight - content.Y - content.Height) * scale));
        }
    }

    /// <inheritdoc/>
    public long NativeHandle => Volatile.Read(ref _disposed) == 0 ? NativeMethods.Send((nint)_window, Selectors.WindowNumber) : 0;

    /// <inheritdoc/>
    public double Scale { get; private set; } = 1;

    /// <inheritdoc/>
    public IRenderBackend Backend { get; }

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var pool = NativeMethods.AutoreleasePoolPush();
        NativeMethods.SendVoid((nint)_window, Selectors.SetTitle, Cocoa.Text(title));
        NativeMethods.AutoreleasePoolPop(pool);
    }

    /// <inheritdoc/>
    public void SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pool = NativeMethods.AutoreleasePoolPush();
        var pasteboard = NativeMethods.Send(NativeMethods.GetClass("NSPasteboard"), Selectors.GeneralPasteboard);
        _ = NativeMethods.Send(pasteboard, Selectors.ClearContents);
        _ = NativeMethods.SendFlag(pasteboard, Selectors.SetStringForType, Cocoa.Text(text), Cocoa.Text(PlainTextType));
        NativeMethods.AutoreleasePoolPop(pool);
    }

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor)
    {
        var shape = cursor switch
        {
            ViewerCursor.Hand => Selectors.PointingHandCursor,
            ViewerCursor.Cross => Selectors.CrosshairCursor,
            ViewerCursor.Text => Selectors.BeamCursor,
            ViewerCursor.Move => Selectors.OpenHandCursor,
            _ => Selectors.ArrowCursor,
        };
        NativeMethods.SendVoid(NativeMethods.Send(NativeMethods.GetClass("NSCursor"), shape), Selectors.Set);
    }

    /// <inheritdoc/>
    public void RequestFrame()
    {
        Volatile.Write(ref _frameWanted, 1);
        Wake();
    }

    /// <inheritdoc/>
    public void Wake()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // An empty application defined event ends the wait in PumpEvents. Posting an event is safe from any thread.
        var pool = NativeMethods.AutoreleasePoolPush();
        var wake = NativeMethods.SendOtherEvent(NativeMethods.GetClass("NSEvent"), Selectors.OtherEvent, ApplicationDefined, default, 0, 0, 0, 0, 0, 0, 0);
        NativeMethods.SendPostEvent((nint)_application, Selectors.PostEvent, wake, 1);
        NativeMethods.AutoreleasePoolPop(pool);
    }

    /// <inheritdoc/>
    public void PumpEvents(int timeoutMilliseconds, WindowEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Events and dates are autoreleased, so each pump has its own pool.
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            var application = (nint)_application;
            var wait = Volatile.Read(ref _frameWanted) != 0 ? 0 : timeoutMilliseconds;
            var next = NativeMethods.SendNextEvent(application, Selectors.NextEvent, nuint.MaxValue, Until(wait), (nint)_mode, 1);
            while (next != 0)
            {
                Dispatch(next, handler);
                next = NativeMethods.SendNextEvent(application, Selectors.NextEvent, nuint.MaxValue, Until(0), (nint)_mode, 1);
            }

            ReportWindowChanges(handler);
            (Accessibility as MacAccessibilityBridge)?.FollowVoiceOver();
            if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
            {
                handler(new(WindowEventKind.Exposed, 0, 0, 0, 0));
            }
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc/>
    public void Show()
    {
        NativeMethods.SendVoid((nint)_window, Selectors.MakeKeyAndOrderFront, 0);
        NativeMethods.SendVoidFlag((nint)_application, Selectors.Activate, 1);
        _shown = true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Dispose();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Backend.Dispose();
        Accessibility.Dispose();
        NativeMethods.SendVoid((nint)_window, Selectors.Close);
        NativeMethods.SendVoid((nint)_mode, Selectors.Release);
        NativeMethods.SendVoid((nint)_window, Selectors.Release);
    }

    /// <summary>Gets the date a wait for events ends.</summary>
    /// <param name="timeoutMilliseconds">The longest wait, 0 for none or a negative number for no limit.</param>
    /// <returns>The autoreleased date.</returns>
    private static nint Until(int timeoutMilliseconds)
    {
        var dates = NativeMethods.GetClass("NSDate");
        return timeoutMilliseconds switch
        {
            0 => NativeMethods.Send(dates, Selectors.DistantPast),
            < 0 => NativeMethods.Send(dates, Selectors.DistantFuture),
            _ => NativeMethods.SendDateSinceNow(dates, Selectors.DateSinceNow, timeoutMilliseconds / MillisecondsPerSecond),
        };
    }

    /// <summary>Starts the Metal backend on a Metal layer in the window.</summary>
    /// <returns>The backend.</returns>
    /// <exception cref="InvalidOperationException">The machine has no GPU Metal can use.</exception>
    private MetalRenderBackend StartMetal()
    {
        var layer = NativeMethods.Send(NativeMethods.GetClass("CAMetalLayer"), Selectors.Layer);
        NativeMethods.SendVoid((nint)_view, Selectors.SetLayer, layer);
        _layer = (ulong)layer;
        return new(layer);
    }

    /// <summary>Starts the CPU backend, whose frames are shown as the contents of a plain layer.</summary>
    /// <returns>The backend.</returns>
    private CpuRenderBackend StartCpu()
    {
        var layer = NativeMethods.Send(NativeMethods.GetClass("CALayer"), Selectors.Layer);
        NativeMethods.SendVoid((nint)_view, Selectors.SetLayer, layer);
        _layer = (ulong)layer;
        return new(Present);
    }

    /// <summary>Shows a finished CPU frame as the contents of the window's layer.</summary>
    /// <param name="pixels">The premultiplied BGRA frame.</param>
    /// <param name="size">The frame size.</param>
    /// <param name="stride">The bytes per line.</param>
    private void Present(ReadOnlySpan<byte> pixels, SurfaceSize size, int stride)
    {
        if (Volatile.Read(ref _disposed) != 0 || size.IsEmpty || _layer == 0)
        {
            return;
        }

        // The layer may read the image after this call returns, so the image owns a copy of the frame.
        nint data;
        fixed (byte* bytes = pixels)
        {
            data = NativeMethods.DataCreate(0, bytes, stride * size.Height);
        }

        var provider = NativeMethods.DataProviderCreate(data);
        var colors = NativeMethods.ColorSpaceCreateDeviceRgb();
        var image = NativeMethods.ImageCreate((nuint)size.Width, (nuint)size.Height, BitsPerChannel, BitsPerPixel, (nuint)stride, colors, PremultipliedBgra, provider, null, 0, 0);
        NativeMethods.SendVoid((nint)_layer, Selectors.SetContents, image);
        NativeMethods.Release(image);
        NativeMethods.Release(colors);
        NativeMethods.Release(provider);
        NativeMethods.Release(data);
    }

    /// <summary>Reads the size of the drawing area and the display scale.</summary>
    private void ReadSize()
    {
        var bounds = Cocoa.Rect((nint)_view, Selectors.Bounds);
        Scale = Math.Max(1, NativeMethods.SendDouble((nint)_window, Selectors.BackingScaleFactor));
        _heightInPoints = bounds.Height;
        _size = new((int)Math.Round(bounds.Width * Scale), (int)Math.Round(bounds.Height * Scale));
        if (_layer != 0)
        {
            NativeMethods.SendVoidDouble((nint)_layer, Selectors.SetContentsScale, Scale);
        }
    }

    /// <summary>Reports a new size and a close, which Cocoa tells a delegate about and this head finds by looking.</summary>
    /// <param name="handler">Receives the events.</param>
    private void ReportWindowChanges(WindowEventHandler handler)
    {
        // A change of display scale changes the size in pixels too, so comparing the size finds both.
        var before = _size;
        ReadSize();
        if (_size != before)
        {
            Backend.Resize(_size);
            handler(new(WindowEventKind.Resized, _size.Width, _size.Height, 0, 0));
        }

        if (!_shown || _closeReported || NativeMethods.SendFlag((nint)_window, Selectors.IsVisible) != 0)
        {
            return;
        }

        _closeReported = true;
        handler(new(WindowEventKind.CloseRequested, 0, 0, 0, 0));
    }

    /// <summary>Reports an event to the viewer and passes it on to Cocoa, which moves, resizes and closes the window.</summary>
    /// <param name="next">The NSEvent.</param>
    /// <param name="handler">Receives the window event.</param>
    private void Dispatch(nint next, WindowEventHandler handler)
    {
        var type = NativeMethods.SendCount(next, Selectors.Type);
        if (type is KeyDown or KeyUp)
        {
            // Key events stay with the viewer: passed on with nothing to take them, Cocoa answers each with the alert sound.
            ReportKey(next, type == KeyDown, handler);
            return;
        }

        if (type != ApplicationDefined && Translate(next, type) is { Kind: not WindowEventKind.None } windowEvent)
        {
            handler(windowEvent);
        }

        NativeMethods.SendVoid((nint)_application, Selectors.SendEvent, next);
    }

    /// <summary>Reports a key event. Command and Q asks to close the window, as it does in every Mac application.</summary>
    /// <param name="next">The NSEvent.</param>
    /// <param name="down">Whether the key went down.</param>
    /// <param name="handler">Receives the window event.</param>
    private void ReportKey(nint next, bool down, WindowEventHandler handler)
    {
        var keyCode = NativeMethods.SendKeyCode(next, Selectors.KeyCode);
        var modifiers = CocoaInput.ToModifiers(NativeMethods.SendCount(next, Selectors.ModifierFlags));
        if (down && keyCode == QuitKey && (modifiers & ViewerModifiers.Control) != 0)
        {
            if (!_closeReported)
            {
                _closeReported = true;
                handler(new(WindowEventKind.CloseRequested, 0, 0, 0, 0));
            }

            return;
        }

        handler(new(down ? WindowEventKind.KeyDown : WindowEventKind.KeyUp, 0, 0, (uint)CocoaInput.ToKey(keyCode), (uint)modifiers));
        if (down && (modifiers & (ViewerModifiers.Control | ViewerModifiers.Meta)) == 0)
        {
            CocoaText.Report(next, (uint)modifiers, handler);
        }
    }

    /// <summary>Turns a pointer or scroll event into a window event.</summary>
    /// <param name="next">The NSEvent.</param>
    /// <param name="type">The event type.</param>
    /// <returns>The window event, or the default for an event the viewer has no use for.</returns>
    private WindowEvent Translate(nint next, nuint type)
    {
        var modifiers = (uint)CocoaInput.ToModifiers(NativeMethods.SendCount(next, Selectors.ModifierFlags));
        if (type == ScrollWheel)
        {
            // A trackpad reports points and a wheel reports lines, and a line is one notch.
            var perUnit = NativeMethods.SendFlag(next, Selectors.HasPreciseScrollingDeltas) != 0 ? UnitsPerPoint : WindowEvent.ScrollNotch;
            var sideways = NativeMethods.SendDouble(next, Selectors.ScrollingDeltaX) * perUnit;
            var upward = NativeMethods.SendDouble(next, Selectors.ScrollingDeltaY) * perUnit;
            return new(WindowEventKind.Scrolled, (int)Math.Round(sideways), (int)Math.Round(upward), 0, modifiers);
        }

        var location = NativeMethods.SendPoint(next, Selectors.LocationInWindow);
        var x = (int)Math.Round(location.X * Scale);
        var y = (int)Math.Round((_heightInPoints - location.Y) * Scale);
        return type switch
        {
            LeftDown => Press(ViewerButtons.Left, pressed: true, x, y, modifiers),
            LeftUp => Press(ViewerButtons.Left, pressed: false, x, y, modifiers),
            RightDown => Press(ViewerButtons.Right, pressed: true, x, y, modifiers),
            RightUp => Press(ViewerButtons.Right, pressed: false, x, y, modifiers),
            OtherDown when NativeMethods.Send(next, Selectors.ButtonNumber) == MiddleButtonNumber => Press(ViewerButtons.Middle, pressed: true, x, y, modifiers),
            OtherUp when NativeMethods.Send(next, Selectors.ButtonNumber) == MiddleButtonNumber => Press(ViewerButtons.Middle, pressed: false, x, y, modifiers),
            MouseMoved or LeftDragged or RightDragged or OtherDragged => new(WindowEventKind.PointerMoved, x, y, (uint)_held, modifiers),
            _ => default,
        };
    }

    /// <summary>Makes a button event and keeps track of the buttons held.</summary>
    /// <param name="button">The button.</param>
    /// <param name="pressed">Whether it went down.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>The window event.</returns>
    private WindowEvent Press(ViewerButtons button, bool pressed, int x, int y, uint modifiers)
    {
        _held = pressed ? _held | button : _held & ~button;
        return new(pressed ? WindowEventKind.PointerDown : WindowEventKind.PointerUp, x, y, (uint)button, modifiers);
    }
}
