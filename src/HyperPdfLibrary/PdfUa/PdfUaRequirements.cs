// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PdfUa;

/// <summary>The clause of ISO 14289-1:2014 and the plain-English sentence for each requirement.</summary>
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
        new("7.2", "The document language is not set, so a screen reader may use the wrong voice."),
        new("7.1", "Some content is neither tagged nor marked as decoration, so screen readers may skip it."),
        new("7.1", "A tag type is not a standard type and has no mapping to one."),
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
    ];

    /// <summary>Gets the number of requirement values, counting <see cref="PdfUaRequirement.None"/>.</summary>
    internal static int Count => Texts.Length;

    /// <summary>Gets the clause of a requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The clause; empty for a value that is not a requirement.</returns>
    internal static string Clause(PdfUaRequirement requirement) => (uint)requirement < (uint)Texts.Length ? Texts[(int)requirement].Clause : string.Empty;

    /// <summary>Gets the sentence for a requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The sentence; empty for a value that is not a requirement.</returns>
    internal static string Message(PdfUaRequirement requirement) => (uint)requirement < (uint)Texts.Length ? Texts[(int)requirement].Message : string.Empty;

    /// <summary>The clause and sentence of one requirement.</summary>
    /// <param name="Clause">The clause.</param>
    /// <param name="Message">The sentence.</param>
    private readonly record struct PdfUaRequirementText(string Clause, string Message);
}
