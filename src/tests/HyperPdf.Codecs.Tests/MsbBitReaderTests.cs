// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks the bit reader against a plain bit by bit reader on random data.</summary>
public sealed class MsbBitReaderTests
{
    /// <summary>The seed of the random data, so a failure can be repeated.</summary>
    private const int Seed = 20_261_023;

    /// <summary>The bytes of the random data.</summary>
    private const int DataLength = 4_096;

    /// <summary>The number of reads in each test.</summary>
    private const int Reads = 20_000;

    /// <summary>The most bits one read asks for.</summary>
    private const int WidestRead = 32;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of the bit within a byte.</summary>
    private const int BitMask = 7;

    /// <summary>The shift from a bit position to its byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The bits skipped before the seek test reads.</summary>
    private const int SeekBits = 13;

    /// <summary>The bits read before the align test aligns.</summary>
    private const int PartialBits = 3;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>The bits in a nibble.</summary>
    private const int Nibble = 4;

    /// <summary>The bits a signed read takes in the last case of the sign test.</summary>
    private const int Twelve = 12;

    /// <summary>The value the last case of the sign test reads: the 12 bits 0111 0000 1000.</summary>
    private const int Twelve0x708 = 0x708;

    /// <summary>The number of bytes in the unary test that are non-zero one time in this many.</summary>
    private const int SparseOne = 8;

    /// <summary>The bytes at the end of the unary test that are left unread so a run cannot run past the data.</summary>
    private const int UnaryMargin = 64;

    /// <summary>Bytes for the sign test.</summary>
    private static readonly byte[] SignBytes = [0xF0, 0x70, 0x80];

    /// <summary>One byte of ones.</summary>
    private static readonly byte[] OneByte = [0xFF];

    /// <summary>Verifies that reads of every width from 0 to 32 bits give the same bits as a bit by bit reader.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_MatchesABitByBitReader()
    {
        var (mismatches, position, reached) = ReadRandomWidths();

        await Assert.That(mismatches).IsEqualTo(0);
        await Assert.That(reached).IsEqualTo(position);
    }

    /// <summary>Verifies that peeking does not move the reader and agrees with reading.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Peek_DoesNotMoveTheReader()
    {
        var reader = new MsbBitReader(RandomData.Bytes(Seed, DataLength));
        var first = reader.Peek(WidestRead);
        var second = reader.Peek(WidestRead);
        var read = reader.Read(WidestRead);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(read).IsEqualTo(first);
    }

    /// <summary>Verifies that signed reads extend the sign of the bits read.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadSigned_ExtendsTheSign()
    {
        var reads = ReadSignedValues();

        await Assert.That(reads).IsEquivalentTo([-1, 0, Twelve0x708]);
    }

    /// <summary>Verifies that unary runs, including ones longer than the window, are counted as a bit by bit reader counts them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadUnary_CountsZerosBeforeTheOne()
    {
        var (mismatches, position, reached) = ReadUnaryRuns();

        await Assert.That(mismatches).IsEqualTo(0);
        await Assert.That(reached).IsEqualTo(position);
    }

    /// <summary>Verifies that aligning skips to the next byte and seeking returns to a bit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AlignAndSeek_MoveToBitPositions()
    {
        var data = RandomData.Bytes(Seed, DataLength);
        var reader = new MsbBitReader(data);
        _ = reader.Read(PartialBits);
        reader.Align();
        var aligned = reader.BitPosition;
        reader.Seek(SeekBits);
        var sought = reader.Read(WidestRead);

        await Assert.That(aligned).IsEqualTo(ByteBits);
        await Assert.That(sought).IsEqualTo(Naive(data, SeekBits, WidestRead));
    }

