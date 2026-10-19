// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Builds a document's <see cref="PdfAccessibilityReport"/>: document entries first, then the structure tree, then each page, then the fonts.</summary>
internal static class PdfAccessibilityReader
{
    /// <summary>The <c>pdfuaid:rev</c> of ISO 14289-2:2024 (ISO 14289-2, clause 5).</summary>
    private const string Part2Revision = "2024";

    /// <summary>Reads a document's accessibility.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">The cancellation token, checked on every element and page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfAccessibilityReport Read(PdfDocument document, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        cancellation.ThrowIfCancellationRequested();

        // An open transaction can replace objects again before it commits.
        return StoreTransactions.GetCurrentTransaction(document.Objects) is not null
            ? Build(document, cancellation)
            : PdfReadSnapshots.GetCached<PdfAccessibilityReport>(document, PdfReadKind.AccessibilityReport)
                ?? PdfReadSnapshots.Read(document, PdfReadKind.AccessibilityReport, Build, cancellation);
    }

    /// <summary>Builds a report for the current document objects.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">Cancels the report's walks.</param>
    /// <returns>The independently owned report.</returns>
    private static PdfAccessibilityReport Build(PdfDocument document, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var pools = document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        try
        {
            return Build(document, findings, cancellation);
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }

    /// <summary>Builds a detached report using the operation's leased finding storage.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">The operation's working storage.</param>
    /// <param name="cancellation">Cancels the report's walks.</param>
    /// <returns>The independently owned report.</returns>
    private static PdfAccessibilityReport Build(PdfDocument document, AccessibilityFindings findings, CancellationToken cancellation)
    {
        var xmp = PdfDocumentMetadata.ReadOwnerXmp(document, document.Catalog, cancellation);
        var metadata = AccessibilityMetadataReader.Read(xmp);
        AccessibilityIso32005Claim.Run(xmp, findings);
        var tree = PdfDocumentTagged.GetStructureTree(document);
        var markInfo = document.Catalog.GetDictionary(KnownName.MarkInfo);
        var part = metadata.Part;
        var scan = tree is null ? default : new AccessibilityStructureState(
            tree,
            tree.Root.GetDictionary(KnownName.RoleMap),
            part,
            findings,
            document.Objects.ScratchPools,
            null,
            null,
            null,
            null,
            null,
            string.Empty,
            false,
            cancellation);
        try
        {
            var isMarked = markInfo?.Flag("Marked", false) == true;
            var catalogLanguage = document.Catalog.GetText(KnownName.Lang);
            var language = catalogLanguage is { Length: > 0 } text ? text : null;
            var displayTitle = PdfDocumentViewerPreferences.GetViewerPreferences(document).DisplayDocTitle;
            var suspects = markInfo?.Flag("Suspects", false) == true;
            var version = PdfDocumentVersioning.GetVersion(document);
            var state = new DocumentState(isMarked, tree is not null, catalogLanguage, metadata.HasMetadata, metadata.HasTitle, displayTitle, suspects);
            CheckDocument(document.Catalog, findings, state, part, version, metadata.Revision, cancellation);
            CheckProtectionAndForms(document, tree, part, findings, cancellation);
            if (tree is not null)
            {
                AccessibilityStructureScan.Run(ref scan);
            }

            var pages = CheckPages(document, tree, scan, part, findings, cancellation);
            AccessibilityFontScan.Run(document, findings, cancellation);
            var claim = new PdfUaClaim(part, metadata.Revision, version.ToString(), UsesPdf20Namespaces(tree, scan.UsesPdf20Namespace), metadata.Amendment, metadata.Corrigendum);
            return new(
                claim,
                isMarked,
                tree is not null,
                language,
                metadata.HasTitle,
                displayTitle,
                suspects,
                pages,
                findings.Findings.ToArray(),
                AccessibilityFindingCollection.GetCounts(findings));
        }
        finally
        {
            if (tree is not null)
            {
                AccessibilityStructureScan.Release(scan);
            }
        }
    }

    /// <summary>Determines whether the tree lists namespaces or uses the PDF 2.0 one.</summary>
    /// <param name="tree">The tree, or <see langword="null"/>.</param>
    /// <param name="usesPdf20Namespace">Whether the structure walk found the PDF 2.0 namespace.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool UsesPdf20Namespaces(PdfStructureTree? tree, bool usesPdf20Namespace) =>
        usesPdf20Namespace || tree?.Root.Array("Namespaces") is { Count: > 0 };

    /// <summary>Reports what the catalog, the metadata and the claim say.</summary>
    /// <param name="catalog">The document catalog.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="state">What the document entries say.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="version">The file's version.</param>
    /// <param name="revision">The claimed <c>pdfuaid:rev</c>, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels catalog traversal.</param>
    private static void CheckDocument(
        PdfDictionary catalog,
        AccessibilityFindings findings,
        in DocumentState state,
        int? part,
        PdfVersion version,
        string? revision,
        CancellationToken cancellationToken)
    {
        AddIf(findings, part == PdfUaClaim.Part2 && !string.Equals(revision, Part2Revision, StringComparison.Ordinal), PdfAccessibilityCode.ClaimRevisionInvalid);
        AddIf(findings, !state.IsMarked, PdfAccessibilityCode.NotMarked);
        AddIf(findings, !state.HasStructureTree, PdfAccessibilityCode.NoStructureTree);
        AddIf(findings, state.Language is null, PdfAccessibilityCode.NoLanguage);
        AddIf(findings, state.Language is not null && !AccessibilityLanguageTags.IsValid(state.Language), PdfAccessibilityCode.LanguageInvalid);
        AddIf(findings, !state.HasMetadata, PdfAccessibilityCode.NoMetadata);
        AddIf(findings, state.HasMetadata && !state.HasTitle, PdfAccessibilityCode.NoTitle);
        AddIf(findings, !state.DisplayDocTitle, PdfAccessibilityCode.DisplayDocTitleOff);
        AddIf(findings, state.HasSuspects, PdfAccessibilityCode.SuspectsSet);
        AddIf(findings, part is not null and not PdfUaClaim.Part1 and not PdfUaClaim.Part2, PdfAccessibilityCode.ClaimPartUnknown);
        AddIf(findings, part == PdfUaClaim.Part2 && version < PdfVersion.Pdf20, PdfAccessibilityCode.ClaimPart2NotPdf2);
        AccessibilityOptionalContentScan.Run(catalog, part, findings, cancellationToken);
        AccessibilityEmbeddedFileScan.Run(catalog, part, findings, cancellationToken);
    }

    /// <summary>Reports the encryption permission and XFA rules, which read the document rather than its catalog alone.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tree">The document's structure tree, when present.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the XFA read.</param>
    private static void CheckProtectionAndForms(PdfDocument document, PdfStructureTree? tree, int? part, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        AccessibilitySecurityScan.Run(document, part, findings);
        AccessibilityXfaScan.Run(document, part, findings, cancellationToken);
        AccessibilityContainmentScan.Run(tree, findings, cancellationToken);
    }

    /// <summary>Adds a document-level finding when a condition holds.</summary>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="condition">Whether to add it.</param>
    /// <param name="code">The code.</param>
    private static void AddIf(AccessibilityFindings findings, bool condition, PdfAccessibilityCode code)
    {
        if (condition)
        {
            AccessibilityFindingCollection.Add(findings, code);
        }
    }

    /// <summary>Checks every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tree">The document's structure tree, when present.</param>
    /// <param name="scan">The structure walk's collected page facts.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>What was counted on each page.</returns>
    private static PdfAccessibilityPage[] CheckPages(
        PdfDocument document,
        PdfStructureTree? tree,
        in AccessibilityStructureState scan,
        int? part,
        AccessibilityFindings findings,
        CancellationToken cancellation)
    {
        if (document.PageCount == 0)
        {
            return [];
        }

        var state = new AccessibilityPageState(document, tree, scan.Owners, scan.TablePages, part, findings, null, cancellation);
        try
        {
            var pages = new PdfAccessibilityPage[document.PageCount];
            for (var index = 0; index < pages.Length; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                pages[index] = AccessibilityPageScan.Check(ref state, index);
            }

            return pages;
        }
        finally
        {
            if (state.CheckedTables is { } checkedTables)
            {
                checkedTables.Clear();
                _ = document.Objects.ScratchPools.Slots<HashSet<PdfStructureElement>>().Offer(checkedTables);
            }
        }
    }

    /// <summary>What the document's own entries say.</summary>
    /// <param name="IsMarked">Whether <c>/MarkInfo /Marked</c> is true.</param>
    /// <param name="HasStructureTree">Whether there is a structure tree.</param>
    /// <param name="Language">The catalog's <c>/Lang</c> text, which may be empty, or <see langword="null"/> when the entry is missing.</param>
    /// <param name="HasMetadata">Whether the catalog has an XMP metadata stream.</param>
    /// <param name="HasTitle">Whether the metadata has a title.</param>
    /// <param name="DisplayDocTitle">Whether the viewer preferences show the title.</param>
    /// <param name="HasSuspects">Whether <c>/Suspects</c> is true.</param>
    private readonly record struct DocumentState(bool IsMarked, bool HasStructureTree, string? Language, bool HasMetadata, bool HasTitle, bool DisplayDocTitle, bool HasSuspects);
}
