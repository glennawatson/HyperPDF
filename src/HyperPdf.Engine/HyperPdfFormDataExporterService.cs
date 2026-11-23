// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdfLibrary.Interchange;

namespace HyperPdf.Engine;

/// <summary>Writes the form's field values as FDF or XFDF through the library.</summary>
/// <param name="owner">The owning document.</param>
internal sealed class HyperPdfFormDataExporterService(HyperPdfDocument owner) : IFormDataExporter
{
    /// <inheritdoc/>
    public IDocument Document => owner;

    /// <inheritdoc/>
    public byte[] Export(FormDataFormat format)
    {
        // The edit gate keeps an edit from changing the fields while they are read.
        lock (owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
            return format == FormDataFormat.Xfdf
                ? PdfInterchange.ExportXfdf(owner.Document, PdfInterchangeContent.Fields)
                : PdfInterchange.ExportFdf(owner.Document, PdfInterchangeContent.Fields);
        }
    }
}
