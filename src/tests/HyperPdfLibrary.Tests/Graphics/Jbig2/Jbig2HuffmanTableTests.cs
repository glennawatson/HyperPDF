// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests for JBIG2 Huffman tables: the standard tables and table segments.</summary>
public sealed class Jbig2HuffmanTableTests
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The index of the last bit in a byte.</summary>
    private const int LastBit = 7;

    /// <summary>A value in table B.1's first range.</summary>
    private const int B1First = 5;

    /// <summary>A value in table B.1's second range.</summary>
    private const int B1Second = 19;

    /// <summary>A value in table B.1's third range.</summary>
    private const int B1Third = 273;

    /// <summary>A value in table B.1's open upper range.</summary>
    private const int B1Upper = 65_810;

    /// <summary>A value in table B.2's fourth range.</summary>
    private const int B2Fourth = 8;

    /// <summary>The first value of table B.2's upper range.</summary>
    private const int B2Upper = 75;

    /// <summary>A value in table B.3's open lower range.</summary>
    private const int B3Lower = -260;

    /// <summary>A value in table B.3's first range.</summary>
    private const int B3First = -251;

    /// <summary>A value in the custom table's second line.</summary>
    private const int CustomSecond = 3;

    /// <summary>A value in the custom table's open upper range.</summary>
    private const int CustomUpper = 45;

    /// <summary>The first value of the custom table's open lower range.</summary>
    private const int CustomLower = -9;

    /// <summary>A value in the custom table's first line.</summary>
    private const int CustomFirst = -6;

    /// <summary>
    /// A table segment: out-of-band, 3-bit prefix and range sizes, values from -8 to 40 in four lines with prefixes
    /// 3, 2, 2 and 3 and ranges 3, 3, 4 and 4; lower and upper range prefixes of 4 and an out-of-band prefix of 3.
    /// </summary>
    private const string CustomSegment = "00100101" + "11111111111111111111111111111000" + "00000000000000000000000000101000"
        + "011011" + "010011" + "010100" + "011100" + "100" + "100" + "011";

    /// <summary>Standard table B.1 decodes values in every range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardTableB1Decodes()
    {
        var table = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);

        await Assert.That(Decode(table, "0" + "0101")).IsEqualTo(new(Jbig2HuffmanResult.Value, B1First));
        await Assert.That(Decode(table, "10" + "00000011")).IsEqualTo(new(Jbig2HuffmanResult.Value, B1Second));
        await Assert.That(Decode(table, "110" + "0000000000000001")).IsEqualTo(new(Jbig2HuffmanResult.Value, B1Third));
        await Assert.That(Decode(table, "111" + "00000000000000000000000000000010")).IsEqualTo(new(Jbig2HuffmanResult.Value, B1Upper));
    }

    /// <summary>Standard table B.2 decodes its out-of-band code and its upper range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardTableB2Decodes()
    {
        var table = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B2);

        await Assert.That(Decode(table, "111111").Result).IsEqualTo(Jbig2HuffmanResult.OutOfBand);
        await Assert.That(Decode(table, "1110" + "101")).IsEqualTo(new(Jbig2HuffmanResult.Value, B2Fourth));
        await Assert.That(Decode(table, "111110" + "00000000000000000000000000000000")).IsEqualTo(new(Jbig2HuffmanResult.Value, B2Upper));
    }

    /// <summary>Standard table B.3's lower range counts down from its low value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardTableB3LowerRangeCountsDown()
    {
        var table = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B3);

        await Assert.That(Decode(table, "11111111" + "00000000000000000000000000000011")).IsEqualTo(new(Jbig2HuffmanResult.Value, B3Lower));
        await Assert.That(Decode(table, "11111110" + "00000101")).IsEqualTo(new(Jbig2HuffmanResult.Value, B3First));
    }

    /// <summary>Data that ends inside a code is an error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedCodeIsAnError()
    {
        var result = Decode(Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1), string.Empty);

        await Assert.That(result.Result).IsEqualTo(Jbig2HuffmanResult.Error);
    }

    /// <summary>A table segment parses and decodes values in its lines, open ranges and out-of-band code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TableSegmentDecodes()
    {
        await Assert.That(DecodeCustom("00" + "011")).IsEqualTo(new(Jbig2HuffmanResult.Value, CustomSecond));
        await Assert.That(DecodeCustom("110").Result).IsEqualTo(Jbig2HuffmanResult.OutOfBand);
        await Assert.That(DecodeCustom("1111" + "00000000000000000000000000000101")).IsEqualTo(new(Jbig2HuffmanResult.Value, CustomUpper));
        await Assert.That(DecodeCustom("1110" + "00000000000000000000000000000000")).IsEqualTo(new(Jbig2HuffmanResult.Value, CustomLower));
        await Assert.That(DecodeCustom("100" + "010")).IsEqualTo(new(Jbig2HuffmanResult.Value, CustomFirst));
    }

    /// <summary>A table segment whose lowest value is above its highest is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvertedTableSegmentIsRejected()
    {
        var inverted = Parse("00000000" + "00000000000000000000000000101000" + "11111111111111111111111111111000");

        await Assert.That(inverted).IsFalse();
    }

    /// <summary>Decodes one value from bits written as text.</summary>
    /// <param name="table">The table.</param>
    /// <param name="bits">The bits, '0' and '1'.</param>
    /// <returns>The result.</returns>
    private static Decoded Decode(Jbig2HuffmanTable table, string bits)
    {
        var reader = new Jbig2Reader(Pack(bits));
        var result = table.Decode(ref reader, out var value);
        return new(result, value);
    }

    /// <summary>Parses the custom table segment, then decodes one value from the bits after it.</summary>
    /// <param name="bits">The bits after the segment, which starts the value on a byte boundary.</param>
    /// <returns>The result.</returns>
    private static Decoded DecodeCustom(string bits)
    {
        var segment = Pack(CustomSegment);
        var value = Pack(bits);
        var data = new byte[segment.Length + value.Length];
        segment.CopyTo(data, 0);
        value.CopyTo(data, segment.Length);
        var reader = new Jbig2Reader(data);
        if (Jbig2HuffmanTable.Parse(ref reader) is not { } table)
        {
            return new(Jbig2HuffmanResult.Error, 0);
        }

        reader.AlignByte();
        var result = table.Decode(ref reader, out var decoded);
        return new(result, decoded);
    }

    /// <summary>Parses a table segment.</summary>
    /// <param name="bits">The segment's bits.</param>
    /// <returns>Whether a table was parsed.</returns>
    private static bool Parse(string bits)
    {
        var reader = new Jbig2Reader(Pack(bits));
        return Jbig2HuffmanTable.Parse(ref reader) is not null;
    }

    /// <summary>Packs bits written as text into bytes, padding the last byte with zeros.</summary>
    /// <param name="bits">The bits.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Pack(string bits)
    {
        var bytes = new byte[(bits.Length + LastBit) / ByteBits];
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] == '1')
            {
                bytes[i / ByteBits] |= (byte)(1 << (LastBit - (i % ByteBits)));
            }
        }

        return bytes;
    }

    /// <summary>The outcome of decoding one value.</summary>
    /// <param name="Result">Whether a value, out-of-band or an error was read.</param>
    /// <param name="Value">The value.</param>
    [DebuggerDisplay("Decoded: {Result} {Value}")]
    private readonly record struct Decoded(Jbig2HuffmanResult Result, int Value);
}
