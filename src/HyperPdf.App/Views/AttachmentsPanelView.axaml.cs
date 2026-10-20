// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Attachments;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The files embedded in the document, with the buttons that open or save the chosen one.</summary>
[DebuggerDisplay("AttachmentsPanelView: {ViewModel}")]
public sealed partial class AttachmentsPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="AttachmentsPanelView"/> class.</summary>
    public AttachmentsPanelView()
    {
        InitializeComponent();
        AttachmentList.ItemTemplate = new FuncDataTemplate<DocumentAttachment>(static (_, _) => new AttachmentItemView());
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(AttachmentList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsAttachmentsMode, static v => v.AttachmentsPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.Items, static v => v.AttachmentList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.PortfolioText, static v => v.PortfolioHeading.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.PortfolioText, static v => v.PortfolioHeading.IsVisible, static text => text is not null));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Attachments.Selected, static v => v.AttachmentList.SelectedItem, static item => item, static item => item as DocumentAttachment));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Attachments.SaveCommand, static v => v.SaveAttachmentButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Attachments.OpenSelectedCommand, static v => v.OpenAttachmentButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Attachments.SaveInteraction).HandleWith(SaveAttachmentAsync));
        });
    }

    /// <summary>Asks where to save an embedded file, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAttachmentAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save Attachment", SuggestedFileName = context.Input });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
