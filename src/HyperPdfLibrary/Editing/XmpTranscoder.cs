// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Detects the encoding of an XMP packet and converts between UTF-16 or UTF-32 and UTF-8 without building strings. XMP
/// allows all three Unicode encodings (ISO 16684-1 §7.3), and PDF/A test files use each of them.
/// </summary>
internal static class XmpTranscoder
{
    /// <summary>The first byte of a big-endian byte order mark.</summary>
    private const byte BigEndianMarkFirst = 0xFE;

    /// <summary>The second byte of a big-endian byte order mark.</summary>
    private const byte BigEndianMarkSecond = 0xFF;

    /// <summary>The byte of the less-than sign that opens every packet.</summary>
    private const byte OpenAngle = (byte)'<';

    /// <summary>The number of bytes in one UTF-16 code unit, and in a UTF-16 byte order mark.</summary>
    private const int UnitBytes = 2;

    /// <summary>The number of bytes in one UTF-32 code unit, and in a UTF-32 byte order mark.</summary>
    private const int WideUnitBytes = 4;

    /// <summary>The byte order mark's code point.</summary>
    private const uint ByteOrderMark = 0xFEFF;

    /// <summary>Works out a packet's encoding from its first bytes.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="markLength">The length of the byte order mark to skip: 2 or 4 for a mark, otherwise 0.</param>
    /// <returns>The encoding; UTF-8 unless the packet starts with a UTF-16 or UTF-32 mark or <c>&lt;</c>.</returns>
    internal static XmpEncoding Detect(ReadOnlySpan<byte> packet, out int markLength)
    {
        markLength = 0;
        return packet.Length >= WideUnitBytes && DetectWide(packet, out markLength) is { } wide ? wide : DetectNarrow(packet, out markLength);
    }

    /// <summary>Converts UTF-16 or UTF-32 bytes to UTF-8.</summary>
    /// <param name="wide">The bytes, without any leading byte order mark.</param>
    /// <param name="encoding">The encoding and byte order of <paramref name="wide"/>.</param>
    /// <returns>The UTF-8 bytes, or <see langword="null"/> when the input has a partial unit or invalid code points.</returns>
    internal static byte[]? ToUtf8(ReadOnlySpan<byte> wide, XmpEncoding encoding) =>
        encoding is XmpEncoding.Utf32BigEndian or XmpEncoding.Utf32LittleEndian ? Utf32ToUtf8(wide, encoding == XmpEncoding.Utf32BigEndian) : Utf16ToUtf8(wide, encoding);

    /// <summary>Converts UTF-8 bytes to UTF-16 or UTF-32, with a byte order mark when the original had one.</summary>
    /// <param name="utf8">The UTF-8 bytes.</param>
    /// <param name="encoding">The encoding and byte order to write.</param>
    /// <param name="mark">The original's leading byte order mark, or empty for none; it is written back as it was.</param>
    /// <returns>The bytes, or <see langword="null"/> when the input is not valid UTF-8.</returns>
    internal static byte[]? FromUtf8(ReadOnlySpan<byte> utf8, XmpEncoding encoding, ReadOnlySpan<byte> mark) =>
        encoding is XmpEncoding.Utf32BigEndian or XmpEncoding.Utf32LittleEndian ? Utf8ToUtf32(utf8, encoding == XmpEncoding.Utf32BigEndian, mark) : Utf8ToUtf16(utf8, encoding, mark);

    /// <summary>Detects a UTF-16 packet from its byte order mark or its first <c>&lt;</c>, else UTF-8.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="markLength">2 for a byte order mark, otherwise 0.</param>
    /// <returns>The encoding.</returns>
    private static XmpEncoding DetectNarrow(ReadOnlySpan<byte> packet, out int markLength)
    {
        markLength = 0;
        if (packet.Length < UnitBytes)
        {
            return XmpEncoding.Utf8;
        }

        var first = packet[0];
        var second = packet[1];
        if (first == BigEndianMarkFirst && second == BigEndianMarkSecond)
        {
            markLength = UnitBytes;
            return XmpEncoding.Utf16BigEndian;
        }

        if (first == BigEndianMarkSecond && second == BigEndianMarkFirst)
        {
            markLength = UnitBytes;
            return XmpEncoding.Utf16LittleEndian;
        }

        if (first == 0 && second == OpenAngle)
        {
            return XmpEncoding.Utf16BigEndian;
        }

        return first == OpenAngle && second == 0 ? XmpEncoding.Utf16LittleEndian : XmpEncoding.Utf8;
    }

    /// <summary>Detects a UTF-32 packet from its byte order mark or its first <c>&lt;</c>.</summary>
    /// <param name="packet">The packet, at least four bytes long.</param>
    /// <param name="markLength">4 for a byte order mark, otherwise 0.</param>
    /// <returns>The encoding, or <see langword="null"/> when the packet is not UTF-32.</returns>
    private static XmpEncoding? DetectWide(ReadOnlySpan<byte> packet, out int markLength)
    {
        var bigEndian = BinaryPrimitives.ReadUInt32BigEndian(packet);
        var littleEndian = BinaryPrimitives.ReadUInt32LittleEndian(packet);
        markLength = bigEndian == ByteOrderMark || littleEndian == ByteOrderMark ? WideUnitBytes : 0;
        if (bigEndian is ByteOrderMark or OpenAngle)
        {
            return XmpEncoding.Utf32BigEndian;
        }

        return littleEndian is ByteOrderMark or OpenAngle ? XmpEncoding.Utf32LittleEndian : null;
    }

