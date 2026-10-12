// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>Labels the thumbnail selection's page edits and opens file pickers for importing and extracting.</summary>
[DebuggerDisplay("PageManagementView: {ViewModel}")]
public sealed partial class PageManagementView : ReactiveUI.Avalonia.ReactiveUserControl<PageManagementViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PageManagementView"/> class.</summary>
    public PageManagementView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            BindActions(disposables);
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.PageStatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UndoText, static v => v.UndoItem.Header));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RedoText, static v => v.RedoItem.Header));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.InsertInteraction, OpenFilesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.MergeFilesInteraction, OpenFilesAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ExtractInteraction, SaveFileAsync));
        });
    }

    /// <summary>Binds each labelled action to the selection's command.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindActions(MultipleDisposable disposables)
    {
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.DeleteCommand, static v => v.DeleteItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.RotateClockwiseCommand, static v => v.ClockwiseItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.RotateCounterclockwiseCommand, static v => v.CounterclockwiseItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.DuplicateCommand, static v => v.DuplicateItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveEarlierCommand, static v => v.EarlierItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveLaterCommand, static v => v.LaterItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.InsertFilesCommand, static v => v.InsertItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.MergeFilesCommand, static v => v.MergeItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.ExtractCommand, static v => v.ExtractItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.UndoCommand, static v => v.UndoItem));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.RedoCommand, static v => v.RedoItem));
    }

    /// <summary>Asks which PDFs to insert in the order selected.</summary>
    /// <param name="context">The file selection interaction.</param>
    /// <returns>A task completing after the picker closes.</returns>
    private async Task OpenFilesAsync(IInteractionContext<RxVoid, string[]> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput([]);
            return;
        }

        var files = await storage.OpenFilePickerAsync(new() { Title = "Insert or Merge PDFs", AllowMultiple = true, FileTypeFilter = [FilePickerFileTypes.Pdf] });
        var paths = new List<string>(files.Count);
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        context.SetOutput([.. paths]);
    }

    /// <summary>Asks where to save the extracted pages.</summary>
    /// <param name="context">The interaction with the suggested file name.</param>
    /// <returns>A task completing after the picker closes.</returns>
    private async Task SaveFileAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new()
        {
            Title = "Extract Selected Pages",
            SuggestedFileName = context.Input,
            DefaultExtension = "pdf",
            FileTypeChoices = [FilePickerFileTypes.Pdf],
        });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
