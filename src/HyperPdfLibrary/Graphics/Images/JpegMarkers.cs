// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Walks JPEG marker segments and reads the frame and Adobe headers without decoding the image.</summary>
internal static class JpegMarkers
{
    /// <summary>The start-of-image marker.</summary>
    internal const byte StartOfImage = 0xD8;

    /// <summary>The end-of-image marker.</summary>
    internal const byte EndOfImage = 0xD9;

    /// <summary>The start-of-scan marker; the headers end here.</summary>
    internal const byte StartOfScan = 0xDA;

    /// <summary>The define-quantization-tables marker.</summary>
    internal const byte QuantizationTables = 0xDB;

    /// <summary>The define-restart-interval marker.</summary>
    internal const byte RestartInterval = 0xDD;

    /// <summary>The define-Huffman-tables marker, which shares the SOF range.</summary>
    internal const byte HuffmanTables = 0xC4;

    /// <summary>The sequential baseline frame marker, SOF0.</summary>
    internal const byte BaselineFrame = 0xC0;

    /// <summary>The extended sequential frame marker, SOF1.</summary>
    internal const byte ExtendedFrame = 0xC1;

    /// <summary>The progressive frame marker, SOF2.</summary>
    internal const byte ProgressiveFrame = 0xC2;

    /// <summary>The lossless Huffman frame marker, SOF3.</summary>
    internal const byte LosslessFrame = 0xC3;

    /// <summary>The first differential frame marker of the hierarchical mode, SOF5.</summary>
    internal const byte DifferentialSequentialFrame = 0xC5;

    /// <summary>The last differential Huffman frame marker of the hierarchical mode, SOF7.</summary>
    internal const byte DifferentialLosslessFrame = 0xC7;

    /// <summary>The lossless arithmetic frame marker, SOF11.</summary>
    internal const byte LosslessArithmeticFrame = 0xCB;

    /// <summary>The define-number-of-lines marker, DNL.</summary>
    internal const byte NumberOfLines = 0xDC;

    /// <summary>The byte that starts every marker.</summary>
    internal const byte MarkerPrefix = 0xFF;

    /// <summary>The bytes of a marker.</summary>
    internal const int MarkerLength = 2;

    /// <summary>The first restart marker, RST0.</summary>
    internal const byte FirstRestart = 0xD0;

    /// <summary>The last restart marker, RST7.</summary>
    internal const byte LastRestart = 0xD7;

    /// <summary>The first start-of-frame marker, SOF0.</summary>
    private const byte FirstFrame = 0xC0;

    /// <summary>The last start-of-frame marker, SOF15.</summary>
    private const byte LastFrame = 0xCF;

    /// <summary>The JPEG extension marker, which shares the SOF range.</summary>
    private const byte Extension = 0xC8;

    /// <summary>The arithmetic conditioning marker, which shares the SOF range.</summary>
    private const byte ArithmeticConditioning = 0xCC;

    /// <summary>The JFIF application marker, APP0.</summary>
    private const byte JfifApplication = 0xE0;

    /// <summary>The Adobe application marker, APP14.</summary>
    private const byte AdobeApplication = 0xEE;

    /// <summary>The temporary private marker, which has no length.</summary>
    private const byte Temporary = 0x01;

    /// <summary>The bytes of the segment length field.</summary>
    private const int LengthBytes = 2;

    /// <summary>The bytes of a frame header before its component list: precision, height, width and count.</summary>
    private const int FrameFixedBytes = 6;

    /// <summary>The bytes of one component in a frame header.</summary>
    private const int FrameComponentBytes = 3;

    /// <summary>The offset of the third component entry from the first.</summary>
    private const int BlueEntryOffset = 2 * FrameComponentBytes;

    /// <summary>The offset of the height in a frame segment.</summary>
    private const int HeightOffset = 1;

    /// <summary>The offset of the width in a frame segment.</summary>
    private const int WidthOffset = 3;

    /// <summary>The offset of the component count in a frame segment.</summary>
    private const int CountOffset = 5;

    /// <summary>The bytes of the Adobe marker payload: the "Adobe" tag, version, two flag words and the transform.</summary>
    private const int AdobeBytes = 12;

    /// <summary>The offset of the transform byte in the Adobe segment.</summary>
    private const int AdobeTransformOffset = 11;

    /// <summary>The ASCII id of a red component.</summary>
    private const byte IdRed = 0x52;

    /// <summary>The ASCII id of a green component.</summary>
    private const byte IdGreen = 0x47;

