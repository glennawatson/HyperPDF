// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Encodes packed 1-bit rows as CCITT Group 4 (T.6) fax data, the inverse of <see cref="CcittFaxDecoder"/> with
/// <c>/K -1</c>. Each row is coded against the one above it with pass, vertical and horizontal modes, and the data ends
/// with an end-of-facsimile-block code. Decoding the output with the same <c>/BlackIs1</c> gives back the same bits.
/// </summary>
internal static class CcittG4Encoder
{
    /// <summary>The extra entries in a line buffer beyond one per column: room for the end sentinels.</summary>
    private const int LineSlack = 4;

    /// <summary>The longest run a make-up code covers.</summary>
    private const int MaxMakeup = 2560;

    /// <summary>The step between make-up codes.</summary>
    private const int MakeupStep = 64;

    /// <summary>The longest run the colour-specific make-up codes cover.</summary>
    private const int ColorMakeupLimit = 1728;

    /// <summary>The run the first extended make-up code covers.</summary>
    private const int ExtendedStart = 1792;

    /// <summary>The largest vertical-mode offset.</summary>
    private const int MaxVertical = 3;

    /// <summary>The index of the pass code in the mode tables.</summary>
    private const int PassMode = 0;

    /// <summary>The index of the horizontal code in the mode tables.</summary>
    private const int HorizontalMode = 1;

    /// <summary>The index of the V0 code in the mode tables; VL3 to VR3 sit around it.</summary>
    private const int VerticalZero = 5;

    /// <summary>The end-of-line code, written twice as the end-of-facsimile-block.</summary>
    private const int EndOfLine = 1;

    /// <summary>The length of the end-of-line code.</summary>
    private const int EndOfLineBits = 12;

    /// <summary>The shift from a pixel index to its byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of a pixel index's bit within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A byte with every bit set.</summary>
    private const byte FullByte = 0xFF;

    /// <summary>The white terminating codes, runs 0 to 63.</summary>
    private static readonly RunCode[] WhiteTerminating = Parse(
        "00110101 000111 0111 1000 1011 1100 1110 1111 10011 10100 00111 01000 001000 000011 110100 110101 "u8
        + "101010 101011 0100111 0001100 0001000 0010111 0000011 0000100 0101000 0101011 0010011 0100100 "u8
        + "0011000 00000010 00000011 00011010 00011011 00010010 00010011 00010100 00010101 00010110 00010111 "u8
        + "00101000 00101001 00101010 00101011 00101100 00101101 00000100 00000101 00001010 00001011 01010010 "u8
        + "01010011 01010100 01010101 00100100 00100101 01011000 01011001 01011010 01011011 01001010 01001011 "u8
        + "00110010 00110011 00110100"u8);

    /// <summary>The white make-up codes, runs 64 to 1728.</summary>
    private static readonly RunCode[] WhiteMakeup = Parse(
        "11011 10010 010111 0110111 00110110 00110111 01100100 01100101 01101000 01100111 011001100 011001101 "u8
        + "011010010 011010011 011010100 011010101 011010110 011010111 011011000 011011001 011011010 011011011 "u8
        + "010011000 010011001 010011010 011000 010011011"u8);

    /// <summary>The black terminating codes, runs 0 to 63.</summary>
    private static readonly RunCode[] BlackTerminating = Parse(
        "0000110111 010 11 10 011 0011 0010 00011 000101 000100 0000100 0000101 0000111 00000100 00000111 "u8
        + "000011000 0000010111 0000011000 0000001000 00001100111 00001101000 00001101100 00000110111 "u8
        + "00000101000 00000010111 00000011000 000011001010 000011001011 000011001100 000011001101 "u8
        + "000001101000 000001101001 000001101010 000001101011 000011010010 000011010011 000011010100 "u8
        + "000011010101 000011010110 000011010111 000001101100 000001101101 000011011010 000011011011 "u8
        + "000001010100 000001010101 000001010110 000001010111 000001100100 000001100101 000001010010 "u8
        + "000001010011 000000100100 000000110111 000000111000 000000100111 000000101000 000001011000 "u8
        + "000001011001 000000101011 000000101100 000001011010 000001100110 000001100111"u8);

