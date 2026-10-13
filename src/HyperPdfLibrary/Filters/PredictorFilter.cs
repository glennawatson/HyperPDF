// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Undoes PNG and TIFF predictors in place. The PNG "Up" filter, the one most writers use, adds whole rows with
/// hardware-width vectors; the others depend on the previous pixel and run as scalar loops.
/// </summary>
internal static class PredictorFilter
{
    /// <summary>The TIFF predictor.</summary>
    private const int TiffPredictor = 2;

    /// <summary>The first PNG predictor value.</summary>
    private const int PngPredictors = 10;

    /// <summary>PNG row filter: sub.</summary>
    private const byte PngSub = 1;

    /// <summary>PNG row filter: up.</summary>
    private const byte PngUp = 2;

    /// <summary>PNG row filter: average.</summary>
    private const byte PngAverage = 3;

    /// <summary>PNG row filter: Paeth.</summary>
    private const byte PngPaeth = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Sixteen bits per component.</summary>
    private const int SixteenBits = 16;

    /// <summary>One bit per component.</summary>
    private const int OneBit = 1;

    /// <summary>Two bits per component.</summary>
    private const int TwoBits = 2;

    /// <summary>Four bits per component.</summary>
    private const int FourBits = 4;

    /// <summary>The largest supported component count.</summary>
    private const int MaxColors = 32;

    /// <summary>Applies the predictor named in a filter's parameters, if any.</summary>
    /// <param name="parms">The /DecodeParms dictionary.</param>
    /// <param name="data">The decoded data, changed in place.</param>
    internal static void Apply(PdfDictionary? parms, ref PooledBuffer data)
    {
        var predictor = parms?.GetInt32(KnownName.Predictor, 1) ?? 1;

        // Values 3 to 9 name no predictor, and so do invalid layouts: the data passes through unchanged.
        if (predictor is not (TiffPredictor or >= PngPredictors) || data.Length == 0 || !TryReadLayout(parms!, out var layout))
        {
            return;
        }

        var buffer = data.Array!.AsSpan(0, data.Length);
        data.Length = predictor >= PngPredictors
            ? UndoPng(buffer, layout.RowBytes, layout.PixelBytes)
            : UndoTiff(buffer, layout.RowBytes, layout.Colors, layout.Bits);
    }

    /// <summary>Reads the row layout from /Colors, /BitsPerComponent and /Columns (ISO 32000-1 table 8).</summary>
    /// <param name="parms">The /DecodeParms dictionary.</param>
    /// <param name="layout">The layout.</param>
    /// <returns><see langword="false"/> when the bits per component or row size are not usable.</returns>
    private static bool TryReadLayout(PdfDictionary parms, out RowLayout layout)
    {
        var colors = Math.Min(parms.GetInt32(KnownName.Colors, 1), MaxColors);
        var bits = parms.GetInt32(KnownName.BitsPerComponent, ByteBits);
        var columns = parms.GetInt32(KnownName.Columns, 1);
        var bitsPerPixel = colors * bits;
        var rowBytes = (int)Math.Min(int.MaxValue, (((long)columns * bitsPerPixel) + ByteBits - 1) / ByteBits);
        layout = new(colors, bits, rowBytes, Math.Max(1, (bitsPerPixel + ByteBits - 1) / ByteBits));
        return bits is OneBit or TwoBits or FourBits or ByteBits or SixteenBits && rowBytes > 0;
    }

    /// <summary>Undoes PNG row filters, compacting rows over their filter bytes.</summary>
    /// <param name="data">The data.</param>
    /// <param name="rowBytes">The bytes in a row, without the filter byte.</param>
    /// <param name="pixelBytes">The bytes in a pixel, at least one.</param>
    /// <returns>The decoded length.</returns>
    private static int UndoPng(Span<byte> data, int rowBytes, int pixelBytes)
    {
        var rows = data.Length / (rowBytes + 1);
        for (var row = 0; row < rows; row++)
        {
            UndoPngRowAt(data, row, rowBytes, rowBytes, pixelBytes);
        }

        // A truncated last row is decoded as far as its bytes go.
        var partial = data.Length - (rows * (rowBytes + 1)) - 1;
        if (partial <= 0)
        {
            return rows * rowBytes;
        }

        UndoPngRowAt(data, rows, rowBytes, partial, pixelBytes);
        return (rows * rowBytes) + partial;
    }

