// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// A JBIG2 Huffman table (T.88 annex B): lines with a prefix code, a range length and a range low value, with canonical
/// codes assigned by prefix length. The last lines are the lower range, the upper range and, optionally, out-of-band.
/// Standard tables are immutable. Workspace tables are leased to one decode until its workspace returns.
/// </summary>
[DebuggerDisplay("Jbig2HuffmanTable: {LineCount} lines, out-of-band {HasOutOfBand}")]
internal sealed partial class Jbig2HuffmanTable
{
    /// <summary>The largest code value kept: prefix codes are at most 32 bits (T.88 section B.3).</summary>
    private const ulong MaxCode = uint.MaxValue;

    /// <summary>The range length of the lower and upper range lines.</summary>
    private const int OpenRangeLength = 32;

    /// <summary>The shift of the prefix-length size in a table segment's flags.</summary>
    private const int PrefixSizeShift = 1;

    /// <summary>The shift of the range-length size in a table segment's flags.</summary>
    private const int RangeSizeShift = 4;

    /// <summary>The mask of a size field in a table segment's flags.</summary>
    private const int SizeMask = 7;

    /// <summary>The longest range length an ordinary line of a table segment may have.</summary>
    private const int MaxLineRangeLength = 31;

    /// <summary>The dummy lines added after the lines of a code-length table.</summary>
    private const int DummyLines = 2;

    /// <summary>The lines from the end to the lower range line in a table with an out-of-band line.</summary>
    private const int LowerOffsetWithOutOfBand = 3;

    /// <summary>The lines from the end to the lower range line in a table without an out-of-band line.</summary>
    private const int LowerOffset = 2;

    /// <summary>The prefix lengths whose counters fit on the stack; table segments allow at most 255.</summary>
    private const int StackLengths = 256;

    /// <summary>The range length of each line.</summary>
    private byte[] _rangeLengths = [];

    /// <summary>The range low value of each line.</summary>
    private int[] _rangeLows = [];

    /// <summary>The first code of each prefix length.</summary>
    private uint[] _firstCodes = [];

    /// <summary>The number of codes of each prefix length.</summary>
    private int[] _codeCounts = [];

    /// <summary>The index in <see cref="_linesByLength"/> of the first line of each prefix length.</summary>
    private int[] _firstLines = [];

    /// <summary>The line indices ordered by prefix length, then by line order.</summary>
    private int[] _linesByLength = [];

    /// <summary>The index of the lower range line, whose values count down.</summary>
    private int _lowerLine;

    /// <summary>The index of the out-of-band line, or -1.</summary>
    private int _outOfBandLine;

    /// <summary>Initializes a new instance of the <see cref="Jbig2HuffmanTable"/> class.</summary>
    private Jbig2HuffmanTable()
    {
    }

    /// <summary>Gets a value indicating whether the table has an out-of-band line.</summary>
    internal bool HasOutOfBand { get; private set; }

    /// <summary>Gets the longest prefix code.</summary>
    internal int MaxLength { get; private set; }

    /// <summary>Gets the number of lines.</summary>
    internal int LineCount { get; private set; }

    /// <summary>Builds a table from its lines.</summary>
    /// <param name="prefixLengths">The prefix length of each line; zero means the line has no code.</param>
    /// <param name="rangeLengths">The range length of each line.</param>
    /// <param name="rangeLows">The range low value of each line.</param>
    /// <param name="hasOutOfBand">Whether the last line is out-of-band.</param>
    /// <returns>The table, or <see langword="null"/> when the codes do not fit 32 bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Jbig2HuffmanTable? Create(ReadOnlySpan<int> prefixLengths, ReadOnlySpan<byte> rangeLengths, ReadOnlySpan<int> rangeLows, bool hasOutOfBand) =>
        Create(prefixLengths, rangeLengths, rangeLows, hasOutOfBand, null);

    /// <summary>Builds a table that maps each code length's index to itself, as used for symbol IDs and run codes.</summary>
    /// <param name="codeLengths">The code length of each value.</param>
    /// <returns>The table, or <see langword="null"/> when the codes do not fit 32 bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Jbig2HuffmanTable? FromCodeLengths(ReadOnlySpan<byte> codeLengths) =>
        FromCodeLengths(codeLengths, null);

