// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One page thumbnail in the sidebar.</summary>
[DebuggerDisplay("ThumbnailItemView: {ViewModel}")]
public sealed partial class ThumbnailItemView : ReactiveUI.Avalonia.ReactiveUserControl<ThumbnailItemViewModel>
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<ThumbnailItemView, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Initializes a new instance of the <see cref="ThumbnailItemView"/> class.</summary>
    public ThumbnailItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            // A thumbnail item is an immutable record with no change notification, so OneWayBind cannot observe it.
            // The view follows which item it shows, and which tab it belongs to.
            var items = this.WhenChanged(static v => v.ViewModel);
            disposables.Add(items.Select(static item => item?.Width ?? 0).BindTo(this, static v => v.Thumbnail.Width));
            disposables.Add(items.Select(static item => item?.Height ?? 0).BindTo(this, static v => v.Thumbnail.Height));
            disposables.Add(items.Select(static item => item?.PageIndex ?? -1).BindTo(this, static v => v.Thumbnail.PageIndex));
            disposables.Add(items.Select(static item => item?.Caption).BindTo(this, static v => v.LabelText.Text));
            disposables.Add(this.WhenChanged(static v => v.Tab).BindTo(this, static v => v.Thumbnail.Tab));
        });
    }

    /// <summary>Gets or sets the tab the page belongs to.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }
}
