// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Writes the marker segments of an encoded JPEG: SOI, JFIF or Adobe, DQT, SOF, DRI, DHT, SOS and EOI.</summary>
internal static class JpegHeaderWriter
{
    /// <summary>The application marker APP0, which holds the JFIF header.</summary>
    private const byte JfifMarker = 0xE0;

    /// <summary>The application marker APP14, which holds the Adobe header.</summary>
    private const byte AdobeMarker = 0xEE;

    /// <summary>The bytes of a segment's length field.</summary>
    private const int LengthBytes = 2;

    /// <summary>The bytes of a JFIF segment after the length field: the tag, version, units, densities and thumbnail size.</summary>
    private const int JfifBytes = 14;

    /// <summary>The bytes of an Adobe segment after the length field: the tag, version, two flag words and the transform.</summary>
    private const int AdobeBytes = 12;

    /// <summary>The minor version of the JFIF segment the encoder writes (version 1.02).</summary>
    private const byte JfifMinorVersion = 2;

    /// <summary>The offset of the low byte of the horizontal density, counted from the end of the JFIF tag.</summary>
    private const int JfifDensityXOffset = 4;

    /// <summary>The offset of the low byte of the vertical density, counted from the end of the JFIF tag.</summary>
    private const int JfifDensityYOffset = 6;

    /// <summary>The offset of the width in a frame header.</summary>
    private const int FrameWidthOffset = 3;

    /// <summary>The bytes of a frame header before its component entries.</summary>
    private const int FrameFixedBytes = 6;

    /// <summary>The bytes of a component entry in a frame header.</summary>
    private const int FrameEntryBytes = 3;

    /// <summary>The bytes of a scan header before its component entries.</summary>
    private const int ScanFixedBytes = 4;

    /// <summary>The bytes of a component entry in a scan header.</summary>
    private const int ScanEntryBytes = 2;

    /// <summary>The sample precision.</summary>
    private const byte SamplePrecision = 8;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The DCT filter version number of the Adobe segment: X'65, as Adobe Technical Note #5116 section 18 gives.</summary>
    private const ushort AdobeVersion = 0x65;

    /// <summary>The Adobe transform value for plain CMYK.</summary>
    private const byte AdobeCmyk = 0;

    /// <summary>The Adobe transform value for YCCK.</summary>
    private const byte AdobeYcck = 2;

    /// <summary>The bytes of a Huffman table's code counts.</summary>
    private const int CountBytes = JpegHuffmanBuilder.MaxCodeLength;

    /// <summary>Gets the tag that opens a JFIF segment, with its terminating zero.</summary>
    private static ReadOnlySpan<byte> JfifBody => "JFIF\0"u8;

    /// <summary>Gets the tag that opens an Adobe segment.</summary>
    private static ReadOnlySpan<byte> AdobeTag => "Adobe"u8;

    /// <summary>Writes the start-of-image marker.</summary>
    /// <param name="output">The output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteStartOfImage(ref PooledBuffer output) => WriteMarker(ref output, JpegMarkers.StartOfImage);

    /// <summary>Writes the end-of-image marker.</summary>
    /// <param name="output">The output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteEndOfImage(ref PooledBuffer output) => WriteMarker(ref output, JpegMarkers.EndOfImage);

    /// <summary>Writes the JFIF header, or the Adobe header for four components.</summary>
    /// <param name="output">The output.</param>
    /// <param name="kind">The colour model.</param>
    internal static void WriteApplication(ref PooledBuffer output, JpegColorKind kind)
    {
        if (kind is JpegColorKind.Cmyk or JpegColorKind.Ycck)
        {
            var span = BeginSegment(ref output, AdobeMarker, AdobeBytes);
            AdobeTag.CopyTo(span);
            BinaryPrimitives.WriteUInt16BigEndian(span[AdobeTag.Length..], AdobeVersion);
            span[^1] = kind == JpegColorKind.Ycck ? AdobeYcck : AdobeCmyk;
            return;
        }

        var jfif = BeginSegment(ref output, JfifMarker, JfifBytes);
        JfifBody.CopyTo(jfif);

        // Version 1.02 (ISO/IEC 10918-5, 10.1), no density unit, a density of 1 by 1, and no thumbnail; the segment is cleared, so only the non-zero bytes are set.
        jfif[JfifBody.Length] = 1;
        jfif[JfifBody.Length + 1] = JfifMinorVersion;
        jfif[JfifBody.Length + JfifDensityXOffset] = 1;
        jfif[JfifBody.Length + JfifDensityYOffset] = 1;
    }

    /// <summary>Writes the quantization tables of the slots in use.</summary>
    /// <param name="output">The output.</param>
    /// <param name="steps">The steps of both slots in zigzag order, 64 bytes each.</param>
    /// <param name="slots">The number of slots in use, one or two.</param>
    internal static void WriteQuantization(ref PooledBuffer output, ReadOnlySpan<byte> steps, int slots)
    {
        var span = BeginSegment(ref output, JpegMarkers.QuantizationTables, slots * (1 + JpegBlock.Length));
        for (var slot = 0; slot < slots; slot++)
        {
            span[0] = (byte)slot;
            steps.Slice(slot * JpegBlock.Length, JpegBlock.Length).CopyTo(span[1..]);
            span = span[(1 + JpegBlock.Length)..];
        }
    }

