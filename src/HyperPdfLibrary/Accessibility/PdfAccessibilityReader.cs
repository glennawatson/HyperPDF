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
        var findings = new AccessibilityFindings();
        var metadata = AccessibilityMetadata.Get(document);
        var tree = PdfDocumentTagged.GetStructureTree(document);
        var markInfo = document.Catalog.GetDictionary(KnownName.MarkInfo);
        var part = metadata.Part;
        var scan = tree is null ? null : new AccessibilityStructureScan(tree, part, findings, cancellation);

        var isMarked = markInfo?.Flag("Marked", false) == true;
        var catalogLanguage = document.Catalog.GetText(KnownName.Lang);
        var language = catalogLanguage is { Length: > 0 } text ? text : null;
        var displayTitle = PdfDocumentViewerPreferences.GetViewerPreferences(document).DisplayDocTitle;
        var suspects = markInfo?.Flag("Suspects", false) == true;
        var version = PdfDocumentVersioning.GetVersion(document);

        var revision = metadata.Revision;
        var hasTitle = metadata.HasTitle;
        var state = new DocumentState(isMarked, tree is not null, catalogLanguage, metadata.HasMetadata, hasTitle, displayTitle, suspects);
        CheckDocument(findings, state, part, version, revision);
        scan?.Run();
        var pages = CheckPages(document, scan, part, findings, cancellation);
        AccessibilityFontScan.Run(document, findings, cancellation);
        var claim = new PdfUaClaim(part, revision, version.ToString(), UsesPdf20Namespaces(tree, scan));
        return new(claim, isMarked, tree is not null, language, hasTitle, displayTitle, suspects, pages, findings.Findings, findings.GetCounts());
    }

    /// <summary>Determines whether the tree lists namespaces or uses the PDF 2.0 one.</summary>
    /// <param name="tree">The tree, or <see langword="null"/>.</param>
    /// <param name="scan">The structure walk, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool UsesPdf20Namespaces(PdfStructureTree? tree, AccessibilityStructureScan? scan) =>
        scan?.UsesPdf20Namespace == true || tree?.Root.Array("Namespaces") is { Count: > 0 };

    /// <summary>Reports what the catalog, the metadata and the claim say.</summary>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="state">What the document entries say.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="version">The file's version.</param>
    /// <param name="revision">The claimed <c>pdfuaid:rev</c>, or <see langword="null"/>.</param>
    private static void CheckDocument(AccessibilityFindings findings, in DocumentState state, int? part, PdfVersion version, string? revision)
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
    }

    /// <summary>Adds a document-level finding when a condition holds.</summary>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="condition">Whether to add it.</param>
    /// <param name="code">The code.</param>
    private static void AddIf(AccessibilityFindings findings, bool condition, PdfAccessibilityCode code)
    {
        if (condition)
        {
            findings.Add(code);
        }
    }

    /// <summary>Checks every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="scan">The structure walk, or <see langword="null"/> for an untagged document.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>What was counted on each page.</returns>
    private static PdfAccessibilityPage[] CheckPages(PdfDocument document, AccessibilityStructureScan? scan, int? part, AccessibilityFindings findings, CancellationToken cancellation)
    {
        var pageScan = new AccessibilityPageScan(document, scan, part, findings);
        var pages = new PdfAccessibilityPage[document.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            pages[i] = pageScan.Check(i);
        }

        return pages;
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
