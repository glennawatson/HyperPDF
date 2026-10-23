// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>
/// Tests for the encoder's tables and transform against ISO/IEC 10918-1 (the code words of Tables K.3 to K.6, the
/// procedures of Annex K.2) and against Skia's quantization tables.
/// </summary>
public sealed class JpegEncoderTableTests
{
    /// <summary>The DC luminance table slot.</summary>
    private const int DcLuminance = 0;

    /// <summary>The DC chrominance table slot.</summary>
    private const int DcChrominance = 1;

    /// <summary>The AC luminance table slot.</summary>
    private const int AcLuminance = 2;

    /// <summary>The AC chrominance table slot.</summary>
    private const int AcChrominance = 3;

    /// <summary>The symbols of an AC table: sixteen runs of ten sizes, the end of block and the zero run.</summary>
    private const int AcSymbols = 162;

    /// <summary>The symbols of a DC table.</summary>
    private const int DcSymbols = 12;

    /// <summary>The largest size category of an AC coefficient.</summary>
    private const int MaxAcSize = 10;

    /// <summary>The bits that hold the size in an AC symbol.</summary>
    private const int RunShift = 4;

    /// <summary>The seed of the random blocks and counts.</summary>
    private const uint Seed = 0xBADC0DE;

    /// <summary>The random blocks tried against the reference transform.</summary>
    private const int Blocks = 200;

    /// <summary>The random count tables tried.</summary>
    private const int Tables = 60;

    /// <summary>The longest Huffman code.</summary>
    private const int MaxLength = 16;

    /// <summary>The sampling points on a side of a block.</summary>
    private const int Side = 8;

    /// <summary>The side of the picture Skia writes to show its tables.</summary>
    private const int SkiaSide = 16;

    /// <summary>The fixed-point scale of the Kraft sum: two to the power of the longest code.</summary>
    private const long KraftScale = 1L << MaxLength;

    /// <summary>The symbols of the Fibonacci counts that force the length limit.</summary>
    private const int FibonacciSymbols = 40;

    /// <summary>The most bits a random count may have.</summary>
    private const int CountBits = 20;

    /// <summary>The symbol of the single-symbol table.</summary>
    private const int LoneSymbol = 0x37;

    /// <summary>The count of the single-symbol table.</summary>
    private const int LoneCount = 9;

    /// <summary>The sample value of the flat block.</summary>
    private const int FlatSample = 200;

    /// <summary>The level shift of 8-bit samples.</summary>
    private const int LevelShift = 128;

    /// <summary>The angle of one step of the DCT definition: a sixteenth of a half turn.</summary>
    private const double AngleStep = Math.PI / 16;

    /// <summary>The scale of the DCT definition: a quarter.</summary>
    private const double DefinitionScale = 0.25;

    /// <summary>The square root of one half, which scales the zero frequency.</summary>
    private const double RootHalf = 0.70710678118654752;

    /// <summary>The bytes of a segment header: the marker and the length field.</summary>
    private const int SegmentHeader = 4;

    /// <summary>The bytes of the length field.</summary>
    private const int LengthBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The base of a code word's digits.</summary>
    private const int Binary = 2;

    /// <summary>The offset of the length in a segment header.</summary>
    private const int LengthOffset = 2;

    /// <summary>The offset of the marker byte in a segment header.</summary>
    private const int MarkerOffset = 1;

    /// <summary>The mask of the table id in a quantization table byte.</summary>
    private const int TableIdMask = 0x0F;

