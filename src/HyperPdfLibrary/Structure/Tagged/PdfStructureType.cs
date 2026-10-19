// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The standard structure types of PDF 1.7 and PDF 2.0 that an element's type resolves to through the role maps.</summary>
public enum PdfStructureType
{
    /// <summary>A type that is not standard and does not map to one.</summary>
    Unknown = 0,

    /// <summary><c>Document</c>: the whole document.</summary>
    Document = 1,

    /// <summary><c>DocumentFragment</c> (PDF 2.0): part of another document.</summary>
    DocumentFragment = 2,

    /// <summary><c>Part</c>: a large division, such as a chapter.</summary>
    Part = 3,

    /// <summary><c>Art</c> (PDF 1.7): an article.</summary>
    Article = 4,

    /// <summary><c>Sect</c>: a section.</summary>
    Section = 5,

    /// <summary><c>Div</c>: a generic block grouping.</summary>
    Division = 6,

    /// <summary><c>Aside</c> (PDF 2.0): content set apart from the main flow.</summary>
    Aside = 7,

    /// <summary><c>NonStruct</c>: a grouping with no meaning of its own.</summary>
    NonStructural = 8,

    /// <summary><c>Private</c> (PDF 1.7): producer-private content.</summary>
    Private = 9,

    /// <summary><c>BlockQuote</c> (PDF 1.7): a quoted block.</summary>
    BlockQuote = 10,

    /// <summary><c>Caption</c>: a caption for a figure, table or list.</summary>
    Caption = 11,

    /// <summary><c>TOC</c> (PDF 1.7): a table of contents.</summary>
    TableOfContents = 12,

    /// <summary><c>TOCI</c> (PDF 1.7): a table of contents item.</summary>
    TableOfContentsItem = 13,

    /// <summary><c>Index</c> (PDF 1.7): an index.</summary>
    Index = 14,

    /// <summary><c>P</c>: a paragraph.</summary>
    Paragraph = 15,

    /// <summary><c>H</c>: a heading whose level comes from its nesting.</summary>
    Heading = 16,

    /// <summary><c>H1</c>: a level 1 heading.</summary>
    Heading1 = 17,

    /// <summary><c>H2</c>: a level 2 heading.</summary>
    Heading2 = 18,

    /// <summary><c>H3</c>: a level 3 heading.</summary>
    Heading3 = 19,

    /// <summary><c>H4</c>: a level 4 heading.</summary>
    Heading4 = 20,

    /// <summary><c>H5</c>: a level 5 heading.</summary>
    Heading5 = 21,

    /// <summary><c>H6</c>, or a deeper <c>Hn</c> of PDF 2.0: a level 6 heading.</summary>
    Heading6 = 22,

    /// <summary><c>Title</c> (PDF 2.0): the document's title.</summary>
    Title = 23,

    /// <summary><c>L</c>: a list.</summary>
    List = 24,

    /// <summary><c>LI</c>: a list item.</summary>
    ListItem = 25,

    /// <summary><c>Lbl</c>: a label, such as a list item's bullet or number.</summary>
    Label = 26,

    /// <summary><c>LBody</c>: a list item's body.</summary>
    ListBody = 27,

    /// <summary><c>Table</c>: a table.</summary>
    Table = 28,

    /// <summary><c>TR</c>: a table row.</summary>
    TableRow = 29,

    /// <summary><c>TH</c>: a table header cell.</summary>
    TableHeaderCell = 30,

    /// <summary><c>TD</c>: a table data cell.</summary>
    TableDataCell = 31,

    /// <summary><c>THead</c>: a table's header rows.</summary>
    TableHead = 32,

    /// <summary><c>TBody</c>: a table's body rows.</summary>
    TableBody = 33,

    /// <summary><c>TFoot</c>: a table's footer rows.</summary>
    TableFoot = 34,

    /// <summary><c>Span</c>: a run of inline content.</summary>
    Span = 35,

    /// <summary><c>Quote</c> (PDF 1.7): an inline quotation.</summary>
    Quote = 36,

    /// <summary><c>Note</c> (PDF 1.7): a footnote or endnote.</summary>
    Note = 37,

    /// <summary><c>FENote</c> (PDF 2.0): a footnote or endnote.</summary>
    FootnoteOrEndnote = 38,

    /// <summary><c>Reference</c> (PDF 1.7): a citation of content elsewhere.</summary>
    Reference = 39,

    /// <summary><c>BibEntry</c> (PDF 1.7): a bibliography entry.</summary>
    BibliographyEntry = 40,

    /// <summary><c>Code</c> (PDF 1.7): computer code.</summary>
    Code = 41,

    /// <summary><c>Link</c>: a link, with its link annotation as an object reference.</summary>
    Link = 42,

    /// <summary><c>Annot</c>: an annotation that is not a link or widget.</summary>
    Annotation = 43,

    /// <summary><c>Ruby</c>: a ruby annotation group.</summary>
    Ruby = 44,

    /// <summary><c>RB</c>: ruby base text.</summary>
    RubyBase = 45,

    /// <summary><c>RT</c>: ruby annotation text.</summary>
    RubyText = 46,

    /// <summary><c>RP</c>: ruby punctuation.</summary>
    RubyPunctuation = 47,

    /// <summary><c>Warichu</c>: a warichu comment group.</summary>
    Warichu = 48,

    /// <summary><c>WT</c>: warichu text.</summary>
    WarichuText = 49,

    /// <summary><c>WP</c>: warichu punctuation.</summary>
    WarichuPunctuation = 50,

    /// <summary><c>Figure</c>: a picture, read by its alternate text.</summary>
    Figure = 51,

    /// <summary><c>Formula</c>: a mathematical formula.</summary>
    Formula = 52,

    /// <summary><c>Form</c>: a form field, with its widget annotation as an object reference.</summary>
    Form = 53,

    /// <summary><c>Em</c> (PDF 2.0): emphasis.</summary>
    Emphasis = 54,

    /// <summary><c>Strong</c> (PDF 2.0): strong importance.</summary>
    Strong = 55,

    /// <summary><c>Sub</c> (PDF 2.0): a sub-division of a block, such as one line of an address.</summary>
    Subdivision = 56,

    /// <summary><c>Artifact</c> (PDF 2.0): content that is not part of the document's meaning.</summary>
    Artifact = 57,

    /// <summary><c>Hn</c> (PDF 2.0), with n greater than 6: a heading whose level is in its name.</summary>
    NumberedHeading = 58,
}
