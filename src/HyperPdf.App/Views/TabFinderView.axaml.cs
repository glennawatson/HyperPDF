// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The button that finds an open tab by part of its name, title or folder, and the flyout it opens.</summary>
[DebuggerDisplay("TabFinderView: {ViewModel}")]
public sealed partial class TabFinderView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabFinderView"/> class.</summary>
    public TabFinderView()
    {
        InitializeComponent();
        TabFinderList.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabSummaryView());
        var viewModels = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(TabFinderList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.TabFinderButton.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs.Count, static v => v.TabCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"Tabs {count}")));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TabQuery, static v => v.TabFinderBox.Text, static query => query, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FoundTabs, static v => v.TabFinderList.ItemsSource));
            disposables.Add(TabFinderList.SelectionChanges
                .Select(static args => (args.Source as ListBox)?.SelectedItem as DocumentTabViewModel)
                .KeepNotNull()
                .Inject(viewModels)
                .SubscribeSafe(
                    found =>
                    {
                        var (tab, viewModel) = found;
                        viewModel.SelectedTab = tab;
                        TabFinderButton.Flyout?.Hide();
                    },
                    static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabFinderBox.KeyDowns.Pressing(Key.Enter)
                .Inject(viewModels)
                .Where(static found => found.State.FoundTabs.Count > 0)
                .SubscribeSafe(found => TabFinderList.SelectedItem = found.State.FoundTabs[0], static error => Trace.TraceError(error.ToString())));
            disposables.Add(TabFinderButton.FlyoutOpenings.InjectLatest(viewModels).SubscribeSafe(
                viewModel =>
                {
                    viewModel.RefreshFoundTabs();
                    TabFinderList.SelectedItem = null;
                    _ = TabFinderBox.Focus();
                    TabFinderBox.SelectAll();
                },
                static error => Trace.TraceError(error.ToString())));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.TabFinderRequests).SwitchTo()
                .SubscribeSafe(_ => TabFinderButton.Flyout!.ShowAt(TabFinderButton), static error => Trace.TraceError(error.ToString())));
        });
    }
}
