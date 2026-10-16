// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Creation;

/// <summary>How a created document is described and structured.</summary>
[DebuggerDisplay("PdfDocumentCreatorOptions: {Title}, tagged {Tagged}, {UaClaim}")]
public sealed record PdfDocumentCreatorOptions
{
    /// <summary>The producer written to the document information when none is given.</summary>
    private const string DefaultProducer = "HyperPDF";

    /// <summary>Gets the options that describe nothing and tag nothing.</summary>
    public static PdfDocumentCreatorOptions Default { get; } = new();

    /// <summary>Gets the document's title, written to the information dictionary and to the XMP metadata.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the document's author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the document's subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the document's keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the name of the program that made the content, written as the creator.</summary>
    public string? CreatorTool { get; init; }

    /// <summary>Gets the program that wrote the file.</summary>
    public string Producer { get; init; } = DefaultProducer;

    /// <summary>Gets the document's main language as a BCP 47 tag such as <c>en-AU</c> (the catalog's <c>/Lang</c>).</summary>
    public string? Language { get; init; }

    /// <summary>Gets a value indicating whether the document has a structure tree. Pages of a tagged document must put their content inside marked content or artifacts.</summary>
    public bool Tagged { get; init; }

    /// <summary>Gets the PDF/UA part to claim; the claim is dropped, with a note in the report, when the document does not meet what the creator checks.</summary>
    public PdfUaConformance UaClaim { get; init; }

    /// <summary>Gets how fonts are embedded when none are given to <see cref="PdfDocumentCreator.EmbedFont(PdfFontSource, PdfEmbeddedFontOptions)"/>.</summary>
    public PdfEmbeddedFontOptions FontOptions { get; init; } = PdfEmbeddedFontOptions.Default;

    /// <summary>
    /// Gets a value indicating whether fonts created in this document automatically fall back to the bundled
    /// symbol, dingbats, and monochrome emoji fonts when a glyph is missing from both the primary font and any
    /// explicit fallback fonts. Default backup fonts are embedded lazily only when a missing glyph is encountered.
    /// </summary>
    public bool UseDefaultFallbackFonts { get; init; } = true;

    /// <summary>Gets the clock that dates the document, or <see langword="null"/> for the system clock.</summary>
    public TimeProvider? Clock { get; init; }
}
