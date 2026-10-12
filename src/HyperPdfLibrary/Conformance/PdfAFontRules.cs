// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A font rule area. Like veraPDF it checks the fonts text-showing operators use, found by walking the content
/// of pages, annotation appearances, forms, tiling patterns and Type 3 glyph procedures, and the glyphs they show.
/// </summary>
internal static class PdfAFontRules
{
    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        .. PdfAFontDictionaryRules.Rules,
        .. PdfACompositeFontRules.Rules,
        .. PdfAFontProgramRules.Rules,
        .. PdfATextNameRules.Rules,
        .. PdfAGlyphRules.Rules,
        .. PdfAFontSubsetRules.Rules,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    /// <exception cref="OperationCanceledException">The validation was cancelled.</exception>
    internal static void Check(PdfAValidationContext context)
    {
        var walk = PdfATextWalk.Run(context);
        foreach (var font in walk.Used)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            PdfAFontDictionaryRules.Check(context, font);
            PdfAFontProgramRules.Check(context, font);
            PdfAGlyphRules.Check(context, font);
            PdfAFontSubsetRules.Check(context, font);
            if (font.Kind == PdfAFontKind.Type0)
            {
                PdfACompositeFontRules.Check(context, font);
            }
        }

        PdfATextNameRules.Check(context, walk);
    }
}
