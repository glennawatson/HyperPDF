// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Sets the document's title, author, subject and keywords through the library, which keeps the XMP metadata in step.</summary>
/// <param name="owner">The owning document.</param>
internal sealed class HyperPdfDocumentInfoEditorService(HyperPdfDocument owner) : IDocumentInfoEditor
{
    /// <inheritdoc/>
    public IDocument Document => owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<DocumentEditResult> SetInfo(DocumentInfoEdit edit) =>
        Signal.FromAsync(token => HyperPdfDocumentEdits.RunAsync(owner, edit, NothingToLoadAsync, Apply, token).AsTask());

    /// <summary>The details live in the trailer and the catalog, which are read when the document opens.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask NothingToLoadAsync(PdfDocument document, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>Writes the details.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="edit">The new details.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Apply(PdfDocument document, DocumentInfoEdit edit) =>
        PdfDocumentMetadataEditing.SetMetadata(document, new PdfMetadataEdit { Title = edit.Title, Author = edit.Author, Subject = edit.Subject, Keywords = edit.Keywords });
}
