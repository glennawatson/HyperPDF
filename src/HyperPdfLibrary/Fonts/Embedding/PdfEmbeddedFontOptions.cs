// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>How a font is embedded and how text is shown with it.</summary>
[DebuggerDisplay("PdfEmbeddedFontOptions: {ActualText}")]
public sealed record PdfEmbeddedFontOptions
{
    /// <summary>Gets the default options: the font's own character map for shaping, <c>/ActualText</c> for ligatures and clusters, and a <c>/CIDSet</c>.</summary>
    public static PdfEmbeddedFontOptions Default { get; } = new();

    /// <summary>
    /// Gets the shaper that turns text into glyphs, or <see langword="null"/> to use <see cref="PdfOpenTypeShaper"/>, which applies
    /// the font's own GSUB and GPOS tables. A host can supply another, such as <see cref="PdfBasicShaper"/>, which maps one glyph
    /// to each character.
    /// </summary>
    public IPdfGlyphShaper? Shaper { get; init; }

    /// <summary>Gets when shown text is marked with <c>/ActualText</c>.</summary>
    public PdfActualTextPolicy ActualText { get; init; } = PdfActualTextPolicy.Ligatures;

    /// <summary>
    /// Gets the direction text advances in. Vertical writing uses <c>Identity-V</c> with <c>/DW2</c> and <c>/W2</c> from the
    /// font's vertical metrics, and advances each glyph downward; glyph offsets from the shaper are not applied.
    /// </summary>
    public PdfWritingMode WritingMode { get; init; } = PdfWritingMode.Horizontal;

    /// <summary>Gets the BCP 47 language of the text, which picks the font's language system for the shaper, or <see langword="null"/> for the script's default.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the ISO 15924 script of the text, such as <c>Arab</c> or <c>Deva</c>, or <see langword="null"/> to find it from the characters.</summary>
    public string? Script { get; init; }

    /// <summary>Gets the base direction of the text; by default the direction of the first strong letter.</summary>
    public PdfTextDirection Direction { get; init; }

    /// <summary>Gets the OpenType features to turn on or off besides the ones the script uses by default, or <see langword="null"/> for none.</summary>
    public IReadOnlyList<PdfFontFeature>? Features { get; init; }

    /// <summary>Gets a value indicating whether a <c>/CIDSet</c> is written; PDF/A-1 requires one for a subset font.</summary>
    public bool WriteCidSet { get; init; } = true;
}
