// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// The text of one page in reading order, with each character's box. Characters include the spaces and line breaks the
/// extractor generated, the characters of /ActualText and decomposed ligatures. Text is in logical order, also for
/// right-to-left lines, and boxes are where the glyphs are drawn. A character may have no text: unmapped glyphs and the
/// hyphen of a split word occupy a character slot but add nothing to <see cref="Text"/>. Coordinates are in user space;
/// <see cref="PdfPage.ToViewerRectangle"/> maps them to viewer space. The page is immutable and safe to use from many
/// threads; queries allocate nothing beyond what the caller asks for.
/// </summary>
[DebuggerDisplay("PdfTextPage: page {Page.Index}, {CharCount} chars")]
public sealed class PdfTextPage
{
    /// <summary>The smallest width or height of a box that counts towards rectangles.</summary>
    private const float SizeEpsilon = 0.01F;

    /// <summary>The number of distinct combinations of the folding options.</summary>
    private const int FoldKeys = 8;

    /// <summary>The characters.</summary>
    private readonly PdfTextChar[] _chars;

    /// <summary>Bounds of contiguous character blocks on larger pages.</summary>
    private readonly TextHitBlock[] _hitBlocks;

    /// <summary>The line each character is on, or -1 for line breaks; characters of one line share one rectangle.</summary>
    private readonly int[] _lines;

    /// <summary>The page text.</summary>
    private readonly string _text;

    /// <summary>The character index of each text index.</summary>
    private readonly int[] _charOfText;

    /// <summary>The text index of each character, or -1 for a character without text.</summary>
    private readonly int[] _textOfChar;

    /// <summary>The page text folded for each combination of folding options, made on first use.</summary>
    private readonly FoldedText?[] _folds = new FoldedText?[FoldKeys];

    /// <summary>The web links, found on first use.</summary>
    private PdfWebLink[]? _webLinks;

    /// <summary>Initializes a new instance of the <see cref="PdfTextPage"/> class.</summary>
    /// <param name="page">The page.</param>
    /// <param name="chars">The characters.</param>
    /// <param name="lines">The line of each character.</param>
    /// <param name="text">The page text.</param>
    /// <param name="charOfText">The character index of each text index.</param>
    internal PdfTextPage(PdfPage page, PdfTextChar[] chars, int[] lines, string text, int[] charOfText)
    {
        Page = page;
        _chars = chars;
        _hitBlocks = TextHitTesting.BuildBlocks(chars);
        _lines = lines;
        _text = text;
        _charOfText = charOfText;
        _textOfChar = new int[chars.Length];
        Array.Fill(_textOfChar, -1);
        for (var i = 0; i < charOfText.Length; i++)
        {
            _textOfChar[charOfText[i]] = i;
        }
    }

    /// <summary>Gets the page.</summary>
    public PdfPage Page { get; }

    /// <summary>Gets the number of characters, including generated spaces and line breaks.</summary>
    public int CharCount => _chars.Length;

    /// <summary>Gets the characters.</summary>
    public ReadOnlySpan<PdfTextChar> Chars => _chars;

    /// <summary>Gets the whole page text, which leaves out characters that have none.</summary>
    public string Text => _text;

