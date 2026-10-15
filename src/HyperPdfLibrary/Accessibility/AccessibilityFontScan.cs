// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks the fonts that text-showing operators use for the PDF/UA font requirements: the program is embedded, every shown
/// character code has a Unicode value, the values are valid, the font defines each glyph, and <c>.notdef</c> is not shown
/// (ISO 14289-1 7.21.4.1, 7.21.7 and 7.21.8; ISO 14289-2 8.4.5.5.1, 8.4.5.8 and 8.4.5.9). The PDF/UA requirements are the
/// same as the PDF/A ones, so the walk over page content, annotation appearances, forms, patterns and Type 3 glyph
/// procedures and the glyph facts come from the PDF/A validator's font rules. One finding is added for each font that
/// breaks a requirement.
/// </summary>
internal static class AccessibilityFontScan
{
    /// <summary>The PDF/A level whose glyph rules apply: the Unicode rules of PDF/A-2 and later.</summary>
    private const PdfALevel Level = PdfALevel.PdfA2U;

    /// <summary>The number of PDF/A rules in <see cref="Rules"/>.</summary>
    private const int RuleCount = 5;

    /// <summary>The PDF/A rules that match the PDF/UA font requirements, and the codes they report.</summary>
    private static readonly FontRule[] Rules =
    [
        new(PdfARule.FontEmbedded, PdfAccessibilityCode.FontNotEmbedded),
        new(PdfARule.GlyphPresent, PdfAccessibilityCode.GlyphMissingFromFont),
        new(PdfARule.GlyphToUnicode, PdfAccessibilityCode.FontNoUnicodeMap),
        new(PdfARule.GlyphToUnicodeValues, PdfAccessibilityCode.FontUnicodeMapInvalid),
        new(PdfARule.GlyphNotdef, PdfAccessibilityCode.NotdefGlyphShown),
    ];

    /// <summary>
    /// Checks the fonts a document shows. The walk over every page's content is the costly part, so its result is kept
    /// with the document until an edit, and a later report adds the same findings without walking again.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token, checked for every font.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Run(PdfDocument document, AccessibilityFindings findings, CancellationToken cancellation)
    {
        var revision = PdfDocumentCaches.Revision(document);
        var codes = Volatile.Read(ref document.State.AccessibilityFonts)
            ?? PdfDocumentCaches.PublishFirst(document, ref document.State.AccessibilityFonts, Scan(document, cancellation), revision);
        foreach (var code in codes)
        {
            findings.Add(code);
        }
    }

    /// <summary>Walks the document's content and checks each font it shows.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">The cancellation token, checked for every font.</param>
    /// <returns>The code of each finding, in the order found.</returns>
    private static PdfAccessibilityCode[] Scan(PdfDocument document, CancellationToken cancellation)
    {
        var found = new List<PdfAccessibilityCode>();
        var context = new PdfAValidationContext(document, Level, cancellation);
        var walk = PdfATextWalk.Run(context);
        var rules = Rules;
        Span<int> before = stackalloc int[RuleCount];
        foreach (var font in walk.Used)
        {
            cancellation.ThrowIfCancellationRequested();
            Count(context, rules, before);
            PdfAFontProgramRules.Check(context, font);
            PdfAGlyphRules.Check(context, font);
            for (var i = 0; i < rules.Length; i++)
            {
                if (context.FailureOf(rules[i].Rule)?.Count > before[i])
                {
                    found.Add(rules[i].Code);
                }
            }
        }

        return [.. found];
    }

    /// <summary>Reads how many times each rule has failed so far.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="counts">Receives the counts, in rule order.</param>
    private static void Count(PdfAValidationContext context, FontRule[] rules, Span<int> counts)
    {
        for (var i = 0; i < rules.Length; i++)
        {
            counts[i] = context.FailureOf(rules[i].Rule)?.Count ?? 0;
        }
    }

    /// <summary>A PDF/A font rule and the report code that reports it.</summary>
    /// <param name="Rule">The PDF/A rule.</param>
    /// <param name="Code">The code.</param>
    private readonly record struct FontRule(PdfARule Rule, PdfAccessibilityCode Code);
}
