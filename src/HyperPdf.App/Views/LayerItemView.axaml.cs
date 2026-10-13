// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One entry of the layers tree: a check box with the layer's name, or a label; a locked layer says so in words.</summary>
[DebuggerDisplay("LayerItemView: {ViewModel}")]
public sealed partial class LayerItemView : ReactiveUI.Avalonia.ReactiveUserControl<LayerItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="LayerItemView"/> class.</summary>
    public LayerItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.LayerBox.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.LabelText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsLabel, static v => v.LabelText.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsLabel, static v => v.LayerBox.IsVisible, static label => !label));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CanToggle, static v => v.LayerBox.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Note, static v => v.NoteText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasNote, static v => v.NoteText.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsVisible, static v => v.LayerBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString())));

            // The tree item that hosts this view owns the expander, so the model is bound to it directly.
            if (this.FindAncestorOfType<TreeViewItem>() is { } container)
            {
                disposables.Add(this.WhenChanged(static v => v.ViewModel!.IsExpanded).BindTo(container, static item => item.IsExpanded));
                disposables.Add(container.WhenChanged(static item => item.IsExpanded).BindTo(this, static v => v.ViewModel!.IsExpanded));
            }
        });
    }

    /// <summary>Names the tree item for screen readers, including that a layer is locked or that an entry is a group.</summary>
    /// <param name="entry">The entry.</param>
    private void Show(LayerItemViewModel? entry)
    {
        // The name is an attached property, which a binding expression cannot reach.
        ToolTip.SetTip(this, entry?.SpokenName);
        ItemAutomation.NameContainer(this, entry?.SpokenName);
    }
}
