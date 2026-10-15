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

    /// <summary>The catalog has no <c>/Lang</c> entry (7.2).</summary>
    Language = 6,

    /// <summary>A page draws content that is neither tagged nor marked as an artifact (7.1).</summary>
    UntaggedContent = 7,

    /// <summary>A structure type is not standard and does not map to a standard type (7.1).</summary>
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
}
