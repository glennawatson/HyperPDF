// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One outline entry; keeps the tree item's expansion in step with the view model both ways.</summary>
[DebuggerDisplay("OutlineItemView: {ViewModel}")]
public sealed partial class OutlineItemView : ReactiveUI.Avalonia.ReactiveUserControl<OutlineItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OutlineItemView"/> class.</summary>
    public OutlineItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.TitleText.Text));
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, ErrorTrace.Write));

            // The tree item that hosts this view owns the expander, so the model is bound to it directly.
            // The model writes first, so the tree item starts from its state; each direction follows the view's ViewModel.
            if (this.FindAncestorOfType<TreeViewItem>() is { } container)
            {
                disposables.Add(this.WhenChanged(static v => v.ViewModel!.IsExpanded).BindTo(container, static item => item.IsExpanded));
                disposables.Add(container.WhenChanged(static item => item.IsExpanded).BindTo(this, static v => v.ViewModel!.IsExpanded));
            }
        });
    }

    /// <summary>Shows an entry's title as the tooltip and as the tree item's name for screen readers.</summary>
    /// <param name="entry">The entry.</param>
    private void Show(OutlineItemViewModel? entry)
    {
        // Both are attached properties, which a binding expression cannot reach.
        ToolTip.SetTip(this, entry?.Title);
        ItemAutomation.NameContainer(this, entry?.Title);
    }
}
