// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>
/// Groups lines into blocks, puts the blocks in reading order with recursive XY-cuts, and turns a block into the text and
/// character indices the caller reads. A band across the page (a title, a full-width figure) is read before the columns
/// below it, and each column top to bottom before the next.
/// </summary>
internal static class ReadingBlocks
{
    /// <summary>The characters a block's text may take before it is built in a rented buffer.</summary>
    private const int StackChars = 512;

    /// <summary>How far before a line-end hyphen the letter it breaks sits.</summary>
    private const int LetterBeforeHyphen = 2;

    /// <summary>A gap between lines no larger than this many line heights keeps them in one block.</summary>
    private const float BlockGapLines = 0.8F;

    /// <summary>Lines whose font sizes differ by more than this share are in different blocks.</summary>
    private const float FontSizeTolerance = 0.18F;

    /// <summary>The smallest empty strip between columns, in points.</summary>
    private const float MinGutter = 6F;

    /// <summary>The smallest empty band between rows of blocks, in points.</summary>
    private const float MinBand = 1F;

    /// <summary>Groups the scratch's lines into blocks: each line joins the block directly above it when close, overlapping and alike.</summary>
    /// <param name="scratch">The working space; its lines are sorted top to bottom and its blocks are made.</param>
    internal static void Build(ReadingScratch scratch)
    {
        scratch.Lines.AsSpan(0, scratch.LineCount).Sort(static (a, b) => CompareTopLeft(a.Bounds, b.Bounds));
        for (var i = 0; i < scratch.LineCount; i++)
        {
            var target = ReadingText.StartsItem(TextOf(scratch, i)) ? -1 : FindBlockAbove(scratch, i);
            if (target < 0)
            {
                var block = scratch.AddBlock();
                scratch.Blocks[block] = Start(scratch, i);
            }
            else
            {
                Add(scratch, ref scratch.Blocks[target], i);
            }
        }
    }

    /// <summary>Makes one block from a run of consecutive lines, in the order given.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="first">The first line.</param>
    /// <param name="count">The number of lines.</param>
    /// <returns>The block, with no lines when <paramref name="count"/> is zero.</returns>
    internal static BlockRecord Join(ReadingScratch scratch, int first, int count)
    {
        if (count == 0)
        {
            return default;
        }

        var block = Start(scratch, first);
        for (var line = first + 1; line < first + count; line++)
        {
            Add(scratch, ref block, line);
        }

        return block;
    }

    /// <summary>Puts the scratch's blocks in reading order, in place.</summary>
    /// <param name="scratch">The working space.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Order(ReadingScratch scratch) => Order(scratch.Blocks.AsSpan(0, scratch.BlockCount));

    /// <summary>Gets a line's text.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="line">The line.</param>
    /// <returns>The text, with inferred spaces.</returns>
    internal static ReadOnlySpan<char> TextOf(ReadingScratch scratch, int line)
    {
        ref readonly var record = ref scratch.Lines[line];
        return scratch.Chars.AsSpan(record.Start, record.Count);
    }

    /// <summary>Joins a block's lines into one, mending words hyphenated at line ends.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <param name="role">The block's kind and heading level.</param>
    /// <param name="cell">Where the block sits in a table, and whether it comes from the document's structure tags.</param>
    /// <returns>The block to read, with the text of its lines joined.</returns>
    internal static ReadingBlock ToReadingBlock(ReadingScratch scratch, BlockRecord block, ReadingRole role, ReadingCell cell)
    {
        var needed = block.LineCount;
        for (var line = block.First; line >= 0; line = scratch.Lines[line].Next)
        {
            needed += scratch.Lines[line].Count;
        }

        char[]? rentedText = null;
        int[]? rentedMap = null;
        var text = needed <= StackChars ? stackalloc char[StackChars] : (rentedText = ArrayPool<char>.Shared.Rent(needed));
        var map = needed <= StackChars ? stackalloc int[StackChars] : (rentedMap = ArrayPool<int>.Shared.Rent(needed));
        try
        {
            var length = Write(scratch, block, text, map);
            return new(role.Kind, new string(text[..length]), map[..length].ToArray(), block.Bounds, scratch.Lines[block.First].FontSize)
            {
                Level = role.Level,
                IsTagged = cell.IsTagged,
                Row = cell.Row,
                Column = cell.Column,
            };
        }
        finally
        {
            if (rentedText is not null)
            {
                ArrayPool<char>.Shared.Return(rentedText);
            }

            if (rentedMap is not null)
            {
                ArrayPool<int>.Shared.Return(rentedMap);
            }
        }
    }

    /// <summary>Writes a block's joined text and the page index of each character.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <param name="text">Receives the text.</param>
    /// <param name="map">Receives the page index of each character; -1 for joining spaces.</param>
    /// <returns>The characters written.</returns>
    private static int Write(ReadingScratch scratch, BlockRecord block, Span<char> text, Span<int> map)
    {
        var length = 0;
        for (var at = block.First; at >= 0; at = scratch.Lines[at].Next)
        {
            ref readonly var line = ref scratch.Lines[at];
            var lineText = scratch.Chars.AsSpan(line.Start, line.Count);
            if (length > 0)
            {
                if (length >= LetterBeforeHyphen && text[length - 1] == '-' && char.IsLetter(text[length - LetterBeforeHyphen]) && !lineText.IsEmpty && char.IsLower(lineText[0]))
                {
                    length--;
                }
                else
                {
                    text[length] = ' ';
                    map[length] = -1;
                    length++;
                }
            }

            lineText.CopyTo(text[length..]);
            scratch.Indices.AsSpan(line.Start, line.Count).CopyTo(map[length..]);
            length += line.Count;
        }

        return length;
    }

