// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One annotation in the sidebar: its kind, page and note, with its colour beside the name.</summary>
[DebuggerDisplay("AnnotationItemView: {ViewModel}")]
public sealed partial class AnnotationItemView : ReactiveUI.Avalonia.ReactiveUserControl<AnnotationItemViewModel>
{
    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>Initializes a new instance of the <see cref="AnnotationItemView"/> class.</summary>
    public AnnotationItemView()
    {
        InitializeComponent();

        // An annotation item is an immutable snapshot with no change notification, so OneWayBind cannot observe it.
        // The view follows which item it shows instead.
        var items = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(items.Select(static item => item?.Heading).BindTo(this, static v => v.KindText.Text));
            disposables.Add(items.Select(static item => item?.Summary).BindTo(this, static v => v.SummaryText.Text));
            disposables.Add(items.Select(static item => item?.Details).BindTo(this, static v => v.DetailsText.Text));
            disposables.Add(items.Select(static item => item?.RepliesText).BindTo(this, static v => v.RepliesText.Text));
            disposables.Add(items.Select(static item => item is { RepliesText.Length: > 0 }).BindTo(this, static v => v.RepliesText.IsVisible));
            disposables.Add(items
                .Select(static item => item is null ? null : (IBrush)new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | item.Annotation.Color)))
                .BindTo(this, static v => v.Swatch.Background));
        });
    }
}
