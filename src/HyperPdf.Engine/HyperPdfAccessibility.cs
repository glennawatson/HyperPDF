// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Implements Accessibility over the document's owned state.</summary>
internal static class HyperPdfAccessibility
{
    /// <summary>Reads the document's PDF/UA claim and common accessibility faults. This is a reading report, not a validator.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="cancellation">A token that stops the work between elements and pages.</param>
    /// <returns>The report.</returns>
    /// <exception cref="System.ObjectDisposedException">The document is disposed.</exception>
    /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
    internal static PdfAccessibilityReport GetAccessibilityReport(HyperPdfDocument self, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        return PdfDocumentAccessibility.GetAccessibilityReport(self.Document, cancellation);
    }
}
