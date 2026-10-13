// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Scripting;
using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Implements ContentInspection over the document's owned state.</summary>
internal static class HyperPdfContentInspection
{
    /// <summary>The scripts of the page this thread is checking.</summary>
    [ThreadStatic]
    private static List<string>? _scripts;

    /// <summary>Determines whether a page has a field script that is not one of the formats and sums that are run.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns><see langword="true"/> when a script would be needed that is not run.</returns>
    internal static bool HasUnknownScripts(HyperPdfDocument self, int pageIndex)
    {
        // Each thread keeps one list for the scripts of the page being checked, emptied after use.
        var scripts = _scripts ??= [];
        try
        {
            PdfDocumentContent.GetWidgetScripts(self.Document, pageIndex, scripts);
            foreach (var script in scripts)
            {
                if (ReferenceEquals(FormScript.Parse(script), FormScript.None))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            scripts.Clear();
        }
    }
}
