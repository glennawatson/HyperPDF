// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// The veraPDF PDF/UA rules HyperPdf checks. A rule joins a link only when the check gives veraPDF's pass or fail on every one of
/// veraPDF's PDF/UA test files, which <c>PdfAccessibilityAgreementTests</c> compares in CI; a rule whose link states a difference
/// is held to the weaker test described on <see cref="StandardsRuleLink"/>. Every other rule is reported as not checked.
/// </summary>
internal static class StandardsRuleLinks
{
    /// <summary>The PDF/UA-1 rules (ISO 14289-1:2014).</summary>
    internal const string Ua1 = "ISO_14289_1";

    /// <summary>The PDF/UA-2 rules (ISO 14289-2:2024).</summary>
    internal const string Ua2 = "ISO_14289_2";

    /// <summary>The Well-Tagged PDF clause for sound, movie and trap network annotations, which PDF/UA-2 forbids.</summary>
    private const string DeprecatedAnnotations = "8.9.2.4";

    /// <summary>Gets the links the report is held to.</summary>
    internal static StandardsRuleLink[] All { get; } =
    [
        new(Ua1, ["6.2:1"], [PdfAccessibilityCode.NotMarked]),
        new(Ua1, ["7.1:11"], [PdfAccessibilityCode.NoStructureTree]),
        new(Ua1, ["7.1:8"], [PdfAccessibilityCode.NoMetadata]),
        new(Ua1, ["7.1:9"], [PdfAccessibilityCode.NoTitle]),
        new(Ua1, ["7.1:10"], [PdfAccessibilityCode.DisplayDocTitleOff]),
        new(Ua1, ["7.1:4"], [PdfAccessibilityCode.SuspectsSet]),
        new(Ua1, ["7.1:3"], [PdfAccessibilityCode.UntaggedContent]),
        new(Ua1, ["7.1:5"], [PdfAccessibilityCode.UnmappedStructureType]),
        new(Ua1, ["7.1:6"], [PdfAccessibilityCode.RoleMapCycle]),
        new(Ua1, ["7.1:7"], [PdfAccessibilityCode.StandardTypeRemapped]),

        // Clause 7.2 to 7.5: language, lists, figures, headings and tables.
        new(
            Ua1,
            ["7.2:34"],
            [PdfAccessibilityCode.NoLanguage],
            "veraPDF passes a file with no catalog /Lang when every text run has its own language; the report asks for the document language, which PDF/UA-2 requires."),
        new(Ua1, ["7.2:29"], [PdfAccessibilityCode.LanguageInvalid]),
        new(Ua1, ["7.2:17"], [PdfAccessibilityCode.ListItemOutsideList]),
        new(Ua1, ["7.3:1"], [PdfAccessibilityCode.FigureNoDescription]),
        new(Ua1, ["7.4.2:1"], [PdfAccessibilityCode.HeadingLevelSkipped]),

        // Clause 7.18: annotations, tab order, forms and links.
        new(Ua1, ["7.18.3:1"], [PdfAccessibilityCode.TabsNotStructure]),
        new(Ua1, ["7.18.5:1"], [PdfAccessibilityCode.LinkNotTagged]),
        new(
            Ua1,
            ["7.18.1:3"],
            [PdfAccessibilityCode.FormFieldNoLabel],
            "veraPDF passes a field that is its own widget (no /Kids) with no /TU and no /Alt on its structure element, as 7.18.1-t03-pass-d.pdf shows, "
            + "and fails the same gap on a field with separate widget kids; the report asks for a label in both cases (ISO 14289-1 7.18.1)."),
        new(Ua1, ["7.18.4:1"], [PdfAccessibilityCode.FormFieldNotTagged]),
        new(Ua1, ["7.18.1:1"], [PdfAccessibilityCode.AnnotationNotTagged, PdfAccessibilityCode.AnnotationWrongStructureType]),

        // Clause 7.21: fonts.
        new(Ua1, ["7.21.4.1:1"], [PdfAccessibilityCode.FontNotEmbedded]),
        new(Ua1, ["7.21.4.1:2"], [PdfAccessibilityCode.GlyphMissingFromFont]),
        new(Ua1, ["7.21.7:1"], [PdfAccessibilityCode.FontNoUnicodeMap]),
        new(Ua1, ["7.21.7:2"], [PdfAccessibilityCode.FontUnicodeMapInvalid]),
        new(Ua1, ["7.21.8:1"], [PdfAccessibilityCode.NotdefGlyphShown]),

        // ISO 14289-2:2024 clause 5, 6 and 8.2: claim, marking, structure root and namespaces.
        new(Ua2, ["5:5"], [PdfAccessibilityCode.ClaimRevisionInvalid]),
        new(Ua2, ["6.1:1"], [PdfAccessibilityCode.ClaimPart2NotPdf2]),
        new(Ua2, ["6.2:1"], [PdfAccessibilityCode.NotMarked]),
        new(Ua2, ["8.2.1:1"], [PdfAccessibilityCode.NoStructureTree]),
        new(Ua2, ["8.2.2:1"], [PdfAccessibilityCode.UntaggedContent]),
        new(Ua2, ["8.2.4:1"], [PdfAccessibilityCode.UnmappedStructureType]),
        new(Ua2, ["8.2.4:4"], [PdfAccessibilityCode.StandardTypeRemapped]),
        new(Ua2, ["8.2.4:2"], [PdfAccessibilityCode.RoleMapCycle]),
        new(Ua2, ["8.2.4:3"], [PdfAccessibilityCode.RoleMapSameNamespace]),
        new(Ua2, ["8.2.5.12:1"], [PdfAccessibilityCode.HeadingTypeNotAllowed]),
        new(Ua2, ["8.2.5.14:1"], [PdfAccessibilityCode.NoteTypeNotAllowed]),
        new(Ua2, ["8.2.5.20:1"], [PdfAccessibilityCode.LinkNotTagged]),
        new(Ua2, ["8.2.5.28.2:1"], [PdfAccessibilityCode.FigureNoDescription]),

        // Clause 8.4 to 8.11: language, fonts, annotations, forms and metadata.
        new(Ua2, ["8.4.4:1"], [PdfAccessibilityCode.NoLanguage]),
        new(Ua2, ["8.4.4:2"], [PdfAccessibilityCode.LanguageInvalid]),
        new(Ua2, ["8.4.5.5.1:1"], [PdfAccessibilityCode.FontNotEmbedded]),
        new(Ua2, ["8.4.5.5.1:2"], [PdfAccessibilityCode.GlyphMissingFromFont]),
        new(Ua2, ["8.4.5.8:1"], [PdfAccessibilityCode.FontNoUnicodeMap]),
        new(Ua2, ["8.4.5.8:2"], [PdfAccessibilityCode.FontUnicodeMapInvalid]),
        new(Ua2, ["8.4.5.9:1"], [PdfAccessibilityCode.NotdefGlyphShown]),
        new(Ua2, ["8.9.3.3:1"], [PdfAccessibilityCode.TabsNotStructure]),
        new(Ua2, ["8.10.1:1"], [PdfAccessibilityCode.FormFieldNotTagged]),
        new(Ua2, ["8.11.1:2"], [PdfAccessibilityCode.NoMetadata]),
        new(Ua2, ["8.11.1:1"], [PdfAccessibilityCode.NoTitle]),
        new(Ua2, ["8.11.2:1"], [PdfAccessibilityCode.DisplayDocTitleOff]),

        // Clauses 7.10 to 7.20: optional content, XFA, security, annotations and reference XObjects.
        new(Ua1, ["7.10:2"], [PdfAccessibilityCode.OptionalContentAutomaticState]),
        new(Ua1, ["7.15:1"], [PdfAccessibilityCode.DynamicXfaForm]),
        new(Ua1, ["7.16:1"], [PdfAccessibilityCode.SecurityBlocksAssistiveTechnology]),
        new(Ua1, ["7.18.2:1"], [PdfAccessibilityCode.TrapNetAnnotation]),
        new(Ua1, ["7.18.5:2"], [PdfAccessibilityCode.LinkNoDescription]),
        new(Ua1, ["7.20:1"], [PdfAccessibilityCode.ReferenceXObject]),

        // ISO 14289-2:2024 clause 8: the rules Well-Tagged PDF 1.0 repeats, decided by its findings.
        new(Ua2, ["8.2.5.8:1"], ["8.2.5.8"]),
        new(Ua2, ["8.2.5.23:1"], ["8.2.5.23"]),
        new(Ua2, ["8.2.5.24:1"], ["8.2.5.24"]),
        new(Ua2, ["8.2.5.25:1"], ["8.2.5.25"]),
        new(Ua2, ["8.2.5.27:1"], ["8.2.5.27"]),
        new(Ua2, ["8.7:1"], [PdfAccessibilityCode.OptionalContentNameMissing]),
        new(Ua2, ["8.7:2"], [PdfAccessibilityCode.OptionalContentAutomaticState]),
        new(Ua2, ["8.9.2.2:1"], ["8.9.2.2"]),
        new(Ua2, ["8.9.2.4.7:1"], ["8.9.2.4.7"]),
        new(Ua2, ["8.9.2.4.8:1"], ["8.9.2.4.8"]),
        new(Ua2, ["8.9.2.4.9:1"], ["8.9.2.4.9"]),
        new(Ua2, ["8.9.2.4.10:1"], ["8.9.2.4.10"]),
        new(Ua2, ["8.9.2.4.11:1"], [DeprecatedAnnotations]),
        new(Ua2, ["8.9.2.4.11:2"], [DeprecatedAnnotations]),
        new(Ua2, ["8.9.2.4.15:1"], [DeprecatedAnnotations]),
        new(Ua2, ["8.9.2.4.12:1"], ["8.9.2.4.12"]),
        new(Ua2, ["8.9.2.4.16:1"], ["8.9.2.4.16"]),
        new(Ua2, ["8.9.2.4.19:1"], ["8.9.2.4.19"]),
        new(Ua2, ["8.9.2.4.19:2"], ["8.9.2.4.19"]),
        new(Ua2, ["8.9.4.2:1"], ["8.9.4.2"]),
        new(Ua2, ["8.10.1:2"], ["8.10.1"]),
        new(Ua2, ["8.10.2.3:1"], ["8.10.2.3"]),
        new(Ua2, ["8.10.3.3:1"], ["8.10.3.3"]),
        new(Ua2, ["8.10.3.5:1"], ["8.10.3.5"]),
        new(Ua2, ["8.14.1:1"], ["8.14.1"]),
        new(Ua2, ["8.4.5.3.1:1"], ["8.4.5.3.1"]),
        new(Ua2, ["8.4.5.3.2:1"], ["8.4.5.3.2"]),
        new(Ua2, ["8.4.5.6:1"], ["8.4.5.6"]),
    ];

    /// <summary>
    /// Determines whether a rule is one that veraPDF's WCAG profiles repeat from its PDF/UA profiles with a different test, so that the link
    /// proved on the PDF/UA test files does not hold for it there. <c>WcagAgreementTests</c> finds them.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when HyperPdf's link for the rule does not hold in the WCAG profiles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsWcagVariant(PdfStandardsRule rule) =>
        (rule.Specification == Ua1 && rule.Reference == "7.2:17") || (rule.Specification == Ua2 && rule.Reference == "8.2.4:4");
}
