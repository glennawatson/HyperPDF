// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentFormOrder over the document's owned state.</summary>
internal static class HyperPdfDocumentFormOrder
{
    /// <summary>Appends the names of the calculated fields in the order the form recalculates them.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="output">The list receiving the names.</param>
    internal static void GetCalculationOrder(HyperPdfDocument self, List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (!self.IsDisposed)
        {
            PdfFormOrder.GetCalculationOrder(PdfDocumentForms.GetForm(self.Document), output);
        }
    }

    /// <summary>Appends the widget indexes of a page in the order the Tab key visits them, following the page's <c>/Tabs</c> entry.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the widget indexes.</param>
    internal static void GetTabOrder(HyperPdfDocument self, int pageIndex, List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (!self.IsDisposed)
        {
            PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(self.Document), pageIndex, output);
        }
    }
}
