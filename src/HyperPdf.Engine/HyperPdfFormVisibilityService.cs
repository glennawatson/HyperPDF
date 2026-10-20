// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Provides IFormVisibility through the owning document, with the library's hide action.</summary>
/// <param name="owner">The owning document.</param>
internal sealed class HyperPdfFormVisibilityService(HyperPdfDocument owner) : IFormVisibility
{
    /// <inheritdoc/>
    public IDocument Document => owner;

    /// <inheritdoc/>
    public bool SetHidden(string fieldName, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        lock (owner.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(owner);
            return !owner.IsDisposed && HyperPdfEditing.FieldChanged(
                owner,
                PdfFormActions.SetHidden(PdfDocumentForms.GetForm(owner.Document), new([fieldName], hidden)) > 0);
        }
    }
}
