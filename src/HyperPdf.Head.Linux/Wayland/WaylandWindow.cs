// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Vulkan;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>A native Wayland window: an xdg toplevel that shows CPU frames through shared memory and reports its input as <see cref="WindowEvent"/> values.</summary>
[DebuggerDisplay("WaylandWindow: {ClientSize}")]
public sealed class WaylandWindow : IPlatformWindow, IDmabufWindow
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

    /// <summary>The keyboard layout and the modifier keys held.</summary>
    private readonly WaylandKeyboard _keyboard = new();

    /// <summary>Whether the backend is chosen. Sizes that arrive before are given to it once it is.</summary>
    private readonly bool _hasBackend;

    /// <summary>The window's side of the clipboard.</summary>
    private readonly WaylandClipboard _clipboard;

    /// <summary>Carries the window's events to whoever follows the window.</summary>
    private readonly Signal<WindowEvent> _events = new();

    /// <summary>The frame memory.</summary>
    private WaylandShmPool? _pool;

    /// <summary>The size of the drawing area.</summary>
    private SurfaceSize _size = new(InitialWidth, InitialHeight);

    /// <summary>The last pointer position.</summary>
    private WindowEvent _pointer;

    /// <summary>The modifier keys the keyboard last reported, as <see cref="ViewerModifiers"/> bits.</summary>
    private uint _modifiers;

    /// <summary>The size the compositor gave the window, in its own units.</summary>
    private SurfaceSize _logical = new(InitialWidth, InitialHeight);

    /// <summary>The device pixels of one compositor unit on the display the window is on.</summary>
    private int _bufferScale = 1;

    /// <summary>The buffer scale the compositor was last told, so it is only told again when it changes.</summary>
    private int _sentScale = 1;

    /// <summary>The scale the compositor reports as a fraction, or 0 when it only reports whole numbers.</summary>
    private double _fraction;

    /// <summary>The size in the compositor's units the viewport was last told, so it is only told again when it changes.</summary>
    private SurfaceSize _sentDestination;

    /// <summary>The serial of the last key or button event, which the compositor asks for when the clipboard is set.</summary>
    private uint _serial;

    /// <summary>The pointer buttons held.</summary>
    private ViewerButtons _held;

    /// <summary>One when a frame was asked for and not yet reported.</summary>
    private int _frameWanted;

    /// <summary>Whether the first xdg_surface configure arrived.</summary>
    private bool _configured;

    /// <summary>Whether the compositor last said the window is maximised.</summary>
    private bool _maximized;

    /// <summary>Whether the sync callback arrived.</summary>
    private bool _synced;

    /// <summary>The buffers of GPU frames, or <see langword="null"/> when the compositor takes no dmabufs.</summary>
    private WaylandDmabufPool? _dmabuf;

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
        _clipboard = new(_connection);
        _wake = new();

        // The compositor says what it can take before the backend is chosen: the GPU needs it to accept dmabufs.
        Setup();
        Backend = RenderBackendSelector.Choose(preference, this, static window => window.StartVulkan(), static window => new CpuRenderBackend(window.Present)).Backend;
        Backend.Resize(_size);
        _hasBackend = true;
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize => _size;

    /// <inheritdoc/>
    public double Scale => _fraction > 0 ? _fraction : _bufferScale;

    /// <inheritdoc/>
    public bool IsMaximized => _maximized;

    /// <inheritdoc/>
    public long NativeHandle => 0;

    /// <summary>Gets the corner of the screen, because a Wayland compositor does not tell a window where it is.</summary>
    public ScreenPoint ScreenOrigin => default;

    /// <inheritdoc/>
    public IRenderBackend Backend { get; }

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <inheritdoc/>
    public IObservable<WindowEvent> Events => _events;

    /// <inheritdoc/>
    public IObservable<IReadOnlyList<string>> FileDrops => _clipboard.Drops;

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
    public void SetMaximized(bool on)
    {
        _connection.Writer.Begin(_objects.Toplevel, on ? WaylandProtocol.ToplevelSetMaximized : WaylandProtocol.ToplevelUnsetMaximized);
        _connection.Writer.End();
        _connection.Flush();
    }

    /// <inheritdoc/>
    public void SetSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // A Wayland client chooses its own size until the compositor says otherwise, so the size is taken as a compositor would give it.
        _logical = new(width, height);
        ApplySize();
    }

    /// <inheritdoc/>
    public void SetFullScreen(bool on)
    {
        // The compositor picks the screen the window is on when no output is named.
        _connection.Writer.Begin(_objects.Toplevel, on ? WaylandProtocol.ToplevelSetFullscreen : WaylandProtocol.ToplevelUnsetFullscreen);
        if (on)
        {
            _connection.Writer.WriteUInt(0);
        }

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
    public void SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _clipboard.SetText(text, _serial);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetClipboardText() => _clipboard.GetText();

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
    public void PumpEvents(int timeoutMilliseconds)
    {
        var wait = Volatile.Read(ref _frameWanted) != 0 ? 0 : timeoutMilliseconds;
        _ = _connection.Pump(wait, _wake, Dispatch);
        if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
        {
            _events.OnNext(new(WindowEventKind.Exposed, 0, 0, 0, 0));
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
        _events.Dispose();
        _clipboard.Dispose();
        _dmabuf?.Release();
        _pool?.Dispose();
        _wake.Dispose();
        _connection.Dispose();
    }

    /// <inheritdoc/>
    bool IDmabufWindow.SetFrames(ReadOnlySpan<DmabufFrame> frames)
    {
        if (_dmabuf is not { } buffers)
        {
            return false;
        }

        buffers.SetFrames(frames);
        return true;
    }

    /// <inheritdoc/>
    int IDmabufWindow.TakeFreeFrame() => Volatile.Read(ref _disposed) == 0 && _configured && _dmabuf is { } buffers ? buffers.TakeFree() : -1;

    /// <inheritdoc/>
    void IDmabufWindow.ShowFrame(int index)
    {
        if (_dmabuf is { } buffers)
        {
            Commit(buffers.BufferId(index), buffers.Size);
        }
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
        _objects.Output = _globals.Output.Name == 0 ? 0 : Bind(registry, WaylandProtocol.OutputInterface, _globals.Output, WaylandProtocol.OutputVersion);
        if (_objects.Seat != 0 && _globals.DataDeviceManager.Name != 0)
        {
            var manager = Bind(registry, WaylandProtocol.DataDeviceManagerInterface, _globals.DataDeviceManager, WaylandProtocol.DataDeviceManagerVersion);
            _clipboard.Attach(manager, _objects.Seat);
        }

        _objects.Surface = _connection.NewId();
        Send(_objects.Compositor, WaylandProtocol.CompositorCreateSurface, _objects.Surface);
        _objects.XdgSurface = _connection.NewId();
        _connection.Writer.Begin(_objects.WmBase, WaylandProtocol.WmBaseGetXdgSurface);
        _connection.Writer.WriteUInt(_objects.XdgSurface);
        _connection.Writer.WriteUInt(_objects.Surface);
        _connection.Writer.End();
        FollowFractionalScale(registry);
        if (_globals.Dmabuf.Name != 0 && _globals.Dmabuf.Version >= WaylandDmabufPool.Version)
        {
            _dmabuf = new(_connection, Bind(registry, WaylandDmabufPool.Interface, _globals.Dmabuf, WaylandDmabufPool.Version));
        }

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

    /// <summary>Asks for the surface's scale as a fraction, where the compositor can report one and can show a buffer at another size.</summary>
    /// <param name="registry">The registry object.</param>
    private void FollowFractionalScale(uint registry)
    {
        if (_globals.FractionalScale.Name == 0 || _globals.Viewporter.Name == 0)
        {
            return;
        }

        var manager = Bind(registry, WaylandProtocol.FractionalScaleManagerInterface, _globals.FractionalScale, 1);
        var viewporter = Bind(registry, WaylandProtocol.ViewporterInterface, _globals.Viewporter, 1);
        _objects.FractionalScale = _connection.NewId();
        SendForSurface(manager, WaylandProtocol.FractionalScaleManagerGetScale, _objects.FractionalScale);
        _objects.Viewport = _connection.NewId();
        SendForSurface(viewporter, WaylandProtocol.ViewporterGetViewport, _objects.Viewport);
    }

    /// <summary>Sends a request that makes an object for the window's surface.</summary>
    /// <param name="objectId">The object asked.</param>
    /// <param name="opcode">The request.</param>
    /// <param name="created">The id of the object it makes.</param>
    private void SendForSurface(uint objectId, int opcode, uint created)
    {
        _connection.Writer.Begin(objectId, opcode);
        _connection.Writer.WriteUInt(created);
        _connection.Writer.WriteUInt(_objects.Surface);
        _connection.Writer.End();
        _connection.Flush();
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
        else if (objectId == _objects.FractionalScale && opcode == WaylandProtocol.FractionalScalePreferred)
        {
            // The compositor reports the scale in 120ths, so 1.25 and 1.5 are exact.
            _fraction = DisplayScale.Resolve(reader.ReadUInt() / WaylandProtocol.FractionalScaleUnits);
            ApplySize();
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
        if (_clipboard.TryHandle(objectId, opcode, ref reader))
        {
            return;
        }

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
        else if (objectId == _objects.Output && _objects.Output != 0)
        {
            HandleOutput(opcode, ref reader);
        }
        else
        {
            HandleBuffer(objectId, opcode, ref reader);
        }
    }

    /// <summary>Handles an event of a frame buffer: the compositor made one, or is done with one.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleBuffer(uint objectId, int opcode, ref WaylandMessageReader reader)
    {
        var handled = _dmabuf?.Handle(objectId, opcode, ref reader) ?? DmabufEvent.None;
        if (handled == DmabufEvent.BuffersReady)
        {
            // Frames drawn while the buffers were being made were skipped, so the window asks for one now.
            Emit(new(WindowEventKind.Exposed, 0, 0, 0, 0));
        }
        else if (handled == DmabufEvent.None && opcode == 0)
        {
            _ = _pool?.Release(objectId);
        }
    }

    /// <summary>Starts the Vulkan backend, drawing into images the compositor reads as dmabufs.</summary>
    /// <returns>The backend, or <see langword="null"/> when the GPU or the compositor cannot share frames, so the CPU backend is used.</returns>
    private VulkanRenderBackend? StartVulkan()
    {
        if (_dmabuf is not { } buffers || VulkanContext.TryCreate() is not { } context)
        {
            return null;
        }

        if (!context.CanExportDmabuf)
        {
            context.Dispose();
            return null;
        }

        var backend = new VulkanRenderBackend(context);
        backend.Resize(_size);
        try
        {
            // The compositor lists the layouts it takes after the global is bound; they must be in before the first buffers are asked for.
            Roundtrip();
            if (backend.TryPresentToDmabufWindow(this))
            {
                WaitUntil(() => buffers.IsSettled);
                if (!buffers.HasFailed)
                {
                    return backend;
                }
            }
        }
        catch (TimeoutException)
        {
            // A compositor that never answers for the buffers is treated as one that refused them.
        }

        backend.Dispose();
        buffers.Release();
        return null;
    }

    /// <summary>Waits until the compositor has handled every request sent so far and its events have been read.</summary>
    private void Roundtrip()
    {
        _synced = false;
        _objects.Callback = _connection.NewId();
        Send(DisplayId, WaylandProtocol.DisplaySync, _objects.Callback);
        WaitUntil(() => _synced);
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

        // The states come as an array of 32 bit values: its length in bytes, then the values.
        var maximized = false;
        for (var left = reader.ReadUInt() / sizeof(uint); left != 0; left--)
        {
            maximized |= reader.ReadUInt() == WaylandProtocol.ToplevelStateMaximized;
        }

        _maximized = maximized;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _logical = new(width, height);
        ApplySize();
    }

    /// <summary>Follows the scale of the display: the compositor sizes the window in its own units and the window draws that many times more pixels.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleOutput(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode != WaylandProtocol.OutputScale)
        {
            return;
        }

        // A buffer scale is a whole number, so the user's own setting is rounded to the nearest one.
        var scale = Math.Max(1, (int)Math.Round(DisplayScale.Resolve(reader.ReadInt())));
        if (scale == _bufferScale || _objects.FractionalScale != 0)
        {
            // A compositor that reports the scale as a fraction sizes the surface through its viewport, and the buffer scale stays one.
            return;
        }

        _bufferScale = scale;
        ApplySize();
    }

    /// <summary>Sizes the drawing area in device pixels from the size the compositor gave and the display's scale, and reports a change.</summary>
    private void ApplySize()
    {
        var scale = Scale;
        var size = new SurfaceSize((int)Math.Round(_logical.Width * scale), (int)Math.Round(_logical.Height * scale));
        if (size == _size)
        {
            return;
        }

        _size = size;
        if (_hasBackend)
        {
            Backend.Resize(_size);
        }

        Emit(new(WindowEventKind.Resized, size.Width, size.Height, 0, 0));
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
                    _serial = reader.ReadUInt();
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
        // The compositor reports the pointer in its own units, and the viewer works in device pixels.
        var scale = Scale;
        var x = (int)Math.Round(reader.ReadFixed() * scale);
        var y = (int)Math.Round(reader.ReadFixed() * scale);
        _pointer = new(WindowEventKind.PointerMoved, x, y, (uint)_held, _modifiers);
        Emit(_pointer);
    }

    /// <summary>Turns wl_keyboard events into window events.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleKeyboard(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode == WaylandProtocol.KeyboardKeymap)
        {
            // The key map comes as a file, which the keyboard reads and closes.
            var format = reader.ReadUInt();
            if (_connection.TryTakeDescriptor(out var keymap))
            {
                _keyboard.LoadKeymap(keymap, format, reader.ReadUInt());
            }
        }
        else if (opcode == WaylandProtocol.KeyboardKey)
        {
            _serial = reader.ReadUInt();
            _ = reader.ReadUInt();
            var code = reader.ReadUInt();
            var pressed = reader.ReadUInt() == KeyPressed;
            Emit(new(pressed ? WindowEventKind.KeyDown : WindowEventKind.KeyUp, _pointer.X, _pointer.Y, (uint)_keyboard.ToKey(code), _modifiers));
            var scalar = ((ViewerModifiers)_modifiers & (ViewerModifiers.Control | ViewerModifiers.Alt | ViewerModifiers.Meta)) == 0 ? _keyboard.ToScalar(code) : 0;
            if (pressed && scalar != 0)
            {
                Emit(new(WindowEventKind.TextInput, _pointer.X, _pointer.Y, scalar, _modifiers));
            }
        }
        else if (opcode == WaylandProtocol.KeyboardModifiers)
        {
            _ = reader.ReadUInt();
            _keyboard.SetModifiers(reader.ReadUInt(), reader.ReadUInt(), reader.ReadUInt(), reader.ReadUInt());
            _modifiers = _keyboard.Modifiers;
        }
    }

    /// <summary>Hands an event to the current pump handler, if there is one.</summary>
    /// <param name="windowEvent">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Emit(in WindowEvent windowEvent) => _events.OnNext(windowEvent);

    /// <summary>Tells the compositor how the buffer's pixels map to the window's size, when that changed. The next commit applies it with the buffer.</summary>
    /// <param name="writer">The request writer.</param>
    private void SendScale(WaylandMessageWriter writer)
    {
        if (_sentScale != _bufferScale)
        {
            _sentScale = _bufferScale;
            writer.Begin(_objects.Surface, WaylandProtocol.SurfaceSetBufferScale);
            writer.WriteInt(_bufferScale);
            writer.End();
        }

        if (_objects.Viewport == 0 || _sentDestination == _logical)
        {
            return;
        }

        // The buffer holds device pixels; the viewport shows it at the window's size in the compositor's units.
        _sentDestination = _logical;
        writer.Begin(_objects.Viewport, WaylandProtocol.ViewportSetDestination);
        writer.WriteInt(_logical.Width);
        writer.WriteInt(_logical.Height);
        writer.End();
    }

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
        Commit(_pool.BufferId(index), size);
    }

    /// <summary>Shows a buffer that holds a finished frame.</summary>
    /// <param name="buffer">The wl_buffer id.</param>
    /// <param name="size">The frame size.</param>
    private void Commit(uint buffer, SurfaceSize size)
    {
        var writer = _connection.Writer;
        SendScale(writer);
        writer.Begin(_objects.Surface, WaylandProtocol.SurfaceAttach);
        writer.WriteUInt(buffer);
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
