// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The sidebar: the buttons that choose what it lists, and the search results.</summary>
[DebuggerDisplay("SidebarView: {ViewModel}")]
public sealed partial class SidebarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="SidebarView"/> class.</summary>
    public SidebarView()
    {
        InitializeComponent();
        SearchResultList.ItemTemplate = new FuncDataTemplate<SearchResultItemViewModel>(static (_, _) => new SearchResultView());

        // The focus is checked as the sidebar hides; a control it has just hidden still counts as inside it.
        KeyboardToPages = this.WhenChanged(static v => v.ViewModel!.SidebarVisible)
            .Where(static visible => !visible)
            .WhileFocusInside(Sidebar)
            .AsVoid;
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(SearchResultList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.Sidebar.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailsToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsOutlineMode, static v => v.OutlineToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultsToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.HasLayers, static v => v.LayersToggle.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsLayersMode, static v => v.LayersToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsTagsMode, static v => v.TagsToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.HasAttachments, static v => v.AttachmentsToggle.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsAttachmentsMode, static v => v.AttachmentsToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultList.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Results, static v => v.SearchResultList.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.Search.SelectedResult,
                static v => v.SearchResultList.SelectedItem,
                static item => item,
                static item => item as SearchResultItemViewModel));
        });
    }

    /// <summary>Gets the moments the keyboard should go to the pages: the sidebar hides while it holds the focus.</summary>
    internal IObservable<RxVoid> KeyboardToPages { get; }
}
