// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI.Binding;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>The questions a form's Import Data action puts to the reader: whether to load a file, and which one.</summary>
public sealed partial class DocumentView
{
    /// <summary>Handles the form data questions.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFormDataImport(MultipleDisposable bindings)
    {
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ConfirmImportInteraction).HandleWith(ConfirmImportAsync));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Forms.ChooseImportFileInteraction).HandleWith(ChooseImportFileAsync));
    }

    /// <summary>Asks before a form loads answers from a file, naming the file the form asked for.</summary>
    /// <param name="context">The file's name in; <see langword="true"/> out to go on.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmImportAsync(IInteractionContext<string, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        ConfirmRequest request = new(
            "Load Answers",
            $"This form asks to load answers from {context.Input}. You choose the file next, and Undo takes the change back. Only load files you trust.",
            "Choose File");
        context.SetOutput(await new ConfirmWindow { ViewModel = new(request) }.ShowDialog<bool>(owner));
    }

    /// <summary>Lets the reader pick the FDF or XFDF file to load, through the desktop's open dialog.</summary>
    /// <param name="context">The file name the form asked for in; the chosen path out, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    private async Task ChooseImportFileAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var files = await storage.OpenFilePickerAsync(new()
        {
            Title = $"Choose the Answers to Load ({context.Input})",
            AllowMultiple = false,
            FileTypeFilter = [new("Form data (.fdf, .xfdf)") { Patterns = ["*.fdf", "*.xfdf"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
