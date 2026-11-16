// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.FormFilling;

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
        owner.SetNotice(
            string.IsNullOrEmpty(submission.Url)
                ? LocalizedMessage.Get(UiText, "This form asked to send your answers. HyperPDF does not send form data.")
                : LocalizedMessage.Format(UiText, "This form asked to send your answers to {0}. HyperPDF does not send form data.", submission.Url));
        return ValueTask.FromResult(false);
    }

    /// <inheritdoc/>
    /// <remarks>The library's interface asks for a task, so the import, which is a signal, is turned into one here at the edge.</remarks>
    public ValueTask<bool> ImportAsync(string? file, IFormDataImporter importer, CancellationToken cancellationToken) =>
        new(Import(file, importer).ToTask(cancellationToken));

    /// <summary>Imports form data from a file the reader chooses once the reader agrees.</summary>
    /// <param name="file">The file name the document gives, which is only shown.</param>
    /// <param name="importer">Applies the data to the form.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the data was imported; unsubscribing gives up on the questions and the read.</returns>
    internal IObservable<bool> Import(string? file, IFormDataImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);

        // A page action that runs by itself, such as a page's open action, may not put questions to the reader.
        if (!allowNavigation)
        {
            return Signal.Return(false);
        }

        var name = FormDataFiles.DisplayName(file);
        return owner.Forms.ConfirmImportInteraction.Handle(name).ToSignal()
            .SwitchMap(confirmed => confirmed

                // The reader picks the file. The path in the document is only shown, so a form cannot read a file of its choosing.
                ? owner.Forms.ChooseImportFileInteraction.Handle(name).ToSignal()
                : Signal.Return<string?>(null))
            .SwitchMap(path => path is { Length: > 0 } ? Imported(path, importer) : Signal.Return(false));
    }

    /// <summary>Reads the chosen file and applies it to the form.</summary>
    /// <param name="path">The file the reader chose.</param>
    /// <param name="importer">Applies the data to the form.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the data was imported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> Imported(string path, IFormDataImporter importer) =>
        FormDataFiles.Read(path)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(data =>
            {
                var result = data is null ? FormDataImportResult.Failed : importer.Import(data);
                owner.SetNotice(LocalizedMessage.Literal(FormDataFiles.Describe(Path.GetFileName(path), result)));
                return result.Succeeded;
            });
}
