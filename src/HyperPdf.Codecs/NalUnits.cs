// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs;

/// <summary>Splits the packets of an H.264 or H.265 stream into NAL units and removes the emulation prevention bytes from a NAL unit.</summary>
internal static class NalUnits
{
    /// <summary>The bytes of a start code prefix.</summary>
    private const int StartCodeBytes = 3;

    /// <summary>The value of the emulation prevention byte.</summary>
    private const byte EscapeByte = 3;

    /// <summary>The zero bytes that come before an emulation prevention byte.</summary>
    private const int EscapeZeros = 2;

    /// <summary>Gets the three bytes that, with a zero first, start a NAL unit in a byte stream.</summary>
    private static ReadOnlySpan<byte> StartCode => [0, 0, 1];

    /// <summary>Gets the three bytes inside a NAL unit whose last byte is an emulation prevention byte.</summary>
    private static ReadOnlySpan<byte> Escape => [0, 0, EscapeByte];

    /// <summary>Lists the NAL units of a packet written as a byte stream with start codes.</summary>
    /// <param name="data">The packet.</param>
    /// <param name="ranges">Receives the NAL units, after clearing.</param>
    internal static void ListByteStream(ReadOnlySpan<byte> data, List<NalRange> ranges)
    {
        ranges.Clear();
        var position = 0;
        var start = -1;
        while (position < data.Length)
        {
            var found = data[position..].IndexOf(StartCode);
            if (found < 0)
            {
                break;
            }

            var codeAt = position + found;
            if (start >= 0)
            {
                AddTrimmed(data, start, codeAt, ranges);
            }

            start = codeAt + StartCodeBytes;
            position = start;
        }

        if (start >= 0)
        {
            AddTrimmed(data, start, data.Length, ranges);
        }
    }

    /// <summary>Lists the NAL units of a packet written with a length in front of each, as MP4 and Matroska write them.</summary>
    /// <param name="data">The packet.</param>
    /// <param name="lengthSize">The bytes of each length, from 1 to 4.</param>
    /// <param name="ranges">Receives the NAL units, after clearing.</param>
    /// <returns><see langword="false"/> when a length runs past the end of the packet.</returns>
    internal static bool ListLengthPrefixed(ReadOnlySpan<byte> data, int lengthSize, List<NalRange> ranges)
    {
        ranges.Clear();
        var position = 0;
        while (position + lengthSize <= data.Length)
        {
            var length = (int)ReadLength(data[position..], lengthSize);
            position += lengthSize;
            if (length <= 0 || length > data.Length - position)
            {
                return false;
            }

            ranges.Add(new(position, length));
            position += length;
        }

        return position == data.Length;
    }

    /// <summary>Copies the payload of a NAL unit without the emulation prevention bytes.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <param name="destination">Receives the payload; it must be as long as the NAL unit.</param>
    /// <param name="headerBytes">The bytes of the header to leave out: 1 for H.264 and 2 for H.265.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int Unescape(ReadOnlySpan<byte> nal, Span<byte> destination, int headerBytes)
    {
        var source = nal[headerBytes..];
        var written = 0;
        while (true)
        {
            var found = source.IndexOf(Escape);
            if (found < 0)
            {
                source.CopyTo(destination[written..]);
                return written + source.Length;
            }

            // Keep the two zero bytes, drop the 3 that follows them.
            source[..(found + EscapeZeros)].CopyTo(destination[written..]);
            written += found + EscapeZeros;
            source = source[(found + StartCodeBytes)..];
        }
    }

    /// <summary>Reads a big endian length.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="size">The bytes of the length.</param>
    /// <returns>The length.</returns>
    private static uint ReadLength(ReadOnlySpan<byte> data, int size) => size switch
    {
        1 => data[0],
        sizeof(ushort) => BinaryPrimitives.ReadUInt16BigEndian(data),
        StartCodeBytes => (uint)((data[0] << 16) | (data[1] << 8) | data[2]),
        _ => BinaryPrimitives.ReadUInt32BigEndian(data),
    };

    /// <summary>Adds a NAL unit to the list, leaving off the zero bytes that trail it before the next start code.</summary>
    /// <param name="data">The packet.</param>
    /// <param name="start">The index of the NAL unit's first byte.</param>
    /// <param name="end">The index of the next start code, or the end of the packet.</param>
    /// <param name="ranges">The list.</param>
    private static void AddTrimmed(ReadOnlySpan<byte> data, int start, int end, List<NalRange> ranges)
    {
        var last = end;
        while (last > start && data[last - 1] == 0)
        {
            last--;
        }

        if (last > start)
        {
            ranges.Add(new(start, last - start));
        }
    }
}
