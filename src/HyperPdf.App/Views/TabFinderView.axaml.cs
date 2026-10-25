// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.ViewModels;
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
            disposables.Add(UiText.CultureChanges.SubscribeSafe(_ => RefreshCultureText(), ErrorTrace.Write));
            disposables.Add(ItemAutomation.NameItems(TabFinderList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.TabFinderButton.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tabs.Count, static v => v.TabCountText.Text, static count => UiText.Format("Tabs {0}", count)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TabQuery, static v => v.TabFinderBox.Text, static query => query, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FoundTabs, static v => v.TabFinderList.ItemsSource));

            // Picking a tab selects it and puts the finder away.
            disposables.Add(TabFinderList.SelectionChanges
                .Select(static args => (args.Source as ListBox)?.SelectedItem as DocumentTabViewModel)
                .KeepNotNull()
                .Inject(viewModels)
                .Do(static found => found.State.SelectedTab = found.Value)
                .SubscribeSafe(_ => TabFinderButton.Flyout?.Hide(), ErrorTrace.Write));
            disposables.Add(TabFinderBox.KeyDowns.Pressing(Key.Enter)
                .Inject(viewModels)
                .Where(static found => found.State.FoundTabs.Count > 0)
                .SubscribeSafe(found => TabFinderList.SelectedItem = found.State.FoundTabs[0], ErrorTrace.Write));

            // The finder opens from its button or from the view model, and the view model follows the flyout.
            disposables.Add(TabFinderButton.FlyoutOpenings.InjectLatest(viewModels)
                .Do(static viewModel =>
                {
                    viewModel.RefreshFoundTabs();
                    viewModel.IsTabFinderOpen = true;
                })
                .SelectWith(TabFinderList, static (list, _) => list)
                .Do(static list => list.SelectedItem = null)
                .SelectWith(TabFinderBox, static (box, _) => box)
                .FocusSelectingAll());
            disposables.Add(TabFinderButton.FlyoutClosings.InjectLatest(viewModels).SubscribeSafe(static viewModel => viewModel.IsTabFinderOpen = false, ErrorTrace.Write));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.IsTabFinderOpen)
                .Where(static open => open)
                .WhereWith(this, static (view, _) => view.TabFinderButton.Flyout is { IsOpen: false })
                .SubscribeSafe(_ => TabFinderButton.Flyout!.ShowAt(TabFinderButton), ErrorTrace.Write));
        });
    }

    /// <summary>Refreshes translated presentation text without notifying or changing document state.</summary>
    private void RefreshCultureText()
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        TabCountText.Text = UiText.Format("Tabs {0}", model.Tabs.Count);
    }
}
