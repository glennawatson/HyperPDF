// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>Labelled buttons that add, rename, remove and rearrange the bookmarks of the open document.</summary>
[DebuggerDisplay("OutlineEditView: {ViewModel}")]
public sealed partial class OutlineEditView : ReactiveUI.Avalonia.ReactiveUserControl<OutlineEditViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OutlineEditView"/> class.</summary>
    public OutlineEditView()
    {
        InitializeComponent();
        KeysText.Text = OutlineEditDescriptions.Keys;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.StatusText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.AddCommand, static v => v.AddButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RenameCommand, static v => v.RenameButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RemoveCommand, static v => v.RemoveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveUpCommand, static v => v.MoveUpButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.MoveDownCommand, static v => v.MoveDownButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.IndentCommand, static v => v.IndentButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OutdentCommand, static v => v.OutdentButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.UndoCommand, static v => v.UndoButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.PromptInteraction).HandleWith(PromptAsync));
        });
    }

    /// <summary>Asks the user for a bookmark title.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task PromptAsync(IInteractionContext<TextPrompt, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(null);
            return;
        }

        context.SetOutput(await new PromptWindow { ViewModel = new(context.Input) }.ShowDialog<string?>(owner));
    }
}
