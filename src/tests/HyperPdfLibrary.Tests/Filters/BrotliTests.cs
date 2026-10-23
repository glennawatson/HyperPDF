// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests that BrotliDecode streams are read, and that saving replaces them with conforming filters.</summary>
public sealed class BrotliTests
{
    /// <summary>How many numbers the sample holds.</summary>
    private const int Numbers = 4000;

    /// <summary>The fraction of compressed data kept when truncating.</summary>
    private const int TruncateDivisor = 2;

    /// <summary>The Brotli quality used to make samples.</summary>
    private const int Quality = 5;

    /// <summary>The Brotli window used to make samples, as a power of two.</summary>
    private const int Window = 22;

    /// <summary>The PNG predictor value meaning "per-row filter".</summary>
    private const int PngPredictor = 12;

    /// <summary>The columns in the predictor sample.</summary>
    private const int Columns = 3;

    /// <summary>Two PNG-filtered rows: Sub then Up.</summary>
    private static readonly byte[] PngRows = [0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01, 0x01];

    /// <summary>The rows the PNG sample decodes to.</summary>
    private static readonly byte[] PngDecoded = [0x01, 0x02, 0x03, 0x02, 0x03, 0x04];

    /// <summary>Brotli data decodes back to itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrotliRoundTrips()
    {
        var original = Sample();
        await Assert.That(Decode(PdfValue.FromName(KnownName.BrotliDecode), default, Compress(original))).IsEquivalentTo(original);
    }

    /// <summary>Truncated Brotli data keeps what decoded before the cut.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedBrotliKeepsDecodedPrefix()
    {
        var original = Sample();
        var compressed = Compress(original);
        var decoded = Decode(PdfValue.FromName(KnownName.BrotliDecode), default, compressed.AsSpan(0, compressed.Length / TruncateDivisor).ToArray());
        await Assert.That(original.AsSpan().StartsWith(decoded)).IsTrue();
    }

    /// <summary>A Brotli stream's /DecodeParms predictor is undone after decompression.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrotliAppliesPredictor()
    {
        var parms = new PdfDictionary(null);
        parms.Add(KnownName.Predictor, PdfValue.FromInteger(PngPredictor));
        parms.Add(KnownName.Columns, PdfValue.FromInteger(Columns));
        await Assert.That(Decode(PdfValue.FromName(KnownName.BrotliDecode), PdfValue.FromDictionary(parms), Compress(PngRows))).IsEquivalentTo(PngDecoded);
    }

    /// <summary>Saving rewrites a Brotli stream as Flate with the same content, so the output conforms to ISO 32000-2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingReplacesBrotliWithFlate()
    {
        var original = Sample();
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.BrotliDecode));
        var id = StoreEditing.Add(store, PdfValue.FromStream(new(dictionary, Compress(original))));
        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.Metadata, PdfValue.FromReference(id));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
        var saved = PdfCompactWriter.Save(store, new(false, false));
        using var reopened = StoreOpening.Open(saved, null);
        var stream = reopened.Catalog.GetStream(KnownName.Metadata);
        await Assert.That(stream is not null).IsTrue();
        await Assert.That(stream!.Dictionary.GetName(KnownName.Filter).Is(KnownName.FlateDecode)).IsTrue();
        await Assert.That(Encoding.Latin1.GetString(saved).Contains("BrotliDecode", StringComparison.Ordinal)).IsFalse();
        await Assert.That(DecodeStream(stream)).IsEquivalentTo(original);
    }

    /// <summary>Saving a Brotli-wrapped JPEG keeps the DCTDecode filter and drops only the Brotli layer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingKeepsTheImageCodecAfterBrotli()
    {
        var jpeg = Sample();
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var dictionary = new PdfDictionary(store);
        var filters = new PdfArray(store);
        filters.Add(PdfValue.FromName(KnownName.BrotliDecode));
        filters.Add(PdfValue.FromName(KnownName.DCTDecode));
        dictionary.Set(KnownName.Filter, PdfValue.FromArray(filters));
        var data = Rewrite(dictionary, Compress(jpeg), out var rewritten);
        await Assert.That(rewritten.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsTrue();
        await Assert.That(data).IsEquivalentTo(jpeg);
    }

    /// <summary>Rewrites a Brotli stream for saving.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="encoded">The encoded data.</param>
    /// <param name="rewritten">The dictionary to write.</param>
    /// <returns>The data to write.</returns>
    private static byte[] Rewrite(PdfDictionary dictionary, byte[] encoded, out PdfDictionary rewritten)
    {
        var output = default(PooledBuffer);
        try
        {
            rewritten = BrotliRewriter.Rewrite(dictionary, encoded, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Makes compressible sample data.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] Sample() => Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, Numbers).Select(static i => i.ToString(CultureInfo.InvariantCulture))));

    /// <summary>Compresses data with Brotli.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] data)
    {
        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        _ = BrotliEncoder.TryCompress(data, buffer, out var written, Quality, Window);
        return buffer.AsSpan(0, written).ToArray();
    }

    /// <summary>Decodes data through a filter chain.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="parms">The /DecodeParms value.</param>
    /// <param name="encoded">The encoded data.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Decode(PdfValue filters, PdfValue parms, byte[] encoded)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = PdfStreamDecoder.Apply(encoded, filters, parms, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] DecodeStream(PdfStream stream)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = PdfStreamDecoder.Decode(stream, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
