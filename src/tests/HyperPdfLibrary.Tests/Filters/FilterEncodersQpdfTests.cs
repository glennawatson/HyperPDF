// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks the filter encoders against qpdf, an independent reader. Each test skips when qpdf is not installed.</summary>
public sealed class FilterEncodersQpdfTests
{
    /// <summary>The reason a test is skipped.</summary>
    private const string QpdfMissing = "qpdf is not installed.";

    /// <summary>The seed of the image rows.</summary>
    private const uint Seed = 91_817;

    /// <summary>The components per pixel in the predictor images.</summary>
    private const int Colors = 3;

    /// <summary>The pixels in a row of the TIFF predictor image.</summary>
    private const int TiffColumns = 37;

    /// <summary>The pixels in a row of the PNG predictor image.</summary>
    private const int PngColumns = 41;

    /// <summary>The rows in the TIFF predictor image.</summary>
    private const int TiffRows = 11;

    /// <summary>The rows in the PNG predictor image.</summary>
    private const int PngRows = 13;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The samples sent to qpdf: edge cases and shapes that fill tables and runs.</summary>
    private static readonly string[] SampleNames = ["empty", "zeros", "noise", "runs", "alphabet3", "text", "short1", "short5", "short300", "run128", "run129"];

    /// <summary>Checks that qpdf decodes the encoder's LZW with each /EarlyChange value.</summary>
    /// <param name="earlyChange">The /EarlyChange value.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public Task Lzw_DecodedByQpdf_GivesOriginal(int earlyChange, CancellationToken cancellationToken) =>
        CheckAsync(
            $"/Filter /LZWDecode /DecodeParms << /EarlyChange {earlyChange} >>",
            (ReadOnlySpan<byte> input, ref PooledBuffer output) => LzwEncoder.Encode(input, earlyChange, ref output, cancellationToken),
            cancellationToken);

    /// <summary>Checks that qpdf decodes the encoder's run-length data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public Task RunLength_DecodedByQpdf_GivesOriginal(CancellationToken cancellationToken) =>
        CheckAsync("/Filter /RunLengthDecode", static (input, ref output) => RunLengthFilter.Encode(input, ref output), cancellationToken);

    /// <summary>Checks that qpdf decodes the encoder's ASCII base-85 text.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public Task Ascii85_DecodedByQpdf_GivesOriginal(CancellationToken cancellationToken) =>
        CheckAsync("/Filter /ASCII85Decode", static (input, ref output) => Ascii85Filter.Encode(input, ref output), cancellationToken);

    /// <summary>Checks that qpdf decodes the encoder's ASCII hexadecimal text.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public Task AsciiHex_DecodedByQpdf_GivesOriginal(CancellationToken cancellationToken) =>
        CheckAsync("/Filter /ASCIIHexDecode", static (input, ref output) => AsciiHexFilter.Encode(input, ref output), cancellationToken);

    /// <summary>Checks that qpdf decodes a two-filter chain, as ISO 32000-2 7.4.1 example 2 writes it: base-85 text of LZW data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public Task Ascii85ThenLzw_DecodedByQpdf_GivesOriginal(CancellationToken cancellationToken) =>
        CheckAsync(
            "/Filter [/ASCII85Decode /LZWDecode]",
            static (input, ref output) =>
            {
                var lzw = FilterRoundTrip.Encode(static (data, ref lzwOutput) => LzwEncoder.Encode(data, 1, ref lzwOutput), input);
                Ascii85Filter.Encode(lzw, ref output);
            },
            cancellationToken);

    /// <summary>Checks that qpdf undoes TIFF Predictor 2 under LZW for each bit depth the PDF specification allows.</summary>
    /// <param name="bits">The bits per component.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    public async Task TiffPredictor_DecodedByQpdf_GivesOriginal(int bits, CancellationToken cancellationToken)
    {
        var rowBytes = ((TiffColumns * Colors * bits) + ByteBits - 1) / ByteBits;
        var rows = new SampleRandom(Seed).NextBytes(rowBytes * TiffRows);

        // qpdf writes zeros in the unused bits at the end of each row, so the rows hold zeros there too.
        var padding = (rowBytes * ByteBits) - (TiffColumns * Colors * bits);
        for (var row = 1; padding > 0 && row <= TiffRows; row++)
        {
            rows[(row * rowBytes) - 1] &= (byte)(byte.MaxValue << padding);
        }

        var predicted = FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => TiffPredictorEncoder.Encode(input, rowBytes, Colors, bits, ref output), rows);
        var encoded = FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => LzwEncoder.Encode(input, 1, ref output, cancellationToken), predicted);
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Filter /LZWDecode /DecodeParms << /Predictor 2 /Colors {Colors} /BitsPerComponent {bits} /Columns {TiffColumns} >>");

        await CheckOneAsync(entries, encoded, rows, "tiff", cancellationToken);
    }

    /// <summary>Checks that qpdf undoes each PNG predictor under Flate.</summary>
    /// <param name="predictor">The predictor value, from 10 to 15.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(10)]
    [Arguments(11)]
    [Arguments(12)]
    [Arguments(13)]
    [Arguments(14)]
    [Arguments(15)]
    public async Task PngPredictor_DecodedByQpdf_GivesOriginal(int predictor, CancellationToken cancellationToken)
    {
        const int RowBytes = PngColumns * Colors;
        var rows = new SampleRandom(Seed).NextBytes(RowBytes * PngRows);
        var predicted = FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => PngPredictorEncoder.Encode(input, RowBytes, Colors, predictor, ref output), rows);
        var encoded = FilterRoundTrip.Encode(static (input, ref output) => FlateFilter.Encode(input, ref output), predicted);
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Filter /FlateDecode /DecodeParms << /Predictor {predictor} /Colors {Colors} /Columns {PngColumns} >>");

        await CheckOneAsync(entries, encoded, rows, "png", cancellationToken);
    }

    /// <summary>Encodes every sample with an encoder and checks that qpdf decodes it back.</summary>
    /// <param name="filterEntries">The stream dictionary entries naming the filters.</param>
    /// <param name="encode">The encoder.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when every sample matches, or the test is skipped.</returns>
    private static async Task CheckAsync(string filterEntries, FilterEncode encode, CancellationToken cancellationToken)
    {
        foreach (var sample in FilterSamples.All().Where(static s => SampleNames.Contains(s.Name)))
        {
            await CheckOneAsync(filterEntries, FilterRoundTrip.Encode(encode, sample.Data), sample.Data, sample.Name, cancellationToken);
        }
    }

    /// <summary>Has qpdf decode one stream and compares the result.</summary>
    /// <param name="filterEntries">The stream dictionary entries naming the filters.</param>
    /// <param name="encoded">The encoded data.</param>
    /// <param name="expected">The original data.</param>
    /// <param name="name">What the data is, shown when the check fails.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the data matches, or the test is skipped.</returns>
    private static async Task CheckOneAsync(string filterEntries, byte[] encoded, byte[] expected, string name, CancellationToken cancellationToken)
    {
        var result = await QpdfStreamDecoder.DecodeAsync(filterEntries, encoded, cancellationToken);
        if (result is null)
        {
            Skip.Test(QpdfMissing);
            return;
        }

        await Assert.That(result.Data).IsEquivalentTo(expected).Because($"{name}: {result.Errors}");
    }
}
