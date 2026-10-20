// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>
/// Works out the order a person reads a page in, from where its characters are. Characters are joined into lines
/// (splitting at wide gaps, so text side by side in two columns never runs together), lines into blocks, and blocks are
/// ordered by recursive XY-cuts: a band across the page (a title, a full-width figure) is read before the columns
/// below it, and each column top to bottom before the next. Page numbers and headers or footers repeated across pages
/// are left out, footnotes are read after the page's main text, and headings, list items and captions are marked.
/// </summary>
public static class ReadingOrder
{
    /// <summary>The share of the page height at the top and bottom where running headers, footers and page numbers sit.</summary>
    private const float MarginShare = 0.09F;

    /// <summary>The share of the page height at the bottom where footnotes sit.</summary>
    private const float FootnoteShare = 0.3F;

    /// <summary>A heading's font is at least this many times the body size, unless it is bold.</summary>
    private const float HeadingSizeRatio = 1.15F;

    /// <summary>How much larger than the body text a heading found from the layout must be to count as level 1.</summary>
    private const float TopHeadingRatio = 1.5F;

    /// <summary>The level given to a smaller heading found from the layout.</summary>
    private const int MinorHeadingLevel = 2;

    /// <summary>A footnote's font is at most this share of the body size.</summary>
    private const float FootnoteSizeRatio = 0.9F;

    /// <summary>The most lines a heading has.</summary>
    private const int MaxHeadingLines = 3;

    /// <summary>The longest bold-only heading, in characters.</summary>
    private const int MaxBoldHeadingLength = 120;

    /// <summary>The fewest pages a running header or footer appears on.</summary>
    private const int MinRepeats = 2;

    /// <summary>Font sizes are compared in half points.</summary>
    private const float HalfPoints = 2;

    /// <summary>A margin line repeated on at least this share of the sampled pages is a running header or footer.</summary>
    private const float RepeatedShare = 0.4F;

    /// <summary>The characters of a margin line's signature built on the stack.</summary>
    private const int StackSignature = 256;

    /// <summary>Works out a page's reading order.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="repeatedMargins">Signatures of header and footer lines repeated across the document, from <see cref="FindRepeatedMargins"/>.</param>
    /// <returns>The page's blocks in reading order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadingPage Analyze(int pageIndex, PageSize pageSize, IReadOnlyList<PageCharacter> characters, IReadOnlySet<string> repeatedMargins) =>
        Analyze(pageIndex, pageSize, characters, repeatedMargins, new());

    /// <summary>Works out a page's reading order using a caller's reusable buffers.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="repeatedMargins">Signatures of repeated header and footer lines.</param>
    /// <param name="workspace">Buffers retained by the document or caller.</param>
    /// <returns>The page's blocks in reading order.</returns>
    public static ReadingPage Analyze(int pageIndex, PageSize pageSize, IReadOnlyList<PageCharacter> characters, IReadOnlySet<string> repeatedMargins, ReadingOrderWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(repeatedMargins);
        ArgumentNullException.ThrowIfNull(workspace);
        if (characters.Count == 0)
        {
            return new(pageIndex, []);
        }

        var scratch = workspace.Rent();
        try
        {
            ReadingLines.Build(scratch, scratch.View(characters), default);
            RemoveMarginNoise(scratch, pageSize, repeatedMargins);
            if (scratch.LineCount == 0)
            {
                return new(pageIndex, []);
            }

            var body = BodyFontSize(scratch);
            ReadingBlocks.Build(scratch);
            ReadingBlocks.Order(scratch);
            return new(pageIndex, ToBlocks(scratch, body, pageSize));
        }
        finally
        {
            workspace.Return(scratch);
        }
    }

    /// <summary>
    /// Builds a page from a tagged document's structure: one block per element, in the document's logical order,
    /// with each element's characters joined into lines as the layout would be.
    /// </summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="tagged">The page's block-level elements in logical order.</param>
    /// <returns>The page's blocks.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadingPage FromStructure(int pageIndex, IReadOnlyList<PageCharacter> characters, IReadOnlyList<TaggedBlock> tagged) =>
        FromStructure(pageIndex, characters, tagged, new());

