// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Theming;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.Viewer;

/// <summary>
/// One viewer window at work: the view models, the page canvas and the loop that waits for window events and posted work. A platform
/// head opens its window, makes a session on the same thread and runs it.
/// </summary>
[DebuggerDisplay("ViewerSession: closed {_closed}")]
public sealed class ViewerSession : IDisposable
{
    /// <summary>The wait for events that has no limit.</summary>
    private const int NoLimit = -1;

    /// <summary>What the session owns.</summary>
    private readonly MultipleDisposable _owned = [];

    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The context that brings posted work to this thread.</summary>
    private readonly WindowThreadContext _context;

    /// <summary>The event handler handed to every pump, made once so a turn of the loop allocates nothing.</summary>
    private WindowEventHandler? _handleEvent;

    /// <summary>Whether the user asked to close the window.</summary>
    private bool _closed;

    /// <summary>Initializes a new instance of the <see cref="ViewerSession"/> class on the thread that pumps the window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="services">The application services, made with a <see cref="ViewerRenderHead"/> for the same window.</param>
    /// <param name="context">The context from <see cref="UseWindowThread"/>.</param>
    public ViewerSession(IPlatformWindow window, AppServices services, WindowThreadContext context)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        _window = window;
        _context = context;
        Main = new(services);
        Canvas = new(window) { Colors = ViewerColors.From(services.CurrentTheme.Scheme) };
        Accessibility = new(window);
        window.Accessibility.Attach(Accessibility);
        _owned.Add(Accessibility);
        _owned.Add(Canvas);
        _owned.Add(Main);
        _owned.Add(services.Theme.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(ShowTheme, ErrorTrace.Write));
        _owned.Add(Main.WhenChanged(static main => main.SelectedTab).SubscribeSafe(ShowTab, ErrorTrace.Write));
    }

    /// <summary>Gets the main view model.</summary>
    public MainViewModel Main { get; }

    /// <summary>Gets the page canvas.</summary>
    public ViewerCanvas Canvas { get; }

    /// <summary>Gets the tree the window's accessibility bridge serves to screen readers and magnifiers.</summary>
    public ViewerAccessibleTree Accessibility { get; }

    /// <summary>Makes the calling thread the window thread: posted work and awaits come back to it, and ReactiveUI's main thread scheduler posts to it.</summary>
    /// <param name="window">The window this thread pumps.</param>
    /// <returns>The context to make the session with.</returns>
    public static WindowThreadContext UseWindowThread(IPlatformWindow window)
    {
        var context = new WindowThreadContext(window);
        SynchronizationContext.SetSynchronizationContext(context);
        RxSchedulers.MainThreadScheduler = new SynchronizationContextSequencer(context);
        return context;
    }

    /// <summary>Opens documents in tabs.</summary>
    /// <param name="paths">The files or addresses.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Open(IEnumerable<string> paths) => Main.Open(paths);

    /// <summary>Runs until the user closes the window.</summary>
    public void Run()
    {
        _window.Show();
        while (PumpOnce(NoLimit))
        {
            // Each turn handles the events and posted work that woke it.
        }
    }

    /// <summary>Waits for window events or posted work and handles what arrived.</summary>
    /// <param name="timeoutMilliseconds">The longest wait, or -1 for no limit.</param>
    /// <returns><see langword="false"/> once the user asked to close the window.</returns>
    public bool PumpOnce(int timeoutMilliseconds)
    {
        // Work that is already waiting must not sit behind a wait for the window. Work posted after this check wakes the wait itself.
        _window.PumpEvents(_context.HasWork ? 0 : timeoutMilliseconds, _handleEvent ??= HandleEvent);
        _context.Drain();

        // Posted work changes view models without a window event, so the tree is checked after it too.
        Accessibility.RefreshIfRead(Main, Canvas);
        return !_closed;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _owned.Dispose();

    /// <summary>Handles one window event.</summary>
    /// <param name="windowEvent">The event.</param>
    private void HandleEvent(in WindowEvent windowEvent)
    {
        if (windowEvent.Kind == WindowEventKind.CloseRequested)
        {
            _closed = true;
            return;
        }

        Canvas.Handle(windowEvent);
    }

    /// <summary>Shows the selected tab on the canvas and its name in the title bar.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> when none is open.</param>
    private void ShowTab(DocumentTabViewModel? tab)
    {
        Canvas.ShowTab(tab);
        _window.SetTitle(Main.WindowTitle);
    }

    /// <summary>Draws with the colours of a theme.</summary>
    /// <param name="theme">The theme.</param>
    private void ShowTheme(ResolvedTheme theme)
    {
        Canvas.Colors = ViewerColors.From(theme.Scheme);
        _window.RequestFrame();
    }
}
