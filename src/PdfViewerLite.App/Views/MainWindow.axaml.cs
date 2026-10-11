// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace PdfViewerLite.App.Views;

/// <summary>The main window: the tab strip, the start page and the document of the selected tab.</summary>
[DebuggerDisplay("MainWindow: {Title}")]
public sealed partial class MainWindow : ReactiveUI.Avalonia.ReactiveWindow<MainViewModel>
{
    /// <summary>The column of the second view while split.</summary>
    private const int SecondViewColumn = 2;

    /// <summary>How far the pointer must move before a tab drag starts.</summary>
    private const double DragThreshold = 6;

    /// <summary>Pixels the tab strip scrolls per wheel notch.</summary>
    private const double TabWheelStep = 60;

    /// <summary>The PDF file type filter.</summary>
    private static readonly FilePickerFileType PdfFileType = new("PDF documents") { Patterns = ["*.pdf", "*.PDF"], MimeTypes = ["application/pdf"] };

    /// <summary>The Search in Folder window, while it is open.</summary>
    private FolderSearchWindow? _folderSearch;

    /// <summary>Forgets the Search in Folder window when it closes.</summary>
    private IDisposable? _folderSearchClosed;

    /// <summary>The tab being dragged.</summary>
    private DocumentTabViewModel? _draggedTab;

    /// <summary>Where the tab drag started.</summary>
    private Point _dragStart;

    /// <summary>The window state before presenting started, restored when it ends.</summary>
    private WindowState? _stateBeforePresenting;