    /// <summary>Builds tagged reading blocks using a caller's reusable buffers.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="tagged">The page's block-level elements in logical order.</param>
    /// <param name="workspace">Buffers retained by the document or caller.</param>
    /// <returns>The page's blocks.</returns>
    public static ReadingPage FromStructure(int pageIndex, IReadOnlyList<PageCharacter> characters, IReadOnlyList<TaggedBlock> tagged, ReadingOrderWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(tagged);
        ArgumentNullException.ThrowIfNull(workspace);
        if (characters.Count == 0 || tagged.Count == 0)
        {
            return new(pageIndex, []);
        }

        var scratch = workspace.Rent();
        var blocks = ArrayPool<ReadingBlock>.Shared.Rent(Math.Max(tagged.Count, 1));
        try
        {
            var hasHeadings = BuildElementLines(scratch, scratch.View(characters), tagged);

            // Some producers tag every heading as a paragraph; then the layout says which paragraphs are headings.
            var body = hasHeadings || scratch.LineCount == 0 ? 0 : BodyFontSize(scratch);
            var count = 0;
            for (var i = 0; i < tagged.Count; i++)
            {
                if (FromElement(scratch, tagged[i], scratch.ElementFirst[i], scratch.ElementLines[i], body) is not { } block)
                {
                    continue;
                }

                blocks[count] = block;
                count++;
            }

            return new(pageIndex, blocks.AsSpan(0, count).ToArray());
        }
        finally
        {
            ArrayPool<ReadingBlock>.Shared.Return(blocks, true);
            workspace.Return(scratch);
        }
    }

    /// <summary>Collects the signatures of a page's header and footer lines, to find those repeated across pages.</summary>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters.</param>
    /// <param name="output">Receives one signature per margin line.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CollectMarginSignatures(PageSize pageSize, IReadOnlyList<PageCharacter> characters, List<string> output) =>
        CollectMarginSignatures(pageSize, characters, output, new());

    /// <summary>Collects margin signatures using a caller's reusable buffers.</summary>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters.</param>
    /// <param name="output">Receives one signature per margin line.</param>
    /// <param name="workspace">Buffers retained by the document or caller.</param>
    public static void CollectMarginSignatures(PageSize pageSize, IReadOnlyList<PageCharacter> characters, List<string> output, ReadingOrderWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(workspace);
        if (characters.Count == 0)
        {
            return;
        }

        var scratch = workspace.Rent();
        try
        {
            ReadingLines.Build(scratch, scratch.View(characters), default);
            for (var i = 0; i < scratch.LineCount; i++)
            {
                if (InMargin(scratch.Lines[i].Bounds, pageSize) && MakeSignature(ReadingBlocks.TextOf(scratch, i)) is { Length: > 0 } signature)
                {
                    output.Add(signature);
                }
            }
        }
        finally
        {
            workspace.Return(scratch);
        }
    }

    /// <summary>Finds the margin lines that repeat on enough of the sampled pages to be running headers or footers.</summary>
    /// <param name="pages">Each sampled page's margin signatures.</param>
    /// <returns>The repeated signatures.</returns>
    public static HashSet<string> FindRepeatedMargins(IReadOnlyList<List<string>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var repeated = new HashSet<string>(StringComparer.Ordinal);
        if (pages.Count < 2)
        {
            return repeated;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var signature in new HashSet<string>(page, StringComparer.Ordinal))
            {
                counts[signature] = counts.GetValueOrDefault(signature) + 1;
            }
        }

        var needed = Math.Max(MinRepeats, (int)MathF.Ceiling(pages.Count * RepeatedShare));
        foreach (var (signature, count) in counts)
        {
            if (count >= needed)
            {
                _ = repeated.Add(signature);
            }
        }

