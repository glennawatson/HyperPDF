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
        _ = this.WhenActivated(disposables => disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))));
    }

    /// <summary>Shows an annotation item.</summary>
    /// <param name="item">The item.</param>
    private void Show(AnnotationItemViewModel? item)
    {
        KindText.Text = item?.Heading;
        SummaryText.Text = item?.Summary;
        DetailsText.Text = item?.Details;
        RepliesText.Text = item?.RepliesText;
        RepliesText.IsVisible = item is { RepliesText.Length: > 0 };
        Swatch.Background = item is null ? null : new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | item.Annotation.Color));
    }
}
