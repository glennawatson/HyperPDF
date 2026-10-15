// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Creation;

/// <summary>Wraps and draws <see cref="PdfFlowCodeBlock"/> elements in page flow and inside table cells.</summary>
internal static class PdfCodeFlow
{
    /// <summary>One half, for centring lines vertically within their leading.</summary>
    private const float Half = 0.5F;

    /// <summary>The two sides (top and bottom, or left and right) that receive padding.</summary>
    private const float PaddingSides = 2F;

    /// <summary>The characters in a carriage return followed by a line feed.</summary>
    private const int CrLfLength = 2;

    /// <summary>Writes a code block into a page flow, paginating across page boundaries without clipping lines.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="block">The code block.</param>
    /// <exception cref="ArgumentOutOfRangeException">The available width inside the code block is zero or negative.</exception>
    internal static void Write(ref FlowRun run, PdfFlowCodeBlock block)
    {
        Validate(block);
        var innerWidth = run.TextWidth - block.Indent - (block.Padding * PaddingSides);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(innerWidth, 0F);

        var font = block.Font ?? run.Options.BodyFont;
        var expanded = ExpandTabs(block.Code, block.TabSpaces);
        var lines = default(PooledLines);
        try
        {
            WrapCode(font, block.FontSize, expanded, innerWidth, block.Overflow, ref lines);
            var element = run.Creator.Tags is { } tags ? tags.AddElement(tags.Root, "Code") : null;
            WritePages(ref run, block, expanded, ref lines, element);
        }
        finally
        {
            lines.Dispose();
        }
    }