    /// <summary>The code words of the example tables in Tables K.3 to K.6 of the standard.</summary>
    /// <param name="slot">The table slot.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="word">The code word printed in the standard.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DcLuminance, 0, "00")]
    [Arguments(DcLuminance, 1, "010")]
    [Arguments(DcLuminance, 5, "110")]
    [Arguments(DcLuminance, 6, "1110")]
    [Arguments(DcLuminance, 11, "111111110")]
    [Arguments(DcChrominance, 0, "00")]
    [Arguments(DcChrominance, 2, "10")]
    [Arguments(DcChrominance, 3, "110")]
    [Arguments(DcChrominance, 11, "11111111110")]
    [Arguments(AcLuminance, 0x00, "1010")]
    [Arguments(AcLuminance, 0x01, "00")]
    [Arguments(AcLuminance, 0x02, "01")]
    [Arguments(AcLuminance, 0x03, "100")]
    [Arguments(AcLuminance, 0x04, "1011")]
    [Arguments(AcLuminance, 0x05, "11010")]
    [Arguments(AcLuminance, 0x11, "1100")]
    [Arguments(AcLuminance, 0xA1, "111111010")]
    [Arguments(AcLuminance, 0xF0, "11111111001")]
    [Arguments(AcLuminance, 0xFA, "1111111111111110")]
    [Arguments(AcChrominance, 0x00, "00")]
    [Arguments(AcChrominance, 0x01, "01")]
    [Arguments(AcChrominance, 0x02, "100")]
    [Arguments(AcChrominance, 0x03, "1010")]
    [Arguments(AcChrominance, 0x11, "1011")]
    [Arguments(AcChrominance, 0xA1, "111111000")]
    [Arguments(AcChrominance, 0xF0, "1111111010")]
    [Arguments(AcChrominance, 0xFA, "1111111111111110")]
    public async Task ExampleTablesMatchTheStandard(int slot, int symbol, string word)
    {
        var codes = new uint[JpegHuffmanBuilder.SymbolSlots];

        JpegHuffmanBuilder.BuildCodes(CountsOf(slot), ValuesOf(slot), codes);

        var entry = codes[symbol];
        var length = (int)(entry >> JpegHuffmanBuilder.SizeShift);
        var bits = Convert.ToString(entry & JpegHuffmanBuilder.CodeMask, Binary).PadLeft(length, '0');
        await Assert.That(bits).IsEqualTo(word);
    }

    /// <summary>Every example table is complete: the right number of symbols, each once, and a prefix code.</summary>
    /// <param name="slot">The table slot.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DcLuminance)]
    [Arguments(DcChrominance)]
    [Arguments(AcLuminance)]
    [Arguments(AcChrominance)]
    public async Task ExampleTablesAreCompletePrefixCodes(int slot)
    {
        var counts = CountsOf(slot);
        var values = ValuesOf(slot);
        var expected = slot < AcLuminance ? DcSymbols : AcSymbols;

        await Assert.That(values.Length).IsEqualTo(expected);
        await Assert.That(new HashSet<byte>(values).Count).IsEqualTo(expected);
        await Assert.That(new JpegHuffmanTable().TryDefine(counts, values)).IsTrue();
        await Assert.That(KraftSum(counts)).IsLessThan(KraftScale);
        if (slot < AcLuminance)
        {
            return;
        }

        for (var run = 0; run < MaxLength; run++)
        {
            for (var size = 1; size <= MaxAcSize; size++)
            {
                await Assert.That(Array.IndexOf(values, (byte)((run << RunShift) | size))).IsGreaterThanOrEqualTo(0);
            }
        }
    }

    /// <summary>Optimal tables are prefix codes that leave the all-ones code free, hold every counted symbol once and no other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimalTablesAreValidPrefixCodes()
    {
        var random = new JpegTestRandom(Seed);
        for (var round = 0; round < Tables; round++)
        {
            var frequencies = new int[JpegHuffmanBuilder.FrequencySlots];
            var used = random.Next(1, JpegHuffmanBuilder.SymbolSlots + 1);
            for (var i = 0; i < used; i++)
            {
                frequencies[random.Next(0, JpegHuffmanBuilder.SymbolSlots)] += random.Next(1, 1 + (1 << random.Next(1, CountBits)));
            }

            await CheckOptimalTable(frequencies);
        }
    }

    /// <summary>Counts that grow like the Fibonacci numbers need codes longer than sixteen bits; the table still limits them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongCodesAreLimitedToSixteenBits()
    {
        var frequencies = new int[JpegHuffmanBuilder.FrequencySlots];
        var previous = 1;
        var current = 1;
        for (var i = 0; i < FibonacciSymbols; i++)
        {
            frequencies[i] = current;
            var next = previous + current;
            previous = current;
            current = next;
        }

        await CheckOptimalTable(frequencies);
    }

    /// <summary>A table for one symbol holds that symbol with a one-bit code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleSymbolGetsOneBit()
    {
        var frequencies = new int[JpegHuffmanBuilder.FrequencySlots];
        frequencies[LoneSymbol] = LoneCount;
        var counts = new byte[MaxLength];
        var values = new byte[JpegHuffmanBuilder.SymbolSlots];

        var size = JpegHuffmanBuilder.BuildOptimal(frequencies, counts, values);

        await Assert.That(size).IsEqualTo(1);
        await Assert.That(counts[0]).IsEqualTo((byte)1);
        await Assert.That(values[0]).IsEqualTo((byte)LoneSymbol);
    }

