// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// The window's keyboard shortcuts: the shortcut table first, then Escape to leave a full view, F11 for full screen,
/// and Ctrl+Tab and Ctrl+Shift+Tab to move between tabs.
/// </summary>
[DebuggerDisplay("WindowShortcutsView: {ViewModel}")]
public sealed partial class WindowShortcutsView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="WindowShortcutsView"/> class.</summary>
    public WindowShortcutsView()
    {
        InitializeComponent();
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            var keys = window?.KeyDowns ?? Signal.Never<KeyEventArgs>();
            disposables.Add(keys.Inject(viewModels)
                .WhereWith(window, static (shown, press) => Shortcuts.TryRun(press.State, press.Value, shown?.FocusManager?.GetFocusedElement() is TextBox))
                .SubscribeSafe(static press => press.Value.Handled = true, ErrorTrace.Write));
            disposables.Add(keys.Pressing(Key.Escape).Unhandled().Inject(viewModels)
                .Where(static press => LeaveFullView(press.State.SelectedTab))
                .SubscribeSafe(static press => press.Value.Handled = true, ErrorTrace.Write));
            disposables.Add(keys.Pressing(Key.F11).Unhandled().MarkHandled()
                .SelectWith(window, static (shown, _) => shown)
                .KeepNotNull()
                .SubscribeSafe(static shown => shown.WindowState = shown.WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen, ErrorTrace.Write));
            disposables.Add(keys.Pressing(Key.Tab).Holding(KeyModifiers.Control).Without(KeyModifiers.Shift).MarkHandled().AsVoid
                .InvokeCommand(this, static v => v.ViewModel!.NextTabCommand));
            disposables.Add(keys.Pressing(Key.Tab).Holding(KeyModifiers.Control | KeyModifiers.Shift).MarkHandled().AsVoid
                .InvokeCommand(this, static v => v.ViewModel!.PreviousTabCommand));
        });
    }

    /// <summary>Ends presenting or read mode, as Escape does.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns><see langword="true"/> when either was on.</returns>
    private static bool LeaveFullView(DocumentTabViewModel? tab)
    {
        switch (tab)
        {
            case { IsPresenting: true }:
            {
                tab.SetPresenting(false);
                return true;
            }

            case { IsReading: true }:
            {
                tab.SetReading(false);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
