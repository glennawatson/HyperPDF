// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>One page of Focus Mode; its reading order is worked out when the page comes into view.</summary>
[DebuggerDisplay("FocusPageView: {ViewModel}")]
public sealed partial class FocusPageView : ReactiveUI.Avalonia.ReactiveUserControl<FocusPageViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FocusPageView"/> class.</summary>
    public FocusPageView()
    {
        InitializeComponent();
        BlockList.ItemTemplate = new FuncDataTemplate<FocusBlockViewModel>((_, _) => new FocusBlockView { FocusMode = FocusMode });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Label, static v => v.PageLabel.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Blocks, static v => v.BlockList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsEmpty, static v => v.EmptyText.IsVisible));

            // A page above the view that loads and grows pushes the place down; the scroll viewer keeps it still when the
            // pages are anchor candidates.
            disposables.Add(Scope.Create(this, static page => page.FindAncestorOfType<ScrollViewer>()?.UnregisterAnchorCandidate(page)));
            this.FindAncestorOfType<ScrollViewer>()?.RegisterAnchorCandidate(this);

            // The page is a plain, fully realised list item, so its text loads only once it is near the view.
            disposables.Add(this.Events().EffectiveViewportChanged.Nearing(this)
                .InjectLatest(this.WhenChanged(static v => v.ViewModel))
                .SubscribeSafe(static page => _ = page.LoadAsync(), ErrorTrace.Write));
        });
    }

    /// <summary>Gets or sets Focus Mode, handed to each block for Read from Here.</summary>
    public FocusModeViewModel? FocusMode { get; set; }

    /// <summary>Gets the block views currently shown.</summary>
    /// <returns>The views, top to bottom.</returns>
    internal List<FocusBlockView> BlockViews()
    {
        var views = new List<FocusBlockView>();
        foreach (var visual in BlockList.GetVisualDescendants())
        {
            if (visual is FocusBlockView view)
            {
                views.Add(view);
            }
        }

        return views;
    }
}
