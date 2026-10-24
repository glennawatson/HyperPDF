// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using HyperPdf.Codecs;

namespace HyperPdf.Media.Native;

/// <summary>
/// Turns the way MP4 and Matroska store H.264 and H.265 (a decoder configuration record, then pictures made of NAL units that
/// each have their length in front) into the byte stream with start codes that operating system decoders take.
/// </summary>
internal static class AnnexB
{
    /// <summary>The offset in an AVC decoder configuration record of the byte that holds the NAL length size.</summary>
    private const int AvcLengthSizeAt = 4;

    /// <summary>The offset in an AVC decoder configuration record of the byte that holds the number of sequence parameter sets.</summary>
    private const int AvcSpsCountAt = 5;

    /// <summary>The mask of the number of sequence parameter sets.</summary>
    private const int AvcSpsCountMask = 0x1F;

    /// <summary>The offset in an HEVC decoder configuration record of the byte that holds the NAL length size.</summary>
    private const int HevcLengthSizeAt = 21;

    /// <summary>The offset in an HEVC decoder configuration record of the number of arrays of parameter sets.</summary>
    private const int HevcArrayCountAt = 22;

    /// <summary>The offset in an HEVC decoder configuration record of the first array.</summary>
    private const int HevcArraysAt = 23;

    /// <summary>The mask of the NAL length size minus 1.</summary>
    private const int LengthSizeMask = 3;

    /// <summary>The bytes of the length of a parameter set or of the number of units in an array.</summary>
    private const int ShortLength = 2;

    /// <summary>The bytes of a three byte NAL length.</summary>
    private const int MediumLength = 3;

    /// <summary>Gets the bytes that start each NAL unit in a byte stream.</summary>
    private static ReadOnlySpan<byte> StartCode => [0, 0, 0, 1];

    /// <summary>Gets the bytes of each NAL unit's length in front of it.</summary>
    /// <param name="codec">The coding of the track.</param>
    /// <param name="record">The track's decoder configuration record.</param>
    /// <returns>The bytes of the length, from 1 to 4, or 0 when the record is too short.</returns>
    internal static int LengthSize(MediaCodec codec, ReadOnlySpan<byte> record)
    {
        var at = codec == MediaCodec.H265 ? HevcLengthSizeAt : AvcLengthSizeAt;
        return record.Length > at ? (record[at] & LengthSizeMask) + 1 : 0;
    }

    /// <summary>Writes the parameter sets of a decoder configuration record as a byte stream with start codes.</summary>
    /// <param name="codec">The coding of the track.</param>
    /// <param name="record">The track's decoder configuration record.</param>
    /// <param name="destination">Receives the sets.</param>
    /// <returns><see langword="true"/> when the record held at least one set.</returns>
    internal static bool WriteParameterSets(MediaCodec codec, ReadOnlySpan<byte> record, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return codec == MediaCodec.H265 ? WriteHevcSets(record, destination) : WriteAvcSets(record, destination);
    }

    /// <summary>Writes a picture's NAL units as a byte stream with start codes.</summary>
    /// <param name="packet">The packet, with a length in front of each NAL unit.</param>
    /// <param name="lengthSize">The bytes of each length.</param>
    /// <param name="destination">Receives the NAL units.</param>
    /// <returns><see langword="false"/> when a length runs past the end of the packet.</returns>
    internal static bool WriteUnits(ReadOnlySpan<byte> packet, int lengthSize, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var position = 0;
        while (position + lengthSize <= packet.Length)
        {
            var length = ReadLength(packet[position..], lengthSize);
            position += lengthSize;
            if (length <= 0 || length > packet.Length - position)
            {
                return false;
            }

            WriteUnit(packet.Slice(position, length), destination);
            position += length;
        }

        return position == packet.Length;
    }

