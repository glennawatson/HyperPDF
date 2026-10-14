// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The tab strip with its Open button and the Tabs and settings menu; the tab finder is its own view.</summary>
[DebuggerDisplay("TabBarView: {ViewModel}")]
public sealed partial class TabBarView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabBarView"/> class.</summary>
    public TabBarView()
    {
        InitializeComponent();
        TabStrip.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabItemView());
        RecentMenuItem.ItemTemplate = new FuncDataTemplate<RecentDocument>(static (recent, _) => new TextBlock { Text = recent?.FileName });
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(viewModels.BindTo(this, static v => v.TabFinder.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs, static v => v.TabStrip.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedTab, static v => v.TabStrip.SelectedItem, static tab => tab, static item => item as DocumentTabViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageToneEnabled, static v => v.PageToneMenuItem.IsChecked));
            disposables.Add(TabStrip.TabMiddleClicks.InvokeCommand(this, static v => v.ViewModel!.CloseTabCommand));
            disposables.Add(TabStrip.TabReorders.Inject(viewModels).SubscribeSafe(
                static reorder =>
                {
                    var (move, viewModel) = reorder;
                    viewModel.MoveTab(viewModel.Tabs.IndexOf(move.Drag.Tab), viewModel.Tabs.IndexOf(move.Target));
                },
                static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabStrip.TabScrolls.SubscribeSafe(static scroll => scroll.Scroller.Offset = scroll.Offset, static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabStrip.TabPreviewOpenings.SubscribeSafe(static tab => _ = tab.PreparePreviewAsync(CancellationToken.None), static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabStrip.ItemContextRequests<DocumentTabViewModel>().Inject(viewModels).SubscribeSafe(ShowTabMenu, static error => Trace.TraceError(error.ToString())));

            // A tab picked from the finder or opened anywhere else is brought into view in the strip.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.SelectedTab)
                .KeepNotNull()
                .SubscribeSafe(TabStrip.ScrollIntoView, static error => Trace.TraceError(error.ToString())));

            // Open Recent is filled from the recent documents, refreshed each time the menu opens.
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentMenuItem.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentMenuItem.IsEnabled, static count => count > 0));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.ClearRecentMenuItem.IsEnabled, static count => count > 0));
            disposables.Add(RecentMenuItem.ContainersPrepared
                .Pick(static args => args.Container is MenuItem { DataContext: RecentDocument recent } item ? new RecentMenuEntry(item, recent) : default(RecentMenuEntry?))
                .Inject(viewModels)
                .SubscribeSafe(PrepareRecentItem, static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabsMenuButton.FlyoutOpenings.InjectLatest(viewModels)
                .SubscribeSafe(static viewModel => viewModel.RefreshRecentDocuments(), static error => Trace.TraceError(error.ToString())));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ClearRecentCommand, static v => v.ClearRecentMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReopenClosedTabCommand, static v => v.ReopenMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TogglePageToneCommand, static v => v.PageToneMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PreferencesCommand, static v => v.PreferencesMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SearchFolderCommand, static v => v.SearchFolderMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseAllTabsCommand, static v => v.CloseAllMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.LicencesCommand, static v => v.LicencesMenuItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.NewWindowCommand, static v => v.NewWindowMenuItem));
        });
    }

    /// <summary>Opens the menu of the tab under the pointer: close it, close the others, close all, reload.</summary>
    /// <param name="request">The request and the window's view model.</param>
    private static void ShowTabMenu(Injected<ItemContextRequest<DocumentTabViewModel>, MainViewModel> request)
    {
        var (menu, viewModel) = request;
        new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Close", Command = viewModel.CloseTabCommand, CommandParameter = menu.Item, InputGesture = new(Key.W, KeyModifiers.Control) },
                new MenuItem { Header = "Close _Other Tabs", Command = viewModel.CloseOtherTabsCommand, CommandParameter = menu.Item },
                new MenuItem { Header = "Close _All Tabs", Command = viewModel.CloseAllTabsCommand },
                new Separator(),
                new MenuItem { Header = "_Save", Command = menu.Item.SaveCommand, IsEnabled = menu.Item.HasUnsavedChanges },
                new MenuItem { Header = "Re_load", Command = menu.Item.ReloadCommand },
            },
        }.Open(menu.Source);
    }

    /// <summary>Makes a recent document's menu item open it, and says where it is.</summary>
    /// <param name="prepared">The menu item with its recent document, and the window's view model.</param>
    private static void PrepareRecentItem(Injected<RecentMenuEntry, MainViewModel> prepared)
    {
        var (entry, viewModel) = prepared;
        entry.Item.Command = viewModel.OpenRecentCommand;
        entry.Item.CommandParameter = entry.Recent;
        ControlHelp.SetText(entry.Item, string.Create(CultureInfo.CurrentCulture, $"Open {entry.Recent.FileName} from {entry.Recent.Folder}."));
        AutomationProperties.SetName(entry.Item, entry.Recent.FileName);
    }
}