    /// <summary>Undoes the PNG row at a position.</summary>
    /// <param name="data">The data.</param>
    /// <param name="row">The row index.</param>
    /// <param name="rowBytes">The bytes in a full row, without the filter byte.</param>
    /// <param name="length">The bytes in this row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    private static void UndoPngRowAt(Span<byte> data, int row, int rowBytes, int length, int pixelBytes)
    {
        var filter = data[row * (rowBytes + 1)];
        var source = data.Slice((row * (rowBytes + 1)) + 1, length);
        var target = data.Slice(row * rowBytes, length);
        var previous = row == 0 ? default : data.Slice((row - 1) * rowBytes, length);
        UndoPngRow(filter, source, target, previous, pixelBytes);
    }

    /// <summary>Undoes one PNG row. The target overlaps the source but always lies before it, so reads stay ahead.</summary>
    /// <param name="filter">The row's filter byte.</param>
    /// <param name="source">The filtered row.</param>
    /// <param name="target">Where the decoded row goes.</param>
    /// <param name="previous">The decoded previous row, or empty for the first row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    private static void UndoPngRow(byte filter, ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> previous, int pixelBytes)
    {
        if (filter == PngUp)
        {
            AddRows(source, previous, target);
            return;
        }

        CopyForward(source, target);
        if (filter == PngSub)
        {
            for (var i = pixelBytes; i < target.Length; i++)
            {
                target[i] += target[i - pixelBytes];
            }

            return;
        }

        if (filter == PngAverage)
        {
            Average(target, previous, pixelBytes);
        }
        else if (filter == PngPaeth)
        {
            Paeth(target, previous, pixelBytes);
        }
    }

    /// <summary>Copies a row to an earlier, possibly overlapping, position.</summary>
    /// <param name="source">The source.</param>
    /// <param name="target">The target.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyForward(ReadOnlySpan<byte> source, Span<byte> target) => source.CopyTo(target);

    /// <summary>Adds the previous row to a row (the Up filter) a vector at a time.</summary>
    /// <param name="source">The filtered row.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="target">Where the decoded row goes.</param>
    private static void AddRows(ReadOnlySpan<byte> source, ReadOnlySpan<byte> previous, Span<byte> target)
    {
        if (previous.IsEmpty)
        {
            source.CopyTo(target);
            return;
        }

        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            ref var src = ref MemoryMarshal.GetReference(source);
            ref var prev = ref MemoryMarshal.GetReference(previous);
            ref var dst = ref MemoryMarshal.GetReference(target);
            for (; i <= target.Length - Vector<byte>.Count; i += Vector<byte>.Count)
            {
                // The source is loaded before the store; the target sits before the source, so nothing unread is overwritten.
                var sum = Vector.LoadUnsafe(ref src, (nuint)i) + Vector.LoadUnsafe(ref prev, (nuint)i);
                sum.StoreUnsafe(ref dst, (nuint)i);
            }
        }

