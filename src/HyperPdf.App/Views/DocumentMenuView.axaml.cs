// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The Document menu: saving, the copies that are written to a new file, reloading, and the file's folder and properties.</summary>
[DebuggerDisplay("DocumentMenuView: {ViewModel}")]
public sealed partial class DocumentMenuView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="DocumentMenuView"/> class.</summary>
    public DocumentMenuView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveAsCommand, static v => v.SaveAsItem));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.SaveAsInteraction).HandleWith(SaveAsAsync));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Optimize.RunCommand, static v => v.OptimizeCopyItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Optimize.MenuText, static v => v.OptimizeCopyItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Optimize.Interaction).ShowDialogOver(this, static copy => new OptimizeCopyWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Archive.RunCommand, static v => v.ArchiveCopyItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Archive.MenuText, static v => v.ArchiveCopyItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Archive.Interaction).ShowDialogOver(this, static copy => new ArchiveCopyWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Pdf20.RunCommand, static v => v.Pdf20CopyItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Pdf20.MenuText, static v => v.Pdf20CopyItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Pdf20.Interaction).ShowDialogOver(this, static copy => new Pdf20CopyWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Flatten.RunCommand, static v => v.FlattenCopyItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Flatten.MenuText, static v => v.FlattenCopyItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Flatten.Interaction).ShowDialogOver(this, static copy => new FlattenCopyWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Protect.RunCommand, static v => v.ProtectItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Protect.MenuText, static v => v.ProtectItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Protect.Interaction).ShowDialogOver(this, static copy => new ProtectWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Unprotect.RunCommand, static v => v.RemoveProtectionItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Unprotect.MenuText, static v => v.RemoveProtectionItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Unprotect.Interaction).ShowDialogOver(this, static copy => new RemoveProtectionWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Redact.RunCommand, static v => v.ApplyRedactionsItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Redact.MenuText, static v => v.ApplyRedactionsItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Redact.Interaction).ShowDialogOver(this, static copy => new RedactionWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Validation.RunCommand, static v => v.ValidationDataItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.Validation.MenuText, static v => v.ValidationDataItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.Validation.Interaction).ShowDialogOver(this, static copy => new ValidationDataWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.ExternalSign.RunCommand, static v => v.ExternalSignItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Copies.ExternalSign.MenuText, static v => v.ExternalSignItem.Header));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Copies.ExternalSign.Interaction).ShowDialogOver(this, static copy => new ExternalSignWindow { ViewModel = copy }));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PageTools.SelectAllCommand, static v => v.SelectAllItem));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadItem));

            // The commands belong to the window's view model, not this view's, so the binding starts from that source.
            if (TopLevel.GetTopLevel(this) is MainWindow { ViewModel: { } main })
            {
                disposables.Add(main.BindOneWay(this, static vm => vm.ShowInFolderCommand, static v => v.ShowInFolderItem.Command, static command => (ICommand?)command));
                disposables.Add(main.BindOneWay(this, static vm => vm.PropertiesCommand, static v => v.PropertiesItem.Command, static command => (ICommand?)command));
            }
        });
    }

    /// <summary>Asks where to save a copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save a Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
