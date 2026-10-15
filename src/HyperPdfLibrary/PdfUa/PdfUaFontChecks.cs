// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Checks the fonts the text uses against the font rules of ISO 14289-1:2014, clause 7.21. They are the same font rules
/// the PDF/A validator applies at level 2A, which ISO 19005-2 writes from the same ISO 32000-1 requirements: embedded
/// programs (7.21.4.1), defined glyphs and matching widths (7.21.4.1, 7.21.5), Unicode mapping (7.21.7) and no
/// <c>.notdef</c> (7.21.8). The font area is the only PDF/A area that runs, so the check reads no PDF/A-only rule.
/// </summary>
internal static class PdfUaFontChecks
{
    /// <summary>The PDF/A level whose font rules are used: the level that asks for Unicode mapping.</summary>
    private const PdfALevel FontLevel = PdfALevel.PdfA2A;

    /// <summary>The rule that each font requirement reads, in the order the requirements are added.</summary>
    private static readonly FontRule[] Rules =
    [
        new(PdfARule.FontEmbedded, PdfUaRequirement.FontEmbedded),
        new(PdfARule.GlyphPresent, PdfUaRequirement.FontGlyphs),
        new(PdfARule.GlyphWidth, PdfUaRequirement.FontGlyphs),
        new(PdfARule.GlyphToUnicode, PdfUaRequirement.UnicodeMapping),
        new(PdfARule.GlyphToUnicodeValues, PdfUaRequirement.UnicodeMapping),
        new(PdfARule.GlyphNotdef, PdfUaRequirement.NotdefGlyph),
    ];

    /// <summary>Runs the font rules and adds a finding for each requirement that fails.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        var context = new PdfAValidationContext(document, FontLevel, cancellationToken);
        PdfAFontRules.Check(context);
        for (var i = 0; i < Rules.Length; i++)
        {
            if (context.FailureOf(Rules[i].Rule) is not null && !FailedEarlier(context, i))
            {
                findings.Add(new(Rules[i].Requirement, PdfUaFinding.DocumentLevel));
            }
        }
    }

    /// <summary>Determines whether an earlier rule that stands for the same requirement already failed, so the requirement is added once.</summary>
    /// <param name="context">The validation that recorded the failures.</param>
    /// <param name="index">The index of the rule being added.</param>
    /// <returns><see langword="true"/> when an earlier rule for the same requirement failed.</returns>
    private static bool FailedEarlier(PdfAValidationContext context, int index)
    {
        for (var earlier = 0; earlier < index; earlier++)
        {
            if (Rules[earlier].Requirement == Rules[index].Requirement && context.FailureOf(Rules[earlier].Rule) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Which requirement a font rule's failure counts as.</summary>
    /// <param name="Rule">The PDF/A rule the font area records.</param>
    /// <param name="Requirement">The PDF/UA requirement it stands for.</param>
    private readonly record struct FontRule(PdfARule Rule, PdfUaRequirement Requirement);
}