    /// <summary>The black make-up codes, runs 64 to 1728.</summary>
    private static readonly RunCode[] BlackMakeup = Parse(
        "0000001111 000011001000 000011001001 000001011011 000000110011 000000110100 000000110101 "u8
        + "0000001101100 0000001101101 0000001001010 0000001001011 0000001001100 0000001001101 "u8
        + "0000001110010 0000001110011 0000001110100 0000001110101 0000001110110 0000001110111 "u8
        + "0000001010010 0000001010011 0000001010100 0000001010101 0000001011010 0000001011011 "u8
        + "0000001100100 0000001100101"u8);

    /// <summary>The extended make-up codes shared by both colours, runs 1792 to 2560.</summary>
    private static readonly RunCode[] ExtendedMakeup = Parse(
        "00000001000 00000001100 00000001101 000000010010 000000010011 000000010100 000000010101 000000010110 "u8
        + "000000010111 000000011100 000000011101 000000011110 000000011111"u8);

    /// <summary>The mode codes: pass, horizontal, then vertical left 3, 2, 1, zero and right 1, 2, 3.</summary>
    private static readonly RunCode[] Modes = Parse("0001 001 0000010 000010 010 1 011 000011 0000011"u8);

    /// <summary>Encodes rows as Group 4 data.</summary>
    /// <param name="rows">The packed rows, most significant bit first, each <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="width">The pixels in a row.</param>
    /// <param name="height">The rows.</param>
    /// <param name="blackIs1">Whether 1 bits are black; otherwise 0 bits are.</param>
    /// <param name="output">Receives the encoded data.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or the rows are too short.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Encode(ReadOnlySpan<byte> rows, int width, int height, bool blackIs1, ref PooledBuffer output) =>
        Encode(rows, width, height, blackIs1, ref output, new());

