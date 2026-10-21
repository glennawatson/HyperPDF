// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>Plain words for tags, so nobody has to know that <c>LBody</c> means the body of a list item.</summary>
internal static class TagText
{
    /// <summary>The longest piece of a description shown in a list row before it is cut.</summary>
    private const int PreviewLength = 60;

    /// <summary>The plain name of each standard type, sorted by ordinal type so a binary search finds it; types not listed are shown as the file spells them.</summary>
    private static readonly TagName[] Names =
    [
        new("Annot", "Annotation"),
        new("Art", "Article"),
        new("Aside", "Side note"),
        new("BibEntry", "Bibliography entry"),
        new("BlockQuote", "Block quote"),
        new("Caption", "Caption"),
        new("Code", "Code"),
        new("Div", "Group"),
        new("Document", "Document"),
        new("DocumentFragment", "Document part"),
        new("Em", "Emphasis"),
        new("FENote", "Footnote or endnote"),
        new("Figure", "Picture"),
        new("Form", "Form field"),
        new("Formula", "Formula"),
        new("H", "Heading"),
        new("Index", "Index"),
        new("L", "List"),
        new("LBody", "List item text"),
        new("LI", "List item"),
        new("Lbl", "List label"),
        new("Link", "Link"),
        new("NonStruct", "Grouping only"),
        new("Note", "Note"),
        new("P", "Paragraph"),
        new("Part", "Part"),
        new("Quote", "Quote"),
        new("Reference", "Reference"),
        new("Sect", "Section"),
        new("Span", "Text"),
        new("Strong", "Strong text"),
        new("Sub", "Sub-part"),
        new("TBody", "Table body"),
        new("TD", "Table cell"),
        new("TFoot", "Table foot"),
        new("TH", "Table header cell"),
        new("THead", "Table head"),
        new("TOC", "Table of contents"),
        new("TOCI", "Contents entry"),
        new("TR", "Table row"),
        new("Table", "Table"),
        new("Title", "Title"),
    ];

    /// <summary>Gets the plain name of a type.</summary>
    /// <param name="type">The type as the file spells it.</param>
    /// <returns>The plain name; headings read as <c>Heading 2</c>.</returns>
    internal static string Name(string type)
    {
        var low = 0;
        var high = Names.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var order = string.CompareOrdinal(Names[middle].Type, type);
            if (order == 0)
            {
                return Names[middle].Plain;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return type.Length > 1 && type[0] == 'H' && int.TryParse(type.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var level)
            ? string.Create(CultureInfo.CurrentCulture, $"Heading {level}")
            : type;
    }

    /// <summary>Gets the line a list row shows.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The plain name, then the start of its description when it has one.</returns>
    internal static string Row(DocumentTag tag) =>
        tag.AlternateText is { Length: > 0 } description ? $"{Name(tag.Type)}: {Preview(description)}" : Name(tag.Type);

    /// <summary>Gets what a screen reader says for a row: the name, the page, the description and what is missing.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The sentence.</returns>
    internal static string Speak(DocumentTag tag)
    {
        var builder = new StringBuilder(Name(tag.Type));
        _ = builder.Append(CultureInfo.CurrentCulture, $", level {tag.Depth + 1}");
        if (tag.PageIndex >= 0)
        {
            _ = builder.Append(CultureInfo.CurrentCulture, $", page {tag.PageIndex + 1}");
        }

        if (tag.AlternateText is { Length: > 0 } description)
        {
            _ = builder.Append(CultureInfo.CurrentCulture, $", described as {description}");
        }
        else if (tag.Type == "Figure")
        {
            _ = builder.Append(", has no description");
        }

        if (tag.ChildTagCount > 0)
        {
            _ = builder.Append(CultureInfo.CurrentCulture, $", contains {tag.ChildTagCount} tags");
        }

        return builder.ToString();
    }

    /// <summary>Cuts a description to a length a row can show.</summary>
    /// <param name="text">The description.</param>
    /// <returns>The description, with an ellipsis when it was cut.</returns>
    private static string Preview(string text) => text.Length <= PreviewLength ? text : string.Concat(text.AsSpan(0, PreviewLength), "…");

    /// <summary>A standard type and the plain words for it.</summary>
    /// <param name="Type">The type as the file spells it.</param>
    /// <param name="Plain">The plain name.</param>
    private readonly record struct TagName(string Type, string Plain);
}
