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

    /// <summary>The byte and component chunk length for cancellation checks.</summary>
    private const int ChunkLength = 4096;

    /// <summary>The mask selecting a byte or component chunk boundary.</summary>
    private const int CancellationCheckMask = ChunkLength - 1;

    /// <summary>Applies the predictor named in a filter's parameters, if any.</summary>
    /// <param name="parms">The /DecodeParms dictionary.</param>
    /// <param name="data">The decoded data, changed in place.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Apply(PdfDictionary? parms, ref PooledBuffer data) => Apply(parms, ref data, CancellationToken.None);

    /// <summary>Undoes row prediction with cancellation between rows and byte chunks.</summary>
    /// <param name="parms">The stream's predictor parameters.</param>
    /// <param name="data">The decoded bytes changed in place.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal static void Apply(PdfDictionary? parms, ref PooledBuffer data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var predictor = parms?.GetInt32(KnownName.Predictor, 1) ?? 1;

        // Values 3 to 9 name no predictor, and so do invalid layouts: the data passes through unchanged.
        if (predictor is not (TiffPredictor or >= PngPredictors) || data.Length == 0 || !TryReadLayout(parms!, out var layout))
        {
            return;
        }

        var buffer = data.Array!.AsSpan(0, data.Length);
        data.Length = predictor >= PngPredictors
            ? UndoPng(buffer, layout.RowBytes, layout.PixelBytes, cancellationToken)
            : UndoTiff(buffer, layout.RowBytes, layout.Colors, layout.Bits, cancellationToken);
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
        if (colors <= 0 || columns <= 0 || bits is not (OneBit or TwoBits or FourBits or ByteBits or SixteenBits))
        {
            layout = default;
            return false;
        }

        var bitsPerPixel = colors * bits;
        var rowBytes = (((long)columns * bitsPerPixel) + ByteBits - 1) / ByteBits;
        layout = new(colors, bits, (int)Math.Min(rowBytes, int.MaxValue - 1), (bitsPerPixel + ByteBits - 1) / ByteBits);
        return rowBytes < int.MaxValue;
    }

    /// <summary>Undoes PNG row filters, compacting rows over their filter bytes.</summary>
    /// <param name="data">The data.</param>
    /// <param name="rowBytes">The bytes in a row, without the filter byte.</param>
    /// <param name="pixelBytes">The bytes in a pixel, at least one.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded length.</returns>
    private static int UndoPng(Span<byte> data, int rowBytes, int pixelBytes, CancellationToken cancellationToken)
    {
        var rows = data.Length / (rowBytes + 1);
        for (var row = 0; row < rows; row++)
        {
            CheckCancellation(row, cancellationToken);
            UndoPngRowAt(data, row, rowBytes, rowBytes, pixelBytes, cancellationToken);
        }

        // A truncated last row is decoded as far as its bytes go.
        var partial = data.Length - (rows * (rowBytes + 1)) - 1;
        if (partial <= 0)
        {
            return rows * rowBytes;
        }

        UndoPngRowAt(data, rows, rowBytes, partial, pixelBytes, cancellationToken);
        return (rows * rowBytes) + partial;
    }

    /// <summary>Undoes the PNG row at a position.</summary>
    /// <param name="data">The data.</param>
    /// <param name="row">The row index.</param>
    /// <param name="rowBytes">The bytes in a full row, without the filter byte.</param>
    /// <param name="length">The bytes in this row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void UndoPngRowAt(Span<byte> data, int row, int rowBytes, int length, int pixelBytes, CancellationToken cancellationToken)
    {
        var filter = data[row * (rowBytes + 1)];
        var source = data.Slice((row * (rowBytes + 1)) + 1, length);
        var target = data.Slice(row * rowBytes, length);
        var previous = row == 0 ? default : data.Slice((row - 1) * rowBytes, length);
        UndoPngRow(filter, source, target, previous, pixelBytes, cancellationToken);
    }

    /// <summary>Undoes one PNG row. The target overlaps the source but always lies before it, so reads stay ahead.</summary>
    /// <param name="filter">The row's filter byte.</param>
    /// <param name="source">The filtered row.</param>
    /// <param name="target">Where the decoded row goes.</param>
    /// <param name="previous">The decoded previous row, or empty for the first row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void UndoPngRow(byte filter, ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> previous, int pixelBytes, CancellationToken cancellationToken)
    {
        if (filter == PngUp)
        {
            AddRows(source, previous, target, cancellationToken);
            return;
        }

        CopyForward(source, target, cancellationToken);
        if (filter == PngSub)
        {
            AddLeft(target, pixelBytes, cancellationToken);
            return;
        }

        if (filter == PngAverage)
        {
            Average(target, previous, pixelBytes, cancellationToken);
        }
        else if (filter == PngPaeth)
        {
            Paeth(target, previous, pixelBytes, cancellationToken);
        }
    }

    /// <summary>Copies a row to an earlier, possibly overlapping, position.</summary>
    /// <param name="source">The source.</param>
    /// <param name="target">The target.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void CopyForward(ReadOnlySpan<byte> source, Span<byte> target, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < source.Length; offset += ChunkLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(ChunkLength, source.Length - offset);
            source.Slice(offset, length).CopyTo(target.Slice(offset, length));
        }
    }

    /// <summary>Adds the previous row to a row (the Up filter) a vector at a time.</summary>
    /// <param name="source">The filtered row.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="target">Where the decoded row goes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void AddRows(ReadOnlySpan<byte> source, ReadOnlySpan<byte> previous, Span<byte> target, CancellationToken cancellationToken)
    {
        if (previous.IsEmpty)
        {
            CopyForward(source, target, cancellationToken);
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
                CheckCancellation(i, cancellationToken);

                // The source is loaded before the store; the target sits before the source, so nothing unread is overwritten.
                var sum = Vector.LoadUnsafe(ref src, (nuint)i) + Vector.LoadUnsafe(ref prev, (nuint)i);
                sum.StoreUnsafe(ref dst, (nuint)i);
            }
        }

        for (; i < target.Length; i++)
        {
            CheckCancellation(i, cancellationToken);
            target[i] = (byte)(source[i] + previous[i]);
        }
    }

    /// <summary>Undoes the Average filter in place.</summary>
    /// <param name="row">The filtered row, decoded in place.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Average(Span<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, CancellationToken cancellationToken)
    {
        // The first pixel has nothing to its left, so it adds half the byte above. The first row has nothing above.
        var head = Math.Min(pixelBytes, row.Length);
        ref var current = ref MemoryMarshal.GetReference(row);
        if (previous.IsEmpty)
        {
            for (var start = head; start < row.Length; start += ChunkLength)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var end = Math.Min(start + ChunkLength, row.Length);
                for (var i = start; i < end; i++)
                {
                    Unsafe.Add(ref current, i) += (byte)(Unsafe.Add(ref current, i - pixelBytes) >> 1);
                }
            }

            return;
        }

        ref var above = ref MemoryMarshal.GetReference(previous);
        for (var i = 0; i < head; i++)
        {
            Unsafe.Add(ref current, i) += (byte)(Unsafe.Add(ref above, i) >> 1);
        }

        for (var start = head; start < row.Length; start += ChunkLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(start + ChunkLength, row.Length);
            for (var i = start; i < end; i++)
            {
                Unsafe.Add(ref current, i) += (byte)((Unsafe.Add(ref current, i - pixelBytes) + Unsafe.Add(ref above, i)) >> 1);
            }
        }
    }

    /// <summary>Undoes the Paeth filter in place.</summary>
    /// <param name="row">The filtered row, decoded in place.</param>
    /// <param name="previous">The decoded previous row, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Paeth(Span<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, CancellationToken cancellationToken)
    {
        for (var i = 0; i < row.Length; i++)
        {
            CheckCancellation(i, cancellationToken);
            var left = i >= pixelBytes ? row[i - pixelBytes] : 0;
            var up = previous.IsEmpty ? 0 : previous[i];
            var upLeft = i >= pixelBytes && !previous.IsEmpty ? previous[i - pixelBytes] : 0;
            row[i] = (byte)(row[i] + PaethPredict(left, up, upLeft));
        }
    }

    /// <summary>Adds the byte a stride earlier to each byte from the stride on, in place: the Sub filter and TIFF Predictor 2 at eight bits.</summary>
    /// <param name="row">The row, decoded in place.</param>
    /// <param name="stride">The distance to the byte added, at least one.</param>
    /// <param name="cancellationToken">Cancels this operation between chunks.</param>
    private static void AddLeft(Span<byte> row, int stride, CancellationToken cancellationToken)
    {
        ref var current = ref MemoryMarshal.GetReference(row);
        for (var start = stride; start < row.Length; start += ChunkLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(start + ChunkLength, row.Length);
            for (var i = start; i < end; i++)
            {
                Unsafe.Add(ref current, i) += Unsafe.Add(ref current, i - stride);
            }
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded length.</returns>
    private static int UndoTiff(Span<byte> data, int rowBytes, int colors, int bits, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < data.Length; offset += Math.Min(rowBytes, data.Length - offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = data.Slice(offset, Math.Min(rowBytes, data.Length - offset));
            if (bits == ByteBits)
            {
                AddLeft(line, colors, cancellationToken);
            }
            else if (bits == SixteenBits)
            {
                UndoTiff16(line, colors, cancellationToken);
            }
            else
            {
                UndoTiffPacked(line, colors, bits, cancellationToken);
            }
        }

        return data.Length;
    }

    /// <summary>Undoes the TIFF predictor on a row of 1, 2 or 4 bit components, adding each to the one a pixel earlier.</summary>
    /// <param name="line">The row.</param>
    /// <param name="colors">The components per pixel.</param>
    /// <param name="bits">The bits per component.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void UndoTiffPacked(Span<byte> line, int colors, int bits, CancellationToken cancellationToken)
    {
        var mask = (1 << bits) - 1;
        var components = (long)line.Length * ByteBits / bits;
        for (long i = colors; i < components; i++)
        {
            CheckCancellation(i, cancellationToken);
            var sum = (ReadPacked(line, i, bits) + ReadPacked(line, i - colors, bits)) & mask;
            var shift = ByteBits - bits - (int)((i * bits) % ByteBits);
            var index = (int)(i * bits / ByteBits);
            line[index] = (byte)((line[index] & ~(mask << shift)) | (sum << shift));
        }
    }

    /// <summary>Reads a packed component, most significant bits first.</summary>
    /// <param name="line">The row.</param>
    /// <param name="index">The component index.</param>
    /// <param name="bits">The bits per component.</param>
    /// <returns>The component value.</returns>
    private static int ReadPacked(ReadOnlySpan<byte> line, long index, int bits)
    {
        var shift = ByteBits - bits - (int)((index * bits) % ByteBits);
        return (line[(int)(index * bits / ByteBits)] >> shift) & ((1 << bits) - 1);
    }

    /// <summary>Undoes the TIFF predictor on a row of 16-bit big-endian components.</summary>
    /// <param name="line">The row.</param>
    /// <param name="colors">The components per pixel.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void UndoTiff16(Span<byte> line, int colors, CancellationToken cancellationToken)
    {
        var stride = colors * sizeof(ushort);
        for (var i = stride; i + 1 < line.Length; i += sizeof(ushort))
        {
            CheckCancellation(i, cancellationToken);
            var value = (ushort)(((line[i] << ByteBits) | line[i + 1]) + ((line[i - stride] << ByteBits) | line[i - stride + 1]));
            line[i] = (byte)(value >> ByteBits);
            line[i + 1] = (byte)value;
        }
    }

    /// <summary>Checks cancellation at component or byte chunk boundaries.</summary>
    /// <param name="offset">The current component or byte offset.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckCancellation(long offset, CancellationToken cancellationToken)
    {
        if ((offset & CancellationCheckMask) == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>The shape of the rows a predictor works on.</summary>
    /// <param name="Colors">The components per pixel.</param>
    /// <param name="Bits">The bits per component.</param>
    /// <param name="RowBytes">The bytes in a row.</param>
    /// <param name="PixelBytes">The bytes in a pixel, at least one.</param>
    private readonly record struct RowLayout(int Colors, int Bits, int RowBytes, int PixelBytes);
}
