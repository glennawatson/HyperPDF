// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Removes form fields (ISO 32000-2 12.7). A field goes with the fields below it and all its widgets. The pages, the form's
/// field and calculation order arrays, the structure tree and actions that name the field are all brought up to date, and a
/// group field left with no kid goes too, so no reference is left pointing at a deleted object.
/// </summary>
public static class PdfFormFieldRemoval
{
    /// <summary>Removes a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <returns>What was done; a name that is not a field is listed as skipped.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRemoval RemoveField(PdfDocument document, string name) => RemoveField(document, name, CancellationToken.None);

    /// <summary>Removes a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was done; a name that is not a field is listed as skipped.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFieldRemoval RemoveField(PdfDocument document, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Run(document, [name], cancellationToken);
    }

    /// <summary>Removes fields in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="names">The fully qualified names.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRemoval RemoveFields(PdfDocument document, IReadOnlyList<string> names) => RemoveFields(document, names, CancellationToken.None);

    /// <summary>Removes fields in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="names">The fully qualified names.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRemoval RemoveFields(PdfDocument document, IReadOnlyList<string> names, CancellationToken cancellationToken) => Run(document, names, cancellationToken);

    /// <summary>Removes a field after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldRemoval> RemoveFieldAsync(PdfDocument document, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return await RemoveFieldsAsync(document, [name], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes fields after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="names">The fully qualified names.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldRemoval> RemoveFieldsAsync(PdfDocument document, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return Run(document, names, cancellationToken);
    }

    /// <summary>Removes the fields in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="names">The fully qualified names.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <returns>What was done.</returns>
    private static PdfFieldRemoval Run(PdfDocument document, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(names);
        cancellationToken.ThrowIfCancellationRequested();
        var run = new RemovalRun(document, names, cancellationToken);
        PdfDocumentEditing.RunEdit(document, names.Count == 1 ? "Remove form field" : "Remove form fields", PdfChangeKinds.Other, PdfCacheScopes.Structure, run, Apply);
        return run.Result ?? new([], [], 0, []);
    }

    /// <summary>Removes the fields inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The removal.</param>
    private static void Apply(PdfEditTransaction transaction, RemovalRun run)
    {
        lock (PdfDocumentForms.GetForm(run.Document).Gate)
        {
            var index = FormFieldIndex.Build(run.Document, run.CancellationToken);
            var removal = new FormFieldRemoval(run.Document, transaction, index);
            removal.Select(run.Names);
            run.Result = removal.Remove(run.CancellationToken);
        }
    }

    /// <summary>The fields to remove and what was done.</summary>
    /// <param name="document">The document.</param>
    /// <param name="names">The names.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    [DebuggerDisplay("RemovalRun: {Names.Count} names")]
    private sealed class RemovalRun(PdfDocument document, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the names.</summary>
        internal IReadOnlyList<string> Names { get; } = names;

        /// <summary>Gets the token that cancels the edit.</summary>
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets or sets what the removal did.</summary>
        internal PdfFieldRemoval? Result { get; set; }
    }
}