        return repeated;
    }

    /// <summary>Reduces a margin line to a signature that ignores page numbers and case.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The signature: lower case, digits as <c>#</c>, single spaces.</returns>
    public static string Signature(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MakeSignature(text);
    }

    /// <summary>Determines whether a line is only a page number, such as "12", "Page 3 of 9", "- 4 -" or "xii".</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a page number.</returns>
    public static bool IsPageNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ReadingText.IsPageNumber(text);
    }

    /// <summary>Reduces a margin line to a signature that ignores page numbers and case.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The signature.</returns>
    private static string MakeSignature(ReadOnlySpan<char> text)
    {
        char[]? rented = null;
        var buffer = text.Length <= StackSignature ? stackalloc char[StackSignature] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            return new(buffer[..ReadingText.WriteSignature(text, buffer)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Determines whether a line's signature is one of the repeated margin signatures.</summary>
    /// <param name="repeatedMargins">The repeated margin signatures.</param>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a repeated header or footer.</returns>
    private static bool IsRepeatedMargin(IReadOnlySet<string> repeatedMargins, ReadOnlySpan<char> text)
    {
        if (repeatedMargins.Count == 0)
        {
            return false;
        }

        char[]? rented = null;
        var buffer = text.Length <= StackSignature ? stackalloc char[StackSignature] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            var signature = buffer[..ReadingText.WriteSignature(text, buffer)];

            // A set made with an ordinal comparer looks the signature up without making a string of it.
            return repeatedMargins is HashSet<string> set && set.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup)
                ? lookup.Contains(signature)
                : repeatedMargins.Contains(new(signature));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Determines whether a box sits in the top or bottom margin band.</summary>
    /// <param name="bounds">The box.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns><see langword="true"/> in a margin band.</returns>
    private static bool InMargin(in PageRect bounds, PageSize pageSize) =>
        bounds.Bottom <= pageSize.Height * MarginShare || bounds.Top >= pageSize.Height * (1 - MarginShare);

    /// <summary>Drops the lines that are page numbers or repeated headers and footers, keeping the others in order.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="repeatedMargins">The repeated margin signatures.</param>
    private static void RemoveMarginNoise(ReadingScratch scratch, PageSize pageSize, IReadOnlySet<string> repeatedMargins)
    {
        var kept = 0;
        for (var i = 0; i < scratch.LineCount; i++)
        {
            if (IsMarginNoise(scratch, i, pageSize, repeatedMargins))
            {
                continue;
            }

            scratch.Lines[kept] = scratch.Lines[i];
            kept++;
        }

        scratch.LineCount = kept;
    }

    /// <summary>Determines whether a line is a page number or a repeated header or footer.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="line">The line.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="repeatedMargins">The repeated margin signatures.</param>
    /// <returns><see langword="true"/> to leave it out.</returns>
    private static bool IsMarginNoise(ReadingScratch scratch, int line, PageSize pageSize, IReadOnlySet<string> repeatedMargins) =>
        InMargin(scratch.Lines[line].Bounds, pageSize)
        && (ReadingText.IsPageNumber(ReadingBlocks.TextOf(scratch, line)) || IsRepeatedMargin(repeatedMargins, ReadingBlocks.TextOf(scratch, line)));

    /// <summary>Joins each tagged element's characters into lines, one run of lines per element.</summary>
    /// <param name="scratch">The working space; each element's first line and line count are recorded in it.</param>
    /// <param name="characters">The page's characters.</param>
    /// <param name="tagged">The page's block-level elements.</param>
    /// <returns><see langword="true"/> when the tags mark a heading.</returns>
    private static bool BuildElementLines(ReadingScratch scratch, ReadOnlySpan<PageCharacter> characters, IReadOnlyList<TaggedBlock> tagged)
    {
        scratch.EnsureElements(tagged.Count);
        var hasHeadings = false;
        for (var i = 0; i < tagged.Count; i++)
        {
            var element = tagged[i];
            scratch.EnsureSubset(element.Characters.Length);
            var count = 0;
            foreach (var index in element.Characters)
            {
                if ((uint)index >= (uint)characters.Length)
                {
                    continue;
                }

                scratch.Subset[count] = characters[index];
                scratch.SubsetIndices[count] = index;
                count++;
            }

            scratch.ElementFirst[i] = scratch.LineCount;
            ReadingLines.Build(scratch, scratch.Subset.AsSpan(0, count), scratch.SubsetIndices.AsSpan(0, count));
            scratch.ElementLines[i] = scratch.LineCount - scratch.ElementFirst[i];
            hasHeadings |= element.Kind == ReadingBlockKind.Heading;
        }

        return hasHeadings;
    }

    /// <summary>Makes one block from a tagged element's lines, or from its replacement text.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="element">The element.</param>
    /// <param name="first">The element's first line.</param>
    /// <param name="count">The element's line count.</param>
    /// <param name="body">The body font size, to find headings tagged as paragraphs; zero to trust the tags.</param>
    /// <returns>The block, or <see langword="null"/> when it has nothing to read.</returns>
    private static ReadingBlock? FromElement(ReadingScratch scratch, TaggedBlock element, int first, int count, float body)
    {
        var block = ReadingBlocks.Join(scratch, first, count);
        if (element.ReplacementText is { Length: > 0 } replacement)
        {
            // Actual text and figure descriptions are read as written; they map to no single page character.
            var unmapped = new int[replacement.Length];
            Array.Fill(unmapped, -1);
            var hasLines = block.LineCount > 0;
            return new(element.Kind, replacement, unmapped, hasLines ? block.Bounds : default, hasLines ? scratch.Lines[block.First].FontSize : 0)
            {
                Level = element.Level,
                IsTagged = true,
                Row = element.Row,
                Column = element.Column,
                Language = element.Language,
            };
        }

        return block.LineCount == 0
            ? null
            : ReadingBlocks.ToReadingBlock(scratch, block, TaggedRole(scratch, element, block, body), new(true, element.Row, element.Column, element.Language));
    }

    /// <summary>Gets a tagged element's role, promoting a paragraph that looks like a heading when the tags have none.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="element">The element.</param>
    /// <param name="block">Its text.</param>
    /// <param name="body">The body font size, or zero to trust the tags.</param>
    /// <returns>The kind and heading level.</returns>
    private static ReadingRole TaggedRole(ReadingScratch scratch, TaggedBlock element, BlockRecord block, float body)
    {
        if (body <= 0 || element.Kind != ReadingBlockKind.Paragraph || !IsHeading(scratch, block, body))
        {
            return new(element.Kind, element.Level);
        }

        return new(ReadingBlockKind.Heading, FontSizeOf(scratch, block) >= body * TopHeadingRatio ? 1 : MinorHeadingLevel);
    }

    /// <summary>Gets the body text size: the font size most characters on the page use.</summary>
    /// <param name="scratch">The working space.</param>
    /// <returns>The size in points.</returns>
    private static float BodyFontSize(ReadingScratch scratch)
    {
        scratch.EnsureSizes(scratch.LineCount);
        var distinct = 0;
        for (var i = 0; i < scratch.LineCount; i++)
        {
            var key = (int)MathF.Round(scratch.Lines[i].FontSize * HalfPoints);
            var at = scratch.SizeKeys.AsSpan(0, distinct).IndexOf(key);
            if (at < 0)
            {
                at = distinct;
                scratch.SizeKeys[at] = key;
                scratch.SizeCounts[at] = 0;
                distinct++;
            }

            scratch.SizeCounts[at] += scratch.Lines[i].Visible;
        }

        var best = 0;
        var bestCount = -1;
        for (var i = 0; i < distinct; i++)
        {
            if (scratch.SizeCounts[i] <= bestCount)
            {
                continue;
            }

            best = scratch.SizeKeys[i];
            bestCount = scratch.SizeCounts[i];
        }

        return best / HalfPoints;
    }

    /// <summary>Makes the page's reading blocks from its ordered blocks, with footnotes after the main text.</summary>
    /// <param name="scratch">The working space, with its blocks in reading order.</param>
    /// <param name="body">The body text size.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The blocks.</returns>
    private static ReadingBlock[] ToBlocks(ReadingScratch scratch, float body, PageSize pageSize)
    {
        var footnotes = 0;
        for (var i = 0; i < scratch.BlockCount; i++)
        {
            var kind = Classify(scratch, scratch.Blocks[i], body, pageSize);
            scratch.Blocks[i].Kind = kind;
            footnotes += kind == ReadingBlockKind.Footnote ? 1 : 0;
        }

        var blocks = new ReadingBlock[scratch.BlockCount];
        var main = 0;
        var foot = blocks.Length - footnotes;
        for (var i = 0; i < scratch.BlockCount; i++)
        {
            var block = scratch.Blocks[i];
            var isFootnote = block.Kind == ReadingBlockKind.Footnote;
            var at = isFootnote ? foot : main;
            foot += isFootnote ? 1 : 0;
            main += isFootnote ? 0 : 1;
            blocks[at] = ReadingBlocks.ToReadingBlock(scratch, block, new(block.Kind, 0), default);
        }

        return blocks;
    }

    /// <summary>Gets a block's font size, which is its first line's.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <returns>The size in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float FontSizeOf(ReadingScratch scratch, BlockRecord block) => scratch.Lines[block.First].FontSize;

    /// <summary>Decides what a block is.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <param name="body">The body text size.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The kind.</returns>
    private static ReadingBlockKind Classify(ReadingScratch scratch, BlockRecord block, float body, PageSize pageSize)
    {
        var text = ReadingBlocks.TextOf(scratch, block.First);
        if (ReadingText.IsCaption(text))
        {
            return ReadingBlockKind.Caption;
        }

        if (ReadingText.StartsItem(text))
        {
            return ReadingBlockKind.ListItem;
        }

        if (FontSizeOf(scratch, block) <= body * FootnoteSizeRatio && block.Bounds.Top >= pageSize.Height * (1 - FootnoteShare) && ReadingText.StartsFootnote(text))
        {
            return ReadingBlockKind.Footnote;
        }

        return IsHeading(scratch, block, body) ? ReadingBlockKind.Heading : ReadingBlockKind.Paragraph;
    }

    /// <summary>Determines whether a block is a heading: a few lines, larger than the body or bold without ending a sentence.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <param name="body">The body text size.</param>
    /// <returns><see langword="true"/> for a heading.</returns>
    private static bool IsHeading(ReadingScratch scratch, BlockRecord block, float body)
    {
        if (block.LineCount > MaxHeadingLines)
        {
            return false;
        }

        return FontSizeOf(scratch, block) >= body * HeadingSizeRatio
            || (scratch.Lines[block.First].IsBold && block.TextLength <= MaxBoldHeadingLength && !ReadingText.EndsSentence(ReadingBlocks.TextOf(scratch, block.Last)));
    }
}