    /// <summary>Builds a symbol-ID or run-code table for a decode.</summary>
    /// <param name="codeLengths">The prefix length of each value.</param>
    /// <param name="workspace">The owner, or null for a permanent table.</param>
    /// <returns>The table, or null for invalid codes.</returns>
    internal static Jbig2HuffmanTable? FromCodeLengths(ReadOnlySpan<byte> codeLengths, Jbig2Workspace? workspace)
    {
        var count = codeLengths.Length + DummyLines;
        var scratch = workspace?.ScratchPools ?? new ScratchPools();
        var prefixLengths = scratch.Ints.Rent(count);
        var rangeLows = scratch.Ints.Rent(count);
        var ranges = scratch.Bytes.Rent(count);
        try
        {
            prefixLengths.AsSpan(0, count).Clear();
            rangeLows.AsSpan(0, count).Clear();
            ranges.AsSpan(0, count).Clear();
            for (var i = 0; i < codeLengths.Length; i++)
            {
                prefixLengths[i] = codeLengths[i];
                rangeLows[i] = i;
            }

            return Create(prefixLengths.AsSpan(0, count), ranges.AsSpan(0, count), rangeLows.AsSpan(0, count), false, workspace);
        }
        finally
        {
            scratch.Ints.Return(prefixLengths);
            scratch.Ints.Return(rangeLows);
            scratch.Bytes.Return(ranges);
        }
    }

    /// <summary>Parses a table segment (T.88 section 7.4.13 and B.2).</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <returns>The table, or <see langword="null"/> when the segment is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Jbig2HuffmanTable? Parse(ref Jbig2Reader reader) =>
        Parse(ref reader, null);

    /// <summary>Parses a table segment into workspace-owned storage.</summary>
    /// <param name="reader">The reader at the table segment.</param>
    /// <param name="workspace">The owner, or null for a permanent table.</param>
    /// <returns>The table, or null for damaged input.</returns>
    internal static Jbig2HuffmanTable? Parse(ref Jbig2Reader reader, Jbig2Workspace? workspace)
    {
        if (!reader.TryReadByte(out var flags) || !reader.TryReadInt32(out var low) || !reader.TryReadInt32(out var high) || low > high)
        {
            return null;
        }

        var lines = workspace?.HuffmanLines ?? new LineList();
        lines.Begin(((flags >> PrefixSizeShift) & SizeMask) + 1, ((flags >> RangeSizeShift) & SizeMask) + 1);
        var hasOutOfBand = (flags & 1) != 0;
        return ReadLines(ref reader, lines, low, high) && ReadEndLines(ref reader, lines, low, high, hasOutOfBand)
            ? Create(CollectionsMarshal.AsSpan(lines.PrefixLengths), CollectionsMarshal.AsSpan(lines.RangeLengths), CollectionsMarshal.AsSpan(lines.RangeLows), hasOutOfBand, workspace)
            : null;
    }

    /// <summary>Returns a workspace table's arrays after all of the decode's references have expired.</summary>
    /// <param name="scratch">The owner of the arrays.</param>
    internal void Release(ScratchPools scratch)
    {
        scratch.Bytes.Return(_rangeLengths);
        scratch.Ints.Return(_rangeLows);
        scratch.Get<uint>().Return(_firstCodes);
        scratch.Ints.Return(_codeCounts);
        scratch.Ints.Return(_firstLines);
        scratch.Ints.Return(_linesByLength);
        _rangeLengths = [];
        _rangeLows = [];
        _firstCodes = [];
        _codeCounts = [];
        _firstLines = [];
        _linesByLength = [];
    }

    /// <summary>Decodes one value.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether a value, the out-of-band code or an error was read.</returns>
    internal Jbig2HuffmanResult Decode(ref Jbig2Reader reader, out int value)
    {
        value = 0;
        ulong code = 0;
        for (var length = 1; length <= MaxLength; length++)
        {
            if (!reader.TryReadBit(out var bit))
            {
                return Jbig2HuffmanResult.Error;
            }

            code = (code << 1) | (uint)bit;
            if (code > MaxCode)
            {
                return Jbig2HuffmanResult.Error;
            }

            var line = FindLine(length, (uint)code);
            if (line >= 0)
            {
                return ReadValue(ref reader, line, out value);
            }
        }

        return Jbig2HuffmanResult.Error;
    }

