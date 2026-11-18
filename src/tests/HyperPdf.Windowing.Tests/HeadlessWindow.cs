// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdf.Presentation.Input;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Tests;

/// <summary>A window with no display: it draws into the CPU backend and reports the events a test sends it, so the viewer runs end to end without a window system.</summary>
internal sealed class HeadlessWindow : IPlatformWindow
{
    /// <summary>The events a test queued.</summary>
    private readonly ConcurrentQueue<WindowEvent> _events = new();

    /// <summary>Set when something is waiting: an event, a frame request or a wake-up.</summary>
    private readonly ManualResetEventSlim _signal = new(false);

    /// <summary>Carries the events to whoever follows the window, as they are pumped.</summary>
    private readonly Signal<WindowEvent> _published = new();

    /// <summary>Carries the files dropped on the window, as they are pumped.</summary>
    private readonly Signal<IReadOnlyList<string>> _drops = new();

    /// <summary>The drops waiting for the next pump.</summary>
    private readonly ConcurrentQueue<IReadOnlyList<string>> _dropped = new();

    /// <summary>One when a frame was asked for and not yet reported.</summary>
    private int _frameWanted;

    /// <summary>Initializes a new instance of the <see cref="HeadlessWindow"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal HeadlessWindow(int width, int height)
    {
        ClientSize = new(width, height);
        var backend = new CpuRenderBackend((_, _, _) => FramesPresented++);
        backend.Resize(ClientSize);
        Cpu = backend;
        Accessibility = new RecordingAccessibilityBridge();
    }

    /// <inheritdoc/>
    public SurfaceSize ClientSize { get; }

    /// <inheritdoc/>
    public double Scale => 1;

    /// <inheritdoc/>
    public bool IsMaximized { get; private set; }

    /// <inheritdoc/>
    public bool HasSystemFrame => !OwnsFrame;

    /// <inheritdoc/>
    public long NativeHandle => 0;

    /// <inheritdoc/>
    public ScreenPoint ScreenOrigin => default;

    /// <inheritdoc/>
    public IRenderBackend Backend => Cpu;

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility { get; }

    /// <inheritdoc/>
    public IObservable<WindowEvent> Events => _published;

    /// <inheritdoc/>
    public IObservable<IReadOnlyList<string>> FileDrops => _drops;

    /// <summary>Gets the text last put on the clipboard, or <see langword="null"/> when none was.</summary>
    public string? ClipboardText { get; private set; }

    /// <summary>Gets the CPU backend, whose frame a test reads.</summary>
    internal CpuRenderBackend Cpu { get; }

    /// <summary>Gets the number of frames presented.</summary>
    internal int FramesPresented { get; private set; }

    /// <summary>Gets a value indicating whether a tree was attached to the window's accessibility bridge.</summary>
    internal bool Attached => Accessibility is RecordingAccessibilityBridge { Tree: not null };

    /// <summary>Gets the last title set.</summary>
    internal string Title { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the window was asked to fill the screen.</summary>
    internal bool IsFullScreen { get; private set; }

    /// <summary>Gets the size the viewer last asked for, or <see langword="null"/> when it asked for none.</summary>
    internal SurfaceSize? RequestedSize { get; private set; }

    /// <summary>Gets or sets a value indicating whether the window stands for one the system gives no title bar.</summary>
    internal bool OwnsFrame { get; set; }

    /// <summary>Gets how many times the viewer started moving the window.</summary>
    internal int Moves { get; private set; }

    /// <summary>Gets the edges of the last resize the viewer started.</summary>
    internal WindowEdges ResizedEdges { get; private set; }

    /// <summary>Gets a value indicating whether the viewer put the window away.</summary>
    internal bool Minimized { get; private set; }

    /// <inheritdoc/>
    public void SetTitle(string title) => Title = title;

    /// <inheritdoc/>
    public void SetFullScreen(bool on) => IsFullScreen = on;

    /// <inheritdoc/>
    public void SetMaximized(bool on) => IsMaximized = on;

    /// <inheritdoc/>
    public void BeginMove() => Moves++;

    /// <inheritdoc/>
    public void BeginResize(WindowEdges edges) => ResizedEdges = edges;

    /// <inheritdoc/>
    public void Minimize() => Minimized = true;

    /// <inheritdoc/>
    public void AskToClose() => Send(new(WindowEventKind.CloseRequested, 0, 0, 0, 0));

    /// <inheritdoc/>
    public void SetSize(int width, int height) => RequestedSize = new(width, height);

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor)
    {
        // A window with no display has no pointer to show.
    }

    /// <inheritdoc/>
    public void SetClipboardText(string text) => ClipboardText = text;

    /// <inheritdoc/>
    public string? GetClipboardText() => ClipboardText;

    /// <inheritdoc/>
    public void RequestFrame()
    {
        Volatile.Write(ref _frameWanted, 1);
        _signal.Set();
    }

    /// <inheritdoc/>
    public void Wake() => _signal.Set();

    /// <inheritdoc/>
    public void PumpEvents(int timeoutMilliseconds)
    {
        if (timeoutMilliseconds != 0)
        {
            _ = _signal.Wait(timeoutMilliseconds);
        }

        _signal.Reset();
        if (Interlocked.Exchange(ref _frameWanted, 0) != 0)
        {
            _published.OnNext(new(WindowEventKind.Exposed, 0, 0, 0, 0));
        }

        while (_events.TryDequeue(out var windowEvent))
        {
            _published.OnNext(windowEvent);
        }

        while (_dropped.TryDequeue(out var files))
        {
            _drops.OnNext(files);
        }
    }

    /// <inheritdoc/>
    public void Show()
    {
        // There is no display to show the window on.
    }

    /// <inheritdoc/>
    public void Close() => Dispose();

    /// <inheritdoc/>
    public void Dispose()
    {
        Cpu.Dispose();
        Accessibility.Dispose();
        _signal.Dispose();
        _published.Dispose();
    }

    /// <summary>Queues files dropped on the window for the next pump.</summary>
    /// <param name="files">The files.</param>
    internal void Drop(IReadOnlyList<string> files)
    {
        _dropped.Enqueue(files);
        _signal.Set();
    }

    /// <summary>Queues an event for the next pump.</summary>
    /// <param name="windowEvent">The event.</param>
    internal void Send(in WindowEvent windowEvent)
    {
        _events.Enqueue(windowEvent);
        _signal.Set();
    }
}
