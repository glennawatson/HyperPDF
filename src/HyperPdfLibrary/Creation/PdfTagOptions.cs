// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Creation;

/// <summary>What a structure element says besides its type (ISO 32000-2 14.7.2 and 14.8.4).</summary>
[DebuggerDisplay("PdfTagOptions: alt {AlternativeText}, scope {TableScope}")]
public sealed record PdfTagOptions
{
    /// <summary>Gets the options that add nothing.</summary>
    public static PdfTagOptions Default { get; } = new();

    /// <summary>Gets the description of a figure, formula or other element that has no text of its own (<c>/Alt</c>).</summary>
    public string? AlternativeText { get; init; }

    /// <summary>Gets the text the element's content stands for, when it is not what the glyphs spell (<c>/ActualText</c>).</summary>
    public string? ActualText { get; init; }

    /// <summary>Gets the language of the element's text as a BCP 47 tag, when it differs from the document's (<c>/Lang</c>).</summary>
    public string? Language { get; init; }

    /// <summary>Gets a title for the element (<c>/T</c>).</summary>
    public string? Title { get; init; }

    /// <summary>Gets which cells a table header cell labels; used for <c>TH</c> elements.</summary>
    public PdfTableScope TableScope { get; init; }

    /// <summary>Gets the number of rows a table cell spans; 1 or less writes nothing.</summary>
    public int RowSpan { get; init; }

    /// <summary>Gets the number of columns a table cell spans; 1 or less writes nothing.</summary>
    public int ColumnSpan { get; init; }
}
