// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One part of the document in the parts tree: its name, the pages it covers and its metadata.</summary>
[DebuggerDisplay("PartItemView: {ViewModel}")]
public sealed partial class PartItemView : ReactiveUI.Avalonia.ReactiveUserControl<PartItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PartItemView"/> class.</summary>
    public PartItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.NameText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PagesText, static v => v.PagesText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PagesText, static v => v.PagesText.IsVisible, static text => text.Length > 0));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Details, static v => v.DetailText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasDetails, static v => v.DetailText.IsVisible));
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(NameTreeItem, ErrorTrace.Write));

            // Every level of the tree is open, so no part is hidden behind a collapsed one.
            if (this.FindAncestorOfType<TreeViewItem>() is { } container)
            {
                container.IsExpanded = true;
            }
        });
    }

    /// <summary>Names the tree item for screen readers.</summary>
    /// <param name="entry">The entry.</param>
    private void NameTreeItem(PartItemViewModel? entry)
    {
        // The name is an attached property, which a binding expression cannot reach.
        ToolTip.SetTip(this, entry?.SpokenName);
        ItemAutomation.NameContainer(this, entry?.SpokenName);
    }
}
