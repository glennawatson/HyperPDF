// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for the stream filters.</summary>
public sealed class FilterTests
{
    /// <summary>How many times the sample content line repeats.</summary>
    private const int Repeats = 200;

    /// <summary>How many numbers the truncation sample holds.</summary>
    private const int Numbers = 2000;

    /// <summary>The fraction of compressed data kept when truncating.</summary>
    private const int TruncateDivisor = 2;

    /// <summary>The PNG predictor value meaning "per-row filter".</summary>
    private const int PngPredictor = 12;

    /// <summary>The columns in the predictor sample.</summary>
    private const int Columns = 3;

    /// <summary>The LZW example from the PDF specification.</summary>
    private static readonly byte[] LzwExample = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

    /// <summary>A RunLength sample: a literal run of three and a repeat of three.</summary>
    private static readonly byte[] RunLengthExample = [0x02, (byte)'a', (byte)'b', (byte)'c', 0xFE, (byte)'z', 0x80];

    /// <summary>Two PNG-filtered rows: Sub then Up.</summary>
    private static readonly byte[] PngRows = [0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01, 0x01];

    /// <summary>The rows the PNG sample decodes to.</summary>
    private static readonly byte[] PngDecoded = [0x01, 0x02, 0x03, 0x02, 0x03, 0x04];

    /// <summary>Data compressed with the Flate encoder decodes back to itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlateRoundTrips()
    {
        var original = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("BT /F1 12 Tf (Hello) Tj ET\n", Repeats)));
        var compressed = Compress(original);

        await Assert.That(compressed.Length).IsLessThan(original.Length);
        await Assert.That(Inflate(compressed)).IsEquivalentTo(original);
    }

    /// <summary>Truncated Flate data keeps what decoded before the damage.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedFlateKeepsDecodedPrefix()
    {
        var original = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, Numbers).Select(static i => i.ToString(CultureInfo.InvariantCulture))));
        var compressed = Compress(original);
        var decoded = Inflate(compressed.AsSpan(0, compressed.Length / TruncateDivisor).ToArray());

        await Assert.That(decoded.Length).IsGreaterThan(0);
        await Assert.That(original.AsSpan().StartsWith(decoded)).IsTrue();
    }

    /// <summary>The LZW example from the PDF specification decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LzwDecodesSpecificationExample() =>
        await Assert.That(Decode(KnownName.LZWDecode, LzwExample)).IsEqualTo("-----A---B");

    /// <summary>ASCII85 decodes groups, the 'z' shortcut and a partial final group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85Decodes()
    {
        await Assert.That(Decode(KnownName.ASCII85Decode, "9jqo^BlbD-BleB1DJ+*+F(f,q~>"u8.ToArray())).IsEqualTo("Man is distinguished");
        await Assert.That(Decode(KnownName.ASCII85Decode, "z~>"u8.ToArray())).IsEqualTo("\0\0\0\0");
    }

    /// <summary>ASCIIHex decodes with and without white space, padding an odd digit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsciiHexDecodes()
    {
        await Assert.That(Decode(KnownName.ASCIIHexDecode, "48656C6C6F>"u8.ToArray())).IsEqualTo("Hello");
        await Assert.That(Decode(KnownName.ASCIIHexDecode, "48 65 6c\n6C 6F 4>"u8.ToArray())).IsEqualTo("Hello@");
    }

    /// <summary>RunLength decodes literal and repeated runs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunLengthDecodes() =>
        await Assert.That(Decode(KnownName.RunLengthDecode, RunLengthExample)).IsEqualTo("abczzz");

    /// <summary>The PNG Sub predictor adds the byte to the left; the Up predictor adds the row above.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngPredictorsUndo()
    {
        var parms = new PdfDictionary(null);
        parms.Add(KnownName.Predictor, PdfValue.FromInteger(PngPredictor));
        parms.Add(KnownName.Columns, PdfValue.FromInteger(Columns));

        await Assert.That(Predict(parms, PngRows)).IsEquivalentTo(PngDecoded);
    }

    /// <summary>Compresses data with the Flate encoder.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] data)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes Flate data.</summary>
    /// <param name="data">The compressed bytes.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Inflate(byte[] data)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Decode(data, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Undoes a predictor.</summary>
    /// <param name="parms">The predictor parameters.</param>
    /// <param name="data">The filtered bytes.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Predict(PdfDictionary parms, byte[] data)
    {
        var buffer = new PooledBuffer(data.Length);
        try
        {
            buffer.Write(data);
            PredictorFilter.Apply(parms, ref buffer);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Decodes data through one named filter.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="encoded">The encoded data.</param>
    /// <returns>The decoded text.</returns>
    private static string Decode(KnownName filter, byte[] encoded)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = PdfStreamDecoder.Apply(encoded, PdfValue.FromName(filter), default, ref output);
            return Encoding.Latin1.GetString(output.WrittenSpan);
        }
        finally
        {
            output.Dispose();
        }
    }
}