    /// <summary>The ASCII id of a blue component.</summary>
    private const byte IdBlue = 0x42;

    /// <summary>The components of an RGB JPEG.</summary>
    private const int RgbComponents = 3;

    /// <summary>Gets the tag that opens a JFIF segment: "JFIF" and its terminating zero (ISO/IEC 10918-5, 10.1).</summary>
    private static ReadOnlySpan<byte> JfifTag => "JFIF\0"u8;

    /// <summary>Gets the tag that opens the Adobe segment.</summary>
    private static ReadOnlySpan<byte> AdobeTag => "Adobe"u8;

    /// <summary>Determines whether JPEG data ends with the end-of-image marker, ignoring zero, space and line-break padding after it. Data that does not was cut short.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <returns><see langword="true"/> when the marker is there.</returns>
    internal static bool EndsWithEndOfImage(ReadOnlySpan<byte> data)
    {
        var end = data.Length;
        while (end > 0 && data[end - 1] is 0 or (byte)'\r' or (byte)'\n' or (byte)' ')
        {
            end--;
        }

        return end >= MarkerLength && data[end - MarkerLength] == MarkerPrefix && data[end - 1] == EndOfImage;
    }

    /// <summary>Reads the frame and Adobe headers.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="info">Receives the header values.</param>
    /// <returns><see langword="true"/> when a frame header was found.</returns>
    internal static bool TryReadInfo(ReadOnlySpan<byte> data, out JpegInfo info)
    {
        info = default;
        var adobe = -1;
        var jfif = false;
        var found = false;
        var position = 0;
        while (TryNextMarker(data, ref position, out var marker))
        {
            if (marker == StartOfScan)
            {
                break;
            }

            if (!HasLength(marker))
            {
                continue;
            }

            if (!TryReadSegment(data, ref position, out var segment))
            {
                break;
            }

            if (IsFrame(marker) && !found)
            {
                found = TryReadFrame(marker, segment, out info);
            }
            else if (marker == AdobeApplication)
            {
                adobe = ReadAdobeTransform(segment, adobe);
            }
            else if (marker == JfifApplication && segment.StartsWith(JfifTag))
            {
                jfif = true;
            }
        }

        info = WithDeferredHeight(data, info with { AdobeTransform = adobe, HasJfif = jfif });
        return found;
    }

    /// <summary>
    /// Finds the line count that a DNL marker gives a frame whose header says zero lines (ISO/IEC 10918-1, B.2.5). The marker
    /// follows the first scan.
    /// </summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="height">Receives the number of lines.</param>
    /// <returns><see langword="true"/> when a DNL marker with a non-zero line count was found.</returns>
    internal static bool TryFindDnlHeight(ReadOnlySpan<byte> data, out int height)
    {
        height = 0;
        var position = 0;
        while (TryNextMarker(data, ref position, out var marker) && marker != EndOfImage)
        {
            if (!HasLength(marker))
            {
                continue;
            }

            if (!TryReadSegment(data, ref position, out var segment))
            {
                return false;
            }

            if (marker != NumberOfLines)
            {
                continue;
            }

            height = segment.Length >= LengthBytes ? BinaryPrimitives.ReadUInt16BigEndian(segment) : 0;
            return height > 0;
        }

        return false;
    }

    /// <summary>Finds the next marker, skipping entropy-coded bytes, stuffed zeros and fill bytes.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="position">The position to search from; set just after the marker.</param>
    /// <param name="marker">Receives the marker byte.</param>
    /// <returns><see langword="true"/> when a marker was found.</returns>
    internal static bool TryNextMarker(ReadOnlySpan<byte> data, ref int position, out byte marker)
    {
        marker = 0;
        while (position < data.Length)
        {
            var prefix = data[position..].IndexOf(MarkerPrefix);
            if (prefix < 0)
            {
                position = data.Length;
                return false;
            }

            position += prefix + 1;
            while (position < data.Length && data[position] == MarkerPrefix)
            {
                position++;
            }

            if (position >= data.Length)
            {
                break;
            }

            if (data[position] == 0)
            {
                continue;
            }

            marker = data[position];
            position++;
            return true;
        }

        return false;
    }

