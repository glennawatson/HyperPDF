// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>A native Wayland window: an xdg toplevel that shows CPU frames through shared memory and reports its input as <see cref="WindowEvent"/> values.</summary>
[DebuggerDisplay("WaylandWindow: {ClientSize}")]
public sealed class WaylandWindow : IPlatformWindow
{
    /// <summary>The wl_display object id.</summary>
    private const uint DisplayId = 1;

    /// <summary>How long the first setup steps may wait for the compositor.</summary>
    private const int SetupTimeoutMilliseconds = 2000;

    /// <summary>The first window width in pixels.</summary>
    private const int InitialWidth = 1024;

    /// <summary>The first window height in pixels.</summary>
    private const int InitialHeight = 768;

    /// <summary>The application id compositors use to find the desktop file and icon.</summary>
    private const string ApplicationId = "hyperpdf";

    /// <summary>The wl_seat capability bit for a pointer.</summary>
    private const uint PointerCapability = 1;

    /// <summary>The wl_seat capability bit for a keyboard.</summary>
    private const uint KeyboardCapability = 2;

    /// <summary>The wl_pointer button state for pressed.</summary>
    private const uint ButtonPressed = 1;

    /// <summary>The wl_keyboard key state for pressed.</summary>
    private const uint KeyPressed = 1;

    /// <summary>The wl_pointer axis number of the vertical wheel.</summary>
    private const uint VerticalAxis = 0;

    /// <summary>The connection.</summary>
    private readonly WaylandConnection _connection;

    /// <summary>The wake-up that ends a wait for events.</summary>
    private readonly EventWake _wake;

    /// <summary>The global names and versions found in the registry.</summary>
    private readonly WaylandGlobals _globals = new();

    /// <summary>The object ids of this window's protocol objects.</summary>
    private readonly WaylandObjects _objects = new();

    /// <summary>The handler of the current <see cref="PumpEvents"/> call.</summary>
    private WindowEventHandler? _sink;

    /// <summary>The frame memory.</summary>
    private WaylandShmPool? _pool;

    /// <summary>The size of the drawing area.</summary>
    private SurfaceSize _size = new(InitialWidth, InitialHeight);

    /// <summary>The last pointer position.</summary>
    private WindowEvent _pointer;

    /// <summary>The modifier keys the keyboard last reported, as <see cref="ViewerModifiers"/> bits.</summary>
    private uint _modifiers;

    /// <summary>The pointer buttons held.</summary>
    private ViewerButtons _held;

    /// <summary>One when a frame was asked for and not yet reported.</summary>
    private int _frameWanted;

    /// <summary>Whether the first xdg_surface configure arrived.</summary>
    private bool _configured;

    /// <summary>Whether the sync callback arrived.</summary>
    private bool _synced;

    /// <summary>One once the window is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="WaylandWindow"/> class.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes this window to AT-SPI.</param>
    /// <exception cref="PlatformNotSupportedException">No Wayland compositor could be reached or it lacks a required protocol.</exception>
    public WaylandWindow(RenderBackendPreference preference, IAccessibilityBridge accessibility)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        Accessibility = accessibility;
        Core.Platform.NativeLibraries.Register(typeof(NativeMethods).Assembly, NativeMethods.Library, ["libc.so.6"]);
        _connection = WaylandConnection.TryConnect() ?? throw new PlatformNotSupportedException("No Wayland compositor could be reached.");
        _wake = new();
        Backend = RenderBackendSelector.Choose(preference, this, static _ => null, static window => new CpuRenderBackend(window.Present)).Backend;
        Backend.Resize(_size);
        Setup();
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public double Scale => 1.0;

    /// <inheritdoc/>
    public long NativeHandle => 0;

    /// <summary>Gets the corner of the screen, because a Wayland compositor does not tell a window where it is.</summary>
    public ScreenPoint ScreenOrigin => default;