    /// <summary>Encodes Group 4 rows with an explicitly owned scratch cache.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The pixels in a row.</param>
    /// <param name="height">The rows.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <param name="output">Receives encoded data.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal static void Encode(ReadOnlySpan<byte> rows, int width, int height, bool blackIs1, ref PooledBuffer output, ScratchPools scratchPools)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var stride = (width + BitMask) >> ByteShift;
        ArgumentOutOfRangeException.ThrowIfLessThan((long)rows.Length, (long)stride * height, nameof(rows));
        var lineLength = width + LineSlack;
        var pool = scratchPools.Get<int>();
        var rented = pool.Rent(lineLength + lineLength);
        var bits = default(BitWriter);
        try
        {
            var even = rented.AsSpan(0, lineLength);
            var odd = rented.AsSpan(lineLength, lineLength);

            // The line above an image's first row is all white: no changes.
            odd.Fill(width);
            for (var row = 0; row < height; row++)
            {
                // Rows alternate between the two buffers, so the previous row is always the reference.
                var current = (row & 1) == 0 ? even : odd;
                var reference = (row & 1) == 0 ? odd : even;
                var count = FindChanges(rows.Slice(row * stride, stride), width, blackIs1, current);
                current[count..].Fill(width);
                EncodeLine(ref bits, current, reference, width);
            }

            bits.Write(EndOfLine, EndOfLineBits);
            bits.Write(EndOfLine, EndOfLineBits);
            bits.Flush();
            output.Write(bits.WrittenSpan);
        }
        finally
        {
            bits.Dispose();
            pool.Return(rented);
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

    /// <summary>Codes one row against the row above it.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="current">The row's changes, padded with the width.</param>
    /// <param name="reference">The row above's changes, padded with the width.</param>
    /// <param name="width">The pixels in a row.</param>
    private static void EncodeLine(ref BitWriter bits, ReadOnlySpan<int> current, ReadOnlySpan<int> reference, int width)
    {
        var a0 = -1;
        var black = false;
        var currentIndex = 0;
        var referenceIndex = 0;
        while (a0 < width)
        {
            currentIndex = FirstAfter(current, currentIndex, a0);
            referenceIndex = FirstAfter(reference, referenceIndex, a0);

            // b1 is the first change after a0 to the colour opposite a0's: even entries start black runs.
            var b1Index = referenceIndex + (((referenceIndex & 1) == 0) == black ? 1 : 0);
            var a1 = current[currentIndex];
            var b1 = reference[b1Index];
            var b2 = reference[b1Index + 1];
            if (b2 < a1)
            {
                bits.Write(Modes[PassMode]);
                a0 = b2;
                continue;
            }

            var offset = a1 - b1;
            if (Math.Abs(offset) <= MaxVertical)
            {
                bits.Write(Modes[VerticalZero + offset]);
                a0 = a1;
                black = !black;
                continue;
            }

            var a2 = current[currentIndex + 1];
            bits.Write(Modes[HorizontalMode]);
            WriteRun(ref bits, a1 - Math.Max(a0, 0), black);
            WriteRun(ref bits, a2 - a1, !black);
            a0 = a2;
        }
    }

    /// <summary>Finds the first change after a position, starting from an index known to be at or before it.</summary>
    /// <param name="changes">The changes, padded with the width.</param>
    /// <param name="start">The index to start at.</param>
    /// <param name="position">The position.</param>
    /// <returns>The index of the first change after the position, or of the first padding entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstAfter(ReadOnlySpan<int> changes, int start, int position)
    {
        var index = start;
        while (index < changes.Length - LineSlack && changes[index] <= position)
        {
            index++;
        }

        return index;
    }

    /// <summary>Writes one run: make-up codes, then a terminating code.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="run">The run length.</param>
    /// <param name="black">Whether the run is black.</param>
    private static void WriteRun(ref BitWriter bits, int run, bool black)
    {
        while (run > MaxMakeup)
        {
            bits.Write(ExtendedMakeup[^1]);
            run -= MaxMakeup;
        }

        if (run >= MakeupStep)
        {
            var makeup = run / MakeupStep * MakeupStep;
            bits.Write(makeup <= ColorMakeupLimit
                ? (black ? BlackMakeup : WhiteMakeup)[(makeup / MakeupStep) - 1]
                : ExtendedMakeup[(makeup - ExtendedStart) / MakeupStep]);
            run -= makeup;
        }

        bits.Write((black ? BlackTerminating : WhiteTerminating)[run]);
    }

    /// <summary>Turns codes written as bit strings separated by spaces into code values and lengths.</summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The parsed codes, in order.</returns>
    private static RunCode[] Parse(ReadOnlySpan<byte> codes)
    {
        var parsed = new List<RunCode>();
        foreach (var range in codes.Split((byte)' '))
        {
            var code = codes[range];
            var value = 0;
            foreach (var digit in code)
            {
                value = (value << 1) | (digit - (byte)'0');
            }

            parsed.Add(new(value, code.Length));
        }

        return [.. parsed];
    }

    /// <summary>One code: its value and length in bits.</summary>
    /// <param name="Value">The code's bits, right-aligned.</param>
    /// <param name="Length">The number of bits.</param>
    private readonly record struct RunCode(int Value, int Length);

    /// <summary>Writes codes most significant bit first into a pooled buffer.</summary>
    private ref struct BitWriter
    {
        /// <summary>The bytes written.</summary>
        private PooledBuffer _output;

        /// <summary>The bits not yet written, right-aligned.</summary>
        private uint _pending;

        /// <summary>The number of bits in <see cref="_pending"/>.</summary>
        private int _count;

        /// <summary>Gets the bytes written.</summary>
        internal readonly ReadOnlySpan<byte> WrittenSpan => _output.WrittenSpan;

        /// <summary>Returns the buffer to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Dispose() => _output.Dispose();

        /// <summary>Writes a code.</summary>
        /// <param name="code">The code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Write(RunCode code) => Write(code.Value, code.Length);

        /// <summary>Writes the low bits of a value.</summary>
        /// <param name="value">The bits, right-aligned.</param>
        /// <param name="length">The number of bits, at most 16.</param>
        internal void Write(int value, int length)
        {
            _pending = (_pending << length) | (uint)value;
            _count += length;
            while (_count >= ByteBits)
            {
                _count -= ByteBits;
                _output.WriteByte((byte)(_pending >> _count));
            }

            _pending &= (1U << _count) - 1;
        }

        /// <summary>Writes the last bits, padded with zeros to a whole byte.</summary>
        internal void Flush()
        {
            if (_count == 0)
            {
                return;
            }

            _output.WriteByte((byte)(_pending << (ByteBits - _count)));
            _count = 0;
            _pending = 0;
        }
    }
}