    /// <summary>Converts UTF-16 bytes to UTF-8.</summary>
    /// <param name="utf16">The UTF-16 bytes, without any leading byte order mark.</param>
    /// <param name="encoding">The byte order of <paramref name="utf16"/>.</param>
    /// <returns>The UTF-8 bytes, or <see langword="null"/> when the input has an odd length or invalid surrogates.</returns>
    private static byte[]? Utf16ToUtf8(ReadOnlySpan<byte> utf16, XmpEncoding encoding)
    {
        if (utf16.Length % UnitBytes != 0)
        {
            return null;
        }

        var units = new char[utf16.Length / UnitBytes];
        for (var i = 0; i < units.Length; i++)
        {
            var pair = utf16.Slice(i * UnitBytes, UnitBytes);
            units[i] = (char)(encoding == XmpEncoding.Utf16BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(pair) : BinaryPrimitives.ReadUInt16LittleEndian(pair));
        }

        var buffer = new byte[Encoding.UTF8.GetMaxByteCount(units.Length)];
        return Utf8.FromUtf16(units, buffer, out _, out var written, false) == OperationStatus.Done ? buffer.AsSpan(0, written).ToArray() : null;
    }

    /// <summary>Converts UTF-8 bytes to UTF-16.</summary>
    /// <param name="utf8">The UTF-8 bytes.</param>
    /// <param name="encoding">The byte order to write.</param>
    /// <param name="mark">The original's leading byte order mark, or empty for none.</param>
    /// <returns>The UTF-16 bytes, or <see langword="null"/> when the input is not valid UTF-8.</returns>
    private static byte[]? Utf8ToUtf16(ReadOnlySpan<byte> utf8, XmpEncoding encoding, ReadOnlySpan<byte> mark)
    {
        var units = new char[utf8.Length];
        if (Utf8.ToUtf16(utf8, units, out _, out var written, false) != OperationStatus.Done)
        {
            return null;
        }

        var result = new byte[mark.Length + (written * UnitBytes)];
        mark.CopyTo(result);
        for (var i = 0; i < written; i++)
        {
            var pair = result.AsSpan(mark.Length + (i * UnitBytes), UnitBytes);
            if (encoding == XmpEncoding.Utf16BigEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(pair, units[i]);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pair, units[i]);
            }
        }

        return result;
    }

    /// <summary>Converts UTF-32 bytes to UTF-8, one code point at a time.</summary>
    /// <param name="utf32">The UTF-32 bytes, without any leading byte order mark.</param>
    /// <param name="bigEndian">Whether the units are big-endian.</param>
    /// <returns>The UTF-8 bytes, or <see langword="null"/> when the input has a partial unit or a value that is not a scalar.</returns>
    private static byte[]? Utf32ToUtf8(ReadOnlySpan<byte> utf32, bool bigEndian)
    {
        if (utf32.Length % WideUnitBytes != 0)
        {
            return null;
        }

        // A scalar takes at most four UTF-8 bytes, the size of its UTF-32 unit.
        var buffer = new byte[utf32.Length];
        var written = 0;
        for (var at = 0; at < utf32.Length; at += WideUnitBytes)
        {
            var unit = utf32.Slice(at, WideUnitBytes);
            var value = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(unit) : BinaryPrimitives.ReadUInt32LittleEndian(unit);
            if (!Rune.TryCreate(value, out var rune))
            {
                return null;
            }

            written += rune.EncodeToUtf8(buffer.AsSpan(written));
        }

        return buffer.AsSpan(0, written).ToArray();
    }

    /// <summary>Converts UTF-8 bytes to UTF-32, one code point at a time.</summary>
    /// <param name="utf8">The UTF-8 bytes.</param>
    /// <param name="bigEndian">Whether to write big-endian units.</param>
    /// <param name="mark">The original's leading byte order mark, or empty for none.</param>
    /// <returns>The UTF-32 bytes, or <see langword="null"/> when the input is not valid UTF-8.</returns>
    private static byte[]? Utf8ToUtf32(ReadOnlySpan<byte> utf8, bool bigEndian, ReadOnlySpan<byte> mark)
    {
        var result = new byte[mark.Length + (utf8.Length * WideUnitBytes)];
        mark.CopyTo(result);
        var written = mark.Length;
        while (!utf8.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(utf8, out var rune, out var consumed) != OperationStatus.Done)
            {
                return null;
            }

            var unit = result.AsSpan(written, WideUnitBytes);
            if (bigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(unit, (uint)rune.Value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(unit, (uint)rune.Value);
            }

            written += WideUnitBytes;
            utf8 = utf8[consumed..];
        }

        return result.AsSpan(0, written).ToArray();
    }
}
