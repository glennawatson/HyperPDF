// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Renames form fields (ISO 32000-2 12.7.3.2). A field's fully qualified name is the partial names from the top of the
/// hierarchy joined by periods. <see cref="RenameField(PdfDocument, string, string)"/> takes a new fully qualified name and
/// moves the field under the groups it names; <see cref="RenameFieldPartial(PdfDocument, string, string)"/> changes only the
/// field's own part. A name that clashes, or a path that runs through a field that holds a value, is refused with a plain
/// reason. Actions that list or hide the field by name are updated, and scripts that mention it are reported.
/// </summary>
public static class PdfFormFieldRenaming
{
    /// <summary>Renames a field to a new fully qualified name, moving it when the name has other parents.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRename RenameField(PdfDocument document, string name, string newName) => RenameField(document, name, newName, CancellationToken.None);

    /// <summary>Renames a field to a new fully qualified name, moving it when the name has other parents.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRename RenameField(PdfDocument document, string name, string newName, CancellationToken cancellationToken) =>
        Run(document, name, newName, false, cancellationToken);

    /// <summary>Changes the last part of a field's name and keeps its parents.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newPartialName">The new partial name, which cannot hold a period.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRename RenameFieldPartial(PdfDocument document, string name, string newPartialName) => RenameFieldPartial(document, name, newPartialName, CancellationToken.None);

    /// <summary>Changes the last part of a field's name and keeps its parents.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newPartialName">The new partial name, which cannot hold a period.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFieldRename RenameFieldPartial(PdfDocument document, string name, string newPartialName, CancellationToken cancellationToken) =>
        Run(document, name, newPartialName, true, cancellationToken);

    /// <summary>Renames a field after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldRename> RenameFieldAsync(PdfDocument document, string name, string newName, CancellationToken cancellationToken)
    {
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return Run(document, name, newName, false, cancellationToken);
    }

    /// <summary>Changes the last part of a field's name after loading the document's pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newPartialName">The new partial name.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<PdfFieldRename> RenameFieldPartialAsync(PdfDocument document, string name, string newPartialName, CancellationToken cancellationToken)
    {
        await FormFieldPrefetch.LoadAllPagesAsync(document, cancellationToken).ConfigureAwait(false);
        return Run(document, name, newPartialName, true, cancellationToken);
    }

    /// <summary>Plans the rename and, if it is allowed, makes it in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="requested">The new name: fully qualified, or only the last part.</param>
    /// <param name="partialOnly">Whether <paramref name="requested"/> is only the last part.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <returns>What was done, or why the rename was refused.</returns>
    private static PdfFieldRename Run(PdfDocument document, string name, string requested, bool partialOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(requested);
        cancellationToken.ThrowIfCancellationRequested();
        var run = new RenameRun(document, name, requested, partialOnly, cancellationToken);
        PdfDocumentEditing.RunEdit(document, "Rename form field", PdfChangeKinds.Other, PdfCacheScopes.None, run, Apply);
        return run.Result ?? Refused(name, requested, "The rename was not made.");
    }

    /// <summary>Plans the rename and makes it inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The rename.</param>
    private static void Apply(PdfEditTransaction transaction, RenameRun run)
    {
        lock (PdfDocumentForms.GetForm(run.Document).Gate)
        {
            var index = FormFieldIndex.Build(run.Document, run.CancellationToken);
            var newName = run.PartialOnly ? FormFieldNames.Join(FormFieldNames.GetParent(run.Name), run.Requested) : run.Requested;
            var invalid = run.PartialOnly ? FormFieldNames.ValidatePartial(run.Requested) : null;
            var plan = invalid is null ? FormFieldRenaming.Plan(run.Document, index, run.Name, newName, out invalid) : null;
            run.Result = plan?.Apply(transaction, run.CancellationToken) ?? Refused(run.Name, newName, invalid ?? "The rename was not made.");
        }
    }

    /// <summary>Makes the result of a refused rename.</summary>
    /// <param name="name">The field's name.</param>
    /// <param name="newName">The name asked for.</param>
    /// <param name="reason">The plain reason.</param>
    /// <returns>The result.</returns>
    private static PdfFieldRename Refused(string name, string newName, string reason) => new(false, name, newName, reason, 0, []);

    /// <summary>A rename to make and what was done.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="requested">The name asked for.</param>
    /// <param name="partialOnly">Whether <paramref name="requested"/> is only the last part.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    [DebuggerDisplay("RenameRun: {Name} to {Requested}")]
    private sealed class RenameRun(PdfDocument document, string name, string requested, bool partialOnly, CancellationToken cancellationToken)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the field's fully qualified name.</summary>
        internal string Name { get; } = name;

        /// <summary>Gets the name asked for.</summary>
        internal string Requested { get; } = requested;

        /// <summary>Gets a value indicating whether <see cref="Requested"/> is only the last part.</summary>
        internal bool PartialOnly { get; } = partialOnly;

        /// <summary>Gets the token that cancels the edit.</summary>
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets or sets what the rename did.</summary>
        internal PdfFieldRename? Result { get; set; }
    }
}