        for (; i < target.Length; i++)
        {
            target[i] = (byte)(source[i] + previous[i]);
        }
    }

    /// <summary>Undoes the Average filter in place.</summary>
    /// <param name="row">The filtered row, decoded in place.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    private static void Average(Span<byte> row, ReadOnlySpan<byte> previous, int pixelBytes)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= pixelBytes ? row[i - pixelBytes] : 0;
            var up = previous.IsEmpty ? 0 : previous[i];
            row[i] = (byte)(row[i] + ((left + up) >> 1));
        }
    }

    /// <summary>Undoes the Paeth filter in place.</summary>
    /// <param name="row">The filtered row, decoded in place.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    private static void Paeth(Span<byte> row, ReadOnlySpan<byte> previous, int pixelBytes)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= pixelBytes ? row[i - pixelBytes] : 0;
            var up = previous.IsEmpty ? 0 : previous[i];
            var upLeft = i >= pixelBytes && !previous.IsEmpty ? previous[i - pixelBytes] : 0;
            row[i] = (byte)(row[i] + PaethPredict(left, up, upLeft));
        }
    }

    /// <summary>Chooses the Paeth predictor.</summary>
    /// <param name="left">The byte to the left.</param>
    /// <param name="up">The byte above.</param>
    /// <param name="upLeft">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PaethPredict(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        if (toLeft <= toUp && toLeft <= toUpLeft)
        {
            return left;
        }

        return toUp <= toUpLeft ? up : upLeft;
    }

    /// <summary>Undoes the TIFF horizontal differencing predictor, including a truncated last row.</summary>
    /// <param name="data">The data.</param>
    /// <param name="rowBytes">The bytes in a row.</param>
    /// <param name="colors">The components per pixel.</param>
    /// <param name="bits">The bits per component.</param>
    /// <returns>The decoded length.</returns>
    private static int UndoTiff(Span<byte> data, int rowBytes, int colors, int bits)
    {
        for (var offset = 0; offset < data.Length; offset += rowBytes)
        {
            var line = data.Slice(offset, Math.Min(rowBytes, data.Length - offset));
            if (bits == ByteBits)
            {
                for (var i = colors; i < line.Length; i++)
                {
                    line[i] += line[i - colors];
                }
            }
            else if (bits == SixteenBits)
            {
                UndoTiff16(line, colors);
            }
            else
            {
                UndoTiffPacked(line, colors, bits);
            }
        }

        return data.Length;
    }

    /// <summary>Undoes the TIFF predictor on a row of 1, 2 or 4 bit components, adding each to the one a pixel earlier.</summary>
    /// <param name="line">The row.</param>
    /// <param name="colors">The components per pixel.</param>
    /// <param name="bits">The bits per component.</param>
    private static void UndoTiffPacked(Span<byte> line, int colors, int bits)
    {
        var mask = (1 << bits) - 1;
        var components = line.Length * ByteBits / bits;
        for (var i = colors; i < components; i++)
        {
            var sum = (ReadPacked(line, i, bits) + ReadPacked(line, i - colors, bits)) & mask;
            var shift = ByteBits - bits - ((i * bits) % ByteBits);
            line[i * bits / ByteBits] = (byte)((line[i * bits / ByteBits] & ~(mask << shift)) | (sum << shift));
        }
    }

    /// <summary>Reads a packed component, most significant bits first.</summary>
    /// <param name="line">The row.</param>
    /// <param name="index">The component index.</param>
    /// <param name="bits">The bits per component.</param>
    /// <returns>The component value.</returns>
    private static int ReadPacked(ReadOnlySpan<byte> line, int index, int bits)
    {
        var shift = ByteBits - bits - ((index * bits) % ByteBits);
        return (line[index * bits / ByteBits] >> shift) & ((1 << bits) - 1);
    }

    /// <summary>Undoes the TIFF predictor on a row of 16-bit big-endian components.</summary>
    /// <param name="line">The row.</param>
    /// <param name="colors">The components per pixel.</param>
    private static void UndoTiff16(Span<byte> line, int colors)
    {
        var stride = colors * sizeof(ushort);
        for (var i = stride; i + 1 < line.Length; i += sizeof(ushort))
        {
            var value = (ushort)(((line[i] << ByteBits) | line[i + 1]) + ((line[i - stride] << ByteBits) | line[i - stride + 1]));
            line[i] = (byte)(value >> ByteBits);
            line[i + 1] = (byte)value;
        }
    }

    /// <summary>The shape of the rows a predictor works on.</summary>
    /// <param name="Colors">The components per pixel.</param>
    /// <param name="Bits">The bits per component.</param>
    /// <param name="RowBytes">The bytes in a row.</param>
    /// <param name="PixelBytes">The bytes in a pixel, at least one.</param>
    private readonly record struct RowLayout(int Colors, int Bits, int RowBytes, int PixelBytes);
}
