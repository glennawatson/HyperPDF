// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The comment list: a text filter, the filters and sort order, and the comments, each of which goes to its place when chosen.</summary>
[DebuggerDisplay("AnnotationListView: {ViewModel}")]
public sealed partial class AnnotationListView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AnnotationListView"/> class.</summary>
    public AnnotationListView()
    {
        InitializeComponent();
        AnnotationList.ItemTemplate = new FuncDataTemplate<AnnotationItemViewModel>(static (_, _) => new AnnotationItemView());
        AnnotationTypeBox.ItemsSource = AnnotationsViewModel.TypeFilterOptions;
        AnnotationColourBox.ItemsSource = AnnotationsViewModel.ColorFilterOptions;
        AnnotationSortBox.ItemsSource = AnnotationsViewModel.SortOptions;
        FieldLabels.Link(
            (AnnotationTypeBox, AnnotationTypeLabel),
            (AnnotationColourBox, AnnotationColourLabel),
            (AnnotationAuthorBox, AnnotationAuthorLabel),
            (AnnotationSortBox, AnnotationSortLabel));
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(AnnotationList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.VisibleItems, static v => v.AnnotationList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Items.Count, static v => v.NoAnnotationsText.IsVisible, static count => count == 0));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.NoMatches, static v => v.NoMatchesText.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterText, static v => v.AnnotationFilterBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.ShowFilters, static v => v.AnnotationFiltersToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShowFilters, static v => v.AnnotationFiltersPanel.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterType, static v => v.AnnotationTypeBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.FilterColor, static v => v.AnnotationColourBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.SortOrder, static v => v.AnnotationSortBox.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.AuthorOptions, static v => v.AnnotationAuthorBox.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.Annotations.FilterAuthor,
                static v => v.AnnotationAuthorBox.SelectedItem,
                static author => author,
                static item => item as string ?? AnnotationsViewModel.Everyone));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.ClearFilterCommand, static v => v.ClearAnnotationFilterButton));
            disposables.Add(AnnotationList.SelectionChanges
                .Select(static args => (args.Source as ListBox)?.SelectedItem as AnnotationItemViewModel)
                .Where(static item => item is not null)
                .InvokeCommand(this, static v => v.ViewModel!.Annotations.GoToCommand));
            disposables.Add(AnnotationList.ItemContextRequests<AnnotationItemViewModel>().Inject(tabs).SubscribeSafe(ShowAnnotationMenu, ErrorTrace.Write));
        });
    }

    /// <summary>Opens the menu of the annotation under the pointer in the sidebar: edit its note or delete it.</summary>
    /// <param name="request">The request and the tab whose annotations are listed.</param>
    private static void ShowAnnotationMenu(Injected<ItemContextRequest<AnnotationItemViewModel>, DocumentTabViewModel> request)
    {
        var (menu, tab) = request;
        new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Edit Note…", Command = tab.Annotations.EditNoteCommand, CommandParameter = menu.Item },
                new MenuItem { Header = "_Delete", Command = tab.Annotations.DeleteCommand, CommandParameter = menu.Item },
            },
        }.Open(menu.Source);
    }
}
