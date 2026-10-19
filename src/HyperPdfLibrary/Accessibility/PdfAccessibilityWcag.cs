// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Relates accessibility report findings to the Web Content Accessibility Guidelines (WCAG 2.2) success criteria they most
/// closely touch. The mapping is a guide for readers who work to WCAG; it is not a conformance claim, because WCAG also judges
/// content (such as whether a description is good) that a file check cannot.
/// </summary>
public static class PdfAccessibilityWcag
{
    /// <summary>Non-text content (WCAG 2.2, 1.1.1).</summary>
    private const string NonText = "1.1.1";

    /// <summary>Info and relationships (1.3.1).</summary>
    private const string Relationships = "1.3.1";

    /// <summary>Meaningful sequence (1.3.2).</summary>
    private const string Sequence = "1.3.2";

    /// <summary>Link purpose (2.4.4).</summary>
    private const string LinkPurpose = "2.4.4";

    /// <summary>Headings and labels (2.4.6).</summary>
    private const string HeadingsLabels = "2.4.6";

    /// <summary>Page titled (2.4.2).</summary>
    private const string PageTitled = "2.4.2";

    /// <summary>Language of page (3.1.1).</summary>
    private const string PageLanguage = "3.1.1";

    /// <summary>Name, role, value (4.1.2).</summary>
    private const string NameRoleValue = "4.1.2";

    /// <summary>The rows, sorted by code: a code and the criteria it touches.</summary>
    private static readonly WcagRow[] Rows =
    [
        new(PdfAccessibilityCode.NotMarked, [Relationships]),
        new(PdfAccessibilityCode.NoStructureTree, [Relationships, Sequence]),
        new(PdfAccessibilityCode.NoLanguage, [PageLanguage]),
        new(PdfAccessibilityCode.NoTitle, [PageTitled]),
        new(PdfAccessibilityCode.DisplayDocTitleOff, [PageTitled]),
        new(PdfAccessibilityCode.TabsNotStructure, ["2.4.3"]),
        new(PdfAccessibilityCode.UntaggedContent, [Relationships, Sequence]),
        new(PdfAccessibilityCode.UnmappedStructureType, [Relationships]),
        new(PdfAccessibilityCode.RoleMapCycle, [Relationships]),
        new(PdfAccessibilityCode.HeadingLevelSkipped, [Relationships, HeadingsLabels]),
        new(PdfAccessibilityCode.MultipleLevelOneHeadings, [Relationships, HeadingsLabels]),
        new(PdfAccessibilityCode.FigureNoDescription, [NonText]),
        new(PdfAccessibilityCode.TableNoHeaderCells, [Relationships]),
        new(PdfAccessibilityCode.TableHeaderNoScope, [Relationships]),
        new(PdfAccessibilityCode.TableCellNoHeader, [Relationships]),
        new(PdfAccessibilityCode.ListItemOutsideList, [Relationships]),
        new(PdfAccessibilityCode.LinkNotTagged, [Relationships, LinkPurpose]),
        new(PdfAccessibilityCode.LinkNoDescription, [LinkPurpose]),
        new(PdfAccessibilityCode.FormFieldNoLabel, [Relationships, "3.3.2", NameRoleValue]),
        new(PdfAccessibilityCode.FormFieldNotTagged, [Relationships, NameRoleValue]),
        new(PdfAccessibilityCode.AnnotationNotTagged, [Relationships]),
        new(PdfAccessibilityCode.AnnotationWrongStructureType, [Relationships]),
        new(PdfAccessibilityCode.LanguageInvalid, [PageLanguage]),
        new(PdfAccessibilityCode.FormulaNoAlternative, [NonText]),
        new(PdfAccessibilityCode.MediaClipNoAlternative, ["1.2.1"]),
        new(PdfAccessibilityCode.StructureContainmentViolation, [Relationships]),
        new(PdfAccessibilityCode.StructureOccurrenceViolation, [Relationships]),
    ];

    /// <summary>Gets the WCAG 2.2 success criteria that a finding most closely touches.</summary>
    /// <param name="code">The finding's code.</param>
    /// <returns>The criteria numbers, such as <c>1.1.1</c>; empty when no criterion maps to the code, for example a font encoding problem that WCAG does not name.</returns>
    public static ImmutableArray<string> GetSuccessCriteria(PdfAccessibilityCode code)
    {
        var low = 0;
        var high = Rows.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var order = (int)Rows[middle].Code - (int)code;
            if (order == 0)
            {
                return Rows[middle].Criteria;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return [];
    }

    /// <summary>One code and the criteria it touches.</summary>
    /// <param name="Code">The code.</param>
    /// <param name="Criteria">The criteria numbers.</param>
    private readonly record struct WcagRow(PdfAccessibilityCode Code, ImmutableArray<string> Criteria);
}
