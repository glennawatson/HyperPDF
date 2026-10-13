// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Creates form fields (ISO 32000-2 12.7): text, check box, radio group, combo box, list box, push button and signature
/// fields, each with widgets on the pages you choose. The library draws every widget, adds the form and its default
/// resources when the document has none, names the field for assistive software, and tags the widgets in a tagged document.
/// A document that claims PDF/A keeps the claim or the call fails with a plain reason.
/// </summary>
/// <remarks>
/// The call is one edit: it commits as one undo step, or joins the open transaction. A signed document can still be
/// edited; <see cref="PdfDocumentSignatureEffects.GetSignatureEffects"/> reports whether the signature's permissions allow
/// the incremental save, and adding a field is a change that DocMDP level 1 to 3 does not allow.
/// </remarks>
public static class PdfFormFieldCreation
{
    /// <summary>Creates a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The field.</param>
    /// <returns>The created field.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The name is already used, or the document's PDF/A claim forbids the field.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCreatedField CreateField(PdfDocument document, PdfFieldDefinition definition) =>
        CreateField(document, definition, CancellationToken.None);

    /// <summary>Creates a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The field.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>The created field.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The name is already used, or the document's PDF/A claim forbids the field.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfCreatedField CreateField(PdfDocument document, PdfFieldDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return CreateFields(document, [definition], cancellationToken)[0];
    }

    /// <summary>Creates fields in one edit. The cost grows with the number of fields, not with its square.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definitions">The fields. A refused definition leaves the document unchanged.</param>
    /// <returns>The created fields, in order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">A name is already used, or the document's PDF/A claim forbids a field.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCreatedField[] CreateFields(PdfDocument document, IReadOnlyList<PdfFieldDefinition> definitions) =>
        CreateFields(document, definitions, CancellationToken.None);

    /// <summary>Creates fields in one edit. The cost grows with the number of fields, not with its square.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definitions">The fields. A refused definition leaves the document unchanged.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>The created fields, in order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">A name is already used, or the document's PDF/A claim forbids a field.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCreatedField[] CreateFields(PdfDocument document, IReadOnlyList<PdfFieldDefinition> definitions, CancellationToken cancellationToken) =>
        Run(document, definitions, cancellationToken);

    /// <summary>Creates a field after loading its pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The field.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>The created field.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The name is already used, or the document's PDF/A claim forbids the field.</exception>
    public static async ValueTask<PdfCreatedField> CreateFieldAsync(PdfDocument document, PdfFieldDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (await CreateFieldsAsync(document, [definition], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>Creates fields after loading their pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definitions">The fields.</param>
    /// <param name="cancellationToken">Cancels loading and the edit.</param>
    /// <returns>The created fields, in order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">A name is already used, or the document's PDF/A claim forbids a field.</exception>
    public static async ValueTask<PdfCreatedField[]> CreateFieldsAsync(PdfDocument document, IReadOnlyList<PdfFieldDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definitions);
        await PrefetchAsync(document, definitions, cancellationToken).ConfigureAwait(false);
        return Run(document, definitions, cancellationToken);
    }

    /// <summary>Loads the pages the widgets go on, so the synchronous edit reads them from memory.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definitions">The fields.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when the pages are loaded.</returns>
    private static async ValueTask PrefetchAsync(PdfDocument document, IReadOnlyList<PdfFieldDefinition> definitions, CancellationToken cancellationToken)
    {
        var loaded = new HashSet<int>();
        foreach (var definition in definitions)
        {
            foreach (var widget in definition?.Widgets ?? [])
            {
                if (widget is not null && (uint)widget.PageIndex < (uint)document.PageCount && loaded.Add(widget.PageIndex))
                {
                    _ = await PdfDocumentPages.GetPageAsync(document, widget.PageIndex, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>Creates the fields in one edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definitions">The fields.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <returns>The created fields, in order.</returns>
    private static PdfCreatedField[] Run(PdfDocument document, IReadOnlyList<PdfFieldDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definitions);
        cancellationToken.ThrowIfCancellationRequested();
        if (definitions.Count == 0)
        {
            return [];
        }

        var run = new CreationRun(document, definitions, cancellationToken);
        PdfDocumentEditing.RunEdit(document, definitions.Count == 1 ? "Add form field" : "Add form fields", PdfChangeKinds.Other, PdfCacheScopes.Structure, run, Apply);
        return [.. run.Created];
    }

    /// <summary>Plans every definition.</summary>
    /// <param name="definitions">The checked definitions.</param>
    /// <returns>The plans.</returns>
    private static FormFieldSpec[] PlanAll(IReadOnlyList<PdfFieldDefinition> definitions)
    {
        var specs = new FormFieldSpec[definitions.Count];
        for (var i = 0; i < specs.Length; i++)
        {
            specs[i] = FormFieldSpec.Create(definitions[i]);
        }

        return specs;
    }

    /// <summary>Checks the definitions and creates the fields inside the transaction, one edit at a time on the form.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The fields to create.</param>
    private static void Apply(PdfEditTransaction transaction, CreationRun run)
    {
        var document = run.Document;
        lock (PdfDocumentForms.GetForm(document).Gate)
        {
            var index = FormFieldIndex.Build(document, run.CancellationToken);
            FormFieldValidation.Check(document, index, run.Definitions);
            var creation = new FormFieldCreation(document, transaction, index, run.CancellationToken);
            creation.Prepare();
            foreach (var spec in PlanAll(run.Definitions))
            {
                creation.Create(spec);
            }

            creation.Finish();
            run.Created.AddRange(creation.Created);
        }
    }

    /// <summary>The fields to create and what was created.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Definitions">The fields to create.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    [DebuggerDisplay("CreationRun: {Definitions.Count} fields")]
    private sealed record CreationRun(PdfDocument Document, IReadOnlyList<PdfFieldDefinition> Definitions, CancellationToken CancellationToken)
    {
        /// <summary>Gets the fields created.</summary>
        internal List<PdfCreatedField> Created { get; } = [];
    }
}
