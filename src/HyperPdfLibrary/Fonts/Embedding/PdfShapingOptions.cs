// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>What a shaper is asked to do with a run of text.</summary>
/// <param name="WritingMode">The direction text advances in. A vertical request lets the shaper pick vertical glyph forms ('vert' and 'vrt2').</param>
[DebuggerDisplay("PdfShapingOptions: {WritingMode}")]
public sealed record PdfShapingOptions(PdfWritingMode WritingMode)
{
    /// <summary>Gets the default options: horizontal writing and automatic language and script.</summary>
    public static PdfShapingOptions Default { get; } = new(PdfWritingMode.Horizontal);

    /// <summary>Gets the BCP 47 language of the text, or <see langword="null"/> to let the shaper decide.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the ISO 15924 script of the text, such as <c>Arab</c> or <c>Deva</c>, or <see langword="null"/> to let the shaper decide.</summary>
    public string? Script { get; init; }

    /// <summary>Gets the base direction of the text; by default the direction of the first strong letter.</summary>
    public PdfTextDirection Direction { get; init; }

    /// <summary>Gets the features to turn on or off besides the ones the script uses by default, or <see langword="null"/> for none.</summary>
    public IReadOnlyList<PdfFontFeature>? Features { get; init; }
}
