// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Turns the accessibility report's findings into PDF/UA-1 findings. The report already reads the catalog, the metadata,
/// the structure tree and each page's content; this table keeps the findings that ISO 14289-1:2014 states as "shall"
/// rules and drops advice such as recommended table headers. Font findings come from the separate font preflight.
/// </summary>
internal static class PdfUaReportChecks
{
    /// <summary>The requirement each report code maps to, indexed by code value; <see cref="PdfUaRequirement.None"/> leaves a code out.</summary>
    private static readonly PdfUaRequirement[] ByCode =
    [
        PdfUaRequirement.None,
        PdfUaRequirement.Marked,
        PdfUaRequirement.StructureTree,
        PdfUaRequirement.Language,
        PdfUaRequirement.Title,
        PdfUaRequirement.DisplayDocTitle,
        PdfUaRequirement.Suspects,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.UntaggedContent,
        PdfUaRequirement.StructureTypes,
        PdfUaRequirement.StructureTypes,
        PdfUaRequirement.Headings,
        PdfUaRequirement.None,
        PdfUaRequirement.FigureAlternative,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.Lists,
        PdfUaRequirement.LinkTagged,
        PdfUaRequirement.LinkDescription,
        PdfUaRequirement.FormFieldLabel,
        PdfUaRequirement.FormFieldTagged,
        PdfUaRequirement.AnnotationTagged,
        PdfUaRequirement.AnnotationStructureType,

        // The font preflight checks embedding, glyphs and Unicode mappings separately.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.StructureTypes,

        // These namespace, root, heading-type and revision findings apply only to PDF/UA-2.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,

        // A missing metadata stream already produces the required title finding.
        PdfUaRequirement.None,
        PdfUaRequirement.Language,
        PdfUaRequirement.FormulaAlternative,
        PdfUaRequirement.NoteIdentifier,
        PdfUaRequirement.OptionalContent,
        PdfUaRequirement.OptionalContent,
        PdfUaRequirement.EmbeddedFileFilenames,
        PdfUaRequirement.AssistiveTechnologyAccess,
        PdfUaRequirement.TrapNetAnnotations,
        PdfUaRequirement.DynamicXfa,
        PdfUaRequirement.LinkImageMap,
        PdfUaRequirement.ReferenceXObjects,
        PdfUaRequirement.MediaClipData,
    ];

    /// <summary>The requirement each report code maps to for a PDF/UA-2 claim, indexed by code value; codes of part 1 only are left out.</summary>
    private static readonly PdfUaRequirement[] ByCodePart2 =
    [
        PdfUaRequirement.None,
        PdfUaRequirement.Marked,
        PdfUaRequirement.StructureTree,
        PdfUaRequirement.Language,
        PdfUaRequirement.Title,
        PdfUaRequirement.DisplayDocTitle,
        PdfUaRequirement.Suspects,
        PdfUaRequirement.Part2Identification,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.UntaggedContent,
        PdfUaRequirement.StructureTypes,
        PdfUaRequirement.StructureTypes,

        // Part 2 states no sequential heading rule, so skipped levels and several top-level headings are advice.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.FigureAlternative,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.Lists,
        PdfUaRequirement.LinkTagged,
        PdfUaRequirement.LinkDescription,
        PdfUaRequirement.FormFieldLabel,
        PdfUaRequirement.FormFieldTagged,
        PdfUaRequirement.AnnotationTagged,
        PdfUaRequirement.AnnotationStructureType,

        // The font preflight checks embedding, glyphs and Unicode mappings separately.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.StructureTypes,
        PdfUaRequirement.StructureTypes,
        PdfUaRequirement.Part2DocumentRoot,
        PdfUaRequirement.Part2HeadingTypes,
        PdfUaRequirement.Part2NoteTypes,
        PdfUaRequirement.Part2Identification,
        PdfUaRequirement.None,
        PdfUaRequirement.Language,

        // Part 1 only: formulas, note IDs and embedded file names.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.OptionalContent,
        PdfUaRequirement.OptionalContent,
        PdfUaRequirement.None,

        // Part 1 only: the encryption bit, TrapNet, XFA, image maps, Reference XObjects and media clips.
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.None,
        PdfUaRequirement.Part2Containment,
        PdfUaRequirement.Part2Containment,
    ];

    /// <summary>Reads the accessibility report and adds the findings that are PDF/UA-1 rules.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken) =>
        Check(document, PdfUaClaim.Part1, findings, cancellationToken);

    /// <summary>Reads the accessibility report and adds the findings that are rules of a PDF/UA part.</summary>
    /// <param name="document">The document.</param>
    /// <param name="part">The PDF/UA part: 1 or 2.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, int part, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        var report = PdfDocumentAccessibility.GetAccessibilityReport(document, cancellationToken);
        var table = part == PdfUaClaim.Part2 ? ByCodePart2 : ByCode;
        var reportFindings = report.Findings;
        for (var index = 0; index < reportFindings.Count; index++)
        {
            var finding = reportFindings[index];
            var requirement = (uint)finding.Code < (uint)table.Length ? table[(int)finding.Code] : PdfUaRequirement.None;
            if (requirement != PdfUaRequirement.None)
            {
                findings.Add(new(requirement, finding.PageIndex));
            }
        }
    }
}
