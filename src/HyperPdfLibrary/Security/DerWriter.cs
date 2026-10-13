// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Writes DER values (ITU-T X.690, clause 8 and 11) into a pooled buffer. Nested values are written in place: a constructed value
/// reserves one length byte, and ending it moves its contents along only when the length needs more bytes. The caller supplies
/// the stack that remembers the open values, so nothing is allocated for them.
/// </summary>
internal ref struct DerWriter
{
    /// <summary>The tag of a SEQUENCE.</summary>
    internal const byte SequenceTag = 0x30;

    /// <summary>The tag of a SET.</summary>
    internal const byte SetTag = 0x31;

    /// <summary>The first context-specific constructed tag, <c>[0]</c>.</summary>
    internal const byte ContextConstructedTag = 0xA0;

    /// <summary>The tag of an INTEGER.</summary>
    private const byte IntegerTag = 0x02;

    /// <summary>The tag of an OCTET STRING.</summary>
    private const byte OctetStringTag = 0x04;

    /// <summary>The tag of a NULL.</summary>
    private const byte NullTag = 0x05;

    /// <summary>The tag of an OBJECT IDENTIFIER.</summary>
    private const byte ObjectIdentifierTag = 0x06;

    /// <summary>The tag of a UTCTime.</summary>
    private const byte UtcTimeTag = 0x17;

    /// <summary>The largest length that fits the one-byte short form.</summary>
    private const int MaxShortLength = 127;

    /// <summary>The flag that marks the long length form.</summary>
    private const int LongForm = 0x80;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The first year a UTCTime can hold under the usual two-digit window.</summary>
    private const int FirstUtcYear = 1950;

    /// <summary>The last year a UTCTime can hold under the usual two-digit window.</summary>
    private const int LastUtcYear = 2049;

    /// <summary>The digits of a UTCTime value: yyMMddHHmmss and the Z.</summary>
    private const int UtcTimeLength = 13;

    /// <summary>The most elements of a SET OF that are sorted without a heap array.</summary>
    private const int StackElements = 32;

    /// <summary>The bytes a constructed value takes when it starts: the tag and one reserved length byte.</summary>
    private const int TagAndLengthBytes = 2;

    /// <summary>The factor the buffer grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The fewest elements a SET OF needs before they have an order to put right.</summary>
    private const int MinSortedElements = 2;

    /// <summary>The bytes a primitive value takes besides its contents: the tag and the longest length.</summary>
    private const int PrimitiveOverhead = 1 + sizeof(int) + 1;

    /// <summary>Where each open value's length byte is.</summary>
    private readonly Span<int> _marks;

    /// <summary>The pooled buffer.</summary>
    private byte[]? _buffer;

    /// <summary>The bytes written.</summary>
    private int _length;

    /// <summary>The values open.</summary>
    private int _depth;

    /// <summary>Initializes a new instance of the <see cref="DerWriter"/> struct.</summary>
    /// <param name="marks">Room for one entry per level of nesting, usually from <c>stackalloc</c>.</param>
    /// <param name="capacity">The number of bytes to start with; the buffer grows when it is not enough.</param>
    internal DerWriter(Span<int> marks, int capacity)
    {
        _marks = marks;
        _buffer = ArrayPool<byte>.Shared.Rent(capacity);
        _length = 0;
        _depth = 0;
    }

    /// <summary>Gets the bytes written so far.</summary>
    internal readonly ReadOnlySpan<byte> Written => _buffer.AsSpan(0, _length);

    /// <summary>Starts a constructed value.</summary>
    /// <param name="tag">The tag byte, such as <see cref="SequenceTag"/>.</param>
    internal void Begin(byte tag)
    {
        Reserve(TagAndLengthBytes);
        _buffer![_length] = tag;
        _length++;
        _marks[_depth] = _length;
        _depth++;
        _length++;
    }

    /// <summary>Ends the innermost constructed value.</summary>
    internal void End()
    {
        _depth--;
        var lengthAt = _marks[_depth];
        InsertLength(lengthAt, _length - lengthAt - 1);
    }

    /// <summary>Ends the innermost constructed value, a SET OF, putting its elements in the order DER requires (X.690, 11.6).</summary>
    internal void EndSetOf()
    {
        var lengthAt = _marks[_depth - 1];
        SortElements(lengthAt + 1, _length);
        End();
    }

    /// <summary>Writes an OBJECT IDENTIFIER from its encoded contents.</summary>
    /// <param name="contents">The contents bytes, without tag and length.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteObjectIdentifier(scoped ReadOnlySpan<byte> contents) => WritePrimitive(ObjectIdentifierTag, contents);

    /// <summary>Writes an OCTET STRING.</summary>
    /// <param name="contents">The bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteOctetString(scoped ReadOnlySpan<byte> contents) => WritePrimitive(OctetStringTag, contents);

    /// <summary>Writes a NULL.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteNull() => WritePrimitive(NullTag, default);

    /// <summary>Writes an INTEGER from the contents of its encoding.</summary>
    /// <param name="contents">The big-endian two's complement bytes, already without redundant leading bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteIntegerContents(scoped ReadOnlySpan<byte> contents) => WritePrimitive(IntegerTag, contents);

    /// <summary>Writes a non-negative INTEGER.</summary>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    internal void WriteInteger(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        var start = 0;
        while (start < bytes.Length - 1 && bytes[start] == 0 && bytes[start + 1] < LongForm)
        {
            start++;
        }

        WritePrimitive(IntegerTag, bytes[start..]);
    }

    /// <summary>Writes a UTCTime.</summary>
    /// <param name="time">The time, written in UTC to the second.</param>
    /// <exception cref="ArgumentOutOfRangeException">The year is outside 1950 to 2049.</exception>
    internal void WriteUtcTime(DateTimeOffset time)
    {
        var utc = time.UtcDateTime;
        ArgumentOutOfRangeException.ThrowIfLessThan(utc.Year, FirstUtcYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(utc.Year, LastUtcYear);
        Span<byte> text = stackalloc byte[UtcTimeLength];
        _ = utc.TryFormat(text, out _, "yyMMddHHmmss", CultureInfo.InvariantCulture);
        text[UtcTimeLength - 1] = (byte)'Z';
        WritePrimitive(UtcTimeTag, text);
    }

    /// <summary>Copies a value that is already encoded.</summary>
    /// <param name="encoded">The whole value, tag and length included.</param>
    internal void WriteEncoded(scoped ReadOnlySpan<byte> encoded)
    {
        Reserve(encoded.Length);
        encoded.CopyTo(_buffer.AsSpan(_length));
        _length += encoded.Length;
    }

    /// <summary>Copies a value that is already encoded, giving it another tag.</summary>
    /// <param name="encoded">The whole value, tag and length included.</param>
    /// <param name="tag">The tag to write in place of the first byte.</param>
    internal void WriteEncoded(scoped ReadOnlySpan<byte> encoded, byte tag)
    {
        var start = _length;
        WriteEncoded(encoded);
        _buffer![start] = tag;
    }

    /// <summary>Copies the bytes written into a new array.</summary>
    /// <returns>The encoding.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly byte[] ToArray() => Written.ToArray();

    /// <summary>Returns the buffer to the pool.</summary>
    internal void Dispose()
    {
        if (_buffer is not { } buffer)
        {
            return;
        }

        _buffer = null;
        ArrayPool<byte>.Shared.Return(buffer);
    }

    /// <summary>Gets the number of bytes the length of a value takes after its first length byte.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The extra bytes.</returns>
    private static int ExtraLengthBytes(int length)
    {
        var extra = 1;
        for (var rest = length >> BitsPerByte; rest > 0; rest >>= BitsPerByte)
        {
            extra++;
        }

        return extra;
    }

    /// <summary>Counts the encoded values in a run of bytes.</summary>
    /// <param name="contents">The bytes.</param>
    /// <returns>The number of values.</returns>
    private static int CountElements(ReadOnlySpan<byte> contents)
    {
        var count = 0;
        var at = 0;
        while (at < contents.Length)
        {
            _ = AsnDecoder.ReadEncodedValue(contents[at..], AsnEncodingRules.BER, out _, out _, out var consumed);
            at += consumed;
            count++;
        }

        return count;
    }

    /// <summary>Finds where each encoded value in a run of bytes starts and how long it is.</summary>
    /// <param name="contents">The bytes.</param>
    /// <param name="bounds">Receives the bounds of each value.</param>
    private static void FindBounds(ReadOnlySpan<byte> contents, Span<Bounds> bounds)
    {
        var at = 0;
        for (var i = 0; at < contents.Length; i++)
        {
            _ = AsnDecoder.ReadEncodedValue(contents[at..], AsnEncodingRules.BER, out _, out _, out var consumed);
            bounds[i] = new(at, consumed);
            at += consumed;
        }
    }

    /// <summary>Puts the value bounds in the order of the values' bytes, with an insertion sort, as a SET OF has few elements.</summary>
    /// <param name="contents">The bytes.</param>
    /// <param name="bounds">The bounds of each value, reordered.</param>
    private static void OrderBounds(ReadOnlySpan<byte> contents, Span<Bounds> bounds)
    {
        for (var i = 1; i < bounds.Length; i++)
        {
            var moving = bounds[i];
            var value = contents.Slice(moving.Start, moving.Length);
            var j = i;
            while (j > 0 && contents.Slice(bounds[j - 1].Start, bounds[j - 1].Length).SequenceCompareTo(value) > 0)
            {
                bounds[j] = bounds[j - 1];
                j--;
            }

            bounds[j] = moving;
        }
    }

    /// <summary>Sorts the elements of a SET OF in place, using room to note where each element is.</summary>
    /// <param name="contents">The elements, one after the other.</param>
    /// <param name="bounds">Room for the bounds of every element.</param>
    private static void SortWith(Span<byte> contents, Span<Bounds> bounds)
    {
        FindBounds(contents, bounds);
        OrderBounds(contents, bounds);
        var sorted = ArrayPool<byte>.Shared.Rent(contents.Length);
        try
        {
            var at = 0;
            foreach (var (elementStart, elementLength) in bounds)
            {
                contents.Slice(elementStart, elementLength).CopyTo(sorted.AsSpan(at));
                at += elementLength;
            }

            sorted.AsSpan(0, contents.Length).CopyTo(contents);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sorted);
        }
    }

    /// <summary>Writes a primitive value.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="contents">The contents.</param>
    private void WritePrimitive(byte tag, scoped ReadOnlySpan<byte> contents)
    {
        Reserve(contents.Length + PrimitiveOverhead);
        var buffer = _buffer!;
        buffer[_length] = tag;
        _length++;
        WriteLength(contents.Length);
        contents.CopyTo(buffer.AsSpan(_length));
        _length += contents.Length;
    }

    /// <summary>Writes a length.</summary>
    /// <param name="length">The length.</param>
    private void WriteLength(int length)
    {
        var buffer = _buffer!;
        if (length <= MaxShortLength)
        {
            buffer[_length] = (byte)length;
            _length++;
            return;
        }

        var extra = ExtraLengthBytes(length);
        buffer[_length] = (byte)(LongForm | extra);
        _length++;
        for (var shift = (extra - 1) * BitsPerByte; shift >= 0; shift -= BitsPerByte)
        {
            buffer[_length] = (byte)(length >> shift);
            _length++;
        }
    }

    /// <summary>Sets the length of a constructed value, moving its contents along when the length needs more than one byte.</summary>
    /// <param name="lengthAt">Where the reserved length byte is.</param>
    /// <param name="content">The contents length.</param>
    private void InsertLength(int lengthAt, int content)
    {
        if (content <= MaxShortLength)
        {
            _buffer![lengthAt] = (byte)content;
            return;
        }

        var extra = ExtraLengthBytes(content);
        Reserve(extra);
        var buffer = _buffer!;
        buffer.AsSpan(lengthAt + 1, content).CopyTo(buffer.AsSpan(lengthAt + 1 + extra));
        _length += extra;
        buffer[lengthAt] = (byte)(LongForm | extra);
        for (var i = 0; i < extra; i++)
        {
            buffer[lengthAt + 1 + i] = (byte)(content >> ((extra - 1 - i) * BitsPerByte));
        }
    }

    /// <summary>Makes sure the buffer has room for more bytes.</summary>
    /// <param name="more">The bytes about to be written.</param>
    private void Reserve(int more)
    {
        var buffer = _buffer!;
        if (_length + more <= buffer.Length)
        {
            return;
        }

        var larger = ArrayPool<byte>.Shared.Rent(Math.Max(buffer.Length * GrowthFactor, _length + more));
        buffer.AsSpan(0, _length).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(buffer);
        _buffer = larger;
    }

    /// <summary>Sorts the elements of a SET OF by their encodings, shortest first when one is a prefix of the other.</summary>
    /// <param name="start">Where the first element starts.</param>
    /// <param name="end">Where the last element ends.</param>
    private void SortElements(int start, int end)
    {
        var contents = _buffer.AsSpan(start, end - start);
        var count = CountElements(contents);
        if (count < MinSortedElements)
        {
            return;
        }

        if (count > StackElements)
        {
            SortWith(contents, new Bounds[count]);
        }
        else
        {
            SortWith(contents, stackalloc Bounds[count]);
        }
    }

    /// <summary>Where an encoded value sits in a run of bytes.</summary>
    /// <param name="Start">Where the value starts.</param>
    /// <param name="Length">The bytes the whole value takes.</param>
    private readonly record struct Bounds(int Start, int Length);
}