    /// <summary>The scaled quantization tables equal the ones Skia (libjpeg) writes for the same quality.</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(25)]
    [Arguments(50)]
    [Arguments(75)]
    [Arguments(90)]
    [Arguments(100)]
    public async Task QuantizationTablesMatchSkia(int quality)
    {
        var tables = SkiaQuantization(quality);
        var luminance = new byte[JpegBlock.Length];
        var chrominance = new byte[JpegBlock.Length];

        JpegQuantizer.Scale(false, quality, luminance);
        JpegQuantizer.Scale(true, quality, chrominance);

        await Assert.That(luminance).IsEquivalentTo(tables[0]);
        await Assert.That(chrominance).IsEquivalentTo(tables[1]);
    }

    /// <summary>The forward transform follows the definition in A.3.3 to within one quantizer step, and the vector and scalar forms agree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForwardDctMatchesTheDefinition()
    {
        var random = new JpegTestRandom(Seed);
        var reciprocals = UnitReciprocals();
        var samples = new byte[JpegBlock.Length];
        var work = new int[JpegBlock.Length];
        var fast = new short[JpegBlock.Length];
        var scalar = new short[JpegBlock.Length];
        var worst = 0;
        for (var round = 0; round < Blocks; round++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (byte)random.Next(0, byte.MaxValue + 1);
            }

            JpegForwardDct.Transform(samples, Side, reciprocals, work, fast);
            JpegForwardDct.TransformScalar(samples, Side, reciprocals, work, scalar);

            await Assert.That(fast).IsEquivalentTo(scalar);
            worst = Math.Max(worst, WorstDifference(samples, fast));
        }

