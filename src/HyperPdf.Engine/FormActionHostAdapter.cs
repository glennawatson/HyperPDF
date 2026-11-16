// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine;

/// <summary>Gives the library's action runner the application's host, turning the library's action types into the viewer's.</summary>
/// <param name="owner">The document, whose library document turns actions into link targets and which receives imported form data.</param>
/// <param name="host">The application's host.</param>
[DebuggerDisplay("FormActionHostAdapter")]
internal sealed class FormActionHostAdapter(HyperPdfDocument owner, IFormActionHost host) : IPdfActionHost
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken) =>
        new(host.RunNamed(name, sourcePage).ToTask(cancellationToken));

    /// <inheritdoc/>
    public ValueTask<bool> NavigateAsync(PdfAction action, int sourcePage, CancellationToken cancellationToken)
    {
        var target = LinkTargets.From(owner.Document, action, sourcePage);
        return target.Kind == LinkTargetKind.None ? ValueTask.FromResult(false) : new(host.Open(target).ToTask(cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<bool> SubmitFormAsync(PdfFormSubmission submission, CancellationToken cancellationToken)
    {
        var fields = new FormSubmittedField[submission.Fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = new(submission.Fields[i].Name, submission.Fields[i].Value);
        }

        return new(host.Submit(new(submission.Url, fields)).ToTask(cancellationToken));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> ImportDataAsync(ImportDataAction action, CancellationToken cancellationToken) =>
        new(host.Import(action.File, new HyperPdfFormDataImporter(owner)).ToTask(cancellationToken));
}
