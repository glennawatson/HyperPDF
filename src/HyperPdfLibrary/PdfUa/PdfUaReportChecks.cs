// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    ];

    /// <summary>Reads the accessibility report and adds the findings that are PDF/UA-1 rules.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        var report = PdfDocumentAccessibility.GetAccessibilityReport(document, cancellationToken);
        var reportFindings = report.Findings;
        for (var index = 0; index < reportFindings.Count; index++)
        {
            var finding = reportFindings[index];
            var requirement = RequirementOf(finding.Code);
            if (requirement != PdfUaRequirement.None)
            {
                findings.Add(new(requirement, finding.PageIndex));
            }
        }
    }

    /// <summary>Gets the requirement a report code stands for.</summary>
    /// <param name="code">The report code.</param>
    /// <returns>The requirement, or <see cref="PdfUaRequirement.None"/> when the code is not a PDF/UA-1 rule.</returns>
    private static PdfUaRequirement RequirementOf(PdfAccessibilityCode code) => (uint)code < (uint)ByCode.Length ? ByCode[(int)code] : PdfUaRequirement.None;
}
