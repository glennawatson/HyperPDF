// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The tab's part of running a form's actions: it moves between pages, follows links through the tab's own link handling,
/// imports form data from a file the reader chooses once the reader agrees, and refuses to send form data, saying so in
/// the tab's notice. Sending data off the computer is never done without the user, and the viewer does not offer it.
/// </summary>
/// <param name="owner">The tab.</param>
/// <param name="allowNavigation">
/// Whether actions may move the reader or open links. Actions that run on their own, such as a page's open action, may
/// not, so a page cannot send the reader somewhere by itself.
/// </param>
[DebuggerDisplay("TabFormActionHost: {owner.FileName}")]
internal sealed class TabFormActionHost(DocumentTabViewModel owner, bool allowNavigation) : IFormActionHost
{
    /// <inheritdoc/>
    public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken)
    {
        if (!allowNavigation)
        {
            return ValueTask.FromResult(false);
        }

        var page = sourcePage >= 0 ? sourcePage : owner.CurrentPageIndex;
        int? target = name switch
        {
            "NextPage" => page + 1,
            "PrevPage" => page - 1,
            "FirstPage" => 0,
            "LastPage" => owner.PageCount - 1,
            _ => null,
        };
        if (target is not { } index)
        {
            return ValueTask.FromResult(false);
        }

        owner.GoToPage(index);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> OpenAsync(LinkTarget target, CancellationToken cancellationToken)
    {
        if (!allowNavigation)
        {
            return ValueTask.FromResult(false);
        }

        owner.Navigate(target);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SubmitAsync(FormSubmission submission, CancellationToken cancellationToken)
    {
        // Sending needs network plumbing and a consent flow that the viewer does not have, so the action is refused and explained.
        owner.Notice = string.IsNullOrEmpty(submission.Url)
            ? "This form asked to send your answers. HyperPDF does not send form data."
            : $"This form asked to send your answers to {submission.Url}. HyperPDF does not send form data.";
        return ValueTask.FromResult(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ImportAsync(string? file, IFormDataImporter importer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(importer);

        // A page action that runs by itself, such as a page's open action, may not put questions to the reader.
        if (!allowNavigation)
        {
            return false;
        }

        var name = FormDataFiles.DisplayName(file);
        if (!await owner.Forms.ConfirmImportInteraction.Handle(name).ToTask(cancellationToken).ConfigureAwait(true))
        {
            return false;
        }

        // The reader picks the file. The path in the document is only shown, so a form cannot read a file of its choosing.
        if (await owner.Forms.ChooseImportFileInteraction.Handle(name).ToTask(cancellationToken).ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return false;
        }

        var chosen = Path.GetFileName(path);
        var data = await FormDataFiles.ReadAsync(path, cancellationToken).ConfigureAwait(true);
        var result = data is null ? FormDataImportResult.Failed : importer.Import(data);
        owner.Notice = FormDataFiles.Describe(chosen, result);
        return result.Succeeded;
    }
}