    /// <summary>Verifies that reading past the end gives zeros and sets the overrun flag instead of throwing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadingPastTheEnd_GivesZerosAndFlagsIt()
    {
        var reader = new MsbBitReader(OneByte);

        var inside = reader.Read(ByteBits);
        var flaggedBefore = reader.IsOverrun;
        var outside = reader.Read(WidestRead);
        var flaggedAfter = reader.IsOverrun;

        await Assert.That(inside).IsEqualTo(0xFFU);
        await Assert.That(flaggedBefore).IsFalse();
        await Assert.That(outside).IsEqualTo(0U);
        await Assert.That(flaggedAfter).IsTrue();
    }

    /// <summary>Reads random widths and compares each with a bit by bit read.</summary>
    /// <returns>The mismatches, the bits that should have been read and the bits the reader says it read.</returns>
    private static ReadOutcome ReadRandomWidths()
    {
        var data = RandomData.Bytes(Seed, DataLength);
        var random = new SeededRandom(Seed);
        var reader = new MsbBitReader(data);
        long position = 0;
        var mismatches = 0;
        for (var index = 0; index < Reads && position + WidestRead < (long)data.Length * ByteBits; index++)
        {
            var width = random.Next(WidestRead + 1);
            mismatches += reader.Read(width) == Naive(data, position, width) ? 0 : 1;
            position += width;
        }

        return new(mismatches, position, reader.BitPosition);
    }

    /// <summary>Reads three signed numbers of different sizes.</summary>
    /// <returns>The numbers.</returns>
    private static int[] ReadSignedValues()
    {
        var reader = new MsbBitReader(SignBytes);
        return [reader.ReadSigned(Nibble), reader.ReadSigned(Nibble), reader.ReadSigned(Twelve)];
    }

    /// <summary>Reads unary runs from mostly zero data and compares each with a bit by bit count.</summary>
    /// <returns>The mismatches, the bits that should have been read and the bits the reader says it read.</returns>
    private static ReadOutcome ReadUnaryRuns()
    {
        var data = new byte[DataLength];
        var random = new SeededRandom(Seed);
        for (var index = 0; index < data.Length; index++)
        {
            // Mostly zeros, so runs are long and some cross the window.
            data[index] = random.Next(SparseOne) == 0 ? (byte)random.Next(1, ByteValues) : (byte)0;
        }

        var reader = new MsbBitReader(data);
        long position = 0;
        var mismatches = 0;
        while (position < (((long)data.Length - UnaryMargin) * ByteBits))
        {
            var expected = NaiveUnary(data, position);
            mismatches += reader.ReadUnary() == expected ? 0 : 1;
            position += expected + 1;
        }

        return new(mismatches, position, reader.BitPosition);
    }

    /// <summary>Reads bits one at a time.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The bit to start at.</param>
    /// <param name="width">The number of bits.</param>
    /// <returns>The bits as a number.</returns>
    private static uint Naive(byte[] data, long position, int width)
    {
        var value = 0UL;
        for (var bit = 0; bit < width; bit++)
        {
            value = (value << 1) | BitAt(data, position + bit);
        }

        return (uint)value;
    }

    /// <summary>Counts zero bits one at a time up to the next one.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The bit to start at.</param>
    /// <returns>The number of zero bits.</returns>
    private static int NaiveUnary(byte[] data, long position)
    {
        var count = 0;
        while (BitAt(data, position + count) == 0)
        {
            count++;
        }

        return count;
    }

    /// <summary>Gets one bit.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The bit's position.</param>
    /// <returns>0 or 1.</returns>
    private static uint BitAt(byte[] data, long position) => (uint)((data[position >> ByteShift] >> ((ByteBits - 1) - (int)(position & BitMask))) & 1);

    /// <summary>What a run of reads found.</summary>
    /// <param name="Mismatches">How many reads differed from the bit by bit read.</param>
    /// <param name="Position">The bits that should have been read.</param>
    /// <param name="Reached">The bits the reader says it read.</param>
    private readonly record struct ReadOutcome(int Mismatches, long Position, long Reached);
}
