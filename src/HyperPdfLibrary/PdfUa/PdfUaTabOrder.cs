// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Checks and repairs the tab order of pages with annotations: every page that has an annotation must hold
/// <c>/Tabs /S</c>, so the Tab key follows the structure tree (ISO 14289-1:2014, 7.18.3; ISO 32000-1:2008, 7.7.3.3).
/// </summary>
internal static class PdfUaTabOrder
{
    /// <summary>Adds a finding for each page that has annotations and does not use the structure order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NeedsStructureOrder(PdfDocumentPages.GetPage(document, i)))
            {
                findings.Add(new(PdfUaRequirement.TabOrder, i));
            }
        }
    }

    /// <summary>Sets <c>/Tabs /S</c> on every page that needs it.</summary>
    /// <param name="document">The document, read before any repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, i);

            // A page written directly in its parent has no object of its own to replace.
            if (!NeedsStructureOrder(page) || !page.Id.IsValid)
            {
                continue;
            }

            var copy = page.Dictionary.Clone();
            copy.Set(KnownName.Tabs, PdfValue.FromName(KnownName.S));
            transaction.Replace(page.Id, PdfValue.FromDictionary(copy));
        }
    }

    /// <summary>Determines whether a page has annotations and does not follow the structure order.</summary>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when its <c>/Tabs</c> must be set.</returns>
    private static bool NeedsStructureOrder(PdfPage page) =>
        page.Dictionary.GetArray(KnownName.Annots) is { Count: > 0 } && !page.Dictionary.IsName(KnownName.Tabs, KnownName.S);
}
