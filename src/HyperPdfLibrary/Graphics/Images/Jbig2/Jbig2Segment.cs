// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>A parsed segment header (T.88 section 7.2) and what its data decoded to.</summary>
[DebuggerDisplay("Jbig2Segment: {Number} {Type}")]
internal sealed class Jbig2Segment : IDisposable
{
    /// <summary>The data length that means the length is not known in advance.</summary>
    internal const uint UnknownLength = uint.MaxValue;

    /// <summary>The mask of the type in the segment header flags.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The flag that makes the page association four bytes long.</summary>
    private const int LongPageFlag = 0x40;

    /// <summary>The shift of the short referred-to segment count.</summary>
    private const int CountShift = 5;

    /// <summary>The short count value that means the long form follows.</summary>
    private const int LongCount = 7;

    /// <summary>The mask of the long referred-to segment count.</summary>
    private const uint LongCountMask = 0x1FFFFFFF;

    /// <summary>The highest segment number whose references take one byte.</summary>
    private const uint ByteNumbers = 256;

    /// <summary>The highest segment number whose references take two bytes.</summary>
    private const uint ShortNumbers = 65_536;

    /// <summary>The bytes of a four-byte reference.</summary>
    private const int LongReference = 4;

    /// <summary>The bytes of a two-byte reference.</summary>
    private const int ShortReference = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The referred-to segment numbers; the first <see cref="_referredCount"/> are in use.</summary>
    private uint[] _referred = [];

    /// <summary>The number of referred-to segments.</summary>
    private int _referredCount;

    /// <summary>Whether <see cref="_referred"/> is borrowed from the owner's scratch cache.</summary>
    private bool _referredRented;

    /// <summary>The workspace that takes this segment back when it is disposed, or <see langword="null"/>.</summary>
    private Jbig2Workspace? _owner;

    /// <summary>Gets the segment number.</summary>
    internal uint Number { get; private set; }

    /// <summary>Gets the segment type.</summary>
    internal Jbig2SegmentType Type { get; private set; }

    /// <summary>Gets the data length, or <see cref="UnknownLength"/>.</summary>
    internal uint DataLength { get; private set; }

    /// <summary>Gets the offset of the data in the stream.</summary>
    internal int DataOffset { get; private set; }

    /// <summary>Gets the referred-to segment numbers.</summary>
    internal ReadOnlySpan<uint> Referred => _referred.AsSpan(0, _referredCount);

    /// <summary>Gets or sets the decoded symbol dictionary.</summary>
    internal Jbig2SymbolDictionary? Symbols { get; set; }

    /// <summary>Gets or sets the decoded pattern dictionary.</summary>
    internal Jbig2PatternDictionary? Patterns { get; set; }

    /// <summary>Gets or sets the parsed Huffman table.</summary>
    internal Jbig2HuffmanTable? Table { get; set; }

    /// <summary>Gets or sets the decoded intermediate region.</summary>
    internal Jbig2Bitmap? Bitmap { get; set; }

    /// <summary>Returns the buffers of whatever the segment decoded to, and the segment to its workspace.</summary>
    public void Dispose()
    {
        Symbols?.Dispose();
        Patterns?.Dispose();
        Bitmap?.Dispose();
        Symbols = null;
        Patterns = null;
        Table = null;
        Bitmap = null;
        ReturnReferred();
        var owner = _owner;
        _owner = null;
        owner?.Segments.Give(this);
    }

    /// <summary>Parses a segment header (T.88 section 7.2).</summary>
    /// <param name="reader">The reader, at the header.</param>
    /// <returns>The segment, or <see langword="null"/> when the header is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Jbig2Segment? Parse(ref Jbig2Reader reader) => Parse(ref reader, null);

    /// <summary>Parses a segment header (T.88 section 7.2), reusing a workspace's idle segment.</summary>
    /// <param name="reader">The reader, at the header.</param>
    /// <param name="workspace">The workspace that provides the segment and takes it back when it is disposed, or <see langword="null"/> to make a new one.</param>
    /// <returns>The segment, or <see langword="null"/> when the header is damaged.</returns>
    internal static Jbig2Segment? Parse(ref Jbig2Reader reader, Jbig2Workspace? workspace)
    {
        var segment = workspace?.Segments.Take() ?? new();
        segment._owner = workspace;
        if (segment.TryRead(ref reader))
        {
            return segment;
        }

        segment.Dispose();
        return null;
    }

