// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Estimates the quality setting a JPEG was written with from its first quantization table, by comparing the table's sum
/// with the sum of the standard luminance table (annex K.1 of the JPEG standard, T.81), which quality settings scale. The optimiser uses it to
/// leave a JPEG alone when it is already at or below the quality asked for, so it is not worn down again.
/// </summary>
internal static class JpegQualityEstimate
{
    /// <summary>The sum of the 64 entries of the standard luminance quantization table.</summary>
    private const int StandardSum = 3688;

    /// <summary>The marker byte prefix.</summary>
    private const byte MarkerPrefix = 0xFF;

    /// <summary>The start of image marker's second byte.</summary>
    private const byte StartOfImage = 0xD8;

    /// <summary>The define quantization table marker's second byte.</summary>
    private const byte DefineQuantization = 0xDB;

    /// <summary>The start of scan marker's second byte.</summary>
    private const byte StartOfScan = 0xDA;

    /// <summary>The entries in a quantization table.</summary>
    private const int TableEntries = 64;

    /// <summary>The scale of the quality formula.</summary>
    private const int Hundred = 100;

    /// <summary>The numerator of the formula for qualities below fifty.</summary>
    private const int LowQualityBase = 5000;

    /// <summary>The base of the formula for qualities from fifty up.</summary>
    private const int HighQualityBase = 200;

    /// <summary>The bytes of a segment length.</summary>
    private const int LengthBytes = 2;

    /// <summary>Estimates the quality of a JPEG.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <returns>A quality from 1 to 100, or -1 when the file has no readable quantization table.</returns>
    internal static int Estimate(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < LengthBytes || jpeg[0] != MarkerPrefix || jpeg[1] != StartOfImage)
        {
            return -1;
        }

        for (var position = LengthBytes; position + LengthBytes + LengthBytes <= jpeg.Length && jpeg[position] == MarkerPrefix; position += LengthBytes + SegmentLength(jpeg, position))
        {
            var marker = jpeg[position + 1];
            var length = SegmentLength(jpeg, position);
            if (marker == StartOfScan || length < LengthBytes || position + LengthBytes + length > jpeg.Length)
            {
                return -1;
            }

            if (marker == DefineQuantization)
            {
                return FromTable(jpeg.Slice(position + LengthBytes + LengthBytes, length - LengthBytes));
            }
        }

        return -1;
    }

    /// <summary>Reads the length of the segment that starts at a marker.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <param name="position">The position of the marker's first byte.</param>
    /// <returns>The length, counted from the length field itself.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SegmentLength(ReadOnlySpan<byte> jpeg, int position) => BinaryPrimitives.ReadUInt16BigEndian(jpeg[(position + LengthBytes)..]);

    /// <summary>Estimates the quality from the first table of a DQT segment.</summary>
    /// <param name="segment">The segment's content after its length.</param>
    /// <returns>The quality, or -1 when the table is cut short.</returns>
    private static int FromTable(ReadOnlySpan<byte> segment)
    {
        if (segment.IsEmpty)
        {
            return -1;
        }

        var wide = (segment[0] >> 4) != 0;
        var width = wide ? LengthBytes : 1;
        if (segment.Length < 1 + (TableEntries * width))
        {
            return -1;
        }

        var sum = 0;
        for (var i = 0; i < TableEntries; i++)
        {
            sum += wide ? BinaryPrimitives.ReadUInt16BigEndian(segment[(1 + (i * width))..]) : segment[1 + i];
        }

        var scale = sum * Hundred / StandardSum;
        var quality = scale <= Hundred ? (HighQualityBase - scale) / LengthBytes : LowQualityBase / Math.Max(scale, 1);
        return Math.Clamp(quality, 1, Hundred);
    }
}