    /// <inheritdoc/>
    public IRenderBackend Backend { get; }

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <summary>Tells whether a Wayland compositor looks reachable, without opening a window.</summary>
    /// <returns><see langword="true"/> when WAYLAND_DISPLAY names a socket that exists.</returns>
    public static bool IsAvailable()
    {
        var name = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var path = Path.IsPathRooted(name) ? name : Path.Combine(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty, name);
        return File.Exists(path);
    }

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        _connection.Writer.Begin(_objects.Toplevel, WaylandProtocol.ToplevelSetTitle);
        _connection.Writer.WriteString(title);
        _connection.Writer.End();
        _connection.Flush();
    }

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor)
    {
        // Cursor shapes need the cursor theme and a pointer enter serial; the compositor's default arrow stays until the input work adds them.
        _ = cursor;
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
        // An xdg toplevel is shown by its first buffer commit, which the render backend makes when it presents a frame.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Dispose();

    /// <summary>Waits for and handles queued events.</summary>
    /// <param name="timeoutMilliseconds">How long to wait for the first event, 0 to not wait.</param>
    /// <param name="handler">Receives each event.</param>
    public void PumpEvents(int timeoutMilliseconds, WindowEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _sink = handler;
        try
        {
            var wait = Volatile.Read(ref _frameWanted) != 0 ? 0 : timeoutMilliseconds;
            _ = _connection.Pump(wait, _wake, Dispatch);
            if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
            {
                handler(new(WindowEventKind.Exposed, 0, 0, 0, 0));
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

        Backend.Dispose();
        Accessibility.Dispose();
        _pool?.Dispose();
        _wake.Dispose();
        _connection.Dispose();
    }

    /// <summary>Handles wl_display events, where an error ends the connection.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    /// <exception cref="IOException">The compositor reported a protocol error.</exception>
    private static void HandleDisplay(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode != 0)
        {
            return;
        }

        var objectId = reader.ReadUInt();
        var code = reader.ReadUInt();
        throw new IOException($"The Wayland compositor reported error {code} on object {objectId}: {reader.ReadString()}");
    }

    /// <summary>Binds the globals, makes the toplevel and waits for its first configure.</summary>
    private void Setup()
    {
        var registry = _connection.NewId();
        Send(DisplayId, WaylandProtocol.DisplayGetRegistry, registry);
        _objects.Registry = registry;
        _objects.Callback = _connection.NewId();
        Send(DisplayId, WaylandProtocol.DisplaySync, _objects.Callback);
        WaitUntil(() => _synced);

        _objects.Compositor = Bind(registry, WaylandProtocol.CompositorInterface, _globals.Compositor, WaylandProtocol.CompositorVersion);
        _objects.Shm = Bind(registry, WaylandProtocol.ShmInterface, _globals.Shm, WaylandProtocol.ShmVersion);
        _objects.WmBase = Bind(registry, WaylandProtocol.WmBaseInterface, _globals.WmBase, WaylandProtocol.WmBaseVersion);
        _objects.Seat = _globals.Seat.Name == 0 ? 0 : Bind(registry, WaylandProtocol.SeatInterface, _globals.Seat, WaylandProtocol.SeatVersion);

        _objects.Surface = _connection.NewId();
        Send(_objects.Compositor, WaylandProtocol.CompositorCreateSurface, _objects.Surface);
        _objects.XdgSurface = _connection.NewId();
        _connection.Writer.Begin(_objects.WmBase, WaylandProtocol.WmBaseGetXdgSurface);
        _connection.Writer.WriteUInt(_objects.XdgSurface);
        _connection.Writer.WriteUInt(_objects.Surface);
        _connection.Writer.End();
        _objects.Toplevel = _connection.NewId();
        Send(_objects.XdgSurface, WaylandProtocol.XdgSurfaceGetToplevel, _objects.Toplevel);
        _connection.Writer.Begin(_objects.Toplevel, WaylandProtocol.ToplevelSetAppId);
        _connection.Writer.WriteString(ApplicationId);
        _connection.Writer.End();
        _connection.Writer.Begin(_objects.Surface, WaylandProtocol.SurfaceCommit);
        _connection.Writer.End();
        _connection.Flush();
        WaitUntil(() => _configured);
    }

    /// <summary>Sends a request with one object id argument.</summary>
    /// <param name="objectId">The object.</param>
    /// <param name="opcode">The request.</param>
    /// <param name="argument">The argument.</param>
    private void Send(uint objectId, int opcode, uint argument)
    {
        _connection.Writer.Begin(objectId, opcode);
        _connection.Writer.WriteUInt(argument);
        _connection.Writer.End();
        _connection.Flush();
    }

    /// <summary>Binds a registry global.</summary>
    /// <param name="registry">The registry object.</param>
    /// <param name="interfaceName">The interface name.</param>
    /// <param name="global">The advertised global.</param>
    /// <param name="wanted">The newest version the head implements.</param>
    /// <returns>The new object id.</returns>
    /// <exception cref="PlatformNotSupportedException">The compositor does not offer the global.</exception>
    private uint Bind(uint registry, string interfaceName, in WaylandGlobal global, uint wanted)
    {
        if (global.Name == 0)
        {
            throw new PlatformNotSupportedException($"The Wayland compositor does not offer {interfaceName}.");
        }

        var id = _connection.NewId();
        _connection.Writer.Begin(registry, WaylandProtocol.RegistryBind);
        _connection.Writer.WriteUInt(global.Name);
        _connection.Writer.WriteString(interfaceName);
        _connection.Writer.WriteUInt(Math.Min(wanted, global.Version));
        _connection.Writer.WriteUInt(id);
        _connection.Writer.End();
        _connection.Flush();
        return id;
    }

    /// <summary>Handles events until a condition holds.</summary>
    /// <param name="done">The condition.</param>
    /// <exception cref="TimeoutException">The compositor did not answer in time.</exception>
    private void WaitUntil(Func<bool> done)
    {
        var start = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds > SetupTimeoutMilliseconds)
            {
                throw new TimeoutException("The Wayland compositor did not answer in time.");
            }

            _ = _connection.Pump(SetupTimeoutMilliseconds, _wake, Dispatch);
        }
    }

    /// <summary>Handles one event from the compositor.</summary>
    /// <param name="reader">The event.</param>
    private void Dispatch(ref WaylandMessageReader reader)
    {
        var (objectId, opcode, _) = reader.Header;
        if (objectId == DisplayId)
        {
            HandleDisplay(opcode, ref reader);
        }
        else if (objectId == _objects.Registry)
        {
            HandleRegistry(opcode, ref reader);
        }
        else if (objectId == _objects.Callback && opcode == 0)
        {
            _synced = true;
        }
        else
        {
            DispatchWindow(objectId, opcode, ref reader);
        }
    }

    /// <summary>Handles an event of the shell, the seat or a buffer.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void DispatchWindow(uint objectId, int opcode, ref WaylandMessageReader reader)
    {
        if (objectId == _objects.WmBase && opcode == WaylandProtocol.WmBasePing)
        {
            Send(_objects.WmBase, WaylandProtocol.WmBasePong, reader.ReadUInt());
        }
        else if (objectId == _objects.XdgSurface && opcode == 0)
        {
            Send(_objects.XdgSurface, WaylandProtocol.XdgSurfaceAckConfigure, reader.ReadUInt());
            _configured = true;
        }
        else if (objectId == _objects.Toplevel)
        {
            HandleToplevel(opcode, ref reader);
        }
        else
        {
            DispatchInput(objectId, opcode, ref reader);
        }
    }

    /// <summary>Handles an event of the seat, its pointer or keyboard, or a released buffer.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void DispatchInput(uint objectId, int opcode, ref WaylandMessageReader reader)
    {
        if (objectId == _objects.Seat && opcode == 0)
        {
            HandleSeat(reader.ReadUInt());
        }
        else if (objectId == _objects.Pointer)
        {
            HandlePointer(opcode, ref reader);
        }
        else if (objectId == _objects.Keyboard)
        {
            HandleKeyboard(opcode, ref reader);
        }
        else if (opcode == 0)
        {
            _ = _pool?.Release(objectId);
        }
    }

    /// <summary>Records the globals the compositor offers.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleRegistry(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode != 0)
        {
            return;
        }

        var name = reader.ReadUInt();
        var interfaceName = reader.ReadString();
        _globals.Record(interfaceName, new(name, reader.ReadUInt()));
    }

    /// <summary>Handles xdg_toplevel events: a new size or a close request.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleToplevel(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode == 1)
        {
            Emit(new(WindowEventKind.CloseRequested, 0, 0, 0, 0));
            return;
        }

        if (opcode != 0)
        {
            return;
        }

        var width = reader.ReadInt();
        var height = reader.ReadInt();
        if (width <= 0 || height <= 0 || new SurfaceSize(width, height) == _size)
        {
            return;
        }

        _size = new(width, height);
        Backend.Resize(_size);
        Emit(new(WindowEventKind.Resized, width, height, 0, 0));
    }

    /// <summary>Asks for the pointer and keyboard the seat offers.</summary>
    /// <param name="capabilities">The capability bits.</param>
    private void HandleSeat(uint capabilities)
    {
        if ((capabilities & PointerCapability) != 0 && _objects.Pointer == 0)
        {
            _objects.Pointer = _connection.NewId();
            Send(_objects.Seat, WaylandProtocol.SeatGetPointer, _objects.Pointer);
        }

        if ((capabilities & KeyboardCapability) == 0 || _objects.Keyboard != 0)
        {
            return;
        }

        _objects.Keyboard = _connection.NewId();
        Send(_objects.Seat, WaylandProtocol.SeatGetKeyboard, _objects.Keyboard);
    }

    /// <summary>Turns wl_pointer events into window events.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandlePointer(int opcode, ref WaylandMessageReader reader)
    {
        switch (opcode)
        {
            case WaylandProtocol.PointerEnter:
                {
                    _ = reader.ReadUInt();
                    _ = reader.ReadUInt();
                    MovePointer(ref reader);
                    break;
                }

            case WaylandProtocol.PointerMotion:
                {
                    _ = reader.ReadUInt();
                    MovePointer(ref reader);
                    break;
                }

            case WaylandProtocol.PointerButton:
                {
                    _ = reader.ReadUInt();
                    _ = reader.ReadUInt();
                    var button = EvdevInput.ToButton(reader.ReadUInt());
                    PressButton(button, reader.ReadUInt() == ButtonPressed);
                    break;
                }

            case WaylandProtocol.PointerAxis:
                {
                    _ = reader.ReadUInt();
                    var vertical = reader.ReadUInt() == VerticalAxis;
                    var amount = EvdevInput.ToScroll(reader.ReadFixed());
                    Emit(new(WindowEventKind.Scrolled, vertical ? 0 : amount, vertical ? amount : 0, 0, _modifiers));
                    break;
                }
        }
    }

    /// <summary>Reports a pointer button going down or coming up and keeps track of the buttons held.</summary>
    /// <param name="button">The button.</param>
    /// <param name="pressed">Whether it went down.</param>
    private void PressButton(ViewerButtons button, bool pressed)
    {
        if (button == ViewerButtons.None)
        {
            return;
        }

        _held = pressed ? _held | button : _held & ~button;
        Emit(new(pressed ? WindowEventKind.PointerDown : WindowEventKind.PointerUp, _pointer.X, _pointer.Y, (uint)button, _modifiers));
    }

    /// <summary>Reads a pointer position and reports the move.</summary>
    /// <param name="reader">The event arguments, at the x coordinate.</param>
    private void MovePointer(ref WaylandMessageReader reader)
    {
        var x = (int)Math.Round(reader.ReadFixed());
        var y = (int)Math.Round(reader.ReadFixed());
        _pointer = new(WindowEventKind.PointerMoved, x, y, (uint)_held, _modifiers);
        Emit(_pointer);
    }

    /// <summary>Turns wl_keyboard events into window events.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleKeyboard(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode == WaylandProtocol.KeyboardKey)
        {
            _ = reader.ReadUInt();
            _ = reader.ReadUInt();
            var key = (uint)EvdevInput.ToKey(reader.ReadUInt());
            var kind = reader.ReadUInt() == KeyPressed ? WindowEventKind.KeyDown : WindowEventKind.KeyUp;
            Emit(new(kind, _pointer.X, _pointer.Y, key, _modifiers));
        }
        else if (opcode == WaylandProtocol.KeyboardModifiers)
        {
            _ = reader.ReadUInt();

            // The held modifier mask uses the same bits as X11 state on the standard key maps: Shift, Control, Mod1 for Alt and Mod4 for Meta.
            _modifiers = (uint)X11.X11Input.ToModifiers(reader.ReadUInt());
        }
    }

    /// <summary>Hands an event to the current pump handler, if there is one.</summary>
    /// <param name="windowEvent">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Emit(in WindowEvent windowEvent) => _sink?.Invoke(windowEvent);

    /// <summary>Copies a finished frame into a free buffer and commits it.</summary>
    /// <param name="pixels">The premultiplied BGRA frame.</param>
    /// <param name="size">The frame size.</param>
    /// <param name="stride">The bytes per line.</param>
    private void Present(ReadOnlySpan<byte> pixels, SurfaceSize size, int stride)
    {
        if (Volatile.Read(ref _disposed) != 0 || size.IsEmpty || !_configured)
        {
            return;
        }

        if (_pool is null || _pool.Width != size.Width || _pool.Height != size.Height)
        {
            _pool?.Dispose();
            _pool = new(_connection, _objects.Shm, size.Width, size.Height);
        }

        if (!_pool.TryAcquire(out var index))
        {
            return;
        }

        pixels[..(stride * size.Height)].CopyTo(_pool.Pixels(index));
        var writer = _connection.Writer;
        writer.Begin(_objects.Surface, WaylandProtocol.SurfaceAttach);
        writer.WriteUInt(_pool.BufferId(index));
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.End();
        writer.Begin(_objects.Surface, WaylandProtocol.SurfaceDamageBuffer);
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteInt(size.Width);
        writer.WriteInt(size.Height);
        writer.End();
        writer.Begin(_objects.Surface, WaylandProtocol.SurfaceCommit);
        writer.End();
        _connection.Flush();
    }
}
