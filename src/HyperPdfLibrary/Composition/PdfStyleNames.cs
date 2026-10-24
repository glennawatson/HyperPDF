// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>The names of the styles that <see cref="PdfStyleSheet.CreateDefault"/> makes.</summary>
public static class PdfStyleNames
{
    /// <summary>The deepest heading level.</summary>
    private const int HeadingLevels = 6;

    /// <summary>Gets the name of the style that paragraphs without a style use, and the root of the others.</summary>
    public static string Normal { get; } = "Normal";

    /// <summary>Gets the name of the style for a document's title.</summary>
    public static string Title { get; } = "Title";

    /// <summary>Gets the name of the style for a document's subtitle.</summary>
    public static string Subtitle { get; } = "Subtitle";

    /// <summary>Gets the name of the style of a bulleted list's paragraphs.</summary>
    public static string ListBullet { get; } = "List Bullet";

    /// <summary>Gets the name of the style of a numbered list's paragraphs.</summary>
    public static string ListNumber { get; } = "List Number";

    /// <summary>Gets the name of the style of text in a page header.</summary>
    public static string Header { get; } = "Header";

    /// <summary>Gets the name of the style of text in a page footer.</summary>
    public static string Footer { get; } = "Footer";

    /// <summary>Gets the name of the style of a footnote's text.</summary>
    public static string FootnoteText { get; } = "Footnote Text";

    /// <summary>Gets the name of the style of a footnote's number, in the text and at the foot of the page.</summary>
    public static string FootnoteReference { get; } = "Footnote Reference";

    /// <summary>Gets the name of the style of linked text.</summary>
    public static string Hyperlink { get; } = "Hyperlink";

    /// <summary>Gets the name of the style of text in a table cell.</summary>
    public static string TableText { get; } = "Table Text";

    /// <summary>Gets the name of the style of text in the header cells of a table.</summary>
    public static string TableHeader { get; } = "Table Header";

    /// <summary>Gets the name of the style of a caption under a figure or table.</summary>
    public static string Caption { get; } = "Caption";

    /// <summary>Gets the name of the style of a quotation.</summary>
    public static string Quote { get; } = "Quote";

    /// <summary>Gets the name of the style for a heading level.</summary>
    /// <param name="level">The level, 1 to 6.</param>
    /// <returns>The style name, such as <c>Heading 1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is not from 1 to 6.</exception>
    public static string GetHeading(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, HeadingLevels);
        return $"Heading {level}";
    }
}
