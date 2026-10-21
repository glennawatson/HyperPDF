// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// The main window: the tab strip, the start page and the document of the selected tab. Its keyboard shortcuts and its
/// dialogs are views of their own.
/// </summary>
[DebuggerDisplay("MainWindow: {Title}")]
public sealed partial class MainWindow : ReactiveUI.Avalonia.ReactiveWindow<MainViewModel>
{
    /// <summary>The window state before presenting started, restored when it ends.</summary>
    private WindowState? _stateBeforePresenting;

    /// <summary>Whether the user agreed to close with unsaved edits.</summary>
    private bool _closeConfirmed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        var closing = this.Events().Closing.Inject(viewModels);
        var presenting = this.WhenChanged(static v => v.ViewModel!.SelectedTab!.IsPresenting);

        // Another window owns its view model, which lives exactly as long as the window; this is the window's own event
        // and it completes after one close, so the subscription ends with the window.
        _ = this.Events().Closed.Take(1).Inject(viewModels).Where(static closed => closed.State.IsSecondaryWindow).SubscribeSafe(static closed => closed.State.Dispose(), ErrorTrace.Write);
        _ = this.WhenActivated(
            disposables =>
            {
                disposables.Add(this.DragOvers.SubscribeSafe(static args => args.DragEffects = args.CopyFilesAndText, ErrorTrace.Write));
                disposables.Add(this.Drops.Dropped.Inject(viewModels).SubscribeSafe(static dropped => dropped.State.Open(dropped.Value), ErrorTrace.Write));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.WindowTitle, static v => v.Title));
                disposables.Add(viewModels.BindTo(this, static v => v.TabBar.ViewModel));
                disposables.Add(viewModels.BindTo(this, static v => v.ShortcutKeys.ViewModel));
                disposables.Add(viewModels.BindTo(this, static v => v.Dialogs.ViewModel));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.StartPage.IsVisible, static hasTabs => !hasTabs));

                // Closing with unsaved edits is stopped and the question asked once; closing again after the answer goes
                // through, remembering the window's size and the session.
                disposables.Add(closing
                    .WhereWith(this, static (window, close) => !window._closeConfirmed && close.State.HasUnsavedTabs)
                    .Do(static close => close.Value.Cancel = true)
                    .Where(static close => !close.State.IsConfirmingDiscard)
                    .SwitchMap(static close => Signal.FromAsync(() => close.State.ConfirmDiscardAsync()))
                    .Where(static confirmed => confirmed)
                    .DoWith(this, static (window, _) => window._closeConfirmed = true)
                    .SubscribeSafe(_ => Close(), ErrorTrace.Write));
                disposables.Add(closing
                    .WhereWith(this, static (window, close) => window._closeConfirmed || !close.State.HasUnsavedTabs)
                    .DoWith(this, static (window, close) => window.RememberSize(close.State))
                    .SubscribeSafe(static close => close.State.SaveSession(), ErrorTrace.Write));

                // Presenting goes full screen with only the page showing, and back afterwards.
                disposables.Add(presenting.Select(static on => !on).BindTo(this, static v => v.TabBar.IsVisible));
                disposables.Add(presenting
                    .WhereWith(this, static (window, on) => on && window.WindowState != WindowState.FullScreen)
                    .DoWith(this, static (window, _) => window._stateBeforePresenting = window.WindowState)
                    .SubscribeSafe(_ => WindowState = WindowState.FullScreen, ErrorTrace.Write));
                disposables.Add(presenting
                    .WhereWith(this, static (window, on) => !on && window._stateBeforePresenting is not null)
                    .SubscribeSafe(_ => LeavePresentingState(), ErrorTrace.Write));

                // Hiding or removing the focused control leaves nothing focused; once the input is handled, focus goes back.
                // A screen reader repeat clears focus only for a moment, so it is ignored.
                disposables.Add(this.LostFocuses.FocusDropped
                    .Where(static _ => !FocusAnnouncementRepair.IsRepeating)
                    .WithLatestFrom(this.WhenChanged(static v => v.IsActive), static (_, active) => active)
                    .Where(static active => active)
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .SelectWith(this, static (window, _) => window.LostFocusHome())
                    .KeepNotNull()
                    .TakeFocus());
            },
            this.WhenChanged(static view => view.ViewModel));
    }

    /// <summary>Brings the window to the front, for example when another launch forwards files.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>Records the window size and state for the next start; another window leaves that to the first.</summary>
    /// <param name="viewModel">The window's view model.</param>
    private void RememberSize(MainViewModel viewModel)
    {
        if (!viewModel.IsSecondaryWindow)
        {
            viewModel.RememberWindow(Width, Height, WindowState == WindowState.Maximized, WindowState == WindowState.Normal);
        }
    }

    /// <summary>Goes back to the window state from before presenting started.</summary>
    private void LeavePresentingState()
    {
        WindowState = _stateBeforePresenting.GetValueOrDefault(WindowState);
        _stateBeforePresenting = null;
    }

    /// <summary>
    /// Finds where lost focus goes back to: the document's main content, or the start page's Open button when no
    /// document is open. Hiding or removing the focused control leaves nothing focused, which a screen reader cannot
    /// announce and a keyboard user cannot see.
    /// </summary>
    /// <returns>The control to focus, or <see langword="null"/> when focus is no longer lost.</returns>
    private InputElement? LostFocusHome()
    {
        if (!IsActive || FocusManager?.GetFocusedElement() is not null)
        {
            return null;
        }

        return ViewModel?.HasTabs == true && DocumentArea.FindDocumentView() is { } view
            ? view.PageArea.MainContent
            : StartPage.OpenButton;
    }
}
