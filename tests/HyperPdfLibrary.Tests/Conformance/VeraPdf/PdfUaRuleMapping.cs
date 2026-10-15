// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Ties accessibility report codes to the veraPDF PDF/UA rules they report. The report agrees with veraPDF on a mapping when,
/// on every one of veraPDF's PDF/UA test files, one of the codes is found exactly when one of the rules fails. A mapping with a
/// recorded difference is weaker: the report must find a code whenever a rule fails, and may find one when veraPDF passes, for the
/// stated reason.
/// </summary>
/// <param name="Part">The part of ISO 14289 the rules belong to.</param>
/// <param name="Codes">The report codes; the mapping holds when any of them is found.</param>
/// <param name="Rules">The veraPDF rules as <c>clause:test</c>; the mapping holds when any of them fails.</param>
/// <param name="Difference">Why the report may find a code when veraPDF passes, or <see langword="null"/> when they must agree exactly.</param>
[DebuggerDisplay("PdfUaRuleMapping: part {Part}, {Rules.Length} rules")]
internal sealed record PdfUaRuleMapping(int Part, PdfAccessibilityCode[] Codes, string[] Rules, string? Difference)
{
    /// <summary>Part 1 of ISO 14289.</summary>
    private const int PartOne = 1;

    /// <summary>Part 2 of ISO 14289.</summary>
    private const int PartTwo = 2;

    /// <summary>Initializes a new instance of the <see cref="PdfUaRuleMapping"/> record that the report must match exactly.</summary>
    /// <param name="part">The part of ISO 14289.</param>
    /// <param name="codes">The report codes.</param>
    /// <param name="rules">The veraPDF rules.</param>
    internal PdfUaRuleMapping(int part, PdfAccessibilityCode[] codes, string[] rules)
        : this(part, codes, rules, null)
    {
    }

