// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// A bulleted or numbered list. A tagged document marks it up as a list with items, labels and bodies, so a screen reader
/// announces the number of items.
/// </summary>
/// <param name="Kind">Whether items start with a bullet or a number.</param>
/// <param name="Items">The items.</param>
[DebuggerDisplay("PdfList: {Kind}, {Items.Count} items")]
public sealed record PdfList(PdfListKind Kind, IReadOnlyList<PdfListItem> Items) : PdfBlock
{
    /// <summary>Gets how numbers are written in a numbered list.</summary>
    public PdfNumberFormat NumberFormat { get; init; }

    /// <summary>Gets the number of the first item in a numbered list.</summary>
    public int Start { get; init; } = 1;

    /// <summary>Gets the text after each number in a numbered list.</summary>
    public string NumberSuffix { get; init; } = ".";

    /// <summary>Gets the bullet in a bulleted list.</summary>
    public string Bullet { get; init; } = "•";

    /// <summary>Gets how far in points the items are indented from the list's own indent, which leaves room for the bullet or number.</summary>
    public float Indent { get; init; } = 22F;

    /// <summary>Gets the name of the paragraph style of the items, or <see langword="null"/> for the list styles.</summary>
    public string? Style { get; init; }

    /// <summary>Makes a bulleted list of plain text items.</summary>
    /// <param name="items">The text of each item.</param>
    /// <returns>The list.</returns>
    public static PdfList Bulleted(params string[] items) => new(PdfListKind.Bullet, ToItems(items));

    /// <summary>Makes a numbered list of plain text items.</summary>
    /// <param name="items">The text of each item.</param>
    /// <returns>The list.</returns>
    public static PdfList Numbering(params string[] items) => new(PdfListKind.Numbered, ToItems(items));

    /// <summary>Makes list items from text.</summary>
    /// <param name="items">The text of each item.</param>
    /// <returns>The items.</returns>
    private static PdfListItem[] ToItems(string[] items)
    {
        var result = new PdfListItem[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            result[i] = PdfListItem.Of(items[i]);
        }

        return result;
    }
}
