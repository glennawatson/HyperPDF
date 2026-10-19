// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.PdfUa;

/// <summary>Clause references and save-problem messages for the implemented PDF/UA-1 checks, not a full conformance checklist.</summary>
internal static class PdfUaRequirements
{
    /// <summary>The clause and sentence of each requirement, indexed by requirement value.</summary>
    private static readonly PdfUaRequirementText[] Texts =
    [
        new(string.Empty, string.Empty),
        new("7.1", "The file has no tags, so screen readers cannot read its content in order."),
        new("7.1", "The file is not marked as tagged."),
        new("7.1", "The maker of the file says its tags may be wrong."),
        new("7.1", "The document has no title in its metadata."),
        new("7.1", "The window will not show the document title."),
        new("7.2", "A document or element language is missing or invalid, so a screen reader may use the wrong voice."),
        new("7.1", "Some content is neither tagged nor marked as decoration, so screen readers may skip it."),
        new("7.1", "A tag type or its mapping to a standard type is invalid."),
        new("7.4.2", "A heading skips a level, for example from level 1 to level 3."),
        new("7.3", "A figure has no description, so screen readers cannot say what it shows."),
        new("7.6", "A list item is not inside a list."),
        new("7.18.3", "A page with notes, links or fields does not use the tag order for the Tab key."),
        new("7.18.1", "A note, stamp or other mark is not in the tags, so screen readers may skip it."),
        new("7.18.1", "An annotation is tagged with a type other than Annot."),
        new("7.18.5", "A link is not tagged as a link."),
        new("7.18.5", "A link has no description."),
        new("7.18.4", "A form field is not tagged as a form field."),
        new("7.18.1", "A form field has no label."),
        new("7.21.4.1", "A font is not stored in the file, so the text may look different on another computer."),
        new("7.21.4.1 and 7.21.5", "A font does not hold every glyph the text uses, or its widths do not match its font program."),
        new("7.21.7", "Some characters have no text behind them, so a screen reader cannot read them."),
        new("7.21.8", "Text uses the missing-glyph shape, so a screen reader cannot tell what it says."),
        new("7.1", "The tags point at content or objects that are no longer on the page."),
        new("7.7", "A formula has no alternate text description required by PDF/UA-1."),
        new("7.9", "A footnote or endnote has no ID, or shares its ID with another tag."),
        new("7.10", "A layer configuration has no required name or changes visibility automatically."),
        new("7.11", "An embedded file is missing a required filename."),
        new("7.16", "The file is encrypted without the permission that lets screen readers read it."),
        new("7.18.2", "The file has a print trapping note, which PDF/UA-1 does not allow."),
        new("7.15", "The file uses a dynamic form, which PDF/UA-1 does not allow."),
        new("7.18.5", "A link works by where you click on an image, which PDF/UA-1 does not allow."),
        new("7.20", "A page shows content stored in another file, which PDF/UA-1 does not allow."),
        new("7.18.6.2", "A video or sound has no text description or content type."),
        new("5", "The PDF/UA-2 identification is wrong: the file is not PDF 2.0, or the revision is not 2024."),
        new("8.2.4", "The tags do not start with a single Document tag in the PDF 2.0 namespace."),
        new("8.2.5.12", "The file uses the H heading tag, which PDF/UA-2 replaces with numbered headings."),
        new("8.2.5.14", "The file uses the Note tag without a mapping, which PDF/UA-2 does not allow."),
        new("8.2.4", "A tag is inside a tag that is not allowed to hold it."),
    ];

    /// <summary>
    /// The ISO 14289-2:2024 clause of each requirement, indexed by requirement value; empty where the requirement belongs to
    /// part 1 only. Clauses are named at the level the standard's contents confirm.
    /// </summary>
    private static readonly string[] Part2Clauses =
    [
        string.Empty,
        "8.2",
        "8.2",
        "8.2",
        "8.11",
        "8.11",
        "8.4.4",
        "8.2.2",
        "8.2.4",
        "8.2.5.12",
        "8.4.3",
        "8.2.5",
        "8.9.3",
        "8.9.2",
        "8.9.2",
        "8.9.2",
        "8.9.4",
        "8.10",
        "8.10",
        "8.4.5",
        "8.4.5",
        "8.4.2",
        "8.4.5",
        "8.2",
        string.Empty,
        string.Empty,
        "8.7",
        "8.14",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "5",
        "8.2.4",
        "8.2.5.12",
        "8.2.5.14",
        "8.2.4",
    ];

    /// <summary>Gets the number of requirement values, counting <see cref="PdfUaRequirement.None"/>.</summary>
    internal static int Count => Texts.Length;

    /// <summary>Gets the clause of a requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The clause; empty for a value that is not a requirement.</returns>
    internal static string Clause(PdfUaRequirement requirement) => (uint)requirement < (uint)Texts.Length ? Texts[(int)requirement].Clause : string.Empty;

    /// <summary>Gets the clause of a requirement in a PDF/UA part.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="part">The PDF/UA part: 1 or 2.</param>
    /// <returns>The clause; the part 1 clause when the requirement has no part 2 clause.</returns>
    internal static string Clause(PdfUaRequirement requirement, int part) =>
        part == PdfUaClaim.Part2 && (uint)requirement < (uint)Part2Clauses.Length && Part2Clauses[(int)requirement].Length > 0 ? Part2Clauses[(int)requirement] : Clause(requirement);

    /// <summary>Gets the sentence for a requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The sentence; empty for a value that is not a requirement.</returns>
    internal static string Message(PdfUaRequirement requirement) => (uint)requirement < (uint)Texts.Length ? Texts[(int)requirement].Message : string.Empty;

    /// <summary>The clause and sentence of one requirement.</summary>
    /// <param name="Clause">The clause.</param>
    /// <param name="Message">The sentence.</param>
    private readonly record struct PdfUaRequirementText(string Clause, string Message);
}
