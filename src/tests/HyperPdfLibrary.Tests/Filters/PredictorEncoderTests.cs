// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for <see cref="TiffPredictorEncoder"/> and <see cref="PngPredictorEncoder"/> against the decoder in <see cref="PredictorFilter"/>.</summary>
public sealed class PredictorEncoderTests
{
    /// <summary>The seed of the random rows.</summary>
    private const uint Seed = 7_654_321;

    /// <summary>The rows in each sample image.</summary>
    private const int Rows = 6;

    /// <summary>The TIFF predictor value.</summary>
    private const int TiffPredictor = 2;

    /// <summary>The first PNG predictor value (None on every row).</summary>
    private const int FirstPngPredictor = 10;

    /// <summary>The last PNG predictor value (the best filter on each row).</summary>
    private const int LastPngPredictor = 15;

    /// <summary>The bits per component of the byte-sized samples.</summary>
    private const int EightBits = 8;

    /// <summary>The bits per component of the word-sized samples.</summary>
    private const int SixteenBits = 16;

    /// <summary>The bits per component of the nibble samples.</summary>
    private const int FourBits = 4;

    /// <summary>A bit depth the PDF specification does not have.</summary>
    private const int ThreeBits = 3;

    /// <summary>The components in the forced-predictor sample.</summary>
    private const int ThreeColors = 3;

    /// <summary>The columns in the forced-predictor sample.</summary>
    private const int SampleColumns = 16;

    /// <summary>The columns in the ramp sample.</summary>
    private const int RampColumns = 16;

    /// <summary>The slope of the ramp sample.</summary>
    private const int RampSlope = 3;

    /// <summary>The bytes in a row of the small samples.</summary>
    private const int SmallRow = 4;

    /// <summary>The bits bytes hold.</summary>
    private const int ByteBits = 8;

    /// <summary>The tag byte of the Sub filter.</summary>
    private const int SubTag = 1;

    /// <summary>The tag byte of the Up filter.</summary>
    private const int UpTag = 2;

    /// <summary>The bit depths the decoder supports.</summary>
    private static readonly int[] BitDepths = [1, 2, 4, 8, 16];

    /// <summary>The component counts tried.</summary>
    private static readonly int[] ColorCounts = [1, 2, 3, 4, 5];

    /// <summary>The widths tried, around byte and word edges and past the chunk the decoder checks cancellation at.</summary>
    private static readonly int[] Widths = [1, 2, 3, 7, 8, 9, 31, 64, 100, 1_501, 2_051];

    /// <summary>A rising ramp of 8-bit samples.</summary>
    private static readonly byte[] Ramp = [10, 20, 30, 40, 50, 60];

    /// <summary>The ramp's TIFF differences: the first sample, then the step each time.</summary>
    private static readonly byte[] RampDifferences = [10, 10, 10, 10, 10, 10];

    /// <summary>Two 16-bit samples: 0x0100 and 0x00FF.</summary>
    private static readonly byte[] SixteenBitSamples = [0x01, 0x00, 0x00, 0xFF];

    /// <summary>The 16-bit differences: 0x0100, then 0x00FF minus 0x0100, which borrows to 0xFFFF.</summary>
    private static readonly byte[] SixteenBitDifferences = [0x01, 0x00, 0xFF, 0xFF];

    /// <summary>Four nibbles: 1, 3, 2, 0.</summary>
    private static readonly byte[] NibbleSamples = [0x13, 0x20];

    /// <summary>The nibble differences: 1, 3-1=2, 2-3=F, 0-2=E.</summary>
    private static readonly byte[] NibbleDifferences = [0x12, 0xFE];

    /// <summary>TIFF Predictor 2 reverses to the original for every bit depth, component count and width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffEncode_AllLayouts_RoundTripThroughDecoder()
    {
        var random = new SampleRandom(Seed);
        foreach (var layout in Layouts())
        {
            var samples = random.NextBytes(layout.RowBytes * Rows);
            var predicted = TiffEncode(samples, layout);

            await Assert.That(Undo(TiffPredictor, layout, predicted)).IsEquivalentTo(samples).Because(layout.ToString());
        }
    }

    /// <summary>The TIFF encoder turns a ramp into constant differences, which is what makes the data compress.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffEncode_EightBitRamp_GivesConstantDifferences()
    {
        var predicted = TiffEncode(Ramp, new(1, EightBits, Ramp.Length));

        await Assert.That(predicted).IsEquivalentTo(RampDifferences);
    }

    /// <summary>The TIFF encoder follows TIFF 6.0 for 16-bit data: big-endian differences with borrow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffEncode_SixteenBit_SubtractsWholeWords() =>
        await Assert.That(TiffEncode(SixteenBitSamples, new(1, SixteenBits, SixteenBitSamples.Length * ByteBits / SixteenBits))).IsEquivalentTo(SixteenBitDifferences);

    /// <summary>The TIFF encoder follows TIFF 6.0 for packed 4-bit data: each nibble minus the nibble before it, modulo 16.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffEncode_FourBit_SubtractsNibbles() =>
        await Assert.That(TiffEncode(NibbleSamples, new(1, FourBits, NibbleSamples.Length * ByteBits / FourBits))).IsEquivalentTo(NibbleDifferences);

    /// <summary>PNG predictors 10 to 15 each reverse to the original for every bit depth, component count and width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngEncode_AllPredictorsAndLayouts_RoundTripThroughDecoder()
    {
        var random = new SampleRandom(Seed);
        foreach (var layout in Layouts())
        {
            var samples = random.NextBytes(layout.RowBytes * Rows);
            for (var predictor = FirstPngPredictor; predictor <= LastPngPredictor; predictor++)
            {
                var decoded = Undo(predictor, layout, Predict(samples, layout, predictor));

                await Assert.That(decoded).IsEquivalentTo(samples).Because($"{layout} predictor {predictor}");
            }
        }
    }

