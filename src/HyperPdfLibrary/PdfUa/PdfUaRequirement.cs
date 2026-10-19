// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PdfUa;

/// <summary>What a PDF/UA-1 problem found when a file is saved is about (ISO 14289-1:2014, clause 7). The values are stable, so callers may store them.</summary>
public enum PdfUaRequirement
{
    /// <summary>No requirement. Never reported.</summary>
    None = 0,

    /// <summary>The file has no structure tree (7.1).</summary>
    StructureTree = 1,

    /// <summary>The catalog's <c>/MarkInfo /Marked</c> flag is not true (7.1).</summary>
    Marked = 2,

    /// <summary>The catalog's <c>/MarkInfo /Suspects</c> flag is true (7.1).</summary>
    Suspects = 3,

    /// <summary>The XMP metadata has no <c>dc:title</c> (7.1).</summary>
    Title = 4,

    /// <summary><c>/ViewerPreferences /DisplayDocTitle</c> is not true (7.1).</summary>
    DisplayDocTitle = 5,

    /// <summary>A catalog or element language is missing or invalid (7.2).</summary>
    Language = 6,

    /// <summary>A page draws content that is neither tagged nor marked as an artifact (7.1).</summary>
    UntaggedContent = 7,

    /// <summary>A structure type or its role mapping is invalid (7.1).</summary>
    StructureTypes = 8,

    /// <summary>A heading skips a level (7.4.2).</summary>
    Headings = 9,

    /// <summary>A figure has no alternate description (7.3).</summary>
    FigureAlternative = 10,

    /// <summary>A list item is not inside a list (7.6).</summary>
    Lists = 11,

    /// <summary>A page with annotations does not set <c>/Tabs /S</c> (7.18.3).</summary>
    TabOrder = 12,

    /// <summary>An annotation is not part of the structure tree (7.18.1).</summary>
    AnnotationTagged = 13,

    /// <summary>An annotation is tagged with a type other than <c>Annot</c> (7.18.1).</summary>
    AnnotationStructureType = 14,

    /// <summary>A link annotation is not tagged as a Link (7.18.5).</summary>
    LinkTagged = 15,

    /// <summary>A link has no description (7.18.5).</summary>
    LinkDescription = 16,

    /// <summary>A form field is not tagged as a Form (7.18.4).</summary>
    FormFieldTagged = 17,

    /// <summary>A form field has no label (7.18.1).</summary>
    FormFieldLabel = 18,

    /// <summary>A font used for drawing is not embedded (7.21.4.1).</summary>
    FontEmbedded = 19,

    /// <summary>An embedded font lacks a glyph the text uses, or its widths do not match its program (7.21.4.1 and 7.21.5).</summary>
    FontGlyphs = 20,

    /// <summary>A font does not map every character code it shows to Unicode (7.21.7).</summary>
    UnicodeMapping = 21,

    /// <summary>A text operator shows the <c>.notdef</c> glyph (7.21.8).</summary>
    NotdefGlyph = 22,

    /// <summary>The tags refer to content or objects that are no longer on the page (7.1, ISO 32000-1 14.7.4).</summary>
    StructureReferences = 23,

    /// <summary>A mathematical expression has no Alt text string (7.7).</summary>
    FormulaAlternative = 24,

    /// <summary>A Note has no byte-string ID, or its ID is not unique (7.9).</summary>
    NoteIdentifier = 25,

    /// <summary>An optional-content configuration lacks a required name or contains an AS entry (7.10).</summary>
    OptionalContent = 26,

    /// <summary>An embedded file specification lacks an F or UF string (7.11).</summary>
    EmbeddedFileFilenames = 27,

    /// <summary>An encrypted file lacks the permission bit that lets assistive technology read it (7.16).</summary>
    AssistiveTechnologyAccess = 28,

    /// <summary>A TrapNet annotation is present (7.18.2).</summary>
    TrapNetAnnotations = 29,

    /// <summary>A dynamic XFA form is used (7.15).</summary>
    DynamicXfa = 30,

    /// <summary>A link uses a server-side image map (7.18.5).</summary>
    LinkImageMap = 31,

    /// <summary>A Reference XObject is used (7.20).</summary>
    ReferenceXObjects = 32,

    /// <summary>Media clip data lacks CT or Alt (7.18.6.2).</summary>
    MediaClipData = 33,

    /// <summary>The PDF/UA-2 identification is wrong: the file is not PDF 2.0, or the revision is not 2024 (ISO 14289-2 clause 5).</summary>
    Part2Identification = 34,

    /// <summary>The tags do not start with one Document element in the PDF 2.0 namespace (ISO 14289-2 clause 8.2.4).</summary>
    Part2DocumentRoot = 35,

    /// <summary>The file uses the H heading tag, which PDF/UA-2 replaces with numbered headings (ISO 14289-2 clause 8.2.5.12).</summary>
    Part2HeadingTypes = 36,

    /// <summary>The file uses the Note tag without a mapping (ISO 14289-2 clause 8.2.5.14).</summary>
    Part2NoteTypes = 37,

    /// <summary>A tag is inside a parent the hierarchy rules do not allow (ISO 14289-2 clause 8.2.4; ISO/TS 32005 clause 7.2).</summary>
    Part2Containment = 38,
}