    /// <summary>Gets the mappings the report is held to.</summary>
    internal static PdfUaRuleMapping[] All { get; } =
    [
        new(PartOne, [PdfAccessibilityCode.NotMarked], ["6.2:1"]),
        new(PartOne, [PdfAccessibilityCode.NoStructureTree], ["7.1:11"]),
        new(PartOne, [PdfAccessibilityCode.NoMetadata], ["7.1:8"]),
        new(PartOne, [PdfAccessibilityCode.NoTitle], ["7.1:9"]),
        new(PartOne, [PdfAccessibilityCode.DisplayDocTitleOff], ["7.1:10"]),
        new(PartOne, [PdfAccessibilityCode.SuspectsSet], ["7.1:4"]),
        new(PartOne, [PdfAccessibilityCode.UntaggedContent], ["7.1:3"]),
        new(PartOne, [PdfAccessibilityCode.UnmappedStructureType], ["7.1:5"]),
        new(PartOne, [PdfAccessibilityCode.RoleMapCycle], ["7.1:6"]),
        new(PartOne, [PdfAccessibilityCode.StandardTypeRemapped], ["7.1:7"]),

        // Clause 7.2 to 7.5: language, lists, figures, headings and tables.
        new(
            PartOne,
            [PdfAccessibilityCode.NoLanguage],
            ["7.2:34"],
            "veraPDF passes a file with no catalog /Lang when every text run has its own language; the report asks for the document language, which PDF/UA-2 requires."),
        new(PartOne, [PdfAccessibilityCode.LanguageInvalid], ["7.2:29"]),
        new(PartOne, [PdfAccessibilityCode.ListItemOutsideList], ["7.2:17"]),
        new(PartOne, [PdfAccessibilityCode.FigureNoDescription], ["7.3:1"]),
        new(PartOne, [PdfAccessibilityCode.HeadingLevelSkipped], ["7.4.2:1"]),

        // Clause 7.18: annotations, tab order, forms and links.
        new(PartOne, [PdfAccessibilityCode.TabsNotStructure], ["7.18.3:1"]),
        new(PartOne, [PdfAccessibilityCode.LinkNotTagged], ["7.18.5:1"]),
        new(
            PartOne,
            [PdfAccessibilityCode.FormFieldNoLabel],
            ["7.18.1:3"],
            "veraPDF passes a field that is its own widget (no /Kids) with no /TU and no /Alt on its structure element, as 7.18.1-t03-pass-d.pdf shows, "
            + "and fails the same gap on a field with separate widget kids; the report asks for a label in both cases (ISO 14289-1 7.18.1)."),
        new(PartOne, [PdfAccessibilityCode.FormFieldNotTagged], ["7.18.4:1"]),
        new(PartOne, [PdfAccessibilityCode.AnnotationNotTagged, PdfAccessibilityCode.AnnotationWrongStructureType], ["7.18.1:1"]),

        // Clause 7.21: fonts.
        new(PartOne, [PdfAccessibilityCode.FontNotEmbedded], ["7.21.4.1:1"]),
        new(PartOne, [PdfAccessibilityCode.GlyphMissingFromFont], ["7.21.4.1:2"]),
        new(PartOne, [PdfAccessibilityCode.FontNoUnicodeMap], ["7.21.7:1"]),
        new(PartOne, [PdfAccessibilityCode.FontUnicodeMapInvalid], ["7.21.7:2"]),
        new(PartOne, [PdfAccessibilityCode.NotdefGlyphShown], ["7.21.8:1"]),

        // ISO 14289-2:2024 clause 5, 6 and 8.2: claim, marking, structure root and namespaces.
        new(PartTwo, [PdfAccessibilityCode.ClaimRevisionInvalid], ["5:5"]),
        new(PartTwo, [PdfAccessibilityCode.ClaimPart2NotPdf2], ["6.1:1"]),
        new(PartTwo, [PdfAccessibilityCode.NotMarked], ["6.2:1"]),
        new(PartTwo, [PdfAccessibilityCode.NoStructureTree], ["8.2.1:1"]),
        new(PartTwo, [PdfAccessibilityCode.UntaggedContent], ["8.2.2:1"]),
        new(PartTwo, [PdfAccessibilityCode.UnmappedStructureType, PdfAccessibilityCode.StandardTypeRemapped], ["8.2.4:1", "8.2.4:4"]),
        new(PartTwo, [PdfAccessibilityCode.RoleMapCycle], ["8.2.4:2"]),
        new(PartTwo, [PdfAccessibilityCode.RoleMapSameNamespace], ["8.2.4:3"]),
        new(PartTwo, [PdfAccessibilityCode.DocumentRootInvalid], ["8.2.5.2:1", "8.2.5.2:2"]),
        new(PartTwo, [PdfAccessibilityCode.HeadingTypeNotAllowed], ["8.2.5.12:1"]),
        new(PartTwo, [PdfAccessibilityCode.NoteTypeNotAllowed], ["8.2.5.14:1"]),
        new(PartTwo, [PdfAccessibilityCode.LinkNotTagged], ["8.2.5.20:1"]),
        new(PartTwo, [PdfAccessibilityCode.FigureNoDescription], ["8.2.5.28.2:1"]),

        // Clause 8.4 to 8.11: language, fonts, annotations, forms and metadata.
        new(PartTwo, [PdfAccessibilityCode.NoLanguage], ["8.4.4:1"]),
        new(PartTwo, [PdfAccessibilityCode.LanguageInvalid], ["8.4.4:2"]),
        new(PartTwo, [PdfAccessibilityCode.FontNotEmbedded], ["8.4.5.5.1:1"]),
        new(PartTwo, [PdfAccessibilityCode.GlyphMissingFromFont], ["8.4.5.5.1:2"]),
        new(PartTwo, [PdfAccessibilityCode.FontNoUnicodeMap], ["8.4.5.8:1"]),
        new(PartTwo, [PdfAccessibilityCode.FontUnicodeMapInvalid], ["8.4.5.8:2"]),
        new(PartTwo, [PdfAccessibilityCode.NotdefGlyphShown], ["8.4.5.9:1"]),
        new(PartTwo, [PdfAccessibilityCode.TabsNotStructure], ["8.9.3.3:1"]),
        new(PartTwo, [PdfAccessibilityCode.FormFieldNotTagged], ["8.10.1:1"]),
        new(PartTwo, [PdfAccessibilityCode.NoMetadata], ["8.11.1:2"]),
        new(PartTwo, [PdfAccessibilityCode.NoTitle], ["8.11.1:1"]),
        new(PartTwo, [PdfAccessibilityCode.DisplayDocTitleOff], ["8.11.2:1"]),
    ];

    /// <summary>Gets a label for assertion messages.</summary>
    internal string Label => $"part {Part} {string.Join('+', Codes)} = {string.Join('+', Rules)}";
}
