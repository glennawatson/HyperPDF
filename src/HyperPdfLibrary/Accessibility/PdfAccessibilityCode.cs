// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>What an accessibility report finding is about. The values are stable, so callers may store them.</summary>
public enum PdfAccessibilityCode
{
    /// <summary>No finding. Never reported.</summary>
    None = 0,

    /// <summary>The catalog's <c>/MarkInfo /Marked</c> flag is missing or false.</summary>
    NotMarked = 1,

    /// <summary>The file has no structure tree, so screen readers get no tags.</summary>
    NoStructureTree = 2,

    /// <summary>The catalog has no <c>/Lang</c> entry.</summary>
    NoLanguage = 3,

    /// <summary>The XMP metadata has no <c>dc:title</c>.</summary>
    NoTitle = 4,

    /// <summary><c>DisplayDocTitle</c> in the viewer preferences is not true.</summary>
    DisplayDocTitleOff = 5,

    /// <summary>The <c>/MarkInfo /Suspects</c> flag is true, so the producer doubts the tags.</summary>
    SuspectsSet = 6,

    /// <summary>The file claims PDF/UA-2 but its version is below PDF 2.0.</summary>
    ClaimPart2NotPdf2 = 7,

    /// <summary>The file has a <c>pdfuaid:part</c> value other than 1 or 2.</summary>
    ClaimPartUnknown = 8,

    /// <summary>A page has annotations but its <c>/Tabs</c> entry is not <c>S</c>.</summary>
    TabsNotStructure = 9,

    /// <summary>A page draws text that is neither tagged nor marked as an artifact.</summary>
    UntaggedContent = 10,

    /// <summary>A structure type is not standard and no role map leads it to a standard type.</summary>
    UnmappedStructureType = 11,

    /// <summary>The role map sends a structure type round in a circle.</summary>
    RoleMapCycle = 12,

    /// <summary>A heading is more than one level deeper than the heading before it.</summary>
    HeadingLevelSkipped = 13,

    /// <summary>The document has more than one level 1 heading.</summary>
    MultipleLevelOneHeadings = 14,

    /// <summary>A figure has neither <c>/Alt</c> nor <c>/ActualText</c>.</summary>
    FigureNoDescription = 15,

    /// <summary>A table has no header cells.</summary>
    TableNoHeaderCells = 16,

    /// <summary>A table has header cells but none states a scope or lists the headers of a cell.</summary>
    TableHeaderNoScope = 17,

    /// <summary>A table cell is not labelled by any header cell.</summary>
    TableCellNoHeader = 18,

    /// <summary>A list item is not inside a list.</summary>
    ListItemOutsideList = 19,

    /// <summary>A link annotation is not inside a Link structure element.</summary>
    LinkNotTagged = 20,

    /// <summary>A link has no <c>/Contents</c> and its structure element has no <c>/Alt</c>.</summary>
    LinkNoDescription = 21,

    /// <summary>A form field has no <c>/TU</c> label.</summary>
    FormFieldNoLabel = 22,

    /// <summary>A form field is not inside a Form structure element.</summary>
    FormFieldNotTagged = 23,

    /// <summary>An annotation is not referred to from the structure tree.</summary>
    AnnotationNotTagged = 24,

    /// <summary>An annotation is tagged with a type other than Annot (PDF/UA-1).</summary>
    AnnotationWrongStructureType = 25,

    /// <summary>Text is drawn with a font whose program is not embedded (ISO 14289-1 7.21.4.1; ISO 14289-2 8.4.5.5.1).</summary>
    FontNotEmbedded = 26,

    /// <summary>A font does not map every character code it shows to Unicode (ISO 14289-1 7.21.7; ISO 14289-2 8.4.5.8).</summary>
    FontNoUnicodeMap = 27,

    /// <summary>A font maps a character code to U+0000, U+FEFF or U+FFFE (ISO 14289-1 7.21.7; ISO 14289-2 8.4.5.8).</summary>
    FontUnicodeMapInvalid = 28,

    /// <summary>Text shows the <c>.notdef</c> glyph (ISO 14289-1 7.21.8; ISO 14289-2 8.4.5.9).</summary>
    NotdefGlyphShown = 29,

