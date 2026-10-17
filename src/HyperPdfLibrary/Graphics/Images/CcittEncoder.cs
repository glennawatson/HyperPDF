// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Encodes packed 1-bit rows as CCITT fax data, the inverse of <see cref="CcittFaxDecoder"/>: Group 4 (T.6) for a negative
/// <c>/K</c>, Group 3 one-dimensional (T.4) for zero and Group 3 mixed one- and two-dimensional for a positive one, where
/// every K-th row is coded one-dimensionally. <c>/EndOfLine</c>, <c>/EncodedByteAlign</c>, <c>/EndOfBlock</c> and
/// <c>/BlackIs1</c> follow ISO 32000-2 table 11. Decoding the output with the same parameters gives back the same bits. It
/// writes the code words of Recommendation T.4 tables 2 to 4, never the uncompressed-mode extension, and puts the end-of-line code
/// and tag bit of T.4 4.2.2 before each line, which gives the same bit sequence as T.4's layout of one after each line.
/// </summary>
internal static class CcittEncoder
{
    /// <summary>The extra entries in a line buffer beyond one per column: room for the end sentinels.</summary>
    private const int LineSlack = 4;

    /// <summary>The shift from a pixel index to its byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of a pixel index's bit within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A byte with every bit set.</summary>
    private const byte FullByte = 0xFF;

    /// <summary>The end-of-line codes that end a Group 4 block (EOFB).</summary>
    private const int GroupFourEndCodes = 2;

    /// <summary>The end-of-line codes that end a Group 3 block (RTC).</summary>
    private const int GroupThreeEndCodes = 6;

    /// <summary>The tag bit after an end-of-line code that says the line is one-dimensional.</summary>
    private const int OneDimensionalTag = 1;

    /// <summary>The tag bit after an end-of-line code that says the line is two-dimensional.</summary>
    private const int TwoDimensionalTag = 0;

    /// <summary>Encodes rows as Group 4 data with no options: black pixels are the clear bits, and the data ends with the end-of-block code.</summary>
    /// <param name="rows">The packed rows, most significant bit first, each <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="width">The pixels in a row.</param>
    /// <param name="height">The rows.</param>
    /// <param name="blackIs1">Whether 1 bits are black; otherwise 0 bits are.</param>
    /// <param name="output">Receives the encoded data.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or the rows are too short.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EncodeGroup4(ReadOnlySpan<byte> rows, int width, int height, bool blackIs1, ref PooledBuffer output, ScratchPools scratchPools, CancellationToken cancellationToken) =>
        Encode(rows, width, height, new(-1, width, height, blackIs1, false, false, true), ref output, scratchPools, cancellationToken);

    /// <summary>Encodes rows as CCITT data under the filter parameters.</summary>
    /// <param name="rows">The packed rows, most significant bit first, each <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="width">The pixels in a row, which is the <c>/Columns</c> value the decoder must be given.</param>
    /// <param name="height">The rows.</param>
    /// <param name="parameters">The scheme (<c>K</c>) and options; the columns and rows in it are not used.</param>
    /// <param name="output">Receives the encoded data.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or the rows are too short.</exception>
    internal static void Encode(ReadOnlySpan<byte> rows, int width, int height, in CcittParameters parameters, ref PooledBuffer output, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var stride = (width + BitMask) >> ByteShift;
        ArgumentOutOfRangeException.ThrowIfLessThan((long)rows.Length, (long)stride * height, nameof(rows));
        var lineLength = width + LineSlack;
        var pool = scratchPools.Get<int>();
        var rented = pool.Rent(lineLength + lineLength);
        var bits = default(CcittBitWriter);
        try
        {
            var even = rented.AsSpan(0, lineLength);
            var odd = rented.AsSpan(lineLength, lineLength);

            // The line above an image's first row is all white: no changes.
            odd.Fill(width);
            for (var row = 0; row < height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Rows alternate between the two buffers, so the previous row is always the reference.
                var current = (row & 1) == 0 ? even : odd;
                var reference = (row & 1) == 0 ? odd : even;
                var count = FindChanges(rows.Slice(row * stride, stride), width, parameters.BlackIs1, current);
                current[count..].Fill(width);
                EncodeRow(ref bits, row, current, count, reference, width, parameters);
            }

            if (parameters.EndOfBlock)
            {
                WriteEndOfBlock(ref bits, parameters);
            }

            bits.AlignToByte();
            output.Write(bits.WrittenSpan);
        }
        finally
        {
            bits.Dispose();
            pool.Return(rented);
        }
    }

