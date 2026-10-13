// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentFormFilling over the document's owned state.</summary>
internal static class HyperPdfDocumentFormFilling
{
    /// <summary>Gets HasForm.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static bool GetHasForm(HyperPdfDocument self) => !self.IsDisposed
        && PdfFormReading.HasForm(PdfDocumentForms.GetForm(self.Document));

    /// <summary>Appends the form fields on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the fields.</param>
    internal static void GetFields(HyperPdfDocument self, int pageIndex, List<FormField> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return;
        }

        var page = PdfDocumentPages.GetPage(self.Document, pageIndex);
        List<PdfFormWidget> widgets = [];
        PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(self.Document), pageIndex, widgets);
        foreach (var widget in widgets)
        {
            output.Add(HyperPdfForms.ToField(widget, page));
        }
    }

    /// <summary>Replaces the text of a text field.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="text">The new text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetText(HyperPdfDocument self, int pageIndex, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return !self.IsDisposed && HyperPdfEditing.FieldChanged(
                self,
                PdfFormEditing.SetText(PdfDocumentForms.GetForm(self.Document), pageIndex, index, text));
        }
    }

    /// <summary>Turns a check box or radio button on or off.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="isChecked">The new state.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetChecked(HyperPdfDocument self, int pageIndex, int index, bool isChecked)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return !self.IsDisposed && HyperPdfEditing.FieldChanged(
                self,
                PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(self.Document), pageIndex, index, isChecked));
        }
    }

    /// <summary>Selects a choice of a combo or list box.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="option">The option index.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SelectOption(HyperPdfDocument self, int pageIndex, int index, int option)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return !self.IsDisposed && HyperPdfEditing.FieldChanged(
                self,
                PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(self.Document), pageIndex, index, option));
        }
    }
}