    /// <summary>Each forced PNG predictor tags every row with its own filter byte.</summary>
    /// <param name="predictor">The predictor value, from 10 to 14.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(10)]
    [Arguments(11)]
    [Arguments(12)]
    [Arguments(13)]
    [Arguments(14)]
    public async Task PngEncode_ForcedPredictor_TagsEveryRow(int predictor)
    {
        var layout = new Layout(ThreeColors, EightBits, SampleColumns);
        var predicted = Predict(new SampleRandom(Seed).NextBytes(layout.RowBytes * Rows), layout, predictor);

        for (var row = 0; row < Rows; row++)
        {
            await Assert.That((int)predicted[row * (layout.RowBytes + 1)]).IsEqualTo(predictor - FirstPngPredictor);
        }
    }

    /// <summary>Predictor 15 picks Sub for a horizontal ramp and Up for identical rows, the filters that give zeros.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngEncode_Optimum_PicksTheCheapestFilter()
    {
        var ramp = new byte[RampColumns];
        for (var i = 0; i < ramp.Length; i++)
        {
            ramp[i] = (byte)(i * RampSlope);
        }

        var predicted = Predict([.. ramp, .. ramp, .. ramp], new(1, EightBits, RampColumns), LastPngPredictor);

        await Assert.That((int)predicted[0]).IsEqualTo(SubTag);
        await Assert.That((int)predicted[RampColumns + 1]).IsEqualTo(UpTag);
    }

    /// <summary>A predictor outside 10 to 15 is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngEncode_PredictorOutOfRange_Throws()
    {
        var layout = new Layout(1, EightBits, SmallRow);

        await Assert.That(() => Predict(new byte[SmallRow], layout, FirstPngPredictor - 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Predict(new byte[SmallRow], layout, LastPngPredictor + 1)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Samples that are not a whole number of rows are refused by both encoders.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_PartialRow_Throws()
    {
        var layout = new Layout(1, EightBits, SmallRow);

        await Assert.That(() => Predict(new byte[SmallRow + 1], layout, FirstPngPredictor)).Throws<ArgumentException>();
        await Assert.That(() => TiffEncode(new byte[SmallRow + 1], layout)).Throws<ArgumentException>();
    }

    /// <summary>A bit depth the PDF specification does not have is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffEncode_UnsupportedBitDepth_Throws()
    {
        var layout = new Layout(1, ThreeBits, SmallRow);

        await Assert.That(() => TiffEncode(new byte[SmallRow], layout)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Lists the image layouts under test.</summary>
    /// <returns>The layouts.</returns>
    private static List<Layout> Layouts()
    {
        var layouts = new List<Layout>();
        foreach (var bits in BitDepths)
        {
            foreach (var colors in ColorCounts)
            {
                layouts.AddRange(Widths.Select(width => new Layout(colors, bits, width)));
            }
        }

        return layouts;
    }

    /// <summary>Applies the TIFF predictor to rows.</summary>
    /// <param name="samples">The rows.</param>
    /// <param name="layout">The image layout, whose width gives the row size.</param>
    /// <returns>The differences.</returns>
    private static byte[] TiffEncode(byte[] samples, Layout layout) =>
        FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => TiffPredictorEncoder.Encode(input, layout.RowBytes, layout.Colors, layout.Bits, ref output), samples);

    /// <summary>Filters rows with a PNG predictor.</summary>
    /// <param name="samples">The rows.</param>
    /// <param name="layout">The image layout.</param>
    /// <param name="predictor">The predictor value.</param>
    /// <returns>The filtered rows with their tag bytes.</returns>
    private static byte[] Predict(byte[] samples, Layout layout, int predictor) =>
        FilterRoundTrip.Encode(
            (ReadOnlySpan<byte> input, ref PooledBuffer output) =>
                PngPredictorEncoder.Encode(input, layout.RowBytes, PngPredictorEncoder.GetPixelBytes(layout.Colors, layout.Bits), predictor, ref output),
            samples);

    /// <summary>Undoes a predictor with the library's decoder.</summary>
    /// <param name="predictor">The predictor value.</param>
    /// <param name="layout">The image layout.</param>
    /// <param name="predicted">The predicted bytes.</param>
    /// <returns>The original rows.</returns>
    private static byte[] Undo(int predictor, Layout layout, byte[] predicted)
    {
        var parameters = new PdfDictionary(null);
        parameters.Add(KnownName.Predictor, PdfValue.FromInteger(predictor));
        parameters.Add(KnownName.Colors, PdfValue.FromInteger(layout.Colors));
        parameters.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(layout.Bits));
        parameters.Add(KnownName.Columns, PdfValue.FromInteger(layout.Columns));
        var buffer = new PooledBuffer(predicted.Length);
        try
        {
            buffer.Write(predicted);
            PredictorFilter.Apply(parameters, ref buffer);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>The shape of an image under test.</summary>
    /// <param name="Colors">The components per pixel.</param>
    /// <param name="Bits">The bits per component.</param>
    /// <param name="Columns">The pixels in a row.</param>
    private readonly record struct Layout(int Colors, int Bits, int Columns)
    {
        /// <summary>Gets the bytes in a row, rounded up to whole bytes.</summary>
        internal int RowBytes => ((Columns * Colors * Bits) + ByteBits - 1) / ByteBits;
    }
}
