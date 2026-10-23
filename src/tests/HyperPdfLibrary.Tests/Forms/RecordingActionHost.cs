// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>An action host that records what it was asked and answers the same to everything.</summary>
internal sealed class RecordingActionHost : IPdfActionHost
{
    /// <summary>Gets the named actions asked for, in order.</summary>
    internal List<string> Named { get; } = [];

    /// <summary>Gets the navigation actions asked for, in order.</summary>
    internal List<PdfAction> Navigated { get; } = [];

    /// <summary>Gets the submissions offered, in order.</summary>
    internal List<PdfFormSubmission> Submissions { get; } = [];

    /// <summary>Gets the imports offered, in order.</summary>
    internal List<ImportDataAction> Imports { get; } = [];

    /// <summary>Gets or sets a value indicating whether the host does what it is asked.</summary>
    internal bool Accepts { get; set; } = true;

    /// <inheritdoc/>
    public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken)
    {
        Named.Add(name);
        return ValueTask.FromResult(Accepts);
    }

    /// <inheritdoc/>
    public ValueTask<bool> NavigateAsync(PdfAction action, int sourcePage, CancellationToken cancellationToken)
    {
        Navigated.Add(action);
        return ValueTask.FromResult(Accepts);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SubmitFormAsync(PdfFormSubmission submission, CancellationToken cancellationToken)
    {
        Submissions.Add(submission);
        return ValueTask.FromResult(Accepts);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ImportDataAsync(ImportDataAction action, CancellationToken cancellationToken)
    {
        Imports.Add(action);
        return ValueTask.FromResult(Accepts);
    }
}
