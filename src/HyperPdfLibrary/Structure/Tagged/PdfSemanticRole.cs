// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What a node of a page's reading structure means to a reader, such as a screen reader or a reflow view.</summary>
public enum PdfSemanticRole
{
    /// <summary>An element whose type is not known; it is read as its content.</summary>
    Unknown = 0,

    /// <summary>A grouping with no meaning of its own: document, part, section, division, article or aside.</summary>
    Group = 1,

    /// <summary>A heading; the node's level says which.</summary>
    Heading = 2,

    /// <summary>A paragraph.</summary>
    Paragraph = 3,

    /// <summary>A list.</summary>
    List = 4,

    /// <summary>A list item.</summary>
    ListItem = 5,

    /// <summary>A list item's label, such as its bullet or number.</summary>
    ListLabel = 6,

    /// <summary>A list item's body.</summary>
    ListBody = 7,

    /// <summary>A table.</summary>
    Table = 8,

    /// <summary>A group of table rows: header, body or footer.</summary>
    TableRowGroup = 9,

    /// <summary>A table row.</summary>
    TableRow = 10,

    /// <summary>A table header cell.</summary>
    TableHeaderCell = 11,

    /// <summary>A table data cell.</summary>
    TableCell = 12,

    /// <summary>A figure, read by its alternate text.</summary>
    Figure = 13,

    /// <summary>A formula, read by its alternate text.</summary>
    Formula = 14,

    /// <summary>A link; the node carries its annotation and target.</summary>
    Link = 15,

    /// <summary>A form field; the node carries its widget, label and state.</summary>
    FormField = 16,

    /// <summary>A footnote or endnote.</summary>
    Note = 17,

    /// <summary>A quotation, inline or a block.</summary>
    Quote = 18,

    /// <summary>Computer code.</summary>
    Code = 19,

    /// <summary>A caption.</summary>
    Caption = 20,

    /// <summary>A table of contents or an index.</summary>
    TableOfContents = 21,

    /// <summary>A table of contents item.</summary>
    TableOfContentsItem = 22,

    /// <summary>Inline content: span, emphasis, reference, ruby and similar.</summary>
    Inline = 23,

    /// <summary>An annotation that is not a link or a form field.</summary>
    Annotation = 24,

    /// <summary>A run of marked content: the text and drawing of one marked content id.</summary>
    Content = 25,

    /// <summary>A pagination artifact: a running header, a running footer or a page number. Readers skip it; <see cref="PdfSemanticNode.Pagination"/> says which.</summary>
    Artifact = 26,
}
