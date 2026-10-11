// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Theming;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App;

/// <summary>The Avalonia application.</summary>
[DebuggerDisplay("App: PdfViewerLite")]
public sealed class App : Application
{
    /// <summary>Subscriptions and resources owned for the application's lifetime.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The main window, once created.</summary>
    private MainWindow? _window;

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // The Calm fallback until the services resolve the user's choice, so even a bare lifetime is fully themed.
        DesktopThemeApplier.Apply(this, ThemeResolver.Resolve(new(), null));
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.NeedsSingleInstancePreflight)
            {
                _lifetime.Add(Signal.Return(this, new AvaloniaScheduler(Dispatcher.UIThread, DispatcherPriority.ApplicationIdle))
                    .SubscribeSafe(static app => _ = app.StartAfterForwardingAsync(), static error => Trace.TraceError(error.ToString())));
            }
            else if (OperatingSystem.IsLinux())
            {
                // Avalonia's AT-SPI bridge waits 100 ms for the UI thread to go idle and, if it times out, finishes
                // starting on a pool thread where it cannot read the main window, which then never reaches screen
                // readers. Starting below its ContextIdle wait lets that wait finish first, on the UI thread.
                _lifetime.Add(Signal.Return(this, new AvaloniaScheduler(Dispatcher.UIThread, DispatcherPriority.ApplicationIdle))
                    .SubscribeSafe(static app => app.StartAndShow(), static error => Trace.TraceError(error.ToString())));
            }
            else
            {
                Start(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Releases the services once the desktop lifetime has ended.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => _lifetime.Dispose();

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Starts once the lifetime is already running, so the main window is shown here.</summary>
    private void StartAndShow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        Start(desktop);
        _window?.Show();
    }

    /// <summary>Checks single-instance ownership without holding the UI thread, then starts or closes this window.</summary>
    /// <returns>A task for the forwarding check.</returns>
    private async Task StartAfterForwardingAsync()
    {
        bool shouldStart;
        try
        {
            shouldStart = await Program.StartSingleInstanceAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Trace.TraceError(error.ToString());
            shouldStart = true;
        }

        if (shouldStart)
        {
            Dispatcher.UIThread.Post(StartAndShow);
        }
        else
        {
            Dispatcher.UIThread.Post(ShutdownDesktop);
        }
    }

    /// <summary>Stops a forwarded process after its request reached the running viewer.</summary>
    private void ShutdownDesktop()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>Creates the services and the main window.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = AppServices.CreateDefault(Program.Platform);
        var viewModel = new MainViewModel(services);
        var window = new MainWindow { DataContext = viewModel, Width = services.Settings.WindowWidth, Height = services.Settings.WindowHeight };
        if (services.Settings.WindowMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        _window = window;

        // Reads the lifetime's live window list each time; there is no state-passing overload to use instead.
        _lifetime.Add(new FocusAnnouncementRepair(services.FocusAnnouncements, () => desktop.Windows));
        _lifetime.Add(services);
        _lifetime.Add(viewModel);

        // Both current values arrive synchronously on subscription, so the first frame is already themed.
        _lifetime.Add(services.Theme.SubscribeSafe(ApplyTheme, OnError));
        if (services.ThemeSource is { } themeSource)
        {
            _lifetime.Add(themeSource.Palette.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(services.SetDesktopPalette, OnError));
        }

        if (Program.InstanceHost is { } host)
        {
            _lifetime.Add(host.OpenRequests.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(OpenForwarded, OnError));
        }

        // Documents opened from the Finder (or another app) on macOS arrive as activation events.
        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
        {
            _lifetime.Add(activatable.Events().Activated.SubscribeSafe(OnActivated, OnError));
        }

        if (!OperatingSystem.IsWindows())
        {
            // Shut down cleanly on SIGTERM (for example at logout) so the session is saved.
            _lifetime.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                _lifetime.Add(Signal.Return(RxVoid.Default).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => desktop.Shutdown(), OnError));
            }));
        }

        viewModel.RestoreSession();
        viewModel.Open(Program.StartupDocuments);
        desktop.MainWindow = window;
    }

    /// <summary>Applies a theme to the resources and window font.</summary>
    /// <param name="theme">The theme.</param>
    private void ApplyTheme(ResolvedTheme theme)
    {
        DesktopThemeApplier.Apply(this, theme);
        if (_window is not null)
        {
            DesktopThemeApplier.ApplyFont(_window, theme);
        }
    }

    /// <summary>Opens documents the desktop asked the app to open, such as a PDF chosen in the Finder.</summary>
    /// <param name="activation">The activation.</param>
    private void OnActivated(ActivatedEventArgs activation)
    {
        if (activation is not FileActivatedEventArgs files)
        {
            return;
        }

        var paths = new List<string>(files.Files.Count);
        foreach (var file in files.Files)
        {
            if (file.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        OpenForwarded(new(paths, null));
    }

    /// <summary>Opens forwarded documents and raises the window.</summary>
    /// <param name="request">The request.</param>
    private void OpenForwarded(OpenRequest request)
    {
        if (_window?.ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.Open(request.Uris);
        _window.BringToFront();
    }
}
