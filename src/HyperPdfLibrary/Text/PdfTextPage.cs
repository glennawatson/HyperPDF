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
/// The text of one page in reading order, with each character's box, matching PDFium's text page so character indexes
/// line up with it: generated spaces and line breaks, joined hyphens, /ActualText and right-to-left runs in logical
/// order. Coordinates are in user space; <see cref="PdfPage.ToViewerRectangle"/> maps them to viewer space. The page is
/// immutable and safe to use from many threads; queries allocate nothing beyond what the caller asks for.
/// </summary>
[DebuggerDisplay("PdfTextPage: page {Page.Index}, {CharCount} chars")]
public sealed partial class PdfTextPage
{
    /// <summary>The smallest width or height of a box that counts towards rectangles.</summary>
    private const float SizeEpsilon = 0.01F;

    /// <summary>The characters.</summary>
    private readonly PdfTextChar[] _chars;

    /// <summary>Bounds of contiguous character blocks on larger pages.</summary>
    private readonly TextHitBlock[] _hitBlocks;

    /// <summary>The run each character came from, or -1, which groups characters into rectangles.</summary>
    private readonly int[] _runs;

    /// <summary>The page text: one entry for every character that has text.</summary>
    private readonly string _text;

    /// <summary>The runs of characters that have text, mapping character indexes to text indexes.</summary>
    private readonly TextSegment[] _segments;

    /// <summary>Initializes a new instance of the <see cref="PdfTextPage"/> class.</summary>
    /// <param name="page">The page.</param>
    /// <param name="chars">The characters.</param>
    /// <param name="runs">The run of each character.</param>
    /// <param name="text">The page text.</param>
    /// <param name="segments">The runs of characters that have text.</param>
    internal PdfTextPage(PdfPage page, PdfTextChar[] chars, int[] runs, string text, TextSegment[] segments)
    {
        Page = page;
        _chars = chars;
        _hitBlocks = TextHitTesting.BuildBlocks(chars);
        _runs = runs;
        _text = text;
        _segments = segments;
    }

    /// <summary>Gets the page.</summary>
    public PdfPage Page { get; }

    /// <summary>Gets the number of characters, including generated spaces and line breaks.</summary>
    public int CharCount => _chars.Length;

    /// <summary>Gets the characters.</summary>
    public ReadOnlySpan<PdfTextChar> Chars => _chars;

    /// <summary>Gets the whole page text, which leaves out characters that have none, such as unmapped control codes.</summary>
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
    public int CharIndexFromTextIndex(int textIndex)
    {
        var count = 0;
        foreach (var segment in _segments)
        {
            count += segment.Count;
            if (count > textIndex)
            {
                return textIndex - count + segment.Count + segment.Start;
            }
        }

        return -1;
    }

    /// <summary>Maps a character index to an index in <see cref="Text"/>.</summary>
    /// <param name="charIndex">The character index.</param>
    /// <returns>The text index, or -1 for a character without text.</returns>
    public int TextIndexFromCharIndex(int charIndex)
    {
        var count = 0;
        foreach (var segment in _segments)
        {
            var textIndex = charIndex - segment.Start;
            if (textIndex < segment.Count)
            {
                return textIndex >= 0 ? textIndex + count : -1;
            }

            count += segment.Count;
        }

        return -1;
    }

    /// <summary>Gets the text of a run of characters without copying it, as PDFium's FPDFText_GetText gives it.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text; empty when the range has none.</returns>
    public ReadOnlySpan<char> GetTextSpan(int start, int count)
    {
        var length = _chars.Length;
        if (start < 0 || start >= length || count <= 0 || _text.Length == 0)
        {
            return default;
        }

        var textStart = TextIndexFromCharIndex(start);
        while (textStart < 0)
        {
            if (start >= length)
            {
                return default;
            }

            start++;
            textStart = TextIndexFromCharIndex(start);
        }

        count = Math.Min(count, length - start);
        var last = start + count - 1;
        var textLast = TextIndexFromCharIndex(last);
        while (textLast < 0)
        {
            // PDFium compares the character index with the text index here; kept for identical results.
            if (last < textStart)
            {
                return default;
            }

            last--;
            textLast = TextIndexFromCharIndex(last);
        }

        return textLast < textStart ? default : _text.AsSpan(textStart, textLast - textStart + 1);
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
    /// Appends the rectangles covering a run of characters: one per text run, joining its characters' outline boxes, as
    /// PDFium's FPDFText_CountRects gives them.
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
        var length = _chars.Length;
        if (start < 0 || count == 0 || start >= length)
        {
            return;
        }

        if (count < 0 || start + count > length)
        {
            count = length - start;
        }

        var run = int.MinValue;
        var rect = default(PdfRectangle);
        for (var i = start; i < start + count; i++)
        {
            if (!CountsTowardsRects(_chars[i]))
            {
                continue;
            }

            var box = _chars[i].Box;
            if (run == _runs[i])
            {
                rect = TextGeometry.Union(rect, box);
                continue;
            }

            if (run != int.MinValue)
            {
                visit(state, rect);
            }

            run = _runs[i];
            rect = TextGeometry.Normalize(box);
        }

        visit(state, rect);
    }

    /// <summary>Finds the character at a point, or the nearest one within a tolerance, as PDFium's FPDFText_GetCharIndexAtPos does.</summary>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>The character index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndexAtPosition(Vector2 point, float toleranceX, float toleranceY) =>
        TextHitTesting.Find(_chars, _hitBlocks, point, toleranceX, toleranceY);

    /// <summary>Determines whether a character's box is part of the rectangles covering a range.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> for shown characters with an area.</returns>
    private static bool CountsTowardsRects(in PdfTextChar info) =>
        info.Kind != PdfTextCharKind.Generated && info.Box.Width >= SizeEpsilon && info.Box.Height >= SizeEpsilon;
}