    /// <summary>Lists the NAL units of a byte stream that <see cref="WriteParameterSets"/> or <see cref="WriteUnits"/> wrote, where every unit follows a four byte start code.</summary>
    /// <param name="stream">The byte stream.</param>
    /// <param name="units">Receives the position and length of each unit, without its start code.</param>
    internal static void ListUnits(ReadOnlySpan<byte> stream, List<Range> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        var start = 0;
        while (start + StartCode.Length <= stream.Length)
        {
            var from = start + StartCode.Length;
            var next = stream[from..].IndexOf(StartCode);
            var end = next < 0 ? stream.Length : from + next;
            units.Add(from..end);
            start = end;
        }
    }

    /// <summary>Writes one NAL unit behind a start code.</summary>
    /// <param name="unit">The NAL unit.</param>
    /// <param name="destination">Receives the bytes.</param>
    private static void WriteUnit(ReadOnlySpan<byte> unit, IBufferWriter<byte> destination)
    {
        var span = destination.GetSpan(StartCode.Length + unit.Length);
        StartCode.CopyTo(span);
        unit.CopyTo(span[StartCode.Length..]);
        destination.Advance(StartCode.Length + unit.Length);
    }

    /// <summary>Reads a big endian length.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="size">The bytes of the length.</param>
    /// <returns>The length.</returns>
    private static int ReadLength(ReadOnlySpan<byte> data, int size) => size switch
    {
        1 => data[0],
        ShortLength => BinaryPrimitives.ReadUInt16BigEndian(data),
        MediumLength => (data[0] << 16) | (data[1] << 8) | data[2],
        _ => (int)BinaryPrimitives.ReadUInt32BigEndian(data),
    };

    /// <summary>Writes the sequence and picture parameter sets of an AVC decoder configuration record.</summary>
    /// <param name="record">The record.</param>
    /// <param name="destination">Receives the sets.</param>
    /// <returns><see langword="true"/> when a set was written.</returns>
    private static bool WriteAvcSets(ReadOnlySpan<byte> record, IBufferWriter<byte> destination)
    {
        if (record.Length <= AvcSpsCountAt)
        {
            return false;
        }

        var position = AvcSpsCountAt + 1;
        var written = WriteSets(record, ref position, record[AvcSpsCountAt] & AvcSpsCountMask, destination);
        if (position >= record.Length)
        {
            return written > 0;
        }

        var ppsCount = record[position];
        position++;
        return written + WriteSets(record, ref position, ppsCount, destination) > 0;
    }

    /// <summary>Writes the parameter sets of an HEVC decoder configuration record.</summary>
    /// <param name="record">The record.</param>
    /// <param name="destination">Receives the sets.</param>
    /// <returns><see langword="true"/> when a set was written.</returns>
    private static bool WriteHevcSets(ReadOnlySpan<byte> record, IBufferWriter<byte> destination)
    {
        if (record.Length <= HevcArrayCountAt)
        {
            return false;
        }

        var position = HevcArraysAt;
        var written = 0;
        for (var array = 0; array < record[HevcArrayCountAt] && position + 1 + ShortLength <= record.Length; array++)
        {
            position++;
            var count = BinaryPrimitives.ReadUInt16BigEndian(record[position..]);
            position += ShortLength;
            written += WriteSets(record, ref position, count, destination);
        }

        return written > 0;
    }

    /// <summary>Writes a run of parameter sets, each with its length in two bytes in front of it.</summary>
    /// <param name="record">The record.</param>
    /// <param name="position">The place to read from; moved past the sets.</param>
    /// <param name="count">The number of sets.</param>
    /// <param name="destination">Receives the sets.</param>
    /// <returns>The number of sets written.</returns>
    private static int WriteSets(ReadOnlySpan<byte> record, ref int position, int count, IBufferWriter<byte> destination)
    {
        var written = 0;
        for (var i = 0; i < count; i++)
        {
            if (position + ShortLength > record.Length)
            {
                return written;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(record[position..]);
            position += ShortLength;
            if (length == 0 || position + length > record.Length)
            {
                return written;
            }

            WriteUnit(record.Slice(position, length), destination);
            position += length;
            written++;
        }

        return written;
    }
}
