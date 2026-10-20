// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdf.Engine;

/// <summary>Implements Text over the document's owned state.</summary>
internal static class HyperPdfText
{
    /// <summary>The maximum loose-box scale relative to text size.</summary>
    internal const float LooseBoxToSize = 1.15F;

    /// <summary>The scale ratio used to identify enlarged text.</summary>
    internal const float ScaledTextRatio = 1.5F;

    /// <summary>Gets the number of text characters on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The character count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetCharacterCount(HyperPdfDocument self, int pageIndex) => GetCharacterCountNative(self, pageIndex);

    /// <summary>Gets the index of the character at a point.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="point">The point in page space.</param>
    /// <param name="tolerance">The hit tolerance in points.</param>
    /// <returns>The character index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetCharacterIndexAt(HyperPdfDocument self, int pageIndex, PagePoint point, float tolerance) => GetCharacterIndexAtNative(self, pageIndex, point, tolerance);

    /// <summary>Gets the text of a run of characters.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetText(HyperPdfDocument self, int pageIndex, int start, int count) => GetTextNative(self, pageIndex, start, count);

    /// <summary>Appends the line rectangles covering a run of characters.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetTextBounds(HyperPdfDocument self, int pageIndex, int start, int count, List<PageRect> output) => GetTextBoundsNative(self, pageIndex, start, count, output);

    /// <summary>Finds every occurrence of text on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Find(HyperPdfDocument self, int pageIndex, string query, SearchOptions options, List<TextMatch> output) => FindNative(self, pageIndex, query, options, output);

    /// <summary>Gets the number of characters on a page, as <see cref="HyperPdfDocument.GetCharacterCount"/> will.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The character count; 0 for a missing page.</returns>
    internal static int GetCharacterCountNative(HyperPdfDocument self, int pageIndex) => TextPage(self, pageIndex)?.CharCount ?? 0;

    /// <summary>Gets the index of the character at a point, as <see cref="HyperPdfDocument.GetCharacterIndexAt"/> will.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="point">The point in viewer page space.</param>
    /// <param name="tolerance">The hit tolerance in points.</param>
    /// <returns>The character index, or -1.</returns>
    internal static int GetCharacterIndexAtNative(HyperPdfDocument self, int pageIndex, PagePoint point, float tolerance)
    {
        if (TextPage(self, pageIndex) is not { } text)
        {
            return -1;
        }

        // The point and tolerance are physical points; the text page measures in user space units.
        var user = text.Page.FromPhysical(new(point.X, point.Y));
        var userTolerance = tolerance / text.Page.UserUnit;
        var hit = Math.Max(text.GetIndexAtPosition(user, userTolerance, userTolerance), -1);

        // A point on a vowel sign or an accent is on its letter: a selection starts at the beginning of the cluster.
        return hit < 0 ? hit : text.GetClusterStart(hit);
    }

    /// <summary>Gets the text of a run of characters, as <see cref="HyperPdfDocument.GetText"/> will.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text.</returns>
    internal static string GetTextNative(HyperPdfDocument self, int pageIndex, int start, int count)
    {
        if (count <= 0 || start < 0 || TextPage(self, pageIndex) is not { } text)
        {
            return string.Empty;
        }

        // A selection that ends inside a cluster still takes the whole cluster.
        var range = text.GetClusterRange(start, count);
        return text.GetText(range.Start, range.Count);
    }

    /// <summary>Appends the line rectangles covering a run of characters in viewer space, as <see cref="HyperPdfDocument.GetTextBounds"/> will.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    internal static void GetTextBoundsNative(HyperPdfDocument self, int pageIndex, int start, int count, List<PageRect> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (count <= 0 || TextPage(self, pageIndex) is not { } text)
        {
            return;
        }