        await Assert.That(worst).IsLessThanOrEqualTo(1);
    }

    /// <summary>A flat block has only a DC coefficient, eight times its level-shifted value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlatBlocksHaveOnlyADcCoefficient()
    {
        var samples = new byte[JpegBlock.Length];
        samples.AsSpan().Fill(FlatSample);
        var output = new short[JpegBlock.Length];

        JpegForwardDct.Transform(samples, Side, UnitReciprocals(), new int[JpegBlock.Length], output);

        await Assert.That(output[0]).IsEqualTo((short)((FlatSample - LevelShift) * Side));
        await Assert.That(output.AsSpan(1).IndexOfAnyExcept((short)0)).IsEqualTo(-1);
    }

    /// <summary>Builds the reciprocals of a table whose steps are all one.</summary>
    /// <returns>The 64 reciprocals in storage order.</returns>
    private static int[] UnitReciprocals()
    {
        var steps = new byte[JpegBlock.Length];
        steps.AsSpan().Fill(1);
        var reciprocals = new int[JpegBlock.Length];
        JpegQuantizer.BuildReciprocals(steps, reciprocals);
        return reciprocals;
    }

    /// <summary>Finds the largest difference between the fast transform and the double-precision definition.</summary>
    /// <param name="samples">The 64 samples, row by row.</param>
    /// <param name="coefficients">The quantized coefficients in zigzag order, with a step of one.</param>
    /// <returns>The largest absolute difference.</returns>
    private static int WorstDifference(byte[] samples, short[] coefficients)
    {
        var worst = 0;
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            var position = JpegBlock.Zigzag[i];
            var expected = Math.Round(Reference(samples, position % Side, position / Side));
            worst = Math.Max(worst, (int)Math.Abs(coefficients[i] - expected));
        }

        return worst;
    }

    /// <summary>Evaluates the forward DCT of A.3.3 for one coefficient.</summary>
    /// <param name="samples">The 64 samples, row by row.</param>
    /// <param name="u">The vertical frequency.</param>
    /// <param name="v">The horizontal frequency.</param>
    /// <returns>The coefficient.</returns>
    private static double Reference(byte[] samples, int u, int v)
    {
        double sum = 0;
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var angleX = ((x << 1) | 1) * v * AngleStep;
                var angleY = ((y << 1) | 1) * u * AngleStep;
                sum += (samples[(y * Side) + x] - LevelShift) * Math.Cos(angleX) * Math.Cos(angleY);
            }
        }

        return DefinitionScale * Scale(u) * Scale(v) * sum;
    }

    /// <summary>Gives the normalising factor C(k) of the DCT definition.</summary>
    /// <param name="k">The frequency.</param>
    /// <returns>One over the square root of two for zero, otherwise one.</returns>
    private static double Scale(int k) => k == 0 ? RootHalf : 1.0;

    /// <summary>Builds an optimal table and checks it.</summary>
    /// <param name="frequencies">The symbol counts.</param>
    /// <returns>A task.</returns>
    private static async Task CheckOptimalTable(int[] frequencies)
    {
        var counts = new byte[MaxLength];
        var values = new byte[JpegHuffmanBuilder.SymbolSlots];

        var size = JpegHuffmanBuilder.BuildOptimal(frequencies, counts, values);

        var counted = new List<byte>();
        for (var symbol = 0; symbol < JpegHuffmanBuilder.SymbolSlots; symbol++)
        {
            if (frequencies[symbol] > 0)
            {
                counted.Add((byte)symbol);
            }
        }

        var listed = values.AsSpan(0, size).ToArray();
        Array.Sort(listed);
        var total = 0;
        foreach (var count in counts)
        {
            total += count;
        }

        await Assert.That(size).IsEqualTo(counted.Count);
        await Assert.That(listed).IsEquivalentTo(counted);
        await Assert.That(total).IsEqualTo(size);
        await Assert.That(new JpegHuffmanTable().TryDefine(counts, values.AsSpan(0, size))).IsTrue();
        await Assert.That(KraftSum(counts)).IsLessThan(KraftScale);
    }

    /// <summary>Sums two to the minus length over every code, scaled by two to the power of sixteen; the all-ones code is kept free, so the sum stays below the full scale.</summary>
    /// <param name="counts">The number of codes of each length.</param>
    /// <returns>The scaled sum.</returns>
    private static long KraftSum(ReadOnlySpan<byte> counts)
    {
        long sum = 0;
        for (var length = 1; length <= MaxLength; length++)
        {
            sum += (long)counts[length - 1] << (MaxLength - length);
        }

        return sum;
    }

    /// <summary>Gets the code counts of a table slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The counts.</returns>
    private static byte[] CountsOf(int slot) => slot switch
    {
        DcLuminance => JpegHuffmanBuilder.DcLuminanceCounts.ToArray(),
        DcChrominance => JpegHuffmanBuilder.DcChrominanceCounts.ToArray(),
        AcLuminance => JpegHuffmanBuilder.AcLuminanceCounts.ToArray(),
        _ => JpegHuffmanBuilder.AcChrominanceCounts.ToArray(),
    };

    /// <summary>Gets the symbols of a table slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The symbols.</returns>
    private static byte[] ValuesOf(int slot) => slot switch
    {
        AcLuminance => JpegHuffmanBuilder.AcLuminanceValues.ToArray(),
        AcChrominance => JpegHuffmanBuilder.AcChrominanceValues.ToArray(),
        _ => JpegHuffmanBuilder.DcValues.ToArray(),
    };

    /// <summary>Reads the two quantization tables of a JPEG that Skia writes.</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The luminance table, then the chrominance table, in zigzag order.</returns>
    /// <exception cref="InvalidOperationException">Skia could not make the picture.</exception>
    private static byte[][] SkiaQuantization(int quality)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(SkiaSide, SkiaSide, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using var pixmap = bitmap.PeekPixels() ?? throw new InvalidOperationException("The bitmap has no pixels.");
        SKJpegEncoderOptions options = new(quality, SKJpegEncoderDownsample.Downsample444, SKJpegEncoderAlphaOption.Ignore);
        using var data = pixmap.Encode(options) ?? throw new InvalidOperationException("Skia did not encode.");
        var jpeg = data.ToArray();
        var tables = new byte[LengthBytes][];
        for (var position = LengthBytes; IsHeaderSegment(jpeg, position); position += LengthBytes + SegmentLength(jpeg, position))
        {
            if (jpeg[position + MarkerOffset] == JpegMarkers.QuantizationTables)
            {
                ReadTables(jpeg.AsSpan(position + SegmentHeader, SegmentLength(jpeg, position) - LengthBytes), tables);
            }
        }

        return tables;
    }

    /// <summary>Determines whether a position holds a marker segment that comes before the first scan.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="position">The offset to test.</param>
    /// <returns><see langword="true"/> for a header segment.</returns>
    private static bool IsHeaderSegment(byte[] jpeg, int position) =>
        position + SegmentHeader <= jpeg.Length && jpeg[position] == JpegMarkers.MarkerPrefix && jpeg[position + MarkerOffset] != JpegMarkers.StartOfScan;

    /// <summary>Reads the length field of the segment at a position.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="position">The offset of the segment's marker.</param>
    /// <returns>The length, which counts the length field.</returns>
    private static int SegmentLength(byte[] jpeg, int position) => (jpeg[position + LengthOffset] << ByteBits) | jpeg[position + LengthOffset + 1];

    /// <summary>Reads the tables of one quantization segment.</summary>
    /// <param name="payload">The segment's payload.</param>
    /// <param name="tables">Receives each table by its id.</param>
    private static void ReadTables(ReadOnlySpan<byte> payload, byte[][] tables)
    {
        for (var offset = 0; offset < payload.Length; offset += 1 + JpegBlock.Length)
        {
            tables[payload[offset] & TableIdMask] = payload.Slice(offset + 1, JpegBlock.Length).ToArray();
        }
    }
}