    /// <summary>Gets the bytes of each referred-to segment number, from the segment's own number.</summary>
    /// <param name="number">The segment number.</param>
    /// <returns>1, 2 or 4.</returns>
    private static int ReferenceSize(uint number)
    {
        if (number > ShortNumbers)
        {
            return LongReference;
        }

        return number > ByteNumbers ? ShortReference : 1;
    }

    /// <summary>Reads the referred-to segment count and skips the retention flags.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The segment's own number.</param>
    /// <param name="count">The count.</param>
    /// <returns>
    /// <see langword="false"/> when the data ends or the count is impossible. A segment refers only to lower-numbered
    /// segments (T.88 section 7.2.5), so the count cannot pass its own number, and the numbers it lists must fit in the
    /// data that is left (7.2.4), which together bound the count without a fixed limit.
    /// </returns>
    private static bool TryReadReferredCount(ref Jbig2Reader reader, uint number, out int count)
    {
        count = 0;
        if (reader.PeekByte() >> CountShift != LongCount)
        {
            var read = reader.TryReadByte(out var value);
            count = value >> CountShift;
            return read && count <= number;
        }

        if (!reader.TryReadUInt32(out var longCount))
        {
            return false;
        }

        var declared = longCount & LongCountMask;
        var retentionBytes = ((long)declared + ByteBits) / ByteBits;
        if (declared > number || retentionBytes + (declared * ReferenceSize(number)) > reader.BytesLeft)
        {
            return false;
        }

        count = (int)declared;
        reader.Skip((int)retentionBytes);
        return true;
    }

    /// <summary>Reads one referred-to segment number.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="size">The bytes of the number: 1, 2 or 4.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    private static bool TryReadNumber(ref Jbig2Reader reader, int size, out uint number)
    {
        number = 0;
        if (reader.BytesLeft < size)
        {
            return false;
        }

        var bytes = reader.Data.Slice(reader.Offset, size);
        number = size switch
        {
            1 => bytes[0],
            ShortReference => BinaryPrimitives.ReadUInt16BigEndian(bytes),
            _ => BinaryPrimitives.ReadUInt32BigEndian(bytes),
        };
        reader.Skip(size);
        return true;
    }

    /// <summary>Gives a borrowed referred-to list back to the scratch cache.</summary>
    private void ReturnReferred()
    {
        if (_referredRented && _owner is { } owner)
        {
            owner.ScratchPools.Get<uint>().Return(_referred);
        }

        _referred = [];
        _referredRented = false;
        _referredCount = 0;
    }

    /// <summary>Reads the header fields into this segment.</summary>
    /// <param name="reader">The reader, at the header.</param>
    /// <returns><see langword="false"/> when the header is damaged.</returns>
    private bool TryRead(ref Jbig2Reader reader)
    {
        if (!reader.TryReadUInt32(out var number) || !reader.TryReadByte(out var flags) || !TryReadReferredCount(ref reader, number, out var count)
            || !TryReadReferred(ref reader, number, count))
        {
            return false;
        }

        var pageRead = (flags & LongPageFlag) != 0 ? reader.TryReadUInt32(out _) : reader.TryReadByte(out _);
        if (!pageRead || !reader.TryReadUInt32(out var length))
        {
            return false;
        }

        Number = number;
        Type = (Jbig2SegmentType)(flags & TypeMask);
        DataLength = length;
        DataOffset = reader.Offset;
        return true;
    }

    /// <summary>Reads the referred-to segment numbers, each lower than the segment's own.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The segment's number, which sets the size of each reference.</param>
    /// <param name="count">The number of references.</param>
    /// <returns><see langword="false"/> when the data ends or a reference is not lower.</returns>
    private bool TryReadReferred(ref Jbig2Reader reader, uint number, int count)
    {
        _referredCount = 0;
        if (count == 0)
        {
            return true;
        }

        if (_referred.Length < count)
        {
            // A segment of a workspace borrows its list from the scratch cache, so a reused segment never needs a longer list built.
            ReturnReferred();
            _referred = _owner is { } owner ? owner.ScratchPools.Get<uint>().Rent(count) : new uint[count];
            _referredRented = _owner is not null;
        }

        var size = ReferenceSize(number);
        for (var i = 0; i < count; i++)
        {
            if (!TryReadNumber(ref reader, size, out _referred[i]) || _referred[i] >= number)
            {
                return false;
            }
        }

        _referredCount = count;
        return true;
    }
}
