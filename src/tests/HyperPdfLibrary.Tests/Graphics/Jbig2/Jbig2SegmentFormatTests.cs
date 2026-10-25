// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Tests for the segment level of JBIG2 (T.88 section 7 and ISO/IEC 14492:2019 as ISO 32000-2 clause 7.4.7 uses it):
/// segment headers, generic regions of unknown length, striped pages, damaged segments and implicit pages. Streams are
/// built from the decoder's own samples by moving whole segments, so the expected image is the sample's hash.
/// </summary>
public sealed class Jbig2SegmentFormatTests
{
    /// <summary>The immediate generic region segment type.</summary>
    private const int ImmediateGeneric = 38;

    /// <summary>The page information segment type.</summary>
    private const int PageInformation = 48;

    /// <summary>The end-of-stripe segment type.</summary>
    private const int EndOfStripe = 50;

    /// <summary>The text region segment type used for a damaged segment.</summary>
    private const int ImmediateText = 6;

    /// <summary>The unknown segment data length.</summary>
    private const uint UnknownLength = 0xFFFFFFFF;

    /// <summary>The bytes of a 32-bit field.</summary>
    private const int LengthBytes = 4;

    /// <summary>The offset of the combination operator in a region segment's data.</summary>
    private const int OperatorOffset = 16;

    /// <summary>The combination operator XOR.</summary>
    private const byte XorOperator = 2;

    /// <summary>The offset of the page height in the page information data.</summary>
    private const int PageHeightOffset = 4;

    /// <summary>The offset of the striping field in the page information data.</summary>
    private const int StripingOffset = 17;

    /// <summary>The striping field of a striped page with a stripe size of 16 rows.</summary>
    private const ushort Striped16 = 0x8010;

    /// <summary>A pattern width that a signed byte cannot reach back.</summary>
    private const int WidePattern = 200;

    /// <summary>The bits of zero padding after a code, enough for any range.</summary>
    private const int PaddingBits = 64;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The most significant bit of a byte.</summary>
    private const int TopBit = 0x80;

    /// <summary>The number of standard Huffman tables.</summary>
    private const int StandardTables = 15;

    /// <summary>The number of an unrelated segment added to a stream.</summary>
    private const int ExtraSegmentNumber = 90;

    /// <summary>The number of the end-of-stripe segment.</summary>
    private const int StripeSegmentNumber = 91;

    /// <summary>The data bytes of a text region segment that cannot be decoded: all zero, so its region size is empty.</summary>
    private const int BrokenTextBytes = 32;

    /// <summary>The bytes of a header that the garbage globals hold.</summary>
    private const int GarbageGlobalsBytes = 32;

    /// <summary>The segment number that needs two-byte references.</summary>
    private const uint TwoByteNumber = 300;

    /// <summary>The segment number that needs four-byte references.</summary>
    private const uint FourByteNumber = 70_000;

    /// <summary>The segment data length in the test headers.</summary>
    private const uint HeaderDataLength = 9;

    /// <summary>The segment data length in the long-form test header.</summary>
    private const uint LongHeaderDataLength = 12;

    /// <summary>The first referred segment number in the long-form header.</summary>
    private const uint FirstReferred = 69_000;

    /// <summary>The first reference of the two-byte header, which needs both bytes.</summary>
    private const uint TwoByteReference = 256;

    /// <summary>The second reference of the two-byte header.</summary>
    private const uint SmallReference = 2;

    /// <summary>The number of segments the long-form header refers to.</summary>
    private const int LongFormCount = 5;

    /// <summary>The top three bits of a long-form referred-to segment count field.</summary>
    private const uint LongFormMarker = 0xE0000000;

    /// <summary>The page association flag of a four-byte association.</summary>
    private const int LongPageFlag = 0x40;

    /// <summary>The shift of the short-form referred-to segment count.</summary>
    private const int ReferredShift = 5;

    /// <summary>The mask of the segment type.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The smallest segment header.</summary>
    private const int MinimumHeader = 11;

    /// <summary>The bytes before a short header's referred-to segments: number, flags and the count byte.</summary>
    private const int FixedHeaderBytes = 6;