    /// <summary>Builds a table whose working storage belongs to a decode when a workspace is supplied.</summary>
    /// <param name="prefixLengths">The prefix lengths.</param>
    /// <param name="rangeLengths">The range lengths.</param>
    /// <param name="rangeLows">The range low values.</param>
    /// <param name="hasOutOfBand">Whether the last line is out-of-band.</param>
    /// <param name="workspace">The owner, or null for a permanent table.</param>
    /// <returns>The table, or null for invalid codes.</returns>
    private static Jbig2HuffmanTable? Create(ReadOnlySpan<int> prefixLengths, ReadOnlySpan<byte> rangeLengths, ReadOnlySpan<int> rangeLows, bool hasOutOfBand, Jbig2Workspace? workspace)
    {
        var maxLength = 0;
        foreach (var length in prefixLengths)
        {
            maxLength = Math.Max(maxLength, length);
        }

        var table = workspace?.HuffmanTables.Take() ?? new();
        workspace?.KeepHuffmanTable(table);
        table.Initialize(rangeLengths, rangeLows, hasOutOfBand, maxLength, workspace?.ScratchPools);
        return table.AssignCodes(prefixLengths) ? table : null;
    }

    /// <summary>Reads the ordinary lines of a table segment.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="low">The lowest value of the table.</param>
    /// <param name="high">The value above the highest ordinary value.</param>
    /// <returns><see langword="false"/> when the data ends or a range overflows.</returns>
    private static bool ReadLines(ref Jbig2Reader reader, LineList lines, int low, int high)
    {
        long current = low;
        do
        {
            if (!reader.TryReadBits(lines.PrefixSize, out var prefix) || !reader.TryReadBits(lines.RangeSize, out var range) || range > MaxLineRangeLength)
            {
                return false;
            }

            lines.Add((int)prefix, (byte)range, (int)current);
            current += 1L << (int)range;
            if (current > int.MaxValue)
            {
                return false;
            }
        }
        while (current < high);

        return true;
    }

    /// <summary>Reads the lower range, upper range and out-of-band lines of a table segment.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="low">The lowest value of the table.</param>
    /// <param name="high">The value above the highest ordinary value.</param>
    /// <param name="hasOutOfBand">Whether an out-of-band line follows.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    private static bool ReadEndLines(ref Jbig2Reader reader, LineList lines, int low, int high, bool hasOutOfBand)
    {
        if (low == int.MinValue || !reader.TryReadBits(lines.PrefixSize, out var lower) || !reader.TryReadBits(lines.PrefixSize, out var upper))
        {
            return false;
        }

        lines.Add((int)lower, OpenRangeLength, low - 1);
        lines.Add((int)upper, OpenRangeLength, high);
        if (!hasOutOfBand)
        {
            return true;
        }

        if (!reader.TryReadBits(lines.PrefixSize, out var outOfBand))
        {
            return false;
        }

        lines.Add((int)outOfBand, 0, 0);
        return true;
    }

    /// <summary>Copies line values and prepares cleared canonical-code indexes.</summary>
    /// <param name="ranges">The line range lengths.</param>
    /// <param name="lows">The line range low values.</param>
    /// <param name="hasOutOfBand">Whether the final line is out-of-band.</param>
    /// <param name="maxLength">The longest prefix code.</param>
    /// <param name="scratch">The array owner, or null for permanent storage.</param>
    private void Initialize(ReadOnlySpan<byte> ranges, ReadOnlySpan<int> lows, bool hasOutOfBand, int maxLength, ScratchPools? scratch)
    {
        LineCount = ranges.Length;
        HasOutOfBand = hasOutOfBand;
        MaxLength = maxLength;
        _rangeLengths = scratch is null ? new byte[ranges.Length] : scratch.Bytes.Rent(ranges.Length);
        _rangeLows = scratch is null ? new int[lows.Length] : scratch.Ints.Rent(lows.Length);
        _firstCodes = scratch is null ? new uint[maxLength + 1] : scratch.Get<uint>().Rent(maxLength + 1);
        _codeCounts = scratch is null ? new int[maxLength + 1] : scratch.Ints.Rent(maxLength + 1);
        _firstLines = scratch is null ? new int[maxLength + 1] : scratch.Ints.Rent(maxLength + 1);
        _linesByLength = scratch is null ? new int[ranges.Length] : scratch.Ints.Rent(ranges.Length);
        ranges.CopyTo(_rangeLengths);
        lows.CopyTo(_rangeLows);
        _codeCounts.AsSpan(0, maxLength + 1).Clear();
        _lowerLine = ranges.Length - (hasOutOfBand ? LowerOffsetWithOutOfBand : LowerOffset);
        _outOfBandLine = hasOutOfBand ? ranges.Length - 1 : -1;
    }

