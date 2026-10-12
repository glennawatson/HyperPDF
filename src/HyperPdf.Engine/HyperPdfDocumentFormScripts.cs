// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Scripting;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentFormScripts over the document's owned state.</summary>
internal static class HyperPdfDocumentFormScripts
{
    /// <summary>Appends the scripts of the fields on a page that have any HyperPDF runs.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the scripts.</param>
    internal static void GetScripts(HyperPdfDocument self, int pageIndex, List<FieldScripts> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return;
        }

        List<PdfWidgetScripts> widgets = [];
        PdfFormReading.GetScripts(PdfDocumentForms.GetForm(self.Document), pageIndex, widgets);
        foreach (var widget in widgets)
        {
            FieldScripts scripts = new(
                widget.PageIndex,
                widget.Index,
                widget.Name,
                FormScript.Parse(widget.Keystroke),
                FormScript.Parse(widget.Format),
                FormScript.Parse(widget.Validate),
                FormScript.Parse(widget.Calculate));
            if (scripts.HasAny)
            {
                output.Add(scripts);
            }
        }
    }
}
