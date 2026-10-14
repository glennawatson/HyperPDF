// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The Tabs and settings menu: opening, recent documents, windows, preferences and closing tabs.</summary>
[DebuggerDisplay("TabsMenuView: {ViewModel}")]
public sealed partial class TabsMenuView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabsMenuView"/> class.</summary>
    public TabsMenuView()
    {
        InitializeComponent();
        RecentMenuItem.ItemTemplate = new FuncDataTemplate<RecentDocument>(static (recent, _) => new TextBlock { Text = recent?.FileName });
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PageToneEnabled, static v => v.PageToneMenuItem.IsChecked));

            // Open Recent is filled from the recent documents, refreshed each time the menu opens.
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentMenuItem.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentMenuItem.IsEnabled, static count => count > 0));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.ClearRecentMenuItem.IsEnabled, static count => count > 0));
            disposables.Add(RecentMenuItem.ContainersPrepared
                .Pick(static args => args.Container is MenuItem { DataContext: RecentDocument recent } item ? new RecentMenuEntry(item, recent) : default(RecentMenuEntry?))
                .Inject(viewModels)
                .SubscribeSafe(ShowRecentItem, ErrorTrace.Write));
            disposables.Add(TabsMenuButton.FlyoutOpenings.InjectLatest(viewModels)
                .SubscribeSafe(static viewModel => viewModel.RefreshRecentDocuments(), ErrorTrace.Write));
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

    /// <summary>Makes a recent document's menu item open it, and says where it is.</summary>
    /// <param name="prepared">The menu item with its recent document, and the window's view model.</param>
    private static void ShowRecentItem(Injected<RecentMenuEntry, MainViewModel> prepared)
    {
        var (entry, viewModel) = prepared;
        entry.Item.Command = viewModel.OpenRecentCommand;
        entry.Item.CommandParameter = entry.Recent;
        ControlHelp.SetText(entry.Item, string.Create(CultureInfo.CurrentCulture, $"Open {entry.Recent.FileName} from {entry.Recent.Folder}."));
        AutomationProperties.SetName(entry.Item, entry.Recent.FileName);
    }
}