        var range = text.GetClusterRange(start, count);
        text.GetRects(
            range.Start,
            range.Count,
            new RectOutput(text, output),
            static (state, rect) => state.Output.Add(LinkTargets.ToPageRect(state.Text.Page.ToPhysicalRectangle(rect))));
    }

    /// <summary>Finds every occurrence of text on a page, as <see cref="HyperPdfDocument.Find"/> will.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    internal static void FindNative(HyperPdfDocument self, int pageIndex, string query, SearchOptions options, List<TextMatch> output)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (query.Length == 0 || self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return;
        }

        // Pages without a match never get a text page: only a page that is already cached, or that a later bounds call builds, does.
        PdfDocumentText.FindInPage(
            self.Document,
            pageIndex,
            query,
            ToLibraryOptions(options),
            new MatchOutput(pageIndex, output),
            static (state, start, length) => state.Output.Add(new(state.PageIndex, start, length)),
            CancellationToken.None);
    }

    /// <summary>Appends every character of a page in viewer space.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the characters.</param>
    internal static void GetCharactersNative(HyperPdfDocument self, int pageIndex, List<PageCharacter> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (TextPage(self, pageIndex) is not { } text)
        {
            return;
        }

        _ = output.EnsureCapacity(output.Count + text.CharCount);
        foreach (var info in text.Chars)
        {
            var generated = info.IsGenerated;
            var bounds = generated ? default : CharacterBounds(text, info);
            output.Add(new(info.Unicode, bounds, EffectiveSize(info) * text.Page.UserUnit, !generated && info.IsBold, generated));
        }
    }

    /// <summary>Appends the web and email addresses written as text on a page, for <see cref="HyperPdfDocument.GetLinks"/>.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the links.</param>
    internal static void GetWebLinksNative(HyperPdfDocument self, int pageIndex, List<PageLink> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (TextPage(self, pageIndex) is not { } text)
        {
            return;
        }

        foreach (var link in text.GetWebLinks())
        {
            text.GetRects(
                link.Start,
                link.Count,
                new LinkOutput(text, output, LinkTarget.ForUri(link.Url)),
                static (state, rect) => state.Output.Add(new(
                    LinkTargets.ToPageRect(state.Text.Page.ToPhysicalRectangle(rect)),
                    state.Target)));
        }
    }

    /// <summary>Maps the viewer's search options to the library's.</summary>
    /// <param name="options">The viewer options.</param>
    /// <returns>The library options.</returns>
    internal static PdfTextSearchOptions ToLibraryOptions(SearchOptions options)
    {
        var result = PdfTextSearchOptions.None;
        if ((options & SearchOptions.MatchCase) != 0)
        {
            result |= PdfTextSearchOptions.MatchCase;
        }

        if ((options & SearchOptions.WholeWord) != 0)
        {
            result |= PdfTextSearchOptions.WholeWord;
        }

        if ((options & SearchOptions.IgnoreDiacritics) != 0)
        {
            result |= PdfTextSearchOptions.IgnoreDiacritics;
        }

        if ((options & SearchOptions.FoldCompatibility) != 0)
        {
            result |= PdfTextSearchOptions.FoldCompatibility;
        }

        return result;
    }

    /// <summary>
    /// Gets a character's box from its advance and the font's ascent and descent, so narrow letters keep their side
    /// bearings; the outline box is the fallback.
    /// </summary>
    /// <param name="text">The text page.</param>
    /// <param name="info">The character.</param>
    /// <returns>The box in viewer space.</returns>
    internal static PageRect CharacterBounds(PdfTextPage text, in PdfTextChar info)
    {
        var box = info.LooseBox.Right > info.LooseBox.Left ? info.LooseBox : info.Box;
        return LinkTargets.ToPageRect(text.Page.ToPhysicalRectangle(box));
    }

    /// <summary>Gets the size a character is drawn at: the box height when the text matrix scales a small font size up.</summary>
    /// <param name="info">The character.</param>
    /// <returns>The size in user space units.</returns>
    internal static float EffectiveSize(in PdfTextChar info)
    {
        if (info.IsGenerated)
        {
            return info.FontSize;
        }

        var drawn = info.LooseBox.Height / LooseBoxToSize;
        return drawn > info.FontSize * ScaledTextRatio ? drawn : info.FontSize;
    }

    /// <summary>Gets a page's text from the managed library.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The text page, or <see langword="null"/> when the document is closed or the page does not exist.</returns>
    internal static PdfTextPage? TextPage(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount ? null : PdfDocumentText.GetTextPage(self.Document, pageIndex);
    }

    /// <summary>The page and caller's list for rectangle conversion.</summary>
    /// <param name="Text">The text page.</param>
    /// <param name="Output">The converted rectangles.</param>
    private readonly record struct RectOutput(PdfTextPage Text, List<PageRect> Output);

    /// <summary>The page index and caller's list for match conversion.</summary>
    /// <param name="PageIndex">The page index.</param>
    /// <param name="Output">The converted matches.</param>
    private readonly record struct MatchOutput(int PageIndex, List<TextMatch> Output);

    /// <summary>The page, caller's list and target for link conversion.</summary>
    /// <param name="Text">The text page.</param>
    /// <param name="Output">The converted links.</param>
    /// <param name="Target">The link's target.</param>
    private readonly record struct LinkOutput(PdfTextPage Text, List<PageLink> Output, LinkTarget Target);
}
