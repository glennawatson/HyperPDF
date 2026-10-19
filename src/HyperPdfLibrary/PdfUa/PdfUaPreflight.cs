// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Checks a document that claims PDF/UA-1 against the file format rules of ISO 14289-1:2014, clause 7, that can break
/// when a document is edited: the catalog and metadata, the tags and the content they describe, the tab order, the
/// annotations and the fonts. It reuses the accessibility report for the tags and the PDF/A validator's font rules for
/// the fonts. It is a practical check, not a certification.
/// </summary>
internal static class PdfUaPreflight
{
    /// <summary>Opens a view over a store's objects, which reads fonts as an opened document does.</summary>
    /// <param name="store">The objects.</param>
    /// <returns>The view. It is not disposed, as disposing it would close the objects.</returns>
    internal static PdfDocument OpenView(PdfObjectStore store) => new(store, PdfFontLoader.Load);

    /// <summary>Finds what is wrong with the document's PDF/UA-1 claim.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="edited">Whether edits changed the document, so the tags are also checked against the content they point at.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The findings; empty when every checked rule holds.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static List<PdfUaFinding> Check(PdfObjectStore store, bool edited, CancellationToken cancellationToken) =>
        Check(store, edited, PdfUaClaim.Part1, cancellationToken);

    /// <summary>Finds what is wrong with the document's PDF/UA claim.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="edited">Whether edits changed the document, so the tags are also checked against the content they point at.</param>
    /// <param name="part">The claimed PDF/UA part: 1 or 2.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The findings; empty when every checked rule holds.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static List<PdfUaFinding> Check(PdfObjectStore store, bool edited, int part, CancellationToken cancellationToken)
    {
        var document = OpenView(store);

        // The report, the structure references and the font rules read the same page content; the scope decodes it once for this
        // check and is disposed before the caller repairs anything, so no edit is ever read through it.
        using var contents = PdfPageContentScope.Begin(document);
        var findings = new List<PdfUaFinding>();
        PdfUaReportChecks.Check(document, part, findings, cancellationToken);
        PdfUaTabOrder.Check(document, findings, cancellationToken);
        PdfUaFontChecks.Check(document, findings, cancellationToken);
        if (edited)
        {
            PdfUaStructureReferences.Check(document, findings, cancellationToken);
        }

        return findings;
    }
}
