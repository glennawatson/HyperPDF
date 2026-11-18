// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>
/// Finds the texts a page draws in pieces that are one text. A mark a font places with a vertical offset is drawn with its own text rise, which is its own show
/// of text; when those shows sit in one marked content run that carries replacement text, in the order they are drawn and each beginning where the one before
/// ended, they are the pieces of one text, and a page lists, moves, deletes and retypes them as one.
/// </summary>
internal static class TextPieces
{
    /// <summary>How far, as a multiple of the font size, the start of a piece may be from where the piece before it ended: a mark's offset, and nothing like a new line.</summary>
    private const float NearInSizes = 1.2F;

    /// <summary>The pieces after a text that is drawn in one piece: none. It is never added to.</summary>
    private static readonly List<PdfTextObject> None = [];

    /// <summary>Tells whether a text object is a piece that goes on from the object drawn before it, and so is not listed on its own.</summary>
    /// <param name="page">The page.</param>
    /// <param name="items">The objects of the page's content.</param>
    /// <param name="at">The place of the object.</param>
    /// <returns><see langword="true"/> when the object continues the one before.</returns>
    internal static bool IsFollower(PdfPage page, ReadOnlySpan<PdfPageObject> items, int at) => at > 0 && at < items.Length && Continues(page, items[at - 1], items[at]);

    /// <summary>Gets the text objects that follow a text object as pieces of it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="items">The objects of the page's content.</param>
    /// <param name="leader">The first piece.</param>
    /// <returns>The pieces after the first, in the order they are drawn; none when the text is not in pieces.</returns>
    internal static List<PdfTextObject> Followers(PdfPage page, ReadOnlySpan<PdfPageObject> items, PdfPageObject leader)
    {
        var first = IndexOf(items, leader);
        if (first < 0 || !IsFollower(page, items, first + 1))
        {
            // Nearly every text is drawn in one piece, so none is made for it.
            return None;
        }

        var followers = new List<PdfTextObject>();
        for (var at = first; at >= 0 && at + 1 < items.Length && Continues(page, items[at], items[at + 1]); at++)
        {
            followers.Add((PdfTextObject)items[at + 1]);
        }

        return followers;
    }

    /// <summary>Gets the replacement text of the marked content run a text drawn in pieces is in, which is its words in the order they were typed.</summary>
    /// <param name="leader">The first piece.</param>
    /// <returns>The words; <see langword="null"/> when the run has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string? ReplacementText(PdfPageObject leader) => ReplacementMark(leader)?.Properties?.GetText(KnownName.ActualText)?.Trim();

    /// <summary>Tells whether an object goes on from the object before it as a piece of the same text.</summary>
    /// <param name="page">The page.</param>
    /// <param name="before">The object drawn before.</param>
    /// <param name="next">The object.</param>
    /// <returns><see langword="true"/> when both are text in one marked content run with replacement text, and the next begins right where the first ended.</returns>
    private static bool Continues(PdfPage page, PdfPageObject before, PdfPageObject next)
    {
        if (before is not PdfTextObject { GlyphCount: > 0 } first || next is not PdfTextObject { GlyphCount: > 0 } second || first.IsDeleted || second.IsDeleted)
        {
            return false;
        }

        var mark = ReplacementMark(first);
        return mark is not null && ReferenceEquals(mark, ReplacementMark(second)) && Gap(page, first, second) <= second.FontSize * NearInSizes;
    }

    /// <summary>Finds the marked content run of an object that carries replacement text.</summary>
    /// <param name="item">The object.</param>
    /// <returns>The innermost such run; <see langword="null"/> when the object is in none.</returns>
    private static PdfMark? ReplacementMark(PdfPageObject item)
    {
        var marks = item.Marks;
        for (var i = marks.Count - 1; i >= 0; i--)
        {
            if (marks[i].Properties is { } properties && properties.ContainsKey(KnownName.ActualText))
            {
                return marks[i];
            }
        }

        return null;
    }

    /// <summary>Measures how far the start of a text is from where the text before it ended, as the page shows them.</summary>
    /// <param name="page">The page.</param>
    /// <param name="first">The text before.</param>
    /// <param name="second">The text.</param>
    /// <returns>The distance, in page units.</returns>
    private static float Gap(PdfPage page, PdfTextObject first, PdfTextObject second)
    {
        var matrix = first.TextMatrix * first.Matrix;
        var along = new Vector2(matrix.M11, matrix.M12);
        var last = first.Glyphs[^1];
        var ended = page.ToPhysical(last.Origin + (along * last.Advance));
        return (page.ToPhysical(second.Glyphs[0].Origin) - ended).Length();
    }

    /// <summary>Finds the place of an object among the page's objects.</summary>
    /// <param name="items">The objects.</param>
    /// <param name="item">The object.</param>
    /// <returns>The place; -1 when it is not there.</returns>
    private static int IndexOf(ReadOnlySpan<PdfPageObject> items, PdfPageObject item)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
