// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>What laying out a document shares: the document, its styles, the footnote counter and the values fields show.</summary>
[DebuggerDisplay("LayoutEnv: {Document.Sections.Count} sections")]
internal sealed class LayoutEnv
{
    /// <summary>The next number to give an outline destination.</summary>
    private int _nextOutline;

    /// <summary>Initializes a new instance of the <see cref="LayoutEnv"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="now">The date and time fields show.</param>
    /// <param name="tagged">Whether the document has a structure tree.</param>
    internal LayoutEnv(PdfComposition document, DateTimeOffset now, bool tagged)
    {
        Document = document;
        Now = now;
        Tagged = tagged;
        Fields = new(1, PdfNumberFormat.Decimal, 1, 1, 1, now, document.Title, document.Author);
    }

    /// <summary>Gets the document.</summary>
    internal PdfComposition Document { get; }

    /// <summary>Gets the date and time fields show.</summary>
    internal DateTimeOffset Now { get; }

    /// <summary>Gets a value indicating whether the document has a structure tree.</summary>
    internal bool Tagged { get; }

    /// <summary>Gets the styles.</summary>
    internal PdfStyleSheet Styles => Document.Styles;

    /// <summary>Gets the footnote counter.</summary>
    internal NoteNumbers Notes { get; } = new();

    /// <summary>Gets the notes of the document, which say what could not be done as asked.</summary>
    internal List<string> Warnings { get; } = [];

    /// <summary>Gets the entries of the document outline, in order.</summary>
    internal List<OutlineEntry> Outline { get; } = [];

    /// <summary>Gets or sets the values fields show on the page being laid out.</summary>
    internal FieldContext Fields { get; set; }

    /// <summary>Makes an environment for content that is not part of the structure, such as headers and footers.</summary>
    /// <returns>The environment, which makes no structure nodes and keeps its own notes.</returns>
    internal LayoutEnv WithoutTags() => new(Document, Now, false) { Fields = Fields };

    /// <summary>Gives out the name of a new outline destination.</summary>
    /// <returns>The name.</returns>
    internal string NextOutlineName()
    {
        _nextOutline++;
        return $"__outline{_nextOutline}";
    }

    /// <summary>Makes a structure node when the document is tagged.</summary>
    /// <param name="type">The structure type.</param>
    /// <param name="parent">The parent node, or <see langword="null"/> for a child of the document.</param>
    /// <returns>The node, or <see langword="null"/> when the document is not tagged.</returns>
    internal StructNode? CreateNode(string type, StructNode? parent) => Tagged ? new(type, parent, PdfTagOptions.Default) : null;

    /// <summary>Makes a structure node with options when the document is tagged.</summary>
    /// <param name="type">The structure type.</param>
    /// <param name="parent">The parent node, or <see langword="null"/> for a child of the document.</param>
    /// <param name="options">What the element says besides its type.</param>
    /// <returns>The node, or <see langword="null"/> when the document is not tagged.</returns>
    internal StructNode? CreateNode(string type, StructNode? parent, PdfTagOptions options) => Tagged ? new(type, parent, options) : null;
}
