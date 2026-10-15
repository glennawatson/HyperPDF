// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Rendering;
namespace HyperPdf.Engine;

/// <summary>Implements FormRuntime over the document's owned state.</summary>
internal static class HyperPdfFormRuntime
{
    /// <summary>The shift of the alpha component in the packed highlight value.</summary>
    internal const int AlphaShift = 32;

    /// <summary>Unpacks a tint packed by <see cref="Pack"/>.</summary>
    /// <param name="packed">The packed tint.</param>
    /// <returns>The tint.</returns>
    internal static FormHighlight Unpack(long packed) => new((uint)packed, (byte)(packed >> AlphaShift));

    /// <summary>Packs a tint into one number.</summary>
    /// <param name="highlight">The tint.</param>
    /// <returns>The packed tint.</returns>
    internal static long Pack(FormHighlight highlight) => ((long)highlight.Alpha << AlphaShift) | highlight.Color;

    /// <summary>Gets the renderer options, which carry the tint.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The options.</returns>
    internal static PdfRenderOptions CreateRenderOptions(HyperPdfDocument self)
    {
        var tint = HyperPdfDocumentFormHighlight.GetHighlight(self);
        return PdfRenderOptions.Default with
        {
            FormHighlight = new(tint.Color, tint.Alpha),
            Quality = HyperPdfDocumentRenderSharpness.ToQuality(HyperPdfDocumentRenderSharpness.GetSharpness(self)),
        };
    }

    /// <summary>Creates a runner that hands what the library cannot do to the application's host.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="host">The application's host.</param>
    /// <returns>The runner.</returns>
    internal static PdfActionRunner CreateRunner(HyperPdfDocument self, IFormActionHost host) => new(self.Document, new FormActionHostAdapter(self, host));

    /// <summary>Records what actions changed, so pages draw again and the document is marked unsaved.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="result">What the runner reported.</param>
    /// <returns>The same result, for the viewer.</returns>
    internal static FormActionResult Complete(HyperPdfDocument self, PdfActionResult result)
    {
        if ((result & PdfActionResult.Changed) != 0)
        {
            lock (self.EditGate)
            {
                using var access = HyperPdfNavigation.EnterPageWrite(self);
                if (!self.IsDisposed)
                {
                    // Actions reset, hide or fill fields and run imports of form data: no page content or structure changes.
                    HyperPdfEditing.Edited(self, PdfCacheScopes.None);
                }
            }
        }

        return (FormActionResult)(int)result;
    }
}
