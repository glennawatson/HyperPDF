// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>The main window: the tab strip, the start page and the document of the selected tab.</summary>
[DebuggerDisplay("MainWindow: {Title}")]
public sealed partial class MainWindow : ReactiveUI.Avalonia.ReactiveWindow<MainViewModel>
{
    /// <summary>The column of the second view while split.</summary>
    private const int SecondViewColumn = 2;

    /// <summary>The PDF file type filter.</summary>
    private static readonly FilePickerFileType PdfFileType = new("PDF documents") { Patterns = ["*.pdf", "*.PDF"], MimeTypes = ["application/pdf"] };

    /// <summary>The view model the window shows. Events read it from here, not from a field.</summary>
    private readonly IObservable<MainViewModel?> _viewModels;

    /// <summary>The Search in Folder window, while it is open.</summary>
    private FolderSearchWindow? _folderSearch;

    /// <summary>Forgets the Search in Folder window when it closes.</summary>
    private IDisposable? _folderSearchClosed;

    /// <summary>The window state before presenting started, restored when it ends.</summary>
    private WindowState? _stateBeforePresenting;

    /// <summary>Whether the user agreed to close with unsaved edits.</summary>
    private bool _closeConfirmed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModels = this.WhenChanged(static v => v.ViewModel);
        DocumentHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        SplitHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        var keys = this.KeyDowns;
        _ = this.WhenActivated(
            disposables =>
            {
                disposables.Add(this.DragOvers.SubscribeSafe(static args => args.DragEffects = args.CopyFilesAndText, OnError));
                disposables.Add(this.Drops.Dropped.Inject(_viewModels).SubscribeSafe(static dropped => dropped.State.Open(dropped.Value), OnError));
                disposables.Add(keys.Inject(_viewModels).SubscribeSafe(OnPreviewKeyDown, OnError));
                disposables.Add(keys.Pressing(Key.Tab).Holding(KeyModifiers.Control).Without(KeyModifiers.Shift).MarkHandled().AsVoid
                    .InvokeCommand(this, static v => v.ViewModel!.NextTabCommand));
                disposables.Add(keys.Pressing(Key.Tab).Holding(KeyModifiers.Control | KeyModifiers.Shift).MarkHandled().AsVoid
                    .InvokeCommand(this, static v => v.ViewModel!.PreviousTabCommand));
                disposables.Add(this.Events().Closing.Inject(_viewModels).SubscribeSafe(OnWindowClosing, OnError));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.WindowTitle, static v => v.Title));
                disposables.Add(_viewModels.BindTo(this, static v => v.TabBar.ViewModel));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusText.Text));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusBar.IsVisible, static message => message is not null));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.StartPage.IsVisible, static hasTabs => !hasTabs));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.DocumentHost.IsVisible));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SelectedTab, static v => v.DocumentHost.Content));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.Content));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.IsVisible, static split => split is not null));
                disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitSplitter.IsVisible, static split => split is not null));

                // The width is shared between the two views while split, and given back to the first afterwards.
                disposables.Add(this.WhenChanged(static v => v.ViewModel!.SplitTab).SubscribeSafe(
                    split => DocumentArea.ColumnDefinitions[SecondViewColumn].Width = split is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star),
                    OnError));
                disposables.Add(this.BindCommand(ViewModel, static vm => vm.DismissStatusCommand, static v => v.DismissStatusButton));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.OpenFileInteraction, OpenFilesAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPropertiesInteraction, ShowPropertiesAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ConfirmInteraction, ConfirmAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPreferencesInteraction, ShowPreferencesAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowLicencesInteraction, ShowLicencesAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowFolderSearchInteraction, ShowFolderSearchAsync));
                disposables.Add(this.BindInteraction(ViewModel, static vm => vm.NewWindowInteraction, ShowNewWindowAsync));
                disposables.Add(this.WhenChanged(static v => v.ViewModel!.SelectedTab!.IsPresenting).SubscribeSafe(OnPresentingChanged, OnError));

                // Hiding or removing the focused control leaves nothing focused; once the input is handled, focus goes back.
                // A screen reader repeat clears focus only for a moment, so it is ignored.
                disposables.Add(this.LostFocuses.FocusDropped
                    .Where(static _ => !FocusAnnouncementRepair.IsRepeating)
                    .WithLatestFrom(this.WhenChanged(static v => v.IsActive), static (_, active) => active)
                    .Where(static active => active)
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .SubscribeSafe(_ => RestoreLostFocus(), OnError));
                disposables.Add(Scope.Create(this, static window => window.ReleaseWindows()));
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

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Forgets the windows owned while the window is active.</summary>
    private void ReleaseWindows()
    {
        _folderSearchClosed?.Dispose();
        _folderSearchClosed = null;
        _folderSearch = null;
    }

    /// <summary>Confirms unsaved edits and saves the session before closing.</summary>
    /// <param name="closing">The close request and the window's view model.</param>
    private void OnWindowClosing(Injected<WindowClosingEventArgs, MainViewModel> closing)
    {
        var (e, viewModel) = closing;
        if (!_closeConfirmed && viewModel.HasUnsavedTabs)
        {
            e.Cancel = true;

            // Repeated close requests while the question is open share it.
            if (!viewModel.IsConfirmingDiscard)
            {
                _ = ConfirmCloseAsync(viewModel);
            }

            return;
        }

        if (!viewModel.IsSecondaryWindow)
        {
            viewModel.RememberWindow(Width, Height, WindowState == WindowState.Maximized, WindowState == WindowState.Normal);
        }

        viewModel.SaveSession();
    }

    /// <summary>Shows the open dialog; on KDE this goes through the XDG portal and shows the KDE file dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task OpenFilesAsync(IInteractionContext<RxVoid, IReadOnlyList<string>> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open Document", AllowMultiple = true, FileTypeFilter = [PdfFileType, FilePickerFileTypes.All] });
        context.SetOutput(files.LocalPaths);
    }

    /// <summary>Asks before closing the window with unsaved edits, then closes when the user agrees.</summary>
    /// <param name="viewModel">The view model.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmCloseAsync(MainViewModel viewModel)
    {
        try
        {
            if (!await viewModel.ConfirmDiscardAsync().ConfigureAwait(true))
            {
                return;
            }

            _closeConfirmed = true;
            Close();
        }
        catch (Exception error)
        {
            OnError(error);
        }
    }

    /// <summary>Asks the user to confirm a destructive action.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmAsync(IInteractionContext<ConfirmRequest, bool> context)
    {
        var dialog = new ConfirmWindow { ViewModel = new(context.Input) };
        context.SetOutput(await dialog.ShowDialog<bool>(this));
    }

    /// <summary>Shows the preferences.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPreferencesAsync(IInteractionContext<PreferencesViewModel, RxVoid> context)
    {
        await new PreferencesWindow { ViewModel = context.Input }.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the About and Licences window.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowLicencesAsync(IInteractionContext<LicencesViewModel, RxVoid> context)
    {
        await new LicencesWindow { ViewModel = context.Input }.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the Search in Folder window, or brings it forward when it is already open.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private Task ShowFolderSearchAsync(IInteractionContext<FolderSearchViewModel, RxVoid> context)
    {
        if (_folderSearch is { } open)
        {
            open.Activate();
        }
        else
        {
            var window = new FolderSearchWindow { ViewModel = context.Input };
            _folderSearch = window;
            _folderSearchClosed = window.Events().Closed.SubscribeSafe(
                _ =>
                {
                    _folderSearch = null;
                    _folderSearchClosed?.Dispose();
                    _folderSearchClosed = null;
                },
                OnError);
            window.Show(this);
        }

        context.SetOutput(RxVoid.Default);
        return Task.CompletedTask;
    }

    /// <summary>Shows another window, which releases its view model once closed.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private Task ShowNewWindowAsync(IInteractionContext<MainViewModel, RxVoid> context)
    {
        var viewModel = context.Input;
        var window = new MainWindow { DataContext = viewModel, Width = Width, Height = Height };

        // The new window's view model lives exactly as long as the window, and the Closed event carries neither.
        _ = window.Events().Closed.Take(1).SubscribeSafe(_ => viewModel.Dispose(), OnError);
        window.Show();
        context.SetOutput(RxVoid.Default);
        return Task.CompletedTask;
    }

    /// <summary>Shows the properties dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPropertiesAsync(IInteractionContext<DocumentTabViewModel, RxVoid> context)
    {
        var dialog = new PropertiesWindow { ViewModel = new(context.Input) };
        await dialog.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Goes full screen with only the page showing while the selected tab presents, and back afterwards.</summary>
    /// <param name="presenting">Whether the selected tab is presenting.</param>
    private void OnPresentingChanged(bool presenting)
    {
        TabBar.IsVisible = !presenting;
        if (presenting && WindowState != WindowState.FullScreen)
        {
            _stateBeforePresenting = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else if (!presenting && _stateBeforePresenting is { } previous)
        {
            WindowState = previous;
            _stateBeforePresenting = null;
        }
    }

    /// <summary>Runs the command of a keyboard shortcut, then handles shortcuts that would otherwise move keyboard focus.</summary>
    /// <param name="press">The key press and the window's view model.</param>
    private void OnPreviewKeyDown(Injected<KeyEventArgs, MainViewModel> press)
    {
        var (e, viewModel) = press;
        if (Shortcuts.TryRun(viewModel, e, FocusManager?.GetFocusedElement() is TextBox))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && LeaveFullView(viewModel.SelectedTab))
        {
            e.Handled = true;
            return;
        }

        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        switch (e.Key)
        {
            case Key.L when control:
            {
                FindDocumentView()?.FocusPageBox();
                e.Handled = true;
                break;
            }

            case Key.F11:
            {
                WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
                e.Handled = true;
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>
    /// Focuses the document, or the start page's Open button when no document is open, if focus is still lost. Hiding or
    /// removing the focused control leaves nothing focused, which a screen reader cannot announce and a keyboard user
    /// cannot see.
    /// </summary>
    private void RestoreLostFocus()
    {
        if (!IsActive || FocusManager?.GetFocusedElement() is not null)
        {
            return;
        }

        if (ViewModel?.HasTabs == true && FindDocumentView() is { } view)
        {
            view.FocusMain();
            return;
        }

        StartPage.FocusOpenButton();
    }

    /// <summary>Finds the visible document view.</summary>
    /// <returns>The view, if any.</returns>
    private DocumentView? FindDocumentView()
    {
        foreach (var visual in DocumentHost.GetVisualDescendants())
        {
            if (visual is DocumentView view)
            {
                return view;
            }
        }

        return null;
    }
}