    /// <summary>The seed of the random streams.</summary>
    private const uint Seed = 20_261_014;

    /// <summary>The random streams decoded.</summary>
    private const int RandomStreams = 300;

    /// <summary>The longest random stream.</summary>
    private const int LongestRandomStream = 600;

    /// <summary>The side of the page the random streams decode to.</summary>
    private const int RandomPageSide = 32;

    /// <summary>The number of byte values.</summary>
    private const int ByteValues = 256;

    /// <summary>The divisor that halves the rows kept.</summary>
    private const int Halves = 2;

    /// <summary>A pattern dictionary template with only the first adaptive pixel.</summary>
    private const int OnePixelTemplate = 3;

    /// <summary>A pattern width of one pixel.</summary>
    private const int NarrowPattern = 1;

    /// <summary>A byte with every pixel white in the output.</summary>
    private const byte White = 0xFF;

    /// <summary>Gets the marker that ends arithmetic-coded data.</summary>
    private static ReadOnlySpan<byte> ArithmeticMarker => [0xFF, 0xAC];

    /// <summary>A generic region of unknown length decodes to the image its known-length twin does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownLengthArithmeticRegionDecodesLikeTheKnownLength()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var stream = WithUnknownLength(sample.Data, sample.Height, false);

        await Assert.That(Hash(stream, [], sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>An MMR generic region of unknown length ends at its 0x00 0x00 marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownLengthMmrRegionDecodesLikeTheKnownLength()
    {
        var sample = Jbig2Samples.GenericMmr;
        var stream = WithUnknownLength(sample.Data, sample.Height, true);

        await Assert.That(Hash(stream, [], sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>The row count after an unknown-length region cuts the region, and the next segment still starts in the right place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownLengthRegionKeepsOnlyTheRowsTheCountSays()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var kept = sample.Height / Halves;

        // A second copy of the region, XORed over the first, cancels the rows the first one drew.
        var second = Segments(sample.Data).First(IsGeneric);
        var copy = sample.Data.AsSpan(second.Offset, second.DataOffset + second.DataLength - second.Offset).ToArray();
        copy[second.DataOffset - second.Offset + OperatorOffset] = XorOperator;
        var stream = WithUnknownLength(sample.Data, kept, false, copy);
        var stride = Jbig2Decoder.GetRowBytes(sample.Width);
        var full = Decode(sample.Data, [], sample.Width, sample.Height);

        var rows = Decode(stream, [], sample.Width, sample.Height);

        await Assert.That(rows.AsSpan(0, kept * stride).ContainsAnyExcept(White)).IsFalse();
        await Assert.That(rows.AsSpan(kept * stride).SequenceEqual(full.AsSpan(kept * stride))).IsTrue();
    }

    /// <summary>A segment that fails is skipped, and the regions after it still draw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedSegmentIsSkipped()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var generic = Segments(sample.Data).First(IsGeneric);

        // A text region with an empty region size cannot be decoded.
        var broken = Segment(ExtraSegmentNumber, ImmediateText, new byte[BrokenTextBytes]);
        var stream = Concatenate(sample.Data.AsSpan(0, generic.Offset).ToArray(), broken, sample.Data.AsSpan(generic.Offset).ToArray());

        await Assert.That(Hash(stream, [], sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>Damaged global segments do not stop a page that does not need them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedGlobalsDoNotStopThePage()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var globals = new byte[GarbageGlobalsBytes];
        Array.Fill(globals, byte.MaxValue);

        await Assert.That(Hash(sample.Data, globals, sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>A stream without a page information segment is read as a white page, so its regions draw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPageInformationGivesAWhitePage()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var page = Segments(sample.Data).First(static segment => segment.Type == PageInformation);
        var afterPage = page.DataOffset + page.DataLength;
        var stream = Concatenate(sample.Data.AsSpan(0, page.Offset).ToArray(), sample.Data.AsSpan(afterPage).ToArray());

        await Assert.That(Hash(stream, [], sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>A striped page of unknown height, with end-of-stripe segments, decodes at the height the PDF image gives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripedPageOfUnknownHeightUsesTheImageHeight()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var data = sample.Data.ToArray();
        var page = Segments(data).First(static segment => segment.Type == PageInformation);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(page.DataOffset + PageHeightOffset), UnknownLength);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(page.DataOffset + StripingOffset), Striped16);
        var stripeEnd = new byte[LengthBytes];
        BinaryPrimitives.WriteUInt32BigEndian(stripeEnd, (uint)(sample.Height - 1));
        var stream = Concatenate(data, Segment(StripeSegmentNumber, EndOfStripe, stripeEnd));

        await Assert.That(Hash(stream, [], sample)).IsEqualTo(sample.Sha256);
    }

    /// <summary>The first adaptive pixel of a pattern dictionary reaches one pattern width back, even past 127 pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternDictionaryAdaptivePixelReachesAWidePattern()
    {
        await Assert.That(Jbig2PatternDictionary.AtPixels(0, WidePattern).X1).IsEqualTo((short)-WidePattern);
        await Assert.That(Jbig2PatternDictionary.AtPixels(0, WidePattern).Y1).IsEqualTo((short)0);
        await Assert.That(Jbig2PatternDictionary.AtPixels(1, WidePattern).X1).IsEqualTo((short)-WidePattern);
        await Assert.That(Jbig2PatternDictionary.AtPixels(OnePixelTemplate, NarrowPattern).X1).IsEqualTo((short)-NarrowPattern);
    }

    /// <summary>Every standard Huffman table is a complete prefix code: each bit pattern of the longest length decodes to a line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardTablesAreCompletePrefixCodes()
    {
        for (var number = 1; number <= StandardTables; number++)
        {
            await Assert.That(DecodesEveryPattern(Jbig2HuffmanTable.GetStandard(number))).IsTrue();
        }
    }

    /// <summary>A segment header with a two-byte reference size reads its short-form references.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderWithTwoByteReferencesIsRead()
    {
        // Segment 300, type 6, two references (short form: count 2, retain bits 0) of 256 and 2, page association 1, length 9.
        byte[] header = [0, 0, 0x01, 0x2C, ImmediateText, 0x40, 0x01, 0x00, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00, 0x09];

        var (number, type, referred, length) = ParseHeader(header);

        await Assert.That(number).IsEqualTo(TwoByteNumber);
        await Assert.That(type).IsEqualTo(ImmediateText);
        await Assert.That(referred.SequenceEqual([TwoByteReference, SmallReference])).IsTrue();
        await Assert.That(length).IsEqualTo(HeaderDataLength);
    }

    /// <summary>A segment header in the long form, with four-byte references and a four-byte page association, is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderInTheLongFormIsRead()
    {
        var header = new List<byte>();
        Jbig2StreamBuilder.AddInt(header, (int)FourByteNumber);
        header.Add(ImmediateText | LongPageFlag);
        Jbig2StreamBuilder.AddInt(header, unchecked((int)(LongFormMarker | LongFormCount)));
        header.Add(0);
        for (var i = 0; i < LongFormCount; i++)
        {
            Jbig2StreamBuilder.AddInt(header, (int)FirstReferred + i);
        }

        Jbig2StreamBuilder.AddInt(header, 1);
        Jbig2StreamBuilder.AddInt(header, (int)LongHeaderDataLength);

        var (number, type, referred, length) = ParseHeader([.. header]);

        await Assert.That(number).IsEqualTo(FourByteNumber);
        await Assert.That(type).IsEqualTo(ImmediateText);
        await Assert.That(referred.Length).IsEqualTo(LongFormCount);
        await Assert.That(referred[0]).IsEqualTo(FirstReferred);
        await Assert.That(referred[^1]).IsEqualTo(FirstReferred + LongFormCount - 1);
        await Assert.That(length).IsEqualTo(LongHeaderDataLength);
    }

    /// <summary>A long-form count larger than the data can hold is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderWithAnImpossibleCountIsRejected()
    {
        var header = new List<byte>();
        Jbig2StreamBuilder.AddInt(header, (int)FourByteNumber);
        header.Add(ImmediateText);
        Jbig2StreamBuilder.AddInt(header, unchecked((int)uint.MaxValue));
        header.AddRange(new byte[LengthBytes * Halves]);

        await Assert.That(TryParseHeader([.. header])).IsFalse();
    }

    /// <summary>Random bytes never throw and never write outside the destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RandomBytesDoNotThrow()
    {
        var random = new JpegTestRandom(Seed);
        var decoded = 0;
        for (var i = 0; i < RandomStreams; i++)
        {
            var data = new byte[random.Next(0, LongestRandomStream)];
            for (var j = 0; j < data.Length; j++)
            {
                data[j] = (byte)random.Next(0, ByteValues);
            }

            _ = Decode(data, [], RandomPageSide, RandomPageSide);
            decoded++;
        }

        await Assert.That(decoded).IsEqualTo(RandomStreams);
    }

    /// <summary>Hashes the decoded rows of a stream.</summary>
    /// <param name="data">The page segments.</param>
    /// <param name="globals">The global segments.</param>
    /// <param name="sample">The sample whose size to use.</param>
    /// <returns>The lowercase hex SHA-256.</returns>
    private static string Hash(byte[] data, byte[] globals, Jbig2Sample sample) =>
        Convert.ToHexStringLower(SHA256.HashData(Decode(data, globals, sample.Width, sample.Height)));

    /// <summary>Decodes a stream.</summary>
    /// <param name="data">The page segments.</param>
    /// <param name="globals">The global segments.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The packed rows.</returns>
    private static byte[] Decode(byte[] data, byte[] globals, int width, int height)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(width) * height];
        _ = Jbig2Decoder.TryDecode(data, globals, width, height, rows);
        return rows;
    }

    /// <summary>Determines whether a segment is a generic region.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> for the immediate generic region type.</returns>
    private static bool IsGeneric(SegmentSpan segment) => segment.Type == ImmediateGeneric;

    /// <summary>Joins byte arrays.</summary>
    /// <param name="parts">The arrays.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Concatenate(params byte[][] parts) => [.. parts.SelectMany(static part => part)];

    /// <summary>Builds a segment with no referred segments, page association 1 and a known length.</summary>
    /// <param name="number">The segment number.</param>
    /// <param name="type">The segment type.</param>
    /// <param name="data">The segment data.</param>
    /// <returns>The header and data.</returns>
    private static byte[] Segment(int number, int type, byte[] data)
    {
        var bytes = new List<byte>();
        Jbig2StreamBuilder.AddInt(bytes, number);
        bytes.Add((byte)type);
        bytes.Add(0);
        bytes.Add(1);
        Jbig2StreamBuilder.AddInt(bytes, data.Length);
        bytes.AddRange(data);
        return [.. bytes];
    }

    /// <summary>Lists the segments of a stream whose segments use the short header form.</summary>
    /// <param name="data">The page segments.</param>
    /// <returns>The segments.</returns>
    private static List<SegmentSpan> Segments(byte[] data)
    {
        var segments = new List<SegmentSpan>();
        var offset = 0;
        while (offset + MinimumHeader <= data.Length)
        {
            var type = data[offset + LengthBytes] & TypeMask;
            var pageBytes = (data[offset + LengthBytes] & LongPageFlag) != 0 ? LengthBytes : 1;
            var referred = data[offset + LengthBytes + 1] >> ReferredShift;
            var lengthAt = offset + FixedHeaderBytes + referred + pageBytes;
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(lengthAt));
            segments.Add(new(offset, type, lengthAt + LengthBytes, length));
            offset = lengthAt + LengthBytes + length;
        }

        return segments;
    }

    /// <summary>Rewrites the first generic region of a stream to have an unknown data length.</summary>
    /// <param name="data">The stream.</param>
    /// <param name="rows">The row count to write after the end marker.</param>
    /// <param name="mmr">Whether the region is MMR coded.</param>
    /// <returns>The new stream.</returns>
    private static byte[] WithUnknownLength(byte[] data, int rows, bool mmr) => WithUnknownLength(data, rows, mmr, []);

    /// <summary>Rewrites the first generic region of a stream to have an unknown data length, and adds a segment after it.</summary>
    /// <param name="data">The stream.</param>
    /// <param name="rows">The row count to write after the end marker.</param>
    /// <param name="mmr">Whether the region is MMR coded.</param>
    /// <param name="inserted">Bytes to put right after the rewritten region.</param>
    /// <returns>The new stream.</returns>
    private static byte[] WithUnknownLength(byte[] data, int rows, bool mmr, byte[] inserted)
    {
        var segment = Segments(data).First(IsGeneric);
        var header = data.AsSpan(segment.Offset, segment.DataOffset - LengthBytes - segment.Offset).ToArray();
        var body = data.AsSpan(segment.DataOffset, segment.DataLength).ToArray();
        var marker = mmr ? new byte[] { 0, 0 } : ArithmeticMarker.ToArray();
        if (!body.AsSpan().EndsWith(marker))
        {
            body = Concatenate(body, marker);
        }

        var length = new byte[LengthBytes];
        BinaryPrimitives.WriteUInt32BigEndian(length, UnknownLength);
        var count = new byte[LengthBytes];
        BinaryPrimitives.WriteUInt32BigEndian(count, (uint)rows);
        var after = segment.DataOffset + segment.DataLength;
        return Concatenate(data.AsSpan(0, segment.Offset).ToArray(), header, length, body, count, inserted, data.AsSpan(after).ToArray());
    }

    /// <summary>Parses a segment header.</summary>
    /// <param name="header">The header bytes.</param>
    /// <returns>The parsed fields.</returns>
    /// <exception cref="InvalidDataException">The header is rejected.</exception>
    private static HeaderFields ParseHeader(byte[] header)
    {
        var reader = new Jbig2Reader(header);
        var segment = Jbig2Segment.Parse(ref reader) ?? throw new InvalidDataException("The header was rejected.");
        return new(segment.Number, (int)segment.Type, segment.Referred.ToArray(), segment.DataLength);
    }

    /// <summary>Determines whether a segment header parses.</summary>
    /// <param name="header">The header bytes.</param>
    /// <returns><see langword="true"/> when it parses.</returns>
    private static bool TryParseHeader(byte[] header)
    {
        var reader = new Jbig2Reader(header);
        return Jbig2Segment.Parse(ref reader) is not null;
    }

    /// <summary>Decodes every bit pattern of the table's longest code length, followed by zero bits.</summary>
    /// <param name="table">The table.</param>
    /// <returns><see langword="true"/> when no pattern is an error.</returns>
    private static bool DecodesEveryPattern(Jbig2HuffmanTable table)
    {
        for (var pattern = 0; pattern < 1 << table.MaxLength; pattern++)
        {
            var bits = new byte[(table.MaxLength + PaddingBits + ByteBits - 1) / ByteBits];
            for (var i = 0; i < table.MaxLength; i++)
            {
                if (((pattern >> (table.MaxLength - 1 - i)) & 1) != 0)
                {
                    bits[i / ByteBits] |= (byte)(TopBit >> (i % ByteBits));
                }
            }

            var reader = new Jbig2Reader(bits);
            if (table.Decode(ref reader, out _) == Jbig2HuffmanResult.Error)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A segment's place in a stream.</summary>
    /// <param name="Offset">The offset of the header.</param>
    /// <param name="Type">The segment type.</param>
    /// <param name="DataOffset">The offset of the data.</param>
    /// <param name="DataLength">The data length.</param>
    [DebuggerDisplay("SegmentSpan: type {Type} at {Offset}")]
    private sealed record SegmentSpan(int Offset, int Type, int DataOffset, int DataLength);

    /// <summary>The fields of a parsed segment header.</summary>
    /// <param name="Number">The segment number.</param>
    /// <param name="Type">The segment type.</param>
    /// <param name="Referred">The referred-to segment numbers.</param>
    /// <param name="Length">The data length.</param>
    [DebuggerDisplay("HeaderFields: segment {Number} type {Type}")]
    private sealed record HeaderFields(uint Number, int Type, uint[] Referred, uint Length);
}
