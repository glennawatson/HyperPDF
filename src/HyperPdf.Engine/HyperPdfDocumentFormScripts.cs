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

        // Fields of a form repeat the same few scripts, so each text is recognised once for the page.
        Dictionary<string, FormScript> recognised = [with(StringComparer.Ordinal)];
        foreach (var widget in widgets)
        {
            FieldScripts scripts = new(
                widget.PageIndex,
                widget.Index,
                widget.Name,
                Recognise(recognised, widget.Keystroke),
                Recognise(recognised, widget.Format),
                Recognise(recognised, widget.Validate),
                Recognise(recognised, widget.Calculate));
            if (scripts.HasAny)
            {
                output.Add(scripts);
            }
        }
    }

    /// <summary>Recognises a script, or returns the script already recognised from the same text.</summary>
    /// <param name="recognised">The scripts recognised so far, by their text.</param>
    /// <param name="javaScript">The field action's JavaScript.</param>
    /// <returns>The script, which is never changed and so may be shared.</returns>
    private static FormScript Recognise(Dictionary<string, FormScript> recognised, string? javaScript)
    {
        if (string.IsNullOrWhiteSpace(javaScript))
        {
            return FormScript.None;
        }

        if (recognised.TryGetValue(javaScript, out var script))
        {
            return script;
        }

        script = FormScript.Parse(javaScript);
        recognised[javaScript] = script;
        return script;
    }
}
