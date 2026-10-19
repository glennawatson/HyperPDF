// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>The plain-English sentence for each finding code, written for a reader with no PDF knowledge.</summary>
internal static class PdfAccessibilityMessages
{
    /// <summary>The sentences, indexed by code value.</summary>
    private static readonly string[] Sentences =
    [
        string.Empty,
        "This file is not marked as tagged, so screen readers may not find its structure.",
        "This file has no tags, so screen readers cannot tell headings from body text.",
        "The document language is not set, so a screen reader may use the wrong voice.",
        "The document has no title in its metadata.",
        "The title bar will not show the document title.",
        "The maker of this file says its tags may be wrong.",
        "The file claims PDF/UA-2 but is not a PDF 2.0 file.",
        "The file claims a PDF/UA part that this reader does not know.",
        "Tab order on this page does not follow the tags, so keyboard users may jump around.",
        "Some text on this page is not tagged and not marked as decoration, so screen readers may skip it.",
        "A tag type here is not a standard type and has no mapping to one.",
        "The tag type mapping loops back on itself, so this tag has no clear meaning.",
        "A heading skips a level, for example from level 1 to level 3.",
        "The document has more than one top-level heading.",
        "A figure has no description, so screen readers cannot say what it shows.",
        "A table has no header cells, so a screen reader cannot name its columns or rows.",
        "A table has header cells, but none says whether it heads a row or a column.",
        "A table cell has no header cell that labels it.",
        "A list item is not inside a list.",
        "A link is not tagged as a link, so screen readers may not announce it.",
        "A link has no description, so screen readers may read only its address.",
        "A form field has no label, so screen readers cannot say what to enter.",
        "A form field is not tagged as a form field.",
        "A note, stamp or other mark is not in the tags, so screen readers may skip it.",
        "An annotation is tagged with a type other than Annot.",
        "Text uses a font that is not stored in the file, so it may look different elsewhere.",
        "A font does not say what text some of its characters stand for, so screen readers may not read them.",
        "A font gives some characters an empty or invalid text value.",
        "Text shows a character the font does not have, so readers see a blank box.",
        "Text shows a character that the stored font does not contain.",
        "The tag type mapping changes the meaning of a standard tag type.",
        "A tag type is mapped to another tag type in its own namespace.",
        "The top of the tags is not a single Document tag in the PDF 2.0 namespace.",
        "The file uses the H heading tag, which PDF/UA-2 replaces with numbered headings.",
        "The file uses the Note tag without a mapping, which PDF/UA-2 does not allow.",
        "The file claims a PDF/UA-2 revision other than 2024.",
        "The file has no metadata, so it has no title for readers to show.",
        "A language setting is not a valid language code, so a screen reader may pick the wrong voice.",
        "A formula has no alternate text description required by PDF/UA-1.",
        "A footnote or endnote has no ID, or shares its ID with another tag.",
        "A layer configuration has no required name, so readers cannot identify it.",
        "A layer configuration changes visibility automatically, which PDF/UA does not allow.",
        "An embedded file is missing a required filename, so readers may not identify it correctly.",
        "The file is locked in a way that stops screen readers from reading its content.",
        "The file has a print trapping note, which an accessible file may not contain.",
        "This form builds itself when it opens, which screen readers cannot follow.",
        "A link works by where you click on an image, which keyboard and screen reader users cannot do.",
        "This page shows content that is stored in another file, which an accessible file may not do.",
        "A video or sound has no text description or content type, so a screen reader cannot say what it is.",
        "A tag is inside a tag that is not allowed to hold it, so the reading structure is not valid.",
        "A tag holds more of one kind of tag than allowed, such as two captions.",
        "A tag type that both namespaces define is not in the PDF 2.0 namespace, in a file that mixes namespaces.",
        "A tag type that only the PDF 2.0 namespace defines does not say it belongs to that namespace.",
        "A declaration names ISO/TS 32005 with a wrong address, so no reader will recognise the claim.",
    ];

    /// <summary>Gets the number of code values, counting <see cref="PdfAccessibilityCode.None"/>.</summary>
    internal static int CodeCount => Sentences.Length;

    /// <summary>Gets a code's sentence.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The sentence; empty for a value that is not a code.</returns>
    internal static string For(PdfAccessibilityCode code) => (uint)code < (uint)Sentences.Length ? Sentences[(int)code] : string.Empty;
}
