// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;

namespace HyperPdf.Engine;

/// <summary>Implements ISeparationSource and page edit reports over the document's owned state.</summary>
internal static class HyperPdfSeparations
{
    /// <summary>Gets the printed pages, reading the separation dictionaries first.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The printed pages.</returns>
    internal static async ValueTask<DocumentSeparations> GetSeparationsAsync(HyperPdfDocument self, CancellationToken cancellationToken)
    {
        if (self.IsDisposed)
        {
            return DocumentSeparations.Empty;
        }

        // Loads the dictionaries with async reads outside the page lease, which cannot be held across an await.
        _ = await PdfDocumentSeparations.GetSeparationsAsync(self.Document, cancellationToken).ConfigureAwait(false);
        return HyperPdfSeparations.GetSeparations(self);
    }

    /// <summary>Gets the printed pages.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The printed pages.</returns>
    internal static DocumentSeparations GetSeparations(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed ? DocumentSeparations.Empty : HyperPdfSeparations.Convert(PdfDocumentSeparations.GetSeparations(self.Document));
    }

    /// <summary>Copies what a library page edit reported into the app's report.</summary>
    /// <param name="source">The library report, or <see langword="null"/>.</param>
    /// <param name="target">The app's report, or <see langword="null"/>.</param>
    internal static void CopyReport(PdfPageEditReport? source, PageEditReport? target)
    {
        if (source is null || target is null)
        {
            return;
        }

        var changes = new SeparationChange[source.SeparationChanges.Count];
        for (var i = 0; i < changes.Length; i++)
        {
            var (group, groupPages, kept, unlinked) = source.SeparationChanges[i];
            changes[i] = new(group + 1, groupPages, kept, unlinked);
        }

        target.SeparationChanges = changes;
        target.TrapNetworkWarnings = TrapNetworkMapping.Warnings(source.TrapNetworks);
    }

    /// <summary>Converts the library's groups.</summary>
    /// <param name="info">The groups.</param>
    /// <returns>The app's groups.</returns>
    private static DocumentSeparations Convert(PdfSeparationInfo info)
    {
        if (info.Groups.Length == 0 && info.Findings.Length == 0)
        {
            return DocumentSeparations.Empty;
        }

        var groups = new SeparationGroup[info.Groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            var separations = info.Groups[i].Separations;
            var pages = new SeparationPage[separations.Length];
            for (var j = 0; j < pages.Length; j++)
            {
                pages[j] = new(separations[j].PageIndex, separations[j].Colorant);
            }

            groups[i] = new(info.Groups[i].Index + 1, pages);
        }

        return new(groups, info.Findings.Length);
    }
}