    /// <summary>Whether the user agreed to close with unsaved edits.</summary>
    private bool _closeConfirmed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        TabStrip.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabItemView());
        TabFinderList.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabSummaryView());
        DocumentHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        SplitHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        RecentMenuItem.ItemTemplate = new FuncDataTemplate<RecentDocument>(static (recent, _) => new TextBlock { Text = recent?.FileName });

        _ = this.WhenActivated(
            disposables =>
            {
                disposables.Add(ItemAutomation.NameItems(TabFinderList));
                BindWindowEvents(disposables);
                BindTabStrip(disposables, TabStrip);
                BindTabFinder(disposables, TabFinderList, TabFinderBox, TabFinderButton.Flyout!);
                BindRecentMenu(disposables, RecentMenuItem, TabsMenuButton.Flyout!);
                BindProperties(disposables);
                BindCommands(disposables);
                disposables.Add(this.WhenChanged(static v => v.ViewModel!.SelectedTab!.IsPresenting).SubscribeSafe(OnPresentingChanged, OnError));
                disposables.Add(InputElement.LostFocusEvent.Raised
                    .Where(static raised => raised.Item2 is FocusChangedEventArgs { NewFocusedElement: null })
                    .SubscribeSafe(raised => OnFocusLost(raised.Item1), OnError));
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

    /// <summary>Converts storage items to local paths.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The paths.</returns>
    private static List<string> ToPaths(IEnumerable<IStorageItem> items)
    {
        var paths = new List<string>();
        foreach (var item in items)
        {
            if (item.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Readies the hover preview of the tab under the pointer.</summary>
    /// <param name="e">The event.</param>
    private static void OnTabPreviewOpening(CancelRoutedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is DocumentTabViewModel tab)
        {
            _ = tab.PreparePreviewAsync(CancellationToken.None);
        }
    }

    /// <summary>Accepts dragged files.</summary>
    /// <param name="e">The event.</param>
    private static void OnDragOver(DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text) ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>Gets the tab a pointer event came from, read from the data context its source inherits.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The tab, if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DocumentTabViewModel? TabUnder(RoutedEventArgs e) => (e.Source as StyledElement)?.DataContext as DocumentTabViewModel;

    /// <summary>Gets the tab under a pointer event.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="e">The event.</param>
    /// <returns>The tab, if any.</returns>
    private static DocumentTabViewModel? GetTabAt(ListBox strip, PointerEventArgs e)
    {
        var point = e.GetPosition(strip);
        foreach (var visual in strip.GetVisualsAt(point))
        {
            if (visual is Control { DataContext: DocumentTabViewModel tab })
            {
                return tab;
            }
        }

        return null;
    }

    /// <summary>Scrolls the tab strip horizontally with the mouse wheel.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="e">The event.</param>
    private static void OnTabWheel(ListBox strip, PointerWheelEventArgs e)
    {
        if (strip.FindDescendantOfType<ScrollViewer>() is not { } scroller)
        {
            return;
        }

        scroller.Offset = new(Math.Max(0, scroller.Offset.X - ((e.Delta.Y + e.Delta.X) * TabWheelStep)), scroller.Offset.Y);
        e.Handled = true;
    }

    /// <summary>Forgets the windows and drag state owned while the window is active.</summary>
    private void ReleaseWindows()
    {
        _folderSearchClosed?.Dispose();
        _folderSearchClosed = null;
        _folderSearch = null;
        _draggedTab = null;
    }

    /// <summary>Observes drag and drop, keyboard shortcuts and closing.</summary>
    /// <param name="disposables">Owns the subscriptions.</param>
    private void BindWindowEvents(MultipleDisposable disposables)
    {
        var keys = this.ObserveRouted(InputElement.KeyDownEvent, RoutingStrategies.Tunnel);
        disposables.Add(this.ObserveRouted(DragDrop.DragOverEvent).SubscribeSafe(OnDragOver, OnError));
        disposables.Add(this.ObserveRouted(DragDrop.DropEvent).SubscribeSafe(OnDrop, OnError));
        disposables.Add(keys.SubscribeSafe(OnPreviewKeyDown, OnError));
        disposables.Add(keys.Where(static e => e.Key == Key.Tab && (e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) == 0)
            .Select(static e =>
            {
                e.Handled = true;
                return RxVoid.Default;
            })
            .InvokeCommand(this, static v => v.ViewModel!.NextTabCommand));
        disposables.Add(keys.Where(static e => e.Key == Key.Tab && (e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) != 0)
            .Select(static e =>
            {
                e.Handled = true;
                return RxVoid.Default;
            })
            .InvokeCommand(this, static v => v.ViewModel!.PreviousTabCommand));
        disposables.Add(this.Events().Closing.SubscribeSafe(OnWindowClosing, OnError));
    }

    /// <summary>Observes the tab strip: dragging, wheel, hover preview, close buttons and the tab menu.</summary>
    /// <param name="disposables">Owns the subscriptions.</param>
    /// <param name="tabStrip">The tab strip; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    private void BindTabStrip(MultipleDisposable disposables, ListBox tabStrip)
    {
        var pressed = tabStrip.ObserveRouted(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel);
        disposables.Add(pressed.Where(static e => e.GetCurrentPoint(e.Source as Visual).Properties.IsMiddleButtonPressed && TabUnder(e) is not null)
            .Select(static e =>
            {
                e.Handled = true;
                return TabUnder(e);
            })
            .InvokeCommand(this, static v => v.ViewModel!.CloseTabCommand));
        disposables.Add(pressed.SubscribeSafe(e => OnTabPointerPressed(tabStrip, e), OnError));
        disposables.Add(tabStrip.ObserveRouted(InputElement.PointerMovedEvent, RoutingStrategies.Tunnel).SubscribeSafe(e => OnTabPointerMoved(tabStrip, e), OnError));
        disposables.Add(tabStrip.ObserveRouted(InputElement.PointerReleasedEvent, RoutingStrategies.Tunnel).SubscribeSafe(_ => _draggedTab = null, OnError));
        disposables.Add(tabStrip.ObserveRouted(InputElement.PointerWheelChangedEvent, RoutingStrategies.Tunnel).SubscribeSafe(e => OnTabWheel(tabStrip, e), OnError));
        disposables.Add(tabStrip.ObserveRouted(ToolTip.ToolTipOpeningEvent).SubscribeSafe(OnTabPreviewOpening, OnError));
        disposables.Add(tabStrip.ObserveRouted(Button.ClickEvent, RoutingStrategies.Bubble)
            .Where(static e => e.Source is Button { Name: "CloseButton", DataContext: DocumentTabViewModel })
            .Select(static e =>
            {
                e.Handled = true;
                return (e.Source as Control)?.DataContext as DocumentTabViewModel;
            })
            .InvokeCommand(this, static v => v.ViewModel!.CloseTabCommand));
        disposables.Add(tabStrip.Events().ContextRequested.SubscribeSafe(OnTabContextRequested, OnError));
    }

    /// <summary>Fills Open Recent from the recent documents, refreshed each time the menu opens.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    /// <param name="recentMenu">The Open Recent menu item; Events() needs it typed, as with the tab finder.</param>
    /// <param name="tabsMenu">The flyout that holds it.</param>
    private void BindRecentMenu(MultipleDisposable disposables, MenuItem recentMenu, FlyoutBase tabsMenu)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentMenuItem.ItemsSource));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentMenuItem.IsEnabled, static count => count > 0));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.ClearRecentMenuItem.IsEnabled, static count => count > 0));
        disposables.Add(recentMenu.Events().ContainerPrepared.SubscribeSafe(args => PrepareRecentItem(args.Container), OnError));
        disposables.Add(tabsMenu.Events().Opened.SubscribeSafe(_ => ViewModel?.RefreshRecentDocuments(), OnError));
    }

    /// <summary>Makes a recent document's menu item open it, and says where it is.</summary>
    /// <param name="container">The menu item.</param>
    private void PrepareRecentItem(Control container)
    {
        if (container is not MenuItem item || item.DataContext is not RecentDocument recent || ViewModel is not { } viewModel)
        {
            return;
        }

        item.Command = viewModel.OpenRecentCommand;
        item.CommandParameter = recent;
        ControlHelp.SetText(item, string.Create(CultureInfo.CurrentCulture, $"Open {recent.FileName} from {recent.Folder}."));
        AutomationProperties.SetName(item, recent.FileName);
    }

    /// <summary>Observes the tab finder flyout: picking a tab, searching and opening.</summary>
    /// <param name="disposables">Owns the subscriptions.</param>
    /// <param name="list">The found tabs list; Events() needs the typed parameters because it cannot see fields the XAML name generator creates.</param>
    /// <param name="box">The search box.</param>
    /// <param name="flyout">The flyout that holds them.</param>
    private void BindTabFinder(MultipleDisposable disposables, ListBox list, TextBox box, FlyoutBase flyout)
    {
        disposables.Add(list.Events().SelectionChanged.SubscribeSafe(_ => OnTabFound(list), OnError));
        disposables.Add(box.Events().KeyDown.Where(static args => args.Key == Key.Enter).SubscribeSafe(_ => PickFirstFoundTab(list), OnError));
        disposables.Add(flyout.Events().Opened.SubscribeSafe(_ => OnTabFinderOpened(list, box), OnError));
        disposables.Add(this.WhenChanged(static v => v.ViewModel!.TabFinderRequests).SwitchTo().SubscribeSafe(_ => flyout.ShowAt(TabFinderButton), OnError));
    }

    /// <summary>Binds the view model's values to the controls.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindProperties(MultipleDisposable disposables)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.WindowTitle, static v => v.Title));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs, static v => v.TabStrip.ItemsSource));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedTab, static v => v.TabStrip.SelectedItem, static tab => tab, static item => item as DocumentTabViewModel));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.TabFinderButton.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs.Count, static v => v.TabCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"Tabs {count}")));
        disposables.Add(this.Bind(ViewModel, static vm => vm.TabQuery, static v => v.TabFinderBox.Text, static query => query, static text => text ?? string.Empty));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FoundTabs, static v => v.TabFinderList.ItemsSource));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageToneEnabled, static v => v.PageToneMenuItem.IsChecked));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusBar.IsVisible, static message => message is not null));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.StartPage.IsVisible, static hasTabs => !hasTabs));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.DocumentHost.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SelectedTab, static v => v.DocumentHost.Content));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.Content));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.IsVisible, static split => split is not null));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitSplitter.IsVisible, static split => split is not null));
        disposables.Add(this.WhenChanged(static v => v.ViewModel!.SplitTab).SubscribeSafe(OnSplitChanged, OnError));
    }

    /// <summary>Binds the buttons and menu items to commands, and handles the view model's interactions.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindCommands(MultipleDisposable disposables)
    {
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.ClearRecentCommand, static v => v.ClearRecentMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReopenClosedTabCommand, static v => v.ReopenMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.TogglePageToneCommand, static v => v.PageToneMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.PreferencesCommand, static v => v.PreferencesMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.SearchFolderCommand, static v => v.SearchFolderMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseAllTabsCommand, static v => v.CloseAllMenuItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.DismissStatusCommand, static v => v.DismissStatusButton));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.OpenFileInteraction, OpenFilesAsync));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPropertiesInteraction, ShowPropertiesAsync));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ConfirmInteraction, ConfirmAsync));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowPreferencesInteraction, ShowPreferencesAsync));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.LicencesCommand, static v => v.LicencesMenuItem));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowLicencesInteraction, ShowLicencesAsync));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ShowFolderSearchInteraction, ShowFolderSearchAsync));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.NewWindowInteraction, ShowNewWindowAsync));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.NewWindowCommand, static v => v.NewWindowMenuItem));
    }

    /// <summary>Confirms unsaved edits and saves the session before closing.</summary>
    /// <param name="e">The close request.</param>
    private void OnWindowClosing(WindowClosingEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

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
        context.SetOutput(ToPaths(files));
    }

    /// <summary>Refreshes the tab finder and focuses its search box.</summary>
    /// <param name="list">The found tabs list.</param>
    /// <param name="box">The search box.</param>
    private void OnTabFinderOpened(ListBox list, TextBox box)
    {
        ViewModel?.RefreshFoundTabs();
        list.SelectedItem = null;
        _ = box.Focus();
        box.SelectAll();
    }

    /// <summary>Selects the tab picked in the tab finder and closes it.</summary>
    /// <param name="list">The found tabs list.</param>
    private void OnTabFound(ListBox list)
    {
        if (list.SelectedItem is not DocumentTabViewModel tab || ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.SelectedTab = tab;
        TabFinderButton.Flyout?.Hide();
        TabStrip.ScrollIntoView(tab);
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

    /// <summary>Opens the menu of the tab under the pointer: close it, close the others, close all, reload.</summary>
    /// <param name="e">The event.</param>
    private void OnTabContextRequested(ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not DocumentTabViewModel tab || ViewModel is not { } viewModel || e.Source is not Control source)
        {
            return;
        }

        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Close", Command = viewModel.CloseTabCommand, CommandParameter = tab, InputGesture = new(Key.W, KeyModifiers.Control) },
                new MenuItem { Header = "Close _Other Tabs", Command = viewModel.CloseOtherTabsCommand, CommandParameter = tab },
                new MenuItem { Header = "Close _All Tabs", Command = viewModel.CloseAllTabsCommand },
                new Separator(),
                new MenuItem { Header = "_Save", Command = tab.SaveCommand, IsEnabled = tab.HasUnsavedChanges },
                new MenuItem { Header = "Re_load", Command = tab.ReloadCommand },
            },
        };
        menu.Open(source);
        e.Handled = true;
    }

    /// <summary>Selects the first tab the finder lists.</summary>
    /// <param name="list">The found tabs list.</param>
    private void PickFirstFoundTab(ListBox list)
    {
        if (ViewModel is { FoundTabs.Count: > 0 } viewModel)
        {
            list.SelectedItem = viewModel.FoundTabs[0];
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

    /// <summary>Shares the width between the two views while split, and gives it all back to the first afterwards.</summary>
    /// <param name="split">The second view, or <see langword="null"/>.</param>
    private void OnSplitChanged(DocumentTabViewModel? split) =>
        DocumentArea.ColumnDefinitions[SecondViewColumn].Width = split is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

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

    /// <summary>Opens dropped files, for example from Dolphin, or a dropped web address.</summary>
    /// <param name="e">The event.</param>
    private void OnDrop(DragEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files)
        {
            viewModel.Open(ToPaths(files));
        }
        else if (e.DataTransfer.TryGetText() is { Length: > 0 } text)
        {
            viewModel.Open(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    /// <summary>Runs the command of a keyboard shortcut, then handles shortcuts that would otherwise move keyboard focus.</summary>
    /// <param name="e">The event.</param>
    private void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

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
    /// Waits until the current input is handled, then gives focus back to the main content if it is still lost. Hiding
    /// or removing the focused control leaves nothing focused, which a screen reader cannot announce and a keyboard
    /// user cannot see. A screen reader repeat clears focus only for a moment, so it is ignored.
    /// </summary>
    /// <param name="sender">The element the loss is being raised on.</param>
    private void OnFocusLost(object sender)
    {
        // The class-wide event reports each step of every window's route; only this window's own step is ours.
        if (ReferenceEquals(sender, this) && IsActive && !FocusAnnouncementRepair.IsRepeating)
        {
            Dispatcher.UIThread.Post(static window => ((MainWindow)window!).RestoreLostFocus(), this, DispatcherPriority.Background);
        }
    }

    /// <summary>Focuses the document, or the start page's Open button when no document is open, if focus is still lost.</summary>
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

    /// <summary>Starts a possible tab drag.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="e">The event.</param>
    private void OnTabPointerPressed(ListBox strip, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(strip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedTab = GetTabAt(strip, e);
        _dragStart = e.GetPosition(strip);
    }

    /// <summary>Reorders tabs while dragging.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="e">The event.</param>
    private void OnTabPointerMoved(ListBox strip, PointerEventArgs e)
    {
        if (_draggedTab is null || ViewModel is not { } viewModel || Math.Abs(e.GetPosition(strip).X - _dragStart.X) < DragThreshold)
        {
            return;
        }

        var target = GetTabAt(strip, e);
        if (target is null || target == _draggedTab)
        {
            return;
        }

        viewModel.MoveTab(viewModel.Tabs.IndexOf(_draggedTab), viewModel.Tabs.IndexOf(target));
        _dragStart = e.GetPosition(strip);
    }
}
