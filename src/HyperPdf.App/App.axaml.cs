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
using HyperPdf.App.Accessibility;
using HyperPdf.App.Controls;
using HyperPdf.App.Rendering;
using HyperPdf.App.Services;
using HyperPdf.App.Theming;
using HyperPdf.App.Views;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Theming;
using HyperPdf.ViewModels;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App;

/// <summary>The Avalonia application.</summary>
[DebuggerDisplay("App: HyperPDF")]
public sealed class App : Application
{
    /// <summary>
    /// Runs work after Avalonia's own ContextIdle jobs. The scheduler needs a dispatcher, and the UI thread's is the only
    /// one the app has, so this is the one place the app names it.
    /// </summary>
    private static readonly AvaloniaScheduler IdleScheduler = new(Dispatcher.UIThread, DispatcherPriority.ApplicationIdle);

    /// <summary>Subscriptions and resources owned for the application's lifetime.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The main window, once created.</summary>
    private MainWindow? _window;

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        CP.Localisation.Avalonia.LocalisationWindows.ApplyDirection(this, UiText);
        _lifetime.Add(UiText.CultureChanges
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(culture => CP.Localisation.Avalonia.LocalisationWindows.Refresh(this, UiText, culture), ErrorTrace.Write));

        // The Calm fallback until the services resolve the user's choice, so even a bare lifetime is fully themed.
        DesktopThemeApplier.Apply(this, ThemeResolver.Resolve(new(), null));
    }

    /// <summary>
    /// Starts the app once Avalonia has made the application and its desktop lifetime, from the builder's
    /// <c>AfterSetup</c> step, which runs just before the framework reports set-up complete.
    /// </summary>
    internal void Start()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (Program.NeedsSingleInstancePreflight)
        {
            // A forwarded process stops once its request reached the running viewer.
            _lifetime.Add(Signal.Return(this, IdleScheduler)
                .SwitchMap(static _ => Signal.FromAsync(static _ => CanStartAsync()))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeSafe(shouldStart => ShowOrShutDown(desktop, shouldStart), ErrorTrace.Write));
        }
        else if (OperatingSystem.IsLinux())
        {
            // Avalonia's AT-SPI bridge waits 100 ms for the UI thread to go idle and, if it times out, finishes
            // starting on a pool thread where it cannot read the main window, which then never reaches screen
            // readers. Starting below its ContextIdle wait lets that wait finish first, on the UI thread.
            _lifetime.Add(Signal.Return(desktop, IdleScheduler).SubscribeSafe(StartAndShow, ErrorTrace.Write));
        }
        else
        {
            Open(desktop);
        }
    }

    /// <summary>Releases the services once the desktop lifetime has ended.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => _lifetime.Dispose();

    /// <summary>Checks single-instance ownership without holding the UI thread.</summary>
    /// <returns>A task that gives whether this process should show its own window.</returns>
    private static async Task<bool> CanStartAsync()
    {
        try
        {
            return await Program.StartSingleInstanceAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            ErrorTrace.Write(error);
            return true;
        }
    }

    /// <summary>Applies the documents the desktop asked the app to open, such as a PDF chosen in the Finder.</summary>
    /// <param name="activation">The activation.</param>
    /// <returns>The documents as paths, or <see langword="null"/> when the activation is not about files.</returns>
    private static OpenRequest? ReadFiles(ActivatedEventArgs activation)
    {
        if (activation is not FileActivatedEventArgs files)
        {
            return null;
        }

        var paths = new List<string>(files.Files.Count);
        foreach (var file in files.Files)
        {
            if (file.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        return new(paths, null);
    }

    /// <summary>Starts the app, or ends this process, once the single instance check is answered.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="shouldStart">Whether this process holds the single instance claim.</param>
    private void ShowOrShutDown(IClassicDesktopStyleApplicationLifetime desktop, bool shouldStart)
    {
        if (shouldStart)
        {
            StartAndShow(desktop);
            return;
        }

        desktop.Shutdown();
    }

    /// <summary>Starts once the lifetime is already running, so the main window is shown here.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    private void StartAndShow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Open(desktop);
        _window?.Show();
    }

    /// <summary>Creates the services and the main window.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    private void Open(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = AppServices.CreateDefault(Program.Platform, new AvaloniaRenderHead(), SpellCheckers.Create);
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
        _lifetime.Add(CommandFailures.ShowIn(viewModel.ReportFailure));
        if (AccessibilityHost.Start(desktop) is { } accessibility)
        {
            _lifetime.Add(accessibility);
        }

        // Both current values arrive synchronously on subscription, so the first frame is already themed.
        _lifetime.Add(services.Theme.SubscribeSafe(ApplyTheme, ErrorTrace.Write));
        if (services.ThemeSource is { } themeSource)
        {
            _lifetime.Add(themeSource.Palette.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(services.SetDesktopPalette, ErrorTrace.Write));
        }

        if (Program.InstanceHost is { } host)
        {
            _lifetime.Add(host.OpenRequests.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(OpenForwarded, ErrorTrace.Write));
        }

        // Documents opened from the Finder (or another app) on macOS arrive as activation events.
        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
        {
            _lifetime.Add(activatable.Events().Activated
                .Select(ReadFiles)
                .KeepNotNull()
                .SubscribeSafe(OpenForwarded, ErrorTrace.Write));
        }

        if (!OperatingSystem.IsWindows())
        {
            // Shut down cleanly on SIGTERM (for example at logout) so the session is saved.
            _lifetime.Add(PosixSignalRegistration.Requests(PosixSignal.SIGTERM)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SelectWith(desktop, static (lifetime, _) => lifetime)
                .SubscribeSafe(static lifetime => lifetime.Shutdown(), ErrorTrace.Write));
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
