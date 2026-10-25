// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Composition;

/// <summary>A paragraph: runs of text, tabs, fields, footnotes, links and bookmarks that wrap to the width of the text area.</summary>
/// <param name="Inlines">The paragraph's content, in order.</param>
[DebuggerDisplay("PdfParagraph: {Style}")]
public sealed record PdfParagraph(IReadOnlyList<PdfInline> Inlines) : PdfBlock
{
    /// <summary>Gets the name of the paragraph's style, or <see langword="null"/> for the Normal style.</summary>
    public string? Style { get; init; }

    /// <summary>Gets formatting laid over the style, or <see langword="null"/> for none.</summary>
    public PdfParagraphFormat? Format { get; init; }

    /// <summary>Gets formatting laid over the style for every run of the paragraph, or <see langword="null"/> for none.</summary>
    public PdfCharacterFormat? CharacterFormat { get; init; }

    /// <summary>Makes a paragraph of one run of plain text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The paragraph.</returns>
    public static PdfParagraph Of(string text) => new([new PdfRun(text)]);

    /// <summary>Makes a paragraph from inlines.</summary>
    /// <param name="inlines">The content.</param>
    /// <returns>The paragraph.</returns>
    public static PdfParagraph Of(params PdfInline[] inlines) => new(inlines);

    /// <summary>Makes a paragraph of plain text in a style.</summary>
    /// <param name="style">The name of the style.</param>
    /// <param name="text">The text.</param>
    /// <returns>The paragraph.</returns>
    public static PdfParagraph Styled(string style, string text) => new([new PdfRun(text)]) { Style = style };

    /// <summary>Makes a heading paragraph.</summary>
    /// <param name="level">The level, 1 to 6.</param>
    /// <param name="text">The heading's text.</param>
    /// <returns>The paragraph, in the style for that heading level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is not from 1 to 6.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfParagraph Heading(int level, string text) => Styled(PdfStyleNames.GetHeading(level), text);
}
