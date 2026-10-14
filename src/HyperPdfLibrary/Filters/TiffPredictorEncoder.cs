// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Applies TIFF Predictor 2 (ISO 32000-2 7.4.4.4, TIFF 6.0 section 14): every component is replaced by its difference from
/// the same component of the sample to its left, modulo the component's range. The first sample of each row is kept. Rows
/// are a whole number of bytes, and components are packed high bit first, so 1, 2, 4, 8 and 16 bits per component are all
/// handled, the exact inverse of <see cref="PredictorFilter"/>.
/// </summary>
internal static class TiffPredictorEncoder
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Eight bits per component.</summary>
    private const int EightBits = 8;

    /// <summary>Sixteen bits per component.</summary>
    private const int SixteenBits = 16;

    /// <summary>Applies the predictor to every row of an image.</summary>
    /// <param name="samples">The rows, packed with no padding between them.</param>
    /// <param name="rowBytes">The bytes in a row.</param>
    /// <param name="colors">The components in a sample.</param>
    /// <param name="bitsPerComponent">The bits in a component: 1, 2, 4, 8 or 16.</param>
    /// <param name="output">Receives the differences, as many bytes as <paramref name="samples"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive, or <paramref name="bitsPerComponent"/> is not 1, 2, 4, 8 or 16.</exception>
    /// <exception cref="ArgumentException">The samples are not a whole number of rows.</exception>
    internal static void Encode(ReadOnlySpan<byte> samples, int rowBytes, int colors, int bitsPerComponent, ref PooledBuffer output)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colors);
        if (!BitOperations.IsPow2(bitsPerComponent) || bitsPerComponent > SixteenBits)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), bitsPerComponent, "The bits per component must be 1, 2, 4, 8 or 16.");
        }

        if (samples.Length % rowBytes != 0)
        {
            throw new ArgumentException("The samples are not a whole number of rows.", nameof(samples));
        }

        var target = output.GetSpan(samples.Length);
        for (var offset = 0; offset < samples.Length; offset += rowBytes)
        {
            var row = samples.Slice(offset, rowBytes);
            var differences = target.Slice(offset, rowBytes);
            switch (bitsPerComponent)
            {
                case EightBits:
                {
                    DifferenceBytes(row, colors, differences);
                    break;
                }

                case SixteenBits:
                {
                    DifferenceWords(row, colors, differences);
                    break;
                }

                default:
                {
                    DifferencePacked(row, colors, bitsPerComponent, differences);
                    break;
                }
            }
        }

        output.Advance(samples.Length);
    }

    /// <summary>Subtracts the byte a sample earlier from each byte, a vector at a time.</summary>
    /// <param name="row">The row.</param>
    /// <param name="colors">The components in a sample, which is also the distance in bytes.</param>
    /// <param name="differences">Receives the differences.</param>
    private static void DifferenceBytes(ReadOnlySpan<byte> row, int colors, Span<byte> differences)
    {
        var first = Math.Min(colors, row.Length);
        row[..first].CopyTo(differences);
        var i = first;
        if (Vector.IsHardwareAccelerated)
        {
            ref var source = ref MemoryMarshal.GetReference(row);
            ref var target = ref MemoryMarshal.GetReference(differences);
            for (; i <= row.Length - Vector<byte>.Count; i += Vector<byte>.Count)
            {
                (Vector.LoadUnsafe(ref source, (nuint)i) - Vector.LoadUnsafe(ref source, (nuint)(i - colors))).StoreUnsafe(ref target, (nuint)i);
            }
        }

        for (; i < row.Length; i++)
        {
            differences[i] = (byte)(row[i] - row[i - colors]);
        }
    }

    /// <summary>Subtracts the big-endian 16-bit component a sample earlier from each component.</summary>
    /// <param name="row">The row.</param>
    /// <param name="colors">The components in a sample.</param>
    /// <param name="differences">Receives the differences.</param>
    private static void DifferenceWords(ReadOnlySpan<byte> row, int colors, Span<byte> differences)
    {
        var stride = colors * sizeof(ushort);
        row[..Math.Min(stride, row.Length)].CopyTo(differences);
        for (var i = stride; i + 1 < row.Length; i += sizeof(ushort))
        {
            var value = (ushort)(((row[i] << ByteBits) | row[i + 1]) - ((row[i - stride] << ByteBits) | row[i - stride + 1]));
            differences[i] = (byte)(value >> ByteBits);
            differences[i + 1] = (byte)value;
        }

        if ((row.Length & 1) != 0 && row.Length > stride)
        {
            // A trailing odd byte is not a whole component; the decoder leaves it alone.
            differences[^1] = row[^1];
        }
    }

    /// <summary>Subtracts the 1, 2 or 4-bit component a sample earlier from each component.</summary>
    /// <param name="row">The row.</param>
    /// <param name="colors">The components in a sample.</param>
    /// <param name="bits">The bits in a component.</param>
    /// <param name="differences">Receives the differences.</param>
    private static void DifferencePacked(ReadOnlySpan<byte> row, int colors, int bits, Span<byte> differences)
    {
        var mask = (1 << bits) - 1;
        var components = row.Length * ByteBits / bits;
        differences.Clear();
        for (var i = 0; i < components; i++)
        {
            var value = ReadPacked(row, i, bits);
            var difference = i < colors ? value : (value - ReadPacked(row, i - colors, bits)) & mask;
            differences[i * bits / ByteBits] |= (byte)(difference << (ByteBits - bits - ((i * bits) % ByteBits)));
        }
    }

    /// <summary>Reads a packed component, most significant bits first.</summary>
    /// <param name="row">The row.</param>
    /// <param name="index">The component index.</param>
    /// <param name="bits">The bits in a component.</param>
    /// <returns>The component value.</returns>
    private static int ReadPacked(ReadOnlySpan<byte> row, int index, int bits)
    {
        var shift = ByteBits - bits - ((index * bits) % ByteBits);
        return (row[index * bits / ByteBits] >> shift) & ((1 << bits) - 1);
    }
}