    /// <summary>Validates a code block's configuration.</summary>
    /// <param name="block">The code block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> or its code string is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric option is out of range.</exception>
    internal static void Validate(PdfFlowCodeBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(block.Code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(block.FontSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(block.LineSpacing);
        ArgumentOutOfRangeException.ThrowIfNegative(block.Padding);
        ArgumentOutOfRangeException.ThrowIfNegative(block.Indent);
        ArgumentOutOfRangeException.ThrowIfNegative(block.TabSpaces);
    }

    /// <summary>Expands tab characters into spaces when any tab is present.</summary>
    /// <param name="code">The source text.</param>
    /// <param name="tabSpaces">The spaces per tab stop.</param>
    /// <returns>The expanded string, or <paramref name="code"/> when no tab is present.</returns>
    internal static string ExpandTabs(string code, int tabSpaces)
    {
        if (!code.Contains('\t', StringComparison.Ordinal))
        {
            return code;
        }

        var builder = new StringBuilder(code.Length + tabSpaces);
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == '\t')
            {
                _ = builder.Append(' ', tabSpaces);
            }
            else
            {
                _ = builder.Append(code[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Breaks preformatted code into lines, preserving leading whitespace and explicit line breaks.</summary>
    /// <typeparam name="TSink">The line sink type.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size in points.</param>
    /// <param name="code">The tab-expanded code string.</param>
    /// <param name="maxWidth">The available inner width in points.</param>
    /// <param name="overflow">How overlong lines are handled.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="maxWidth"/> is not positive.</exception>
    internal static void WrapCode<TSink>(PdfCanvasFont font, float size, ReadOnlySpan<char> code, float maxWidth, PdfCodeOverflow overflow, ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
        var start = 0;
        while (start <= code.Length)
        {
            var breakOffset = code[start..].IndexOfAny('\r', '\n');
            var end = breakOffset < 0 ? code.Length : start + breakOffset;
            WrapSourceLine(font, size, code[start..end], start, maxWidth, overflow, ref lines);
            if (end == code.Length)
            {
                break;
            }

            start = SkipLineBreak(code, end);
        }
    }

    /// <summary>Draws the background and border of a code block slice.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="block">The code block.</param>
    /// <param name="left">The box left x.</param>
    /// <param name="bottom">The box bottom y.</param>
    /// <param name="width">The box width.</param>
    /// <param name="height">The box height.</param>
    /// <param name="inArtifact">Whether an outer artifact is already open.</param>
    internal static void DrawBox(ref FlowRun run, PdfFlowCodeBlock block, float left, float bottom, float width, float height, bool inArtifact)
    {
        if (block.BackgroundColor is null && block.Border is null)
        {
            return;
        }

        run.CloseContent();
        var wrapArtifact = run.Creator.Tags is not null && !inArtifact;
        if (wrapArtifact)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
        }

        if (block.BackgroundColor is { } bg)
        {
            run.Canvas.FillRectangle(bg.Red, bg.Green, bg.Blue, left, bottom, width, height);
        }

        if (block.Border is { } stroke)
        {
            run.Canvas.StrokeRectangle(stroke, left, bottom, width, height);
        }

        if (wrapArtifact)
        {
            run.Canvas.EndArtifact();
        }
    }

    /// <summary>Paginates and draws a wrapped code block across one or more pages.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="block">The code block.</param>
    /// <param name="expanded">The tab-expanded code string.</param>
    /// <param name="lines">The wrapped code lines.</param>
    /// <param name="element">The structure element, or <see langword="null"/>.</param>
    private static void WritePages(
        ref FlowRun run,
        PdfFlowCodeBlock block,
        string expanded,
        ref PooledLines lines,
        PdfTagElement? element)
    {
        var inset = block.Padding * PaddingSides;
        var leading = block.FontSize * block.LineSpacing;
        var total = Math.Max(lines.Count, 1);
        _ = run.Reserve(Math.Min(inset + leading, run.TextHeight));
        run.AddSpaceBefore(block.FontSize);
        var done = 0;
        while (done < total)
        {
            var fit = FitLines(run.Top - run.Options.Margin, inset, leading);
            var remaining = total - done;
            if (fit < remaining && run.Top < run.PageTop && (fit < 1 || (done == 0 && remaining <= FitLines(run.TextHeight, inset, leading))))
            {
                run.StartNewPage();
                continue;
            }

            var take = Math.Clamp(fit, 1, remaining);
            DrawSlice(ref run, block, expanded, ref lines, element, done, take);
            done += take;
            if (done < total)
            {
                run.StartNewPage();
            }
        }

        run.CloseContent();
        run.AddSpaceAfter(block.FontSize);
    }

    /// <summary>Draws one page's slice of a code block.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="block">The code block.</param>
    /// <param name="expanded">The tab-expanded code string.</param>
    /// <param name="lines">The wrapped code lines.</param>
    /// <param name="element">The structure element, or <see langword="null"/>.</param>
    /// <param name="done">The index of the first line to draw.</param>
    /// <param name="take">The number of lines to draw on this page.</param>
    private static void DrawSlice(
        ref FlowRun run,
        PdfFlowCodeBlock block,
        string expanded,
        ref PooledLines lines,
        PdfTagElement? element,
        int done,
        int take)
    {
        var font = block.Font ?? run.Options.BodyFont;
        var size = block.FontSize;
        var leading = size * block.LineSpacing;
        var boxLeft = run.Options.Margin + block.Indent;
        var boxWidth = run.TextWidth - block.Indent;
        var boxHeight = (block.Padding * PaddingSides) + (take * leading);
        DrawBox(ref run, block, boxLeft, run.Top - boxHeight, boxWidth, boxHeight, inArtifact: false);

        var ascent = font.GetAscent(size);
        var offset = ((leading - (ascent - font.GetDescent(size))) * Half) + ascent;
        var lineTop = run.Top - block.Padding;
        var textX = boxLeft + block.Padding;
        run.OpenContent(element);
        for (var i = done; i < done + take && i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length > 0)
            {
                run.Canvas.DrawText(font, size, block.TextColor, textX, lineTop - offset, expanded.AsSpan(line.Start, line.Length));
            }

            lineTop -= leading;
        }

        run.CloseContent();
        run.Top -= boxHeight;
    }

    /// <summary>Counts how many code lines fit in an available vertical height.</summary>
    /// <param name="available">The vertical space in points.</param>
    /// <param name="inset">The top and bottom padding together.</param>
    /// <param name="leading">The line pitch in points.</param>
    /// <returns>The number of lines that fit.</returns>
    private static int FitLines(float available, float inset, float leading) =>
        (int)Math.Max(0F, (available - inset) / leading);

    /// <summary>Skips a single line break sequence.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The break index.</param>
    /// <returns>The index after the break.</returns>
    private static int SkipLineBreak(ReadOnlySpan<char> text, int position) =>
        text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n' ? position + CrLfLength : position + 1;

    /// <summary>Wraps or clips one source line of code.</summary>
    /// <typeparam name="TSink">The line sink type.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="sourceLine">The source line slice without line breaks.</param>
    /// <param name="lineStart">The offset of <paramref name="sourceLine"/> in the full code string.</param>
    /// <param name="maxWidth">The available inner width.</param>
    /// <param name="overflow">The overflow mode.</param>
    /// <param name="lines">Receives the wrapped or clipped lines.</param>
    private static void WrapSourceLine<TSink>(
        PdfCanvasFont font,
        float size,
        ReadOnlySpan<char> sourceLine,
        int lineStart,
        float maxWidth,
        PdfCodeOverflow overflow,
        ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        if (sourceLine.IsEmpty)
        {
            lines.Add(new(lineStart, 0, 0F));
            return;
        }

        var fullWidth = font.Measure(sourceLine, size);
        if (fullWidth <= maxWidth)
        {
            lines.Add(new(lineStart, sourceLine.Length, fullWidth));
            return;
        }

        if (overflow == PdfCodeOverflow.Clip)
        {
            var clippedLen = FindFittingPrefix(font, size, sourceLine, maxWidth, out var clippedWidth);
            lines.Add(new(lineStart, clippedLen, clippedWidth));
            return;
        }

        WrapOverlongSourceLine(font, size, sourceLine, lineStart, maxWidth, overflow, ref lines);
    }

    /// <summary>Wraps a source line that exceeds <paramref name="maxWidth"/> across multiple lines.</summary>
    /// <typeparam name="TSink">The line sink type.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="sourceLine">The source line slice.</param>
    /// <param name="lineStart">The offset of <paramref name="sourceLine"/> in the full code string.</param>
    /// <param name="maxWidth">The available inner width.</param>
    /// <param name="overflow">The overflow mode.</param>
    /// <param name="lines">Receives the wrapped lines.</param>
    private static void WrapOverlongSourceLine<TSink>(
        PdfCanvasFont font,
        float size,
        ReadOnlySpan<char> sourceLine,
        int lineStart,
        float maxWidth,
        PdfCodeOverflow overflow,
        ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        var step = 0;
        for (var cursor = 0; cursor < sourceLine.Length; cursor += step)
        {
            var remaining = sourceLine[cursor..];
            var fitLen = FindFittingPrefix(font, size, remaining, maxWidth, out var fitWidth);
            if (fitLen < remaining.Length && overflow == PdfCodeOverflow.Wrap)
            {
                var tokenBreak = FindTokenBreak(remaining, fitLen);
                if (tokenBreak > 0)
                {
                    fitLen = tokenBreak;
                    fitWidth = font.Measure(remaining[..fitLen], size);
                }
            }

            lines.Add(new(lineStart + cursor, fitLen, fitWidth));
            step = overflow == PdfCodeOverflow.Wrap && fitLen < remaining.Length && remaining[fitLen] == ' ' ? fitLen + 1 : fitLen;
        }
    }

    /// <summary>Finds the longest prefix of text elements that fits within <paramref name="maxWidth"/>.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text slice.</param>
    /// <param name="maxWidth">The maximum width.</param>
    /// <param name="width">Receives the width of the prefix.</param>
    /// <returns>The length of the prefix in characters (at least one text element).</returns>
    private static int FindFittingPrefix(PdfCanvasFont font, float size, ReadOnlySpan<char> text, float maxWidth, out float width)
    {
        var bestEnd = 0;
        width = 0F;
        var offset = 0;
        while (offset < text.Length)
        {
            var next = offset + StringInfo.GetNextTextElementLength(text[offset..]);
            var candidate = font.Measure(text[..next], size);
            if (candidate > maxWidth && bestEnd > 0)
            {
                return bestEnd;
            }

            bestEnd = next;
            width = candidate;
            offset = next;
            if (candidate > maxWidth)
            {
                return bestEnd;
            }
        }

        return bestEnd;
    }

    /// <summary>Finds a token boundary (after leading indentation) to break a wrapped code line at.</summary>
    /// <param name="line">The line slice starting at the current cursor.</param>
    /// <param name="fitLen">The furthest character offset that fits.</param>
    /// <returns>The break offset relative to <paramref name="line"/>, or zero when no token boundary exists after leading spaces.</returns>
    private static int FindTokenBreak(ReadOnlySpan<char> line, int fitLen)
    {
        var nonSpace = 0;
        while (nonSpace < fitLen && line[nonSpace] == ' ')
        {
            nonSpace++;
        }

        for (var i = fitLen - 1; i > nonSpace; i--)
        {
            if (line[i] == ' ')
            {
                return i;
            }
        }

        return 0;
    }
}
