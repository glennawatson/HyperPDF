// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms;
using HyperPdfLibrary;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Interchange;

namespace HyperPdf.Engine;

/// <summary>
/// Imports FDF and XFDF files into an open document as one undoable edit named "Import form data" (ISO 32000-2 §12.7.8
/// and ISO 19444-1). The file is parsed with the library's bounded readers; a file that is not valid leaves the document unchanged.
/// </summary>
/// <param name="owner">The document that receives the data.</param>
[DebuggerDisplay("HyperPdfFormDataImporter")]
internal sealed class HyperPdfFormDataImporter(HyperPdfDocument owner) : IFormDataImporter
{
    /// <summary>The label the edit has in the undo history.</summary>
    internal const string UndoLabel = "Import form data";

    /// <summary>Gets the start of every FDF file (ISO 32000-2 §12.7.8.2.1); anything else is read as XFDF.</summary>
    private static ReadOnlySpan<byte> FdfHeader => "%FDF"u8;

    /// <inheritdoc/>
    public FormDataImportResult Import(ReadOnlyMemory<byte> data)
    {
        lock (owner.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(owner);
            return owner.IsDisposed ? FormDataImportResult.Failed : Apply(data.ToArray());
        }
    }

    /// <summary>Applies the file inside a transaction that commits on success and rolls back when the file is refused.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns>What changed.</returns>
    private FormDataImportResult Apply(byte[] bytes)
    {
        if (!HyperPdfEditTransactions.TryBegin(owner.Document, UndoLabel, PdfCacheScopes.None, out var transaction))
        {
            // Another edit holds the document's only transaction, so the file is not applied.
            return FormDataImportResult.Failed;
        }

        using var scope = transaction;
        PdfInterchangeImportResult result;
        try
        {
            result = bytes.AsSpan().StartsWith(FdfHeader)
                ? PdfInterchange.ImportFdf(owner.Document, bytes)
                : PdfInterchange.ImportXfdf(owner.Document, bytes);
        }
        catch (PdfException)
        {
            // Disposing the open transaction puts every object back.
            return FormDataImportResult.Failed;
        }

        transaction.Commit();
        var applied = result.AnnotationsAdded + result.AnnotationsReplaced;
        if (result.FieldsApplied > 0 || applied > 0)
        {
            HyperPdfEditing.Edited(owner, PdfCacheScopes.None);
        }

        return new(true, result.FieldsApplied, result.FieldsSkipped, applied, result.AnnotationsSkipped);
    }
}