    /// <summary>Assigns canonical codes by prefix length and builds the lookup index.</summary>
    /// <param name="prefixLengths">The prefix length of each line.</param>
    /// <returns><see langword="false"/> when a first code passes 32 bits.</returns>
    private bool AssignCodes(ReadOnlySpan<int> prefixLengths)
    {
        foreach (var length in prefixLengths)
        {
            _codeCounts[length]++;
        }

        _codeCounts[0] = 0;
        ulong firstCode = 0;
        var nextLine = 0;
        for (var length = 1; length <= MaxLength; length++)
        {
            firstCode = (firstCode + (uint)_codeCounts[length - 1]) << 1;
            if (firstCode > MaxCode)
            {
                return false;
            }

            _firstCodes[length] = (uint)firstCode;
            _firstLines[length] = nextLine;
            nextLine += _codeCounts[length];
        }

        FillLineIndex(prefixLengths);
        return true;
    }

    /// <summary>Lists the lines of each prefix length in line order.</summary>
    /// <param name="prefixLengths">The prefix length of each line.</param>
    private void FillLineIndex(ReadOnlySpan<int> prefixLengths)
    {
        Span<int> filled = MaxLength < StackLengths ? stackalloc int[StackLengths] : new int[MaxLength + 1];
        filled.Clear();
        for (var i = 0; i < prefixLengths.Length; i++)
        {
            var length = prefixLengths[i];
            if (length == 0)
            {
                continue;
            }

            _linesByLength[_firstLines[length] + filled[length]] = i;
            filled[length]++;
        }
    }

    /// <summary>Finds the line whose code of a length matches.</summary>
    /// <param name="length">The code length.</param>
    /// <param name="code">The code.</param>
    /// <returns>The line, or -1.</returns>
    private int FindLine(int length, uint code)
    {
        var first = _firstCodes[length];
        if (code < first)
        {
            return -1;
        }

        var rank = code - first;
        return rank < (uint)_codeCounts[length] ? _linesByLength[_firstLines[length] + (int)rank] : -1;
    }

    /// <summary>Reads the range bits of a matched line and forms the value.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="line">The line.</param>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    private Jbig2HuffmanResult ReadValue(ref Jbig2Reader reader, int line, out int value)
    {
        value = 0;
        if (line == _outOfBandLine)
        {
            return Jbig2HuffmanResult.OutOfBand;
        }

        if (!reader.TryReadBits(_rangeLengths[line], out var offset))
        {
            return Jbig2HuffmanResult.Error;
        }

        value = line == _lowerLine ? unchecked(_rangeLows[line] - (int)offset) : unchecked(_rangeLows[line] + (int)offset);
        return Jbig2HuffmanResult.Value;
    }

    /// <summary>The lines of a table segment as they are read.</summary>
    [DebuggerDisplay("LineList: {PrefixLengths.Count} lines")]
    internal sealed class LineList
    {
        /// <summary>Gets the bits of a prefix length.</summary>
        internal int PrefixSize { get; private set; }

        /// <summary>Gets the bits of a range length.</summary>
        internal int RangeSize { get; private set; }

        /// <summary>Gets the prefix length of each line.</summary>
        internal List<int> PrefixLengths { get; } = [];

        /// <summary>Gets the range length of each line.</summary>
        internal List<byte> RangeLengths { get; } = [];

        /// <summary>Gets the range low value of each line.</summary>
        internal List<int> RangeLows { get; } = [];

        /// <summary>Starts reading another table into reusable line storage.</summary>
        /// <param name="prefixSize">The bits of a prefix length.</param>
        /// <param name="rangeSize">The bits of a range length.</param>
        internal void Begin(int prefixSize, int rangeSize)
        {
            PrefixSize = prefixSize;
            RangeSize = rangeSize;
            PrefixLengths.Clear();
            RangeLengths.Clear();
            RangeLows.Clear();
        }

        /// <summary>Adds a line.</summary>
        /// <param name="prefixLength">The prefix length.</param>
        /// <param name="rangeLength">The range length.</param>
        /// <param name="rangeLow">The range low value.</param>
        internal void Add(int prefixLength, byte rangeLength, int rangeLow)
        {
            PrefixLengths.Add(prefixLength);
            RangeLengths.Add(rangeLength);
            RangeLows.Add(rangeLow);
        }
    }
}