    /// <summary>Text shows a glyph the embedded font program does not define (ISO 14289-1 7.21.4.1; ISO 14289-2 8.4.5.5.1).</summary>
    GlyphMissingFromFont = 30,

    /// <summary>The role map remaps a standard structure type (ISO 14289-1 7.1).</summary>
    StandardTypeRemapped = 31,

    /// <summary>A namespace's role map sends a type to another type in the same namespace (ISO 14289-2 8.2.4).</summary>
    RoleMapSameNamespace = 32,

    /// <summary>The structure tree root does not hold a single Document element in the PDF 2.0 namespace (ISO 14289-2 8.2.5.2).</summary>
    DocumentRootInvalid = 33,

    /// <summary>The file uses the <c>H</c> heading type, which PDF/UA-2 does not allow (ISO 14289-2 8.2.5.12).</summary>
    HeadingTypeNotAllowed = 34,

    /// <summary>The file uses the <c>Note</c> type without role mapping it into the PDF 2.0 namespace (ISO 14289-2 8.2.5.14).</summary>
    NoteTypeNotAllowed = 35,

    /// <summary>The file claims a PDF/UA-2 revision other than 2024 (ISO 14289-2 clause 5).</summary>
    ClaimRevisionInvalid = 36,

    /// <summary>The catalog has no XMP metadata stream (ISO 14289-1 7.1; ISO 14289-2 8.11.1).</summary>
    NoMetadata = 37,

    /// <summary>A <c>/Lang</c> value, in the catalog or in a structure element, is not a language identifier (ISO 14289-1 7.2; ISO 14289-2 8.4.4).</summary>
    LanguageInvalid = 38,

    /// <summary>A PDF/UA-1 Formula element has no alternate text string (ISO 14289-1 7.7).</summary>
    FormulaNoAlternative = 39,

    /// <summary>A PDF/UA-1 Note has no byte-string ID, or its ID is not unique (ISO 14289-1 7.9).</summary>
    NoteIdentifierInvalid = 40,

    /// <summary>An optional-content configuration lacks a required nonempty text name (ISO 14289-1 7.10; ISO 14289-2 8.7).</summary>
    OptionalContentNameMissing = 41,

    /// <summary>An optional-content configuration has a prohibited AS entry (ISO 14289-1 7.10; ISO 14289-2 8.7).</summary>
    OptionalContentAutomaticState = 42,

    /// <summary>An embedded file specification lacks an F or UF string (ISO 14289-1 7.11).</summary>
    EmbeddedFileFilenameInvalid = 43,

    /// <summary>An encrypted file does not allow assistive technology to read it: the encryption dictionary's P value has bit 10 clear (ISO 14289-1 7.16).</summary>
    SecurityBlocksAssistiveTechnology = 44,

    /// <summary>A TrapNet annotation is present, which a PDF/UA-1 file may not contain (ISO 14289-1 7.18.2).</summary>
    TrapNetAnnotation = 45,

    /// <summary>The XFA configuration asks for dynamic rendering, which a PDF/UA-1 file may not use (ISO 14289-1 7.15).</summary>
    DynamicXfaForm = 46,

    /// <summary>A link's URI action has IsMap set to true, which a PDF/UA-1 file may not use unless the same function is available elsewhere (ISO 14289-1 7.18.5).</summary>
    LinkIsMap = 47,

    /// <summary>A Reference XObject is used, which a PDF/UA-1 file may not contain (ISO 14289-1 7.20).</summary>
    ReferenceXObject = 48,

    /// <summary>Media clip data lacks the CT or Alt key that PDF/UA-1 requires (ISO 14289-1 7.18.6.2).</summary>
    MediaClipNoAlternative = 49,

    /// <summary>A tag is inside a parent that the hierarchy table of ISO/TS 32005 clause 7.2 (ISO 32000-2 Annex L) does not allow.</summary>
    StructureContainmentViolation = 50,

    /// <summary>A tag holds more than one child of a type that the hierarchy table of ISO/TS 32005 clause 7.2 allows at most once (occurrence 0..1).</summary>
    StructureOccurrenceViolation = 51,
}
