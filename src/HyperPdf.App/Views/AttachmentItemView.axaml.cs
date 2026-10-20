// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Attachments;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One embedded file in the attachments panel: its name and size.</summary>
[DebuggerDisplay("AttachmentItemView: {ViewModel}")]
public sealed partial class AttachmentItemView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentAttachment>
{
    /// <summary>Initializes a new instance of the <see cref="AttachmentItemView"/> class.</summary>
    public AttachmentItemView()
    {
        InitializeComponent();

        // An attachment is an immutable record with no change notification, so OneWayBind cannot observe it.
        // The view follows which one it shows instead.
        var attachments = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(attachments.Select(static attachment => attachment?.Name).BindTo(this, static v => v.NameText.Text));
            disposables.Add(attachments.Select(static attachment => attachment is null ? null : AttachmentsViewModel.FormatSize(attachment.Size)).BindTo(this, static v => v.SizeText.Text));
            disposables.Add(attachments.Select(static attachment => attachment?.PortfolioDetails).BindTo(this, static v => v.DetailText.Text));
            disposables.Add(attachments.Select(static attachment => attachment?.PortfolioDetails is not null).BindTo(this, static v => v.DetailText.IsVisible));
        });
    }
}