    /// <summary>Gets a character.</summary>
    /// <param name="index">The character index.</param>
    /// <returns>The character.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the page's characters.</exception>
    public PdfTextChar GetChar(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _chars.Length);
        return _chars[index];
    }

    /// <summary>Maps an index in <see cref="Text"/> to a character index.</summary>
    /// <param name="textIndex">The text index.</param>
    /// <returns>The character index, or -1.</returns>
    public int CharIndexFromTextIndex(int textIndex) => (uint)textIndex < (uint)_charOfText.Length ? _charOfText[textIndex] : -1;

    /// <summary>Maps a character index to an index in <see cref="Text"/>.</summary>
    /// <param name="charIndex">The character index.</param>
    /// <returns>The text index, or -1 for a character without text.</returns>
    public int TextIndexFromCharIndex(int charIndex) => (uint)charIndex < (uint)_textOfChar.Length ? _textOfChar[charIndex] : -1;

    /// <summary>Gets the text of a run of characters without copying it.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text of the characters in the range that have any; empty when none do.</returns>
    public ReadOnlySpan<char> GetTextSpan(int start, int count)
    {
        if (start < 0 || start >= _chars.Length || count <= 0 || _text.Length == 0)
        {
            return default;
        }

        var end = (int)Math.Min((long)start + count, _chars.Length);
        var first = start;
        while (first < end && _textOfChar[first] < 0)
        {
            first++;
        }

        if (first >= end)
        {
            return default;
        }

        var last = LastWithText(first, end);
        return _text.AsSpan(_textOfChar[first], _textOfChar[last] - _textOfChar[first] + 1);
    }

    /// <summary>Gets the text of a run of characters.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text; empty when the range has none.</returns>
    public string GetText(int start, int count)
    {
        var text = GetTextSpan(start, count);
        return text.IsEmpty ? string.Empty : text.ToString();
    }

    /// <summary>
    /// Appends the rectangles covering a run of characters: one per line, joining the boxes of the line's shown characters.
    /// A line that is not axis aligned gets the rectangle that bounds its boxes.
    /// </summary>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters; a negative count runs to the end of the page.</param>
    /// <param name="output">The list receiving the rectangles, in user space.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    public void GetRects(int start, int count, List<PdfRectangle> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GetRects(start, count, output, static (rects, rect) => rects.Add(rect));
    }

    /// <summary>Visits the rectangles covering a run of characters without an intermediate list.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters; a negative count runs to the end of the page.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives each rectangle in user space.</param>
    public void GetRects<TState>(int start, int count, TState state, Action<TState, PdfRectangle> visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        if (start < 0 || count == 0 || start >= _chars.Length)
        {
            return;
        }

        count = count < 0 || start + count > _chars.Length ? _chars.Length - start : count;
        var line = int.MinValue;
        var rect = default(PdfRectangle);
        for (var i = start; i < start + count; i++)
        {
            if (!CountsTowardsRects(_chars[i]))
            {
                continue;
            }

            var box = _chars[i].Box;
            if (line == _lines[i])
            {
                rect = TextGeometry.Union(rect, box);
                continue;
            }

            VisitRect(line, rect, state, visit);
            line = _lines[i];
            rect = TextGeometry.Normalize(box);
        }

        VisitRect(line, rect, state, visit);
    }

    /// <summary>Finds the character at a point, or the nearest one within a tolerance.</summary>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">How far the point may be from a character's box horizontally.</param>
    /// <param name="toleranceY">How far the point may be from a character's box vertically.</param>
    /// <returns>The character index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndexAtPosition(Vector2 point, float toleranceX, float toleranceY) =>
        TextHitTesting.Find(_chars, _hitBlocks, point, toleranceX, toleranceY);

    /// <summary>Appends every match of a query, found from the start of the page.</summary>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    public void Find(ReadOnlySpan<char> query, PdfTextSearchOptions options, List<PdfTextMatch> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Find(query, options, output, static (matches, start, count) => matches.Add(new(start, count)));
    }

    /// <summary>Visits every match of a query without an intermediate list.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives each match's first character and length.</param>
    public void Find<TState>(ReadOnlySpan<char> query, PdfTextSearchOptions options, TState state, Action<TState, int, int> visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        if (query.IsEmpty || _text.Length == 0)
        {
            return;
        }

        var matches = new MatchVisit<TState>(this, state, visit);
        TextSearch.FindAll(FoldedFor(options), query, options, matches, static (visitor, first, last) => visitor.Report(first, last));
    }

    /// <summary>Gets the web and email addresses written on the page.</summary>
    /// <returns>The links, in page order.</returns>
    public IReadOnlyList<PdfWebLink> GetWebLinks()
    {
        if (Volatile.Read(ref _webLinks) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref _webLinks, TextLinkParser.Extract(this), null);
        return Volatile.Read(ref _webLinks)!;
    }

    /// <summary>Appends the rectangles covering a web link, in user space.</summary>
    /// <param name="link">The link.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> or <paramref name="output"/> is null.</exception>
    public void GetWebLinkRects(PdfWebLink link, List<PdfRectangle> output)
    {
        ArgumentNullException.ThrowIfNull(link);
        GetRects(link.Start, link.Count, output);
    }

    /// <summary>Visits a finished line rectangle, unless no line has started.</summary>
    /// <typeparam name="TState">The type of the caller's state.</typeparam>
    /// <param name="line">The line of the rectangle, or the not-started marker.</param>
    /// <param name="rect">The rectangle.</param>
    /// <param name="state">The state handed to the visit.</param>
    /// <param name="visit">The caller's method receiving the rectangle.</param>
    private static void VisitRect<TState>(int line, in PdfRectangle rect, TState state, Action<TState, PdfRectangle> visit)
    {
        if (line != int.MinValue)
        {
            visit(state, rect);
        }
    }

    /// <summary>Determines whether a character's box is part of the rectangles covering a range.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> for shown characters with an area.</returns>
    private static bool CountsTowardsRects(in PdfTextChar info) =>
        info.Kind != PdfTextCharKind.Generated && info.Box.Width >= SizeEpsilon && info.Box.Height >= SizeEpsilon;

    /// <summary>Finds the last character of a range that has text.</summary>
    /// <param name="first">A character of the range that has text.</param>
    /// <param name="end">The index after the range.</param>
    /// <returns>The index.</returns>
    private int LastWithText(int first, int end)
    {
        var last = end - 1;
        while (last > first && _textOfChar[last] < 0)
        {
            last--;
        }

        return last;
    }

    /// <summary>Gets the page text folded for a set of options, making it once.</summary>
    /// <param name="options">The search options.</param>
    /// <returns>The folded text.</returns>
    private FoldedText FoldedFor(PdfTextSearchOptions options)
    {
        var folding = options & TextFolding.FoldOptions;
        var key = (((int)folding >> 1) & 1) | ((((int)folding >> 2) & 1) << 1) | ((((int)folding >> 4) & 1) << 2);
        if (Volatile.Read(ref _folds[key]) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref _folds[key], TextFolding.Fold(_text, folding), null);
        return Volatile.Read(ref _folds[key])!;
    }

    /// <summary>Turns text index matches into character matches for the caller.</summary>
    /// <typeparam name="TState">The type of the caller's state.</typeparam>
    /// <param name="Page">The page whose indexes are mapped.</param>
    /// <param name="State">The state handed back to the caller.</param>
    /// <param name="Visit">The caller's method receiving each match.</param>
    private readonly record struct MatchVisit<TState>(PdfTextPage Page, TState State, Action<TState, int, int> Visit)
    {
        /// <summary>Reports a match.</summary>
        /// <param name="first">The text index of the first character.</param>
        /// <param name="last">The text index of the last character.</param>
        internal void Report(int first, int last)
        {
            var start = Page.CharIndexFromTextIndex(first);
            Visit(State, start, Page.CharIndexFromTextIndex(last) - start + 1);
        }
    }
}