    /// <summary>Starts a block with a line.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="line">The line.</param>
    /// <returns>The block.</returns>
    private static BlockRecord Start(ReadingScratch scratch, int line)
    {
        ref readonly var record = ref scratch.Lines[line];
        return new() { First = line, Last = line, LineCount = 1, TextLength = record.Visible, Bounds = record.Bounds };
    }

    /// <summary>Adds a line below a block's last line.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">The line.</param>
    private static void Add(ReadingScratch scratch, ref BlockRecord block, int line)
    {
        scratch.Lines[block.Last].Next = line;
        block.Last = line;
        block.LineCount++;
        block.TextLength += scratch.Lines[line].Visible;
        block.Bounds = block.Bounds.Union(scratch.Lines[line].Bounds);
    }

    /// <summary>Finds the block a line continues: directly above, close, overlapping and in a similar font.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="at">The line.</param>
    /// <returns>The block, or -1 to start a new one.</returns>
    private static int FindBlockAbove(ReadingScratch scratch, int at)
    {
        ref readonly var line = ref scratch.Lines[at];
        var best = -1;
        var bestGap = float.MaxValue;
        for (var i = 0; i < scratch.BlockCount; i++)
        {
            ref readonly var block = ref scratch.Blocks[i];
            ref readonly var last = ref scratch.Lines[block.Last];
            var gap = line.Bounds.Top - last.Bounds.Bottom;
            var height = Math.Max(last.Bounds.Height, line.Bounds.Height);
            var overlaps = line.Bounds.Left < block.Bounds.Right && line.Bounds.Right > block.Bounds.Left;
            var blockSize = scratch.Lines[block.First].FontSize;
            var alike = MathF.Abs(line.FontSize - blockSize) <= blockSize * FontSizeTolerance;
            if (line.Bounds.Top < last.Bounds.Top || gap > height * BlockGapLines || !overlaps || !alike || gap >= bestGap || ReadingText.IsCaption(TextOf(scratch, at)))
            {
                continue;
            }

            best = i;
            bestGap = gap;
        }

        return best;
    }

    /// <summary>Orders blocks by recursive XY-cuts: columns left to right where there is a gutter, otherwise bands top to bottom.</summary>
    /// <param name="blocks">The blocks of the region; reordered in place.</param>
    private static void Order(Span<BlockRecord> blocks)
    {
        if (blocks.Length <= 1)
        {
            return;
        }

        var vertical = true;
        if (Cut(blocks, vertical, MinGutter) <= 1)
        {
            vertical = false;
            if (Cut(blocks, vertical, MinBand) <= 1)
            {
                blocks.Sort(static (a, b) => CompareTopLeft(a.Bounds, b.Bounds));
                return;
            }
        }

        var minGap = vertical ? MinGutter : MinBand;
        for (var start = 0; start < blocks.Length;)
        {
            var end = GroupEnd(blocks, start, vertical, minGap);
            Order(blocks[start..end]);
            start = end;
        }
    }

    /// <summary>Sorts blocks along an axis and counts the groups that empty strips wide enough to count split them into.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="vertical">Whether to cut into columns (else into bands).</param>
    /// <param name="minGap">The narrowest strip that counts.</param>
    /// <returns>The number of groups.</returns>
    private static int Cut(Span<BlockRecord> blocks, bool vertical, float minGap)
    {
        blocks.Sort(vertical
            ? static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left)
            : static (a, b) => a.Bounds.Top.CompareTo(b.Bounds.Top));
        var groups = 0;
        for (var start = 0; start < blocks.Length; start = GroupEnd(blocks, start, vertical, minGap))
        {
            groups++;
        }

        return groups;
    }

    /// <summary>Finds where the group of sorted blocks that starts at a block ends.</summary>
    /// <param name="blocks">The sorted blocks.</param>
    /// <param name="start">The group's first block.</param>
    /// <param name="vertical">Whether the blocks are sorted left to right (else top to bottom).</param>
    /// <param name="minGap">The narrowest strip that counts.</param>
    /// <returns>The position after the group's last block.</returns>
    private static int GroupEnd(ReadOnlySpan<BlockRecord> blocks, int start, bool vertical, float minGap)
    {
        var reach = vertical ? blocks[start].Bounds.Right : blocks[start].Bounds.Bottom;
        var end = start + 1;
        while (end < blocks.Length)
        {
            var next = vertical ? blocks[end].Bounds.Left : blocks[end].Bounds.Top;
            if (next - reach >= minGap)
            {
                break;
            }

            reach = Math.Max(reach, vertical ? blocks[end].Bounds.Right : blocks[end].Bounds.Bottom);
            end++;
        }

        return end;
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
}
