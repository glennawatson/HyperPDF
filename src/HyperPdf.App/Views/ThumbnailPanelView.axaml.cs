// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The page thumbnails with the page tools above them.</summary>
[DebuggerDisplay("ThumbnailPanelView: {ViewModel}")]
public sealed partial class ThumbnailPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ThumbnailPanelView"/> class.</summary>
    public ThumbnailPanelView()
    {
        InitializeComponent();
        ThumbnailList.ItemTemplate = new FuncDataTemplate<ThumbnailItemViewModel>((_, _) => new ThumbnailItemView { Tab = ViewModel });
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(ThumbnailList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Pages, static v => v.PageTools.ViewModel));
            disposables.Add(ThumbnailList.PageShortcuts(tabs).SubscribeSafe(static shortcut => shortcut.Command.Execute(null), ErrorTrace.Write));
            disposables.Add(ThumbnailList.SelectionChanges.InjectLatest(tabs).SubscribeSafe(ThumbnailList.SyncSelectionTo, ErrorTrace.Write));
            disposables.Add(ThumbnailList.DragMoves(tabs).SubscribeSafe(static _ => { }, ErrorTrace.Write));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Pages.SelectedThumbnails, static v => v.ThumbnailList.SelectedItems));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Thumbnails, static v => v.ThumbnailList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReduceMotion, static v => v.ThumbnailList.ReduceMotion));
            disposables.Add(ThumbnailList.TemplateScrolls
                .Where(static args => args.OffsetDelta.Y != 0)
                .InjectLatest(tabs)
                .SubscribeSafe(RequestVisibleThumbnailsAgain, ErrorTrace.Write));
        });
    }

    /// <summary>Drops thumbnail requests for items scrolled out of view and re-requests the visible ones.</summary>
    /// <param name="tab">The tab whose thumbnails are listed.</param>
    private void RequestVisibleThumbnailsAgain(DocumentTabViewModel tab)
    {
        _ = tab.ThumbnailClient.Advance();
        foreach (var visual in ThumbnailList.GetVisualDescendants())
        {
            if (visual is PageThumbnail thumbnail)
            {
                thumbnail.InvalidateVisual();
            }
        }
    }
}
