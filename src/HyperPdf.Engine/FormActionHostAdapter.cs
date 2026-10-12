// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;

namespace HyperPdf.Engine;

/// <summary>Gives the library's action runner the application's host, turning the library's action types into the viewer's.</summary>
/// <param name="document">The library document, which turns actions into link targets.</param>
/// <param name="host">The application's host.</param>
[DebuggerDisplay("FormActionHostAdapter")]
internal sealed class FormActionHostAdapter(PdfDocument document, IFormActionHost host) : IPdfActionHost
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken) =>
        host.RunNamedAsync(name, sourcePage, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<bool> NavigateAsync(PdfAction action, int sourcePage, CancellationToken cancellationToken)
    {
        var target = LinkTargets.From(document, action, sourcePage);
        return target.Kind == LinkTargetKind.None ? ValueTask.FromResult(false) : host.OpenAsync(target, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SubmitFormAsync(PdfFormSubmission submission, CancellationToken cancellationToken)
    {
        var fields = new FormSubmittedField[submission.Fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = new(submission.Fields[i].Name, submission.Fields[i].Value);
        }

        return host.SubmitAsync(new(submission.Url, fields), cancellationToken);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> ImportDataAsync(ImportDataAction action, CancellationToken cancellationToken) =>
        host.ImportAsync(action.File, cancellationToken);
}
