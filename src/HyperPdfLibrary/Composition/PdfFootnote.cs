// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A footnote: a number in the text and the note itself at the foot of the page the number is on.</summary>
/// <param name="Content">The note's paragraphs.</param>
[DebuggerDisplay("PdfFootnote")]
public sealed record PdfFootnote(IReadOnlyList<PdfBlock> Content) : PdfInline
{
    /// <summary>Gets the number format of the footnote's number; the default is decimal digits.</summary>
    public PdfNumberFormat NumberFormat { get; init; }

    /// <summary>Makes a footnote from one paragraph of text.</summary>
    /// <param name="text">The note's text.</param>
    /// <returns>The footnote.</returns>
    public static PdfFootnote Of(string text) => new([PdfParagraph.Of(text)]);
}