    /// <summary>Writes what comes before a row and then the row.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="row">The row index.</param>
    /// <param name="current">The row's changes, followed by padding entries equal to the width.</param>
    /// <param name="count">The changes in <paramref name="current"/>.</param>
    /// <param name="reference">The row above's changes, padded with the width.</param>
    /// <param name="width">The pixels in a row.</param>
    /// <param name="parameters">The scheme and options.</param>
    private static void EncodeRow(ref CcittBitWriter bits, int row, ReadOnlySpan<int> current, int count, ReadOnlySpan<int> reference, int width, in CcittParameters parameters)
    {
        var twoDimensional = parameters.K < 0 || (parameters.K > 0 && row % parameters.K != 0);
        WriteLineStart(ref bits, parameters, twoDimensional);
        if (twoDimensional)
        {
            CcittLineEncoder.Encode2D(ref bits, current, reference, width);
            return;
        }

        CcittLineEncoder.Encode1D(ref bits, current[..count], width);
    }

    /// <summary>Writes the alignment, end-of-line code and mixed-mode tag bit a row starts with.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="parameters">The scheme and options.</param>
    /// <param name="twoDimensional">Whether the row is two-dimensionally coded.</param>
    private static void WriteLineStart(ref CcittBitWriter bits, in CcittParameters parameters, bool twoDimensional)
    {
        if (parameters.K >= 0 && parameters.EndOfLine)
        {
            WriteEndOfLine(ref bits, parameters.EncodedByteAlign);
        }
        else if (parameters.EncodedByteAlign)
        {
            bits.AlignToByte();
        }

        if (parameters.K > 0)
        {
            bits.Write(twoDimensional ? TwoDimensionalTag : OneDimensionalTag, 1);
        }
    }

    /// <summary>Writes an end-of-line code, preceded by fill bits that make it end on a byte boundary when aligned.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="aligned">Whether lines begin on byte boundaries, so the code must end on one.</param>
    private static void WriteEndOfLine(ref CcittBitWriter bits, bool aligned)
    {
        if (aligned)
        {
            bits.Write(0, (ByteBits - ((bits.PendingBits + CcittCodeTables.EndOfLine.Length) % ByteBits)) % ByteBits);
        }

        bits.Write(CcittCodeTables.EndOfLine);
    }

    /// <summary>Writes the end-of-facsimile-block (two end-of-line codes) or the return-to-control sequence (six, each tagged one-dimensional when mixed).</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="parameters">The scheme and options.</param>
    private static void WriteEndOfBlock(ref CcittBitWriter bits, in CcittParameters parameters)
    {
        var count = parameters.K < 0 ? GroupFourEndCodes : GroupThreeEndCodes;

        // Rows that each start with their own end-of-line code keep each code ending on a boundary; otherwise the block
        // starts on a boundary and its codes follow one another.
        var fillPerCode = parameters.K >= 0 && parameters.EndOfLine && parameters.EncodedByteAlign;
        if (parameters.EncodedByteAlign && !fillPerCode)
        {
            bits.AlignToByte();
        }

        for (var i = 0; i < count; i++)
        {
            WriteEndOfLine(ref bits, fillPerCode);
            if (parameters.K > 0)
            {
                bits.Write(OneDimensionalTag, 1);
            }
        }
    }

    /// <summary>Lists where a row's colour changes, starting from white before the first pixel.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <param name="changes">Receives the positions; even entries start black runs, odd ones white runs.</param>
    /// <returns>The number of changes.</returns>
    private static int FindChanges(ReadOnlySpan<byte> row, int width, bool blackIs1, Span<int> changes)
    {
        var count = 0;
        var black = false;
        var x = 0;
        while (x < width)
        {
            var value = row[x >> ByteShift];
            if ((x & BitMask) == 0 && x + ByteBits <= width && value is 0 or FullByte)
            {
                // A whole byte of one colour changes at most once, at its first pixel.
                var byteBlack = (value == FullByte) == blackIs1;
                if (byteBlack != black)
                {
                    changes[count] = x;
                    count++;
                    black = byteBlack;
                }

                x += ByteBits;
                continue;
            }

            var pixelBlack = (((value >> (BitMask - (x & BitMask))) & 1) == 1) == blackIs1;
            if (pixelBlack != black)
            {
                changes[count] = x;
                count++;
                black = pixelBlack;
            }

            x++;
        }

        return count;
    }
}
