// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What layout inference may use beyond a single page's text: what repeats across the document, a host's figure descriptions and a language guess.</summary>
[DebuggerDisplay("PdfLayoutInference: language guess {GuessLanguage}")]
public readonly record struct PdfLayoutInference
{
    /// <summary>Gets inference with no extras: page numbers are found by their form alone, figures have no descriptions and no language is guessed.</summary>
    public static PdfLayoutInference Default => default;

    /// <summary>Gets what repeats across the document, so running headers and footers are found; <see langword="null"/> to go by page numbers alone.</summary>
    public PdfPaginationProfile? Pagination { get; init; }

    /// <summary>Gets the host's description of each figure, or <see langword="null"/> to leave figures without one.</summary>
    public PdfFigureAltTextProvider? FigureAltText { get; init; }

    /// <summary>
    /// Gets a value indicating whether the language of each heading, paragraph and caption is guessed from its text and
    /// given as <see cref="PdfSemanticNode.InferredLanguage"/>. A guess is a hint, not the document's own language.
    /// </summary>
    public bool GuessLanguage { get; init; }
}
