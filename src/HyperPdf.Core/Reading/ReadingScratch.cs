// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Reading;

/// <summary>
/// The working space for one page's reading order: the lines, their text and the blocks, held in flat arrays that grow
/// once and are reused by the next page, so working out a page allocates only the blocks it returns. One caller at a time
/// holds each scratch.
/// </summary>
[DebuggerDisplay("ReadingScratch: {LineCount} lines, {BlockCount} blocks")]
internal sealed class ReadingScratch
{
    /// <summary>The room a new array starts with.</summary>
    private const int InitialCapacity = 256;

    /// <summary>How many times larger an array grows when it is full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Gets the text of every line, one line after another.</summary>
    internal char[] Chars { get; private set; } = new char[InitialCapacity];

    /// <summary>Gets the page index of each character of <see cref="Chars"/>; -1 for inferred spaces.</summary>
    internal int[] Indices { get; private set; } = new int[InitialCapacity];

    /// <summary>Gets or sets the characters used.</summary>
    internal int CharCount { get; set; }

    /// <summary>Gets the lines.</summary>
    internal LineRecord[] Lines { get; private set; } = new LineRecord[InitialCapacity];

    /// <summary>Gets or sets the lines used.</summary>
    internal int LineCount { get; set; }

    /// <summary>Gets the blocks.</summary>
    internal BlockRecord[] Blocks { get; private set; } = new BlockRecord[InitialCapacity];

    /// <summary>Gets the blocks used.</summary>
    internal int BlockCount { get; private set; }

    /// <summary>Gets the characters of one tagged element.</summary>
    internal PageCharacter[] Subset { get; private set; } = new PageCharacter[InitialCapacity];

    /// <summary>Gets the page index of each character of <see cref="Subset"/>.</summary>
    internal int[] SubsetIndices { get; private set; } = new int[InitialCapacity];

    /// <summary>Gets a copy of a page's characters, for sources that are not a list or an array.</summary>
    internal PageCharacter[] Copy { get; private set; } = new PageCharacter[InitialCapacity];

    /// <summary>Gets the first line of each tagged element.</summary>
    internal int[] ElementFirst { get; private set; } = new int[InitialCapacity];

    /// <summary>Gets the number of lines of each tagged element.</summary>
    internal int[] ElementLines { get; private set; } = new int[InitialCapacity];

    /// <summary>Gets the font sizes seen, in half points, in the order they first appeared.</summary>
    internal int[] SizeKeys { get; private set; } = new int[InitialCapacity];

    /// <summary>Gets the characters set in each font size of <see cref="SizeKeys"/>.</summary>
    internal int[] SizeCounts { get; private set; } = new int[InitialCapacity];

    /// <summary>Clears the used ranges before the owner retains this scratch.</summary>
    internal void Clear()
    {
        LineCount = 0;
        BlockCount = 0;
        CharCount = 0;
    }

    /// <summary>Gets a view of a page's characters, copying them only when the source is neither a list nor an array.</summary>
    /// <param name="characters">The page's characters.</param>
    /// <returns>A view of the same characters.</returns>
    internal ReadOnlySpan<PageCharacter> View(IReadOnlyList<PageCharacter> characters)
    {
        if (characters is List<PageCharacter> list)
        {
            return CollectionsMarshal.AsSpan(list);
        }

        if (characters is PageCharacter[] array)
        {
            return array;
        }

        var copy = Copy;
        Grow(ref copy, characters.Count);
        Copy = copy;
        for (var i = 0; i < characters.Count; i++)
        {
            copy[i] = characters[i];
        }

        return copy.AsSpan(0, characters.Count);
    }

    /// <summary>Makes room for more characters of text.</summary>
    /// <param name="extra">The characters about to be added.</param>
    internal void EnsureChars(int extra)
    {
        var needed = CharCount + extra;
        var chars = Chars;
        var indices = Indices;
        Grow(ref chars, needed);
        Grow(ref indices, needed);
        Chars = chars;
        Indices = indices;
    }

    /// <summary>Starts a line.</summary>
    /// <returns>The line's position.</returns>
    internal int AddLine()
    {
        var lines = Lines;
        Grow(ref lines, LineCount + 1);
        Lines = lines;
        var line = LineCount;
        lines[line] = new() { Start = CharCount, Next = -1 };
        LineCount++;
        return line;
    }

    /// <summary>Starts a block.</summary>
    /// <returns>The block's position.</returns>
    internal int AddBlock()
    {
        var blocks = Blocks;
        Grow(ref blocks, BlockCount + 1);
        Blocks = blocks;
        var block = BlockCount;
        BlockCount++;
        return block;
    }

    /// <summary>Makes room for a tagged element's characters.</summary>
    /// <param name="count">The characters.</param>
    internal void EnsureSubset(int count)
    {
        var subset = Subset;
        var indices = SubsetIndices;
        Grow(ref subset, count);
        Grow(ref indices, count);
        Subset = subset;
        SubsetIndices = indices;
    }

    /// <summary>Makes room for the per-element line ranges.</summary>
    /// <param name="count">The elements.</param>
    internal void EnsureElements(int count)
    {
        var first = ElementFirst;
        var lines = ElementLines;
        Grow(ref first, count);
        Grow(ref lines, count);
        ElementFirst = first;
        ElementLines = lines;
    }

    /// <summary>Makes room for the font size tallies.</summary>
    /// <param name="count">The sizes.</param>
    internal void EnsureSizes(int count)
    {
        var keys = SizeKeys;
        var counts = SizeCounts;
        Grow(ref keys, count);
        Grow(ref counts, count);
        SizeKeys = keys;
        SizeCounts = counts;
    }

    /// <summary>Grows an array to hold at least a number of items, keeping what it holds.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="array">The array; replaced when it is too small.</param>
    /// <param name="needed">The items needed.</param>
    private static void Grow<T>(ref T[] array, int needed)
    {
        if (array.Length >= needed)
        {
            return;
        }

        var larger = new T[Math.Max(needed, array.Length * GrowthFactor)];
        array.AsSpan().CopyTo(larger);
        array = larger;
    }
}
