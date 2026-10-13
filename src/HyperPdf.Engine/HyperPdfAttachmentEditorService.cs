// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Attaches and removes embedded files of an open document through the library.</summary>
internal sealed class HyperPdfAttachmentEditorService : IAttachmentEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAttachmentEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfAttachmentEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> AddAttachmentAsync(string name, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(_owner, new AddStep(name, data), HyperPdfEditPrefetch.AttachmentsAsync, Add, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> RemoveAttachmentAsync(int index, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(_owner, new RemoveStep(index), HyperPdfEditPrefetch.AttachmentsAsync, Remove, cancellationToken);

    /// <summary>Attaches a file.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="step">The arguments.</param>
    private static void Add(PdfDocument document, AddStep step) =>
        _ = PdfDocumentAttachmentEditing.AddAttachment(document, new(step.Name, step.Data));

    /// <summary>Removes an attached file.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="step">The arguments.</param>
    /// <exception cref="ArgumentException">The document has no file at the index.</exception>
    private static void Remove(PdfDocument document, RemoveStep step)
    {
        var attachments = PdfDocumentAttachments.GetAttachments(document);
        if ((uint)step.Index >= (uint)attachments.Count)
        {
            throw new ArgumentException("That file is no longer attached to the document.", nameof(step));
        }

        _ = PdfDocumentAttachmentEditing.RemoveAttachment(document, attachments[step.Index].Key);
    }

    /// <summary>The arguments of an add.</summary>
    /// <param name="Name">The file name.</param>
    /// <param name="Data">The bytes.</param>
    private readonly record struct AddStep(string Name, ReadOnlyMemory<byte> Data);

    /// <summary>The arguments of a removal.</summary>
    /// <param name="Index">The file's index.</param>
    private readonly record struct RemoveStep(int Index);
}
