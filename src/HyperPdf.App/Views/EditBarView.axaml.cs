// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>Edit mode's tools: retyping and removing the picked text, picture or drawing, undo and redo, and leaving the mode.</summary>
[DebuggerDisplay("EditBarView: {ViewModel}")]
public sealed partial class EditBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="EditBarView"/> class.</summary>
    public EditBarView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ContentEdit.IsEditing, static v => v.EditBar.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ContentEdit.EditPickedTextCommand, static v => v.RetypeButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ContentEdit.RemovePickedCommand, static v => v.RemoveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ContentEdit.ToggleEditingCommand, static v => v.EditDoneButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Pages.UndoCommand, static v => v.EditUndoButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Pages.RedoCommand, static v => v.EditRedoButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Copies.Pdf20.RunCommand, static v => v.EditPdf20Button));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ContentEdit.Status, static v => v.EditHint.Text));
        });
    }
}