    /// <summary>Writes the frame header.</summary>
    /// <param name="output">The output.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="progressive">Whether the frame is progressive.</param>
    internal static void WriteFrame(ref PooledBuffer output, in JpegFrameGeometry geometry, ReadOnlySpan<JpegEncoderComponent> components, bool progressive)
    {
        var span = BeginSegment(ref output, progressive ? JpegMarkers.ProgressiveFrame : JpegMarkers.BaselineFrame, FrameFixedBytes + (components.Length * FrameEntryBytes));
        span[0] = SamplePrecision;
        BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)geometry.Height);
        BinaryPrimitives.WriteUInt16BigEndian(span[FrameWidthOffset..], (ushort)geometry.Width);
        span[FrameFixedBytes - 1] = (byte)components.Length;
        span = span[FrameFixedBytes..];
        foreach (var component in components)
        {
            span[0] = (byte)component.Id;
            span[1] = (byte)((component.HorizontalSampling << NibbleBits) | component.VerticalSampling);
            span[2] = (byte)component.QuantSlot;
            span = span[FrameEntryBytes..];
        }
    }

    /// <summary>Writes the restart interval.</summary>
    /// <param name="output">The output.</param>
    /// <param name="interval">The MCUs between restart markers.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRestartInterval(ref PooledBuffer output, int interval) =>
        BinaryPrimitives.WriteUInt16BigEndian(BeginSegment(ref output, JpegMarkers.RestartInterval, LengthBytes), (ushort)interval);

    /// <summary>Writes the Huffman tables of the slots that have one.</summary>
    /// <param name="output">The output.</param>
    /// <param name="state">The encoder state that holds the tables.</param>
    internal static void WriteHuffman(ref PooledBuffer output, JpegEncoderState state)
    {
        var length = 0;
        for (var slot = 0; slot < JpegHuffmanBuilder.TableSlots; slot++)
        {
            if (state.TableSizes[slot] > 0)
            {
                length += 1 + CountBytes + state.TableSizes[slot];
            }
        }

        if (length == 0)
        {
            return;
        }

        var span = BeginSegment(ref output, JpegMarkers.HuffmanTables, length);
        for (var slot = 0; slot < JpegHuffmanBuilder.TableSlots; slot++)
        {
            var size = state.TableSizes[slot];
            if (size == 0)
            {
                continue;
            }

            // The class is 0 for DC and 1 for AC; the id is the low bit of the slot.
            span[0] = (byte)(((slot / JpegHuffmanBuilder.FirstAcSlot) << NibbleBits) | (slot % JpegHuffmanBuilder.FirstAcSlot));
            state.TableCounts.AsSpan(slot * CountBytes, CountBytes).CopyTo(span[1..]);
            state.TableValues.AsSpan(slot * JpegHuffmanBuilder.SymbolSlots, size).CopyTo(span[(1 + CountBytes)..]);
            span = span[(1 + CountBytes + size)..];
        }
    }

    /// <summary>Writes a scan header.</summary>
    /// <param name="output">The output.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="spec">The scan.</param>
    internal static void WriteScan(ref PooledBuffer output, ReadOnlySpan<JpegEncoderComponent> components, in JpegScanSpec spec)
    {
        var count = System.Numerics.BitOperations.PopCount((uint)spec.Components);
        var span = BeginSegment(ref output, JpegMarkers.StartOfScan, ScanFixedBytes + (count * ScanEntryBytes));
        span[0] = (byte)count;
        span = span[1..];
        for (var c = 0; c < components.Length; c++)
        {
            if ((spec.Components & (1 << c)) == 0)
            {
                continue;
            }

            span[0] = (byte)components[c].Id;
            span[1] = (byte)((components[c].TableSlot << NibbleBits) | components[c].TableSlot);
            span = span[ScanEntryBytes..];
        }

        span[0] = (byte)spec.Start;
        span[1] = (byte)spec.End;
        span[2] = (byte)((spec.High << NibbleBits) | spec.Low);
    }

    /// <summary>Writes a marker with no segment.</summary>
    /// <param name="output">The output.</param>
    /// <param name="marker">The marker byte.</param>
    private static void WriteMarker(ref PooledBuffer output, byte marker)
    {
        var span = output.GetSpan(JpegMarkers.MarkerLength);
        span[0] = JpegMarkers.MarkerPrefix;
        span[1] = marker;
        output.Advance(JpegMarkers.MarkerLength);
    }

    /// <summary>Writes a marker and length and returns the space for the payload.</summary>
    /// <param name="output">The output.</param>
    /// <param name="marker">The marker byte.</param>
    /// <param name="payloadBytes">The bytes of the payload, not counting the length field.</param>
    /// <returns>The payload space, which the caller fills.</returns>
    private static Span<byte> BeginSegment(ref PooledBuffer output, byte marker, int payloadBytes)
    {
        var total = JpegMarkers.MarkerLength + LengthBytes + payloadBytes;
        var span = output.GetSpan(total);
        span[0] = JpegMarkers.MarkerPrefix;
        span[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(span[JpegMarkers.MarkerLength..], (ushort)(LengthBytes + payloadBytes));
        output.Advance(total);
        var payload = span.Slice(JpegMarkers.MarkerLength + LengthBytes, payloadBytes);
        payload.Clear();
        return payload;
    }
}
