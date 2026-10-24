// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Decodes the run-length coding of 4-bit and 8-bit bitmaps to palette indexes.</summary>
internal static class BmpRunLength
{
    /// <summary>The escape value that ends a row.</summary>
    private const int EndOfLine = 0;

    /// <summary>The escape value that ends the bitmap.</summary>
    private const int EndOfBitmap = 1;

    /// <summary>The escape value that moves the position.</summary>
    private const int Delta = 2;

    /// <summary>The bytes of a count and value pair.</summary>
    private const int PairBytes = 2;

    /// <summary>The 4-bit pixels in a byte.</summary>
    private const int NibblesPerByte = 2;

    /// <summary>The mask of a low nibble.</summary>
    private const int LowNibble = 0x0F;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>Decodes the coded rows.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="header">The header.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The palette index of every pixel, rows in the order they are stored.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static byte[] Decode(ReadOnlySpan<byte> data, in BmpHeader header, CancellationToken cancellationToken)
    {
        var cursor = new RunCursor(header.Width, header.Height);
        var four = header.Compression == BmpHeaderReader.CompressionRle4;
        var position = 0;
        while (position + 1 < data.Length && !cursor.Done)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = data[position];
            var value = data[position + 1];
            position += PairBytes;
            position = count != 0 ? Repeat(cursor, count, value, position, four) : Escape(data, position, value, four, cursor);
        }

        return cursor.ToArray();
    }

    /// <summary>Handles an escape: the end of a row, a move, or a run of literal pixels.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="position">The position after the escape pair.</param>
    /// <param name="value">The escape value.</param>
    /// <param name="four">Whether the pixels are 4 bits each.</param>
    /// <param name="cursor">The write position.</param>
    /// <returns>The position after what the escape used.</returns>
    private static int Escape(ReadOnlySpan<byte> data, int position, int value, bool four, RunCursor cursor)
    {
        if (value == EndOfBitmap)
        {
            cursor.Finish();
            return position;
        }

        if (value == EndOfLine)
        {
            cursor.NextLine();
            return position;
        }

        if (value != Delta)
        {
            return ReadLiterals(data, position, value, four, cursor);
        }

        if (position + 1 < data.Length)
        {
            cursor.Move(data[position], data[position + 1]);
        }

        return position + PairBytes;
    }

    /// <summary>Repeats a value, or for 4-bit data the two nibbles of a byte in turn.</summary>
    /// <param name="cursor">The write position.</param>
    /// <param name="count">How many pixels.</param>
    /// <param name="value">The byte.</param>
    /// <param name="position">The position after the pair, which is returned unchanged.</param>
    /// <param name="four">Whether the pixels are 4 bits each.</param>
    /// <returns>The position after the pair.</returns>
    private static int Repeat(RunCursor cursor, int count, byte value, int position, bool four)
    {
        for (var i = 0; i < count; i++)
        {
            cursor.Put(four ? Nibble(value, i) : value);
        }

        return position;
    }

    /// <summary>Reads a run of literal pixels.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="position">The position of the first literal byte.</param>
    /// <param name="pixels">The pixels in the run.</param>
    /// <param name="four">Whether the pixels are 4 bits each.</param>
    /// <param name="cursor">The write position.</param>
    /// <returns>The position after the run, rounded up to an even byte.</returns>
    private static int ReadLiterals(ReadOnlySpan<byte> data, int position, int pixels, bool four, RunCursor cursor)
    {
        var bytes = four ? (pixels + 1) / NibblesPerByte : pixels;
        for (var i = 0; i < pixels; i++)
        {
            var at = position + (four ? i / NibblesPerByte : i);
            if (at >= data.Length)
            {
                break;
            }

            cursor.Put(four ? Nibble(data[at], i) : data[at]);
        }

        return position + bytes + (bytes & 1);
    }

    /// <summary>Gets the high or low nibble of a byte, alternating from the high one.</summary>
    /// <param name="value">The byte.</param>
    /// <param name="index">The pixel's index in the run.</param>
    /// <returns>The nibble.</returns>
    private static byte Nibble(byte value, int index) => (byte)((index & 1) == 0 ? value >> NibbleBits : value & LowNibble);
}
