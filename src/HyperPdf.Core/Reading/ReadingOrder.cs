// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>The share of the smaller height two boxes must overlap by to be on one line.</summary>
    private const float SameLineOverlap = 0.3F;

    /// <summary>A gap wider than this many font sizes on one line separates columns rather than words.</summary>
    private const float ColumnGapSizes = 2.5F;

    /// <summary>A gap wider than this share of the font size between characters is a space.</summary>
    private const float SpaceGapShare = 0.2F;

    /// <summary>A gap between lines no larger than this many line heights keeps them in one block.</summary>
    private const float BlockGapLines = 0.8F;

    /// <summary>Lines whose font sizes differ by more than this share are in different blocks.</summary>
    private const float FontSizeTolerance = 0.18F;

    /// <summary>The smallest empty strip between columns, in points.</summary>
    private const float MinGutter = 6F;

    /// <summary>The smallest empty band between rows of blocks, in points.</summary>
    private const float MinBand = 1F;

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

    /// <summary>The longest text taken for a page number.</summary>
    private const int MaxPageNumberLength = 16;

    /// <summary>The fewest pages a running header or footer appears on.</summary>
    private const int MinRepeats = 2;

    /// <summary>Font sizes are compared in half points.</summary>
    private const float HalfPoints = 2;

    /// <summary>The longest number that starts a numbered list item.</summary>
    private const int MaxItemDigits = 3;

    /// <summary>A margin line repeated on at least this share of the sampled pages is a running header or footer.</summary>
    private const float RepeatedShare = 0.4F;

    /// <summary>Characters that start a bulleted list item.</summary>
    private static readonly SearchValues<char> Bullets = SearchValues.Create("•◦▪▫‣⁃∙●○■□–—-*➢►▶✓✔");

    /// <summary>Marks that start a footnote, superscript digits included.</summary>
    private static readonly SearchValues<char> FootnoteMarks = SearchValues.Create("*†‡§¶¹²³⁰⁴⁵⁶⁷⁸⁹");

    /// <summary>Words that start a caption.</summary>
    private static readonly string[] CaptionWords = ["figure", "fig.", "table", "chart", "listing", "plate", "exhibit"];

    /// <summary>Works out a page's reading order.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="repeatedMargins">Signatures of header and footer lines repeated across the document, from <see cref="FindRepeatedMargins"/>.</param>
    /// <returns>The page's blocks in reading order.</returns>
    public static ReadingPage Analyze(int pageIndex, PageSize pageSize, IReadOnlyList<PageCharacter> characters, IReadOnlySet<string> repeatedMargins)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(repeatedMargins);
        var lines = BuildLines(characters, null);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (IsMarginNoise(lines[i], pageSize, repeatedMargins))
            {
                lines.RemoveAt(i);
            }
        }

        if (lines.Count == 0)
        {
            return new(pageIndex, []);
        }

        var body = BodyFontSize(lines);
        var blocks = BuildBlocks(lines);
        var ordered = new List<TextBlock>(blocks.Count);
        Order(blocks, ordered);
        var main = new List<ReadingBlock>(ordered.Count);
        var footnotes = new List<ReadingBlock>();
        foreach (var block in ordered)
        {
            var reading = block.ToReadingBlock(Classify(block, body, pageSize), 0, false);
            (reading.Kind == ReadingBlockKind.Footnote ? footnotes : main).Add(reading);
        }

        main.AddRange(footnotes);
        return new(pageIndex, main);
    }

    /// <summary>
    /// Builds a page from a tagged document's structure: one block per element, in the document's logical order,
    /// with each element's characters joined into lines as the layout would be.
    /// </summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="characters">The page's characters in the engine's order.</param>
    /// <param name="tagged">The page's block-level elements in logical order.</param>
    /// <returns>The page's blocks.</returns>
    public static ReadingPage FromStructure(int pageIndex, IReadOnlyList<PageCharacter> characters, IReadOnlyList<TaggedBlock> tagged)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(tagged);
        var subset = new List<PageCharacter>();
        var indices = new List<int>();
        var elements = new List<List<TextLine>>(tagged.Count);
        var all = new List<TextLine>();
        var hasHeadings = false;
        foreach (var element in tagged)
        {
            subset.Clear();
            indices.Clear();
            foreach (var index in element.Characters)
            {
                if ((uint)index >= (uint)characters.Count)
                {
                    continue;
                }

                subset.Add(characters[index]);
                indices.Add(index);
            }

            var lines = BuildLines(subset, indices);
            elements.Add(lines);
            all.AddRange(lines);
            hasHeadings |= element.Kind == ReadingBlockKind.Heading;
        }

        // Some producers tag every heading as a paragraph; then the layout says which paragraphs are headings.
        var body = hasHeadings || all.Count == 0 ? 0 : BodyFontSize(all);
        var blocks = new List<ReadingBlock>(tagged.Count);
        for (var i = 0; i < tagged.Count; i++)
        {
            if (FromElement(tagged[i], elements[i], body) is { } block)
            {
                blocks.Add(block);
            }
        }

        return new(pageIndex, blocks);
    }

    /// <summary>Collects the signatures of a page's header and footer lines, to find those repeated across pages.</summary>
    /// <param name="pageSize">The page size in points.</param>
    /// <param name="characters">The page's characters.</param>
    /// <param name="output">Receives one signature per margin line.</param>
    public static void CollectMarginSignatures(PageSize pageSize, IReadOnlyList<PageCharacter> characters, List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var line in BuildLines(characters, null))
        {
            if (InMargin(line.Bounds, pageSize) && Signature(line.Text) is { Length: > 0 } signature)
            {
                output.Add(signature);
            }
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
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    _ = builder.Append(' ');
                }
            }
            else
            {
                _ = builder.Append(char.IsDigit(c) ? '#' : char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Determines whether a line is only a page number, such as "12", "Page 3 of 9", "- 4 -" or "xii".</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a page number.</returns>
    public static bool IsPageNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var span = text.AsSpan().Trim().Trim("-–—|·•[]() ");
        if (span.IsEmpty || span.Length > MaxPageNumberLength)
        {
            return false;
        }

        if (span.StartsWith("page", StringComparison.OrdinalIgnoreCase))
        {
            span = span["page".Length..].TrimStart();
        }

        var digits = 0;
        foreach (var c in span)
        {
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }
            else if (!char.IsWhiteSpace(c) && c != '/' && !"of".Contains(char.ToLowerInvariant(c), StringComparison.Ordinal))
            {
                return digits == 0 && IsRomanNumeral(span);
            }
        }

        return digits > 0;
    }

    /// <summary>Determines whether text is a small roman numeral, as front matter is numbered.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for roman numerals.</returns>
    private static bool IsRomanNumeral(ReadOnlySpan<char> text) => !text.IsEmpty && text.IndexOfAnyExcept("ivxlcIVXLC") < 0;

    /// <summary>Determines whether a box sits in the top or bottom margin band.</summary>
    /// <param name="bounds">The box.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns><see langword="true"/> in a margin band.</returns>
    private static bool InMargin(in PageRect bounds, PageSize pageSize) =>
        bounds.Bottom <= pageSize.Height * MarginShare || bounds.Top >= pageSize.Height * (1 - MarginShare);

    /// <summary>Determines whether a line is a page number or a repeated header or footer.</summary>
    /// <param name="line">The line.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="repeatedMargins">The repeated margin signatures.</param>
    /// <returns><see langword="true"/> to leave it out.</returns>
    private static bool IsMarginNoise(TextLine line, PageSize pageSize, IReadOnlySet<string> repeatedMargins) =>
        InMargin(line.Bounds, pageSize) && (IsPageNumber(line.Text) || repeatedMargins.Contains(Signature(line.Text)));

    /// <summary>Joins characters into lines, splitting wide gaps so side-by-side columns stay apart.</summary>
    /// <param name="characters">The characters.</param>
    /// <param name="pageIndices">Each character's page index, when the characters are a subset of the page; otherwise their positions are used.</param>
    /// <returns>The lines.</returns>
    private static List<TextLine> BuildLines(IReadOnlyList<PageCharacter> characters, List<int>? pageIndices)
    {
        var lines = new List<TextLine>();
        TextLine? current = null;
        var pendingSpace = false;
        var rightToLeftPage = IsMostlyRightToLeft(characters);
        for (var i = 0; i < characters.Count; i++)
        {
            var character = characters[i];
            if (character.Generated || char.IsWhiteSpace(character.Value) || character.Bounds.Width <= 0)
            {
                pendingSpace |= current is not null;
                continue;
            }

            if (current is null || StartsNewLine(current, character, RunRight(characters, i, current)))
            {
                current = new(PageIndex(pageIndices, i), character, rightToLeftPage);
                lines.Add(current);
                pendingSpace = false;
                continue;
            }

            current.Append(PageIndex(pageIndices, i), character, pendingSpace || Gap(current.Last, character.Bounds) > character.FontSize * SpaceGapShare);
            pendingSpace = false;
        }

        return lines;
    }

    /// <summary>Gets a character's page index.</summary>
    /// <param name="pageIndices">The page indices of a subset of characters, or <see langword="null"/> for the whole page.</param>
    /// <param name="position">The character's position in the list being read.</param>
    /// <returns>The page index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PageIndex(List<int>? pageIndices, int position) => pageIndices?[position] ?? position;

    /// <summary>Makes one block from a tagged element's lines, or from its replacement text.</summary>
    /// <param name="element">The element.</param>
    /// <param name="lines">Its characters joined into lines.</param>
    /// <param name="body">The body font size, to find headings tagged as paragraphs; zero to trust the tags.</param>
    /// <returns>The block, or <see langword="null"/> when it has nothing to read.</returns>
    private static ReadingBlock? FromElement(TaggedBlock element, List<TextLine> lines, float body)
    {
        var block = Join(lines);
        if (element.ReplacementText is { Length: > 0 } replacement)
        {
            // Actual text and figure descriptions are read as written; they map to no single page character.
            var unmapped = new int[replacement.Length];
            Array.Fill(unmapped, -1);
            return new(element.Kind, replacement, unmapped, block?.Bounds ?? default, block?.FontSize ?? 0) { Level = element.Level, IsTagged = true };
        }

        if (block is null)
        {
            return null;
        }

        var (kind, level) = TaggedRole(element, block, body);
        return block.ToReadingBlock(kind, level, true);
    }

    /// <summary>Gets a tagged element's role, promoting a paragraph that looks like a heading when the tags have none.</summary>
    /// <param name="element">The element.</param>
    /// <param name="block">Its text.</param>
    /// <param name="body">The body font size, or zero to trust the tags.</param>
    /// <returns>The kind and heading level.</returns>
    private static (ReadingBlockKind Kind, int Level) TaggedRole(TaggedBlock element, TextBlock block, float body)
    {
        if (body <= 0 || element.Kind != ReadingBlockKind.Paragraph || !IsHeading(block, body))
        {
            return (element.Kind, element.Level);
        }

        return (ReadingBlockKind.Heading, block.FontSize >= body * TopHeadingRatio ? 1 : MinorHeadingLevel);
    }

    /// <summary>Joins lines into one block, in the order given.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The block, or <see langword="null"/> when there are no lines.</returns>
    private static TextBlock? Join(List<TextLine> lines)
    {
        TextBlock? block = null;
        foreach (var line in lines)
        {
            if (block is null)
            {
                block = new(line);
                continue;
            }

            block.Add(line);
        }

        return block;
    }

    /// <summary>Determines whether a character starts a new line: below the line, back to its left, or past a column gap.</summary>
    /// <param name="line">The current line.</param>
    /// <param name="character">The character.</param>
    /// <param name="runRight">The right edge of the word the character starts, for a left-to-right word in right-to-left text.</param>
    /// <returns><see langword="true"/> for a new line.</returns>
    private static bool StartsNewLine(TextLine line, in PageCharacter character, float runRight)
    {
        var bounds = character.Bounds;
        var overlap = Math.Min(bounds.Bottom, line.Bounds.Bottom) - Math.Max(bounds.Top, line.Bounds.Top);
        var sameLine = overlap > Math.Min(bounds.Height, line.Bounds.Height) * SameLineOverlap;
        var size = Math.Max(character.FontSize, 1);
        if (line.IsRightToLeft)
        {
            // Right-to-left text runs leftwards; embedded left-to-right words stay inside the line's extent.
            var forwards = bounds.Left > line.Bounds.Right + size;
            var gapLeft = line.Bounds.Left - runRight > size * ColumnGapSizes;
            return !sameLine || forwards || gapLeft;
        }

        var backwards = bounds.Left < line.Bounds.Right - size;
        var columnGap = bounds.Left - line.Bounds.Right > size * ColumnGapSizes;
        return !sameLine || backwards || columnGap;
    }

    /// <summary>
    /// Finds the right edge of the left-to-right word starting at a character inside a right-to-left line: such a word
    /// is laid out leftwards as a whole, so its first letter sits a word's width from the text before it.
    /// </summary>
    /// <param name="characters">The characters.</param>
    /// <param name="start">The character.</param>
    /// <param name="line">The current line.</param>
    /// <returns>The word's right edge, or the character's own when it is not such a word.</returns>
    private static float RunRight(IReadOnlyList<PageCharacter> characters, int start, TextLine? line)
    {
        var right = characters[start].Bounds.Right;
        if (line is not { IsRightToLeft: true })
        {
            return right;
        }

        for (var i = start; i < characters.Count; i++)
        {
            var c = characters[i];
            if (c.Generated || char.IsWhiteSpace(c.Value) || IsRightToLeft(c.Value))
            {
                break;
            }

            right = Math.Max(right, c.Bounds.Right);
        }

        return right;
    }

    /// <summary>Determines whether most of a page's letters are from right-to-left scripts.</summary>
    /// <param name="characters">The characters.</param>
    /// <returns><see langword="true"/> for a mostly right-to-left page.</returns>
    private static bool IsMostlyRightToLeft(IReadOnlyList<PageCharacter> characters)
    {
        var rightToLeft = 0;
        var letters = 0;
        for (var i = 0; i < characters.Count; i++)
        {
            var c = characters[i].Value;
            if (!char.IsLetter(c))
            {
                continue;
            }

            letters++;
            rightToLeft += IsRightToLeft(c) ? 1 : 0;
        }

        return rightToLeft > letters - rightToLeft;
    }

    /// <summary>Measures the horizontal gap between two neighbouring characters, whichever way the text runs.</summary>
    /// <param name="previous">The previous character's box.</param>
    /// <param name="next">The next character's box.</param>
    /// <returns>The gap, or a negative number when they overlap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gap(in PageRect previous, in PageRect next) => Math.Max(next.Left - previous.Right, previous.Left - next.Right);

    /// <summary>Determines whether a character belongs to a right-to-left script: Hebrew, Arabic, Syriac or Thaana.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a right-to-left letter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRightToLeft(char c) => c is (>= '\u0590' and <= '\u08FF') or (>= '\uFB1D' and <= '\uFDFF') or (>= '\uFE70' and <= '\uFEFF');

    /// <summary>Gets the body text size: the font size most characters on the page use.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The size in points.</returns>
    private static float BodyFontSize(List<TextLine> lines)
    {
        var counts = new Dictionary<int, int>();
        foreach (var line in lines)
        {
            var key = (int)MathF.Round(line.FontSize * HalfPoints);
            counts[key] = counts.GetValueOrDefault(key) + line.Length;
        }

        var best = 0;
        var bestCount = -1;
        foreach (var (key, count) in counts)
        {
            if (count <= bestCount)
            {
                continue;
            }

            best = key;
            bestCount = count;
        }

        return best / HalfPoints;
    }

    /// <summary>Groups lines into blocks: each line joins the block directly above it when close, overlapping and alike.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The blocks.</returns>
    private static List<TextBlock> BuildBlocks(List<TextLine> lines)
    {
        lines.Sort(static (a, b) => CompareTopLeft(a.Bounds, b.Bounds));
        var blocks = new List<TextBlock>();
        foreach (var line in lines)
        {
            var target = StartsItem(line.Text) ? null : FindBlockAbove(blocks, line);
            if (target is null)
            {
                blocks.Add(new(line));
            }
            else
            {
                target.Add(line);
            }
        }

        return blocks;
    }

    /// <summary>Finds the block a line continues: directly above, close, overlapping and in a similar font.</summary>
    /// <param name="blocks">The blocks so far.</param>
    /// <param name="line">The line.</param>
    /// <returns>The block, or <see langword="null"/> to start a new one.</returns>
    private static TextBlock? FindBlockAbove(List<TextBlock> blocks, TextLine line)
    {
        TextBlock? best = null;
        var bestGap = float.MaxValue;
        foreach (var block in blocks)
        {
            var last = block.Last;
            var gap = line.Bounds.Top - last.Bounds.Bottom;
            var height = Math.Max(last.Bounds.Height, line.Bounds.Height);
            var overlaps = line.Bounds.Left < block.Bounds.Right && line.Bounds.Right > block.Bounds.Left;
            var alike = MathF.Abs(line.FontSize - block.FontSize) <= block.FontSize * FontSizeTolerance;
            if (line.Bounds.Top < last.Bounds.Top || gap > height * BlockGapLines || !overlaps || !alike || gap >= bestGap || IsCaption(line.Text))
            {
                continue;
            }

            best = block;
            bestGap = gap;
        }

        return best;
    }

    /// <summary>Orders blocks by recursive XY-cuts: columns left to right where there is a gutter, otherwise bands top to bottom.</summary>
    /// <param name="blocks">The blocks of the region.</param>
    /// <param name="output">Receives the blocks in reading order.</param>
    private static void Order(List<TextBlock> blocks, List<TextBlock> output)
    {
        if (blocks.Count <= 1)
        {
            output.AddRange(blocks);
            return;
        }

        var groups = Cut(blocks, vertical: true, MinGutter);
        if (groups.Count <= 1)
        {
            groups = Cut(blocks, vertical: false, MinBand);
        }

        if (groups.Count <= 1)
        {
            blocks.Sort(static (a, b) => CompareTopLeft(a.Bounds, b.Bounds));
            output.AddRange(blocks);
            return;
        }

        foreach (var group in groups)
        {
            Order(group, output);
        }
    }

    /// <summary>Splits blocks at empty strips that cross the whole region.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="vertical">Whether to cut into columns (else into bands).</param>
    /// <param name="minGap">The narrowest strip that counts.</param>
    /// <returns>The groups, left to right or top to bottom.</returns>
    private static List<List<TextBlock>> Cut(List<TextBlock> blocks, bool vertical, float minGap)
    {
        blocks.Sort(vertical
            ? static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left)
            : static (a, b) => a.Bounds.Top.CompareTo(b.Bounds.Top));
        var groups = new List<List<TextBlock>> { new() };
        var reach = float.MinValue;
        foreach (var block in blocks)
        {
            var start = vertical ? block.Bounds.Left : block.Bounds.Top;
            var end = vertical ? block.Bounds.Right : block.Bounds.Bottom;
            if (groups[^1].Count > 0 && start - reach >= minGap)
            {
                groups.Add([]);
            }

            groups[^1].Add(block);
            reach = Math.Max(reach, end);
        }

        return groups;
    }

    /// <summary>Orders boxes top to bottom, then left to right.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns>The order.</returns>
    private static int CompareTopLeft(in PageRect a, in PageRect b)
    {
        var top = a.Top.CompareTo(b.Top);
        return top != 0 ? top : a.Left.CompareTo(b.Left);
    }

    /// <summary>Decides what a block is.</summary>
    /// <param name="block">The block.</param>
    /// <param name="body">The body text size.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The kind.</returns>
    private static ReadingBlockKind Classify(TextBlock block, float body, PageSize pageSize)
    {
        var text = block.FirstText;
        if (IsCaption(text))
        {
            return ReadingBlockKind.Caption;
        }

        if (StartsItem(text))
        {
            return ReadingBlockKind.ListItem;
        }

        if (IsFootnote(block, body, pageSize))
        {
            return ReadingBlockKind.Footnote;
        }

        return IsHeading(block, body) ? ReadingBlockKind.Heading : ReadingBlockKind.Paragraph;
    }

    /// <summary>Determines whether a block is a footnote: small, near the foot of the page and starting with a mark.</summary>
    /// <param name="block">The block.</param>
    /// <param name="body">The body text size.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns><see langword="true"/> for a footnote.</returns>
    private static bool IsFootnote(TextBlock block, float body, PageSize pageSize) =>
        block.FontSize <= body * FootnoteSizeRatio && block.Bounds.Top >= pageSize.Height * (1 - FootnoteShare) && StartsFootnote(block.FirstText);

    /// <summary>Determines whether a block is a heading: a few lines, larger than the body or bold without ending a sentence.</summary>
    /// <param name="block">The block.</param>
    /// <param name="body">The body text size.</param>
    /// <returns><see langword="true"/> for a heading.</returns>
    private static bool IsHeading(TextBlock block, float body)
    {
        if (block.LineCount > MaxHeadingLines)
        {
            return false;
        }

        return block.FontSize >= body * HeadingSizeRatio || (block.IsBold && block.TextLength <= MaxBoldHeadingLength && !EndsSentence(block.LastText));
    }

    /// <summary>Determines whether text ends like a sentence.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it ends with a full stop, question or exclamation mark.</returns>
    private static bool EndsSentence(string text) => text.Length > 0 && text[^1] is '.' or '?' or '!' or ':';

    /// <summary>Determines whether a line starts a caption, such as "Figure 3." or "Table 2:".</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a caption.</returns>
    private static bool IsCaption(string text)
    {
        foreach (var word in CaptionWords)
        {
            if (text.Length > word.Length + 1 && text.StartsWith(word, StringComparison.OrdinalIgnoreCase) && StartsCaptionLabel(text.AsSpan(word.Length).TrimStart()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether text starts with a caption's number or letter, such as "3" or "A.".</summary>
    /// <param name="rest">The text after the caption word.</param>
    /// <returns><see langword="true"/> for a label.</returns>
    private static bool StartsCaptionLabel(ReadOnlySpan<char> rest) =>
        !rest.IsEmpty && (char.IsAsciiDigit(rest[0]) || (char.IsAsciiLetterUpper(rest[0]) && rest.Length > 1 && rest[1] is '.' or ':'));

    /// <summary>Determines whether a line starts a list item: a bullet, or a number or letter followed by a full stop or bracket.</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a list item.</returns>
    private static bool StartsItem(string text) => text.Length > 1 && (StartsWithBullet(text) || StartsWithItemNumber(text.AsSpan().TrimStart('(')));

    /// <summary>Determines whether a line starts with a bullet followed by a space.</summary>
    /// <param name="text">The line, at least two characters.</param>
    /// <returns><see langword="true"/> for a bullet.</returns>
    private static bool StartsWithBullet(string text) => Bullets.Contains(text[0]) && (text[1] == ' ' || (!Bullets.Contains(text[1]) && !char.IsLetterOrDigit(text[1])));

    /// <summary>Determines whether text starts with an item number such as "3. ", "12) " or "b. ".</summary>
    /// <param name="marker">The text, without an opening bracket.</param>
    /// <returns><see langword="true"/> for an item number.</returns>
    private static bool StartsWithItemNumber(ReadOnlySpan<char> marker)
    {
        var length = marker.Length > 1 && char.IsAsciiLetterLower(marker[0]) ? 1 : CountDigits(marker);
        return length > 0 && marker.Length > length + 1 && marker[length] is '.' or ')' && marker[length + 1] == ' ';
    }

    /// <summary>Counts the leading digits of an item number.</summary>
    /// <param name="marker">The text.</param>
    /// <returns>The digits, at most <see cref="MaxItemDigits"/>.</returns>
    private static int CountDigits(ReadOnlySpan<char> marker)
    {
        var length = 0;
        while (length < marker.Length && length < MaxItemDigits && char.IsAsciiDigit(marker[length]))
        {
            length++;
        }

        return length;
    }

    /// <summary>Determines whether a line starts a footnote: a number, symbol or superscript mark.</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a footnote start.</returns>
    private static bool StartsFootnote(string text) => text.Length > 0 && (char.IsAsciiDigit(text[0]) || FootnoteMarks.Contains(text[0]));

    /// <summary>A line being built: its characters with their page indices and its box.</summary>
    private sealed class TextLine
    {
        /// <summary>The Arabic letter lam.</summary>
        private const char ArabicLam = '\u0644';

        /// <summary>The soft hyphen some documents use at line breaks.</summary>
        private const char SoftHyphen = '­';

        /// <summary>The text, with inferred spaces.</summary>
        private readonly StringBuilder _text = new();

        /// <summary>The page index of each character; -1 for inferred spaces.</summary>
        private readonly List<int> _indices = [];

        /// <summary>The sum of the characters' font sizes.</summary>
        private float _sizeTotal;

        /// <summary>The bold characters.</summary>
        private int _bold;

        /// <summary>The text, once asked for.</summary>
        private string? _finished;

        /// <summary>Initializes a new instance of the <see cref="TextLine"/> class with its first character.</summary>
        /// <param name="index">The character's page index.</param>
        /// <param name="character">The character.</param>
        /// <param name="rightToLeftPage">Whether the page is mostly right-to-left text.</param>
        internal TextLine(int index, in PageCharacter character, bool rightToLeftPage)
        {
            Bounds = character.Bounds;
            IsRightToLeft = rightToLeftPage || ReadingOrder.IsRightToLeft(character.Value);
            Append(index, character, false);
        }

        /// <summary>Gets the line's box.</summary>
        internal PageRect Bounds { get; private set; }

        /// <summary>Gets the last character's box.</summary>
        internal PageRect Last { get; private set; }

        /// <summary>Gets a value indicating whether the line's first letter is from a right-to-left script.</summary>
        internal bool IsRightToLeft { get; }

        /// <summary>Gets the visible characters.</summary>
        internal int Length { get; private set; }

        /// <summary>Gets the average font size.</summary>
        internal float FontSize => Length == 0 ? 0 : _sizeTotal / Length;

        /// <summary>Gets a value indicating whether most characters are bold.</summary>
        internal bool IsBold => _bold > Length - _bold;

        /// <summary>Gets the text, made once the line is complete.</summary>
        internal string Text => _finished ??= _text.ToString();

        /// <summary>Gets the page index of each character.</summary>
        internal List<int> Indices => _indices;

        /// <summary>Gets the text builder, for joining lines.</summary>
        internal StringBuilder Builder => _text;

        /// <summary>Adds a character.</summary>
        /// <param name="index">Its page index.</param>
        /// <param name="character">The character.</param>
        /// <param name="spaceBefore">Whether a space goes before it.</param>
        internal void Append(int index, in PageCharacter character, bool spaceBefore)
        {
            if (spaceBefore && _text.Length > 0 && _text[^1] != ' ')
            {
                _ = _text.Append(' ');
                _indices.Add(-1);
            }

            _finished = null;
            if (IsReversedLigature(character))
            {
                // A lam-alef ligature mapped to Unicode as alef then lam: put the lam first, as it is read.
                _ = _text.Insert(_text.Length - 1, ArabicLam);
                _indices.Insert(_indices.Count - 1, index);
            }
            else
            {
                _ = _text.Append(character.Value == SoftHyphen ? '-' : character.Value);
                _indices.Add(index);
            }

            Bounds = Bounds.Union(character.Bounds);
            Last = character.Bounds;
            _sizeTotal += character.FontSize;
            _bold += character.Bold ? 1 : 0;
            Length++;
        }

        /// <summary>Determines whether a character is one of the Arabic alef forms a lam-alef ligature joins.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> for alef, alef with madda, or alef with hamza above or below.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAlef(char c) => c is '\u0622' or '\u0623' or '\u0625' or '\u0627';

        /// <summary>Determines whether a lam completes a lam-alef ligature that was mapped as alef then lam.</summary>
        /// <param name="character">The new character.</param>
        /// <returns><see langword="true"/> when the lam belongs before the alef just added.</returns>
        private bool IsReversedLigature(in PageCharacter character) =>
            IsRightToLeft && character.Value == ArabicLam && _text.Length > 0 && IsAlef(_text[^1]) && character.Bounds == Last;
    }

    /// <summary>A block being built from lines.</summary>
    private sealed class TextBlock
    {
        /// <summary>How far before a line-end hyphen the letter it breaks sits.</summary>
        private const int LetterBeforeHyphen = 2;

        /// <summary>The lines, top to bottom.</summary>
        private readonly List<TextLine> _lines = [];

        /// <summary>Initializes a new instance of the <see cref="TextBlock"/> class with its first line.</summary>
        /// <param name="line">The line.</param>
        internal TextBlock(TextLine line)
        {
            Bounds = line.Bounds;
            _lines.Add(line);
        }

        /// <summary>Gets the block's box.</summary>
        internal PageRect Bounds { get; private set; }

        /// <summary>Gets the last line.</summary>
        internal TextLine Last => _lines[^1];

        /// <summary>Gets the first line's text.</summary>
        internal string FirstText => _lines[0].Text;

        /// <summary>Gets the last line's text.</summary>
        internal string LastText => _lines[^1].Text;

        /// <summary>Gets the number of lines.</summary>
        internal int LineCount => _lines.Count;

        /// <summary>Gets the visible characters.</summary>
        internal int TextLength
        {
            get
            {
                var length = 0;
                foreach (var line in _lines)
                {
                    length += line.Length;
                }

                return length;
            }
        }

        /// <summary>Gets the first line's font size, which sets the block's.</summary>
        internal float FontSize => _lines[0].FontSize;

        /// <summary>Gets a value indicating whether the block is bold.</summary>
        internal bool IsBold => _lines[0].IsBold;

        /// <summary>Adds a line below.</summary>
        /// <param name="line">The line.</param>
        internal void Add(TextLine line)
        {
            _lines.Add(line);
            Bounds = Bounds.Union(line.Bounds);
        }

        /// <summary>Joins the lines into one, mending words hyphenated at line ends.</summary>
        /// <param name="kind">The block's kind.</param>
        /// <param name="level">The heading level, or zero.</param>
        /// <param name="isTagged">Whether the block comes from the document's structure tags.</param>
        /// <returns>The block.</returns>
        internal ReadingBlock ToReadingBlock(ReadingBlockKind kind, int level, bool isTagged)
        {
            var text = new StringBuilder(TextLength + _lines.Count);
            var indices = new List<int>(TextLength + _lines.Count);
            foreach (var line in _lines)
            {
                var builder = line.Builder;
                if (text.Length > 0)
                {
                    if (text.Length >= LetterBeforeHyphen && text[^1] == '-' && char.IsLetter(text[text.Length - LetterBeforeHyphen]) && builder.Length > 0 && char.IsLower(builder[0]))
                    {
                        text.Length--;
                        indices.RemoveAt(indices.Count - 1);
                    }
                    else
                    {
                        _ = text.Append(' ');
                        indices.Add(-1);
                    }
                }

                _ = text.Append(builder);
                indices.AddRange(line.Indices);
            }

            return new(kind, text.ToString(), [.. indices], Bounds, FontSize) { Level = level, IsTagged = isTagged };
        }
    }
}