    /// <summary>Reads the length-prefixed payload that follows a marker.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="position">The position just after the marker; set after the segment.</param>
    /// <param name="segment">Receives the payload, without the length field.</param>
    /// <returns><see langword="true"/> when the whole segment is present.</returns>
    internal static bool TryReadSegment(ReadOnlySpan<byte> data, ref int position, out ReadOnlySpan<byte> segment)
    {
        segment = default;
        if (position + LengthBytes > data.Length)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        if (length < LengthBytes || position + length > data.Length)
        {
            return false;
        }

        segment = data.Slice(position + LengthBytes, length - LengthBytes);
        position += length;
        return true;
    }

    /// <summary>Determines whether a marker is followed by a length and payload.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <returns><see langword="false"/> for SOI, EOI, RSTn and TEM.</returns>
    internal static bool HasLength(byte marker) => marker != Temporary && marker is not (>= FirstRestart and <= EndOfImage);

    /// <summary>Determines whether a marker starts a Huffman frame (SOF0 to SOF15 except the table markers).</summary>
    /// <param name="marker">The marker byte.</param>
    /// <returns><see langword="true"/> for a start-of-frame marker.</returns>
    internal static bool IsFrame(byte marker) =>
        marker is >= FirstFrame and <= LastFrame and not HuffmanTables and not Extension and not ArithmeticConditioning;

    /// <summary>Finds the number of components in the first frame header.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <returns>The component count, or zero when no frame header is found.</returns>
    internal static int ReadComponentCount(ReadOnlySpan<byte> data) => TryReadInfo(data, out var info) ? info.Components : 0;

    /// <summary>Reads a frame header segment.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="segment">The frame payload.</param>
    /// <param name="info">Receives the frame values.</param>
    /// <returns><see langword="true"/> when the payload holds the components it declares.</returns>
    private static bool TryReadFrame(byte marker, ReadOnlySpan<byte> segment, out JpegInfo info)
    {
        info = default;
        if (segment.Length < FrameFixedBytes)
        {
            return false;
        }

        var count = segment[CountOffset];
        if (segment.Length < FrameFixedBytes + (count * FrameComponentBytes))
        {
            return false;
        }

        info = new(
            BinaryPrimitives.ReadUInt16BigEndian(segment[WidthOffset..]),
            BinaryPrimitives.ReadUInt16BigEndian(segment[HeightOffset..]),
            count,
            segment[0],
            ProcessOf(marker),
            -1,
            HasRgbIds(segment, count));
        return true;
    }

    /// <summary>Names the coding process of a start-of-frame marker (ISO/IEC 10918-1, Table B.1).</summary>
    /// <param name="marker">The frame marker, SOF0 to SOF15.</param>
    /// <returns>The process.</returns>
    private static JpegProcess ProcessOf(byte marker) => marker switch
    {
        BaselineFrame or ExtendedFrame => JpegProcess.Sequential,
        ProgressiveFrame => JpegProcess.Progressive,
        LosslessFrame or LosslessArithmeticFrame => JpegProcess.Lossless,
        >= DifferentialSequentialFrame and <= DifferentialLosslessFrame => JpegProcess.Hierarchical,
        _ => JpegProcess.Arithmetic,
    };

    /// <summary>Takes the line count from the DNL marker when the frame header left it at zero.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="info">The header values.</param>
    /// <returns>The values with the line count filled in when a DNL marker gives one.</returns>
    private static JpegInfo WithDeferredHeight(ReadOnlySpan<byte> data, in JpegInfo info) =>
        info.Height == 0 && TryFindDnlHeight(data, out var height) ? info with { Height = height } : info;

    /// <summary>Determines whether the three components are labelled R, G and B.</summary>
    /// <param name="segment">The frame payload.</param>
    /// <param name="count">The component count.</param>
    /// <returns><see langword="true"/> for RGB ids.</returns>
    private static bool HasRgbIds(ReadOnlySpan<byte> segment, int count) =>
        count == RgbComponents
        && segment[FrameFixedBytes] == IdRed
        && segment[FrameFixedBytes + FrameComponentBytes] == IdGreen
        && segment[FrameFixedBytes + BlueEntryOffset] == IdBlue;

    /// <summary>Reads the transform byte of an Adobe APP14 segment.</summary>
    /// <param name="segment">The APP14 payload.</param>
    /// <param name="current">The value to keep when the segment is not an Adobe one.</param>
    /// <returns>The transform byte, or <paramref name="current"/>.</returns>
    private static int ReadAdobeTransform(ReadOnlySpan<byte> segment, int current) =>
        segment.Length >= AdobeBytes && segment.StartsWith(AdobeTag) ? segment[AdobeTransformOffset] : current;
}
