// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One sound or video in the media panel: its label and size.</summary>
[DebuggerDisplay("MediaItemView: {ViewModel}")]
public sealed partial class MediaItemView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentMedia>
{
    /// <summary>Initializes a new instance of the <see cref="MediaItemView"/> class.</summary>
    public MediaItemView()
    {
        InitializeComponent();

        // Media is an immutable record with no change notification, so the view follows which one it shows.
        var items = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(items.Select(static media => media?.Name).BindTo(this, static v => v.NameText.Text));
            disposables.Add(items.Select(static media => media is null ? null : AttachmentsViewModel.FormatSize(media.Size)).BindTo(this, static v => v.SizeText.Text));
        });
    }
}
