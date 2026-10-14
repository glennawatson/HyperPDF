// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Writes the segment headers and fixed fields of an embedded JBIG2 stream (T.88 section 7).</summary>
internal static class Jbig2SegmentWriter
{
    /// <summary>The symbol dictionary segment type.</summary>
    internal const int SymbolDictionaryType = 0;

    /// <summary>The immediate text region segment type.</summary>
    internal const int ImmediateTextType = 6;

    /// <summary>The immediate lossless text region segment type.</summary>
    internal const int ImmediateLosslessTextType = 7;

    /// <summary>The page information segment type.</summary>
    internal const int PageInformationType = 48;

    /// <summary>The immediate generic region segment type.</summary>
    internal const int ImmediateGenericType = 38;

    /// <summary>The immediate lossless generic region segment type.</summary>
    internal const int ImmediateLosslessGenericType = 39;

    /// <summary>The bytes of a 32-bit field.</summary>
    private const int IntBytes = 4;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int ShortBytes = 2;

    /// <summary>The shift of the referred-to segment count in its byte.</summary>
    private const int CountShift = 5;

    /// <summary>The most segments a short-form header can refer to.</summary>
    private const int MaximumShortReferences = 4;

    /// <summary>The page information flag for a page with no lossy coding.</summary>
    private const int LosslessFlag = 0x01;

    /// <summary>The largest segment number that refers to others with one-byte numbers.</summary>
    private const int ByteNumbers = 256;

    /// <summary>The largest segment number that refers to others with two-byte numbers.</summary>
    private const int ShortNumbers = 65_536;

    /// <summary>The bytes of the data length field.</summary>
    private const int LengthBytes = 4;

    /// <summary>Starts a segment: writes its header with a zero data length, to be patched by <see cref="EndSegment"/>.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="type">The segment type.</param>
    /// <param name="referred">The segment numbers this one refers to, at most four.</param>
    /// <param name="page">The page association, 0 for a global segment.</param>
    /// <param name="retain">Whether later segments may refer to this one.</param>
    /// <returns>The position of the data length field.</returns>
    /// <exception cref="ArgumentException">More than four segments are referred to.</exception>
    internal static int BeginSegment(ref PooledBuffer output, int number, int type, scoped ReadOnlySpan<int> referred, int page, bool retain)
    {
        if (referred.Length > MaximumShortReferences)
        {
            throw new ArgumentException("A segment can refer to at most four others.", nameof(referred));
        }

        WriteInt32(ref output, number);
        output.WriteByte((byte)type);
        var retainBits = (retain ? 1 : 0) | (((1 << referred.Length) - 1) << 1);
        output.WriteByte((byte)((referred.Length << CountShift) | retainBits));
        foreach (var segment in referred)
        {
            WriteReference(ref output, number, segment);
        }

        output.WriteByte((byte)page);
        var position = output.Length;
        WriteInt32(ref output, 0);
        return position;
    }

    /// <summary>Ends a segment by patching the data length written by <see cref="BeginSegment"/>.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="lengthPosition">The position returned by <see cref="BeginSegment"/>.</param>
    internal static void EndSegment(ref PooledBuffer output, int lengthPosition)
    {
        var length = output.Length - lengthPosition - LengthBytes;
        BinaryPrimitives.WriteInt32BigEndian(output.Array.AsSpan(lengthPosition, IntBytes), length);
    }

    /// <summary>Writes a page information segment (T.88 section 7.4.8) for a page with no striping.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="width">The page width.</param>
    /// <param name="height">The page height.</param>
    /// <param name="lossless">Whether every region is coded losslessly.</param>
    internal static void WritePageInformation(ref PooledBuffer output, int number, int width, int height, bool lossless)
    {
        var position = BeginSegment(ref output, number, PageInformationType, [], 1, false);
        WriteInt32(ref output, width);
        WriteInt32(ref output, height);
        WriteInt32(ref output, 0);
        WriteInt32(ref output, 0);
        output.WriteByte(lossless ? (byte)LosslessFlag : (byte)0);
        WriteInt16(ref output, 0);
        EndSegment(ref output, position);
    }

    /// <summary>Writes a region segment information field (T.88 section 7.4.1) with the OR combination operator.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="region">The region's size and place on the page.</param>
    internal static void WriteRegionInformation(ref PooledBuffer output, in Jbig2RegionBox region)
    {
        WriteInt32(ref output, region.Width);
        WriteInt32(ref output, region.Height);
        WriteInt32(ref output, region.X);
        WriteInt32(ref output, region.Y);
        output.WriteByte(0);
    }

    /// <summary>Writes a big-endian 32-bit integer.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="value">The value.</param>
    internal static void WriteInt32(ref PooledBuffer output, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(output.GetSpan(IntBytes), value);
        output.Advance(IntBytes);
    }

    /// <summary>Writes a big-endian 16-bit integer.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="value">The value.</param>
    internal static void WriteInt16(ref PooledBuffer output, int value)
    {
        BinaryPrimitives.WriteInt16BigEndian(output.GetSpan(ShortBytes), (short)value);
        output.Advance(ShortBytes);
    }

    /// <summary>Writes a reference to an earlier segment, in the width its referrer's number allows (T.88 section 7.2.5).</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The referring segment's number.</param>
    /// <param name="target">The referred-to segment's number.</param>
    private static void WriteReference(ref PooledBuffer output, int number, int target)
    {
        if (number <= ByteNumbers)
        {
            output.WriteByte((byte)target);
        }
        else if (number <= ShortNumbers)
        {
            WriteInt16(ref output, target);
        }
        else
        {
            WriteInt32(ref output, target);
        }
    }
}
