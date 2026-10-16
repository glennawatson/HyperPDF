// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks strict output bounds for compressed streams and every filter in a chain.</summary>
public sealed class DecodeLimitTests
{
    /// <summary>A small configured limit, below the large-object heap threshold.</summary>
    private const int Limit = 4096;

    /// <summary>A non-pool-bucket limit exposes unused capacity beyond the logical bound.</summary>
    private const int UnalignedLimit = 257;

    /// <summary>The payload length for a Flate stream with at least 1000:1 expansion.</summary>
    private const int BombLength = 1 << 23;

    /// <summary>The chain's expanded bytes; its compressed form fits below the stage limit.</summary>
    private const int ChainBombLength = 1 << 21;

    /// <summary>The zero chunk reused while generating compressed data.</summary>
    private const int ChunkLength = 8192;

    /// <summary>The minimum expansion ratio of the Flate bomb.</summary>
    private const int BombRatio = 1000;

    /// <summary>A ceiling allowing decoder tables and exceptions but no large byte array.</summary>
    private const int MaximumDecodeAllocation = 1 << 16;

    /// <summary>The synthetic document page size.</summary>
    private const int PageSize = 100;

    /// <summary>A high-ratio Flate stream fails before the decoded result is materialised.</summary>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlateBombThrowsBeforeLargeOutputBuffer(CancellationToken testToken)
    {
        ReadOnlyMemory<byte> compressed = CompressZeros(BombLength, testToken);
        await Assert.That(BombLength / compressed.Length).IsGreaterThanOrEqualTo(BombRatio);
        await Assert.That(() => Decode(compressed, "/FlateDecode"u8.ToArray(), Limit, testToken)).Throws<PdfException>();
    }

    /// <summary>LZW rejects an output byte beyond a non-aligned limit.</summary>
    /// <param name="earlyChange">The LZW code-width convention.</param>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task LzwExpansionCannotUsePoolSlack(int earlyChange, CancellationToken testToken)
    {
        ReadOnlyMemory<byte> encoded = EncodeLzw(new byte[Limit], earlyChange, testToken);
        var parameters = new PdfDictionary(null);
        parameters.Set(KnownName.EarlyChange, PdfValue.FromInteger(earlyChange));
        await Assert.That(() => Decode(encoded, "/LZWDecode"u8.ToArray(), UnalignedLimit, PdfValue.FromDictionary(parameters), testToken)).Throws<PdfException>();
    }

    /// <summary>The second Flate stage cannot bypass the bound using the previous stage's rented buffer.</summary>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedFlateAscii85BombThrowsAtIntermediateStage(CancellationToken testToken)
    {
        var intermediate = CompressRun(ChainBombLength, (byte)'z', testToken);
        await Assert.That(intermediate.Length).IsLessThan(Limit);
        ReadOnlyMemory<byte> compressed = Compress(intermediate, testToken);
        await Assert.That(() => Decode(compressed, "[/FlateDecode /FlateDecode /ASCII85Decode]"u8.ToArray(), Limit, testToken)).Throws<PdfException>();
    }

    /// <summary>A final ASCII85 stage is bounded even when both Flate stages fit.</summary>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinalAscii85ExpansionThrows(CancellationToken testToken)
    {
        var ascii = new byte[Limit];
        ascii.AsSpan().Fill((byte)'z');
        ReadOnlyMemory<byte> compressed = Compress(Compress(ascii, testToken), testToken);
        await Assert.That(() => Decode(compressed, "[/FlateDecode /FlateDecode /ASCII85Decode]"u8.ToArray(), Limit, testToken)).Throws<PdfException>();
    }

    /// <summary>Exactly the configured number of decoded bytes is accepted.</summary>
    /// <param name="length">The byte limit, including a non-aligned pool bucket.</param>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Limit)]
    [Arguments(UnalignedLimit)]
    public async Task FlateAtExactLimitSucceeds(int length, CancellationToken testToken)
    {
        var decoded = Decode(CompressZeros(length, testToken), "/FlateDecode"u8.ToArray(), length, testToken);
        await Assert.That(decoded).IsEqualTo(length);
    }

    /// <summary>Each LZW convention accepts an output exactly at the configured limit.</summary>
    /// <param name="earlyChange">The LZW code-width convention.</param>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task LzwAtExactLimitSucceeds(int earlyChange, CancellationToken testToken)
    {
        var parameters = new PdfDictionary(null);
        parameters.Set(KnownName.EarlyChange, PdfValue.FromInteger(earlyChange));
        var decoded = Decode(
            EncodeLzw(new byte[UnalignedLimit], earlyChange, testToken),
            "/LZWDecode"u8.ToArray(),
            UnalignedLimit,
            PdfValue.FromDictionary(parameters),
            testToken);

        await Assert.That(decoded).IsEqualTo(UnalignedLimit);
    }

    /// <summary>An ASCII85 partial group needs only the bytes it actually produces.</summary>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85PartialGroupAtExactLimitSucceeds(CancellationToken testToken)
    {
        var decoded = Decode("!!~>"u8.ToArray(), "/ASCII85Decode"u8.ToArray(), 1, testToken);

        await Assert.That(decoded).IsEqualTo(1);
    }

    /// <summary>Unfiltered and unknown-filter streams cannot copy more than the output limit.</summary>
    /// <param name="filters">The stream's filter declaration.</param>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("null")]
    [Arguments("/UnknownFilter")]
    [Arguments("/Crypt")]
    public async Task PassthroughRespectsOutputLimit(string filters, CancellationToken testToken)
    {
        ReadOnlyMemory<byte> input = new byte[Limit];

        await Assert.That(() => Decode(
            input,
            System.Text.Encoding.UTF8.GetBytes(filters),
            UnalignedLimit,
            testToken)).Throws<PdfException>();
    }

    /// <summary>A Brotli stage follows the same logical bound as the dictionary decoders.</summary>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrotliExpansionRespectsOutputLimit(CancellationToken testToken)
    {
        var input = CompressBrotli(new byte[Limit], testToken);

        await Assert.That(() => Decode(input, "/BrotliDecode"u8.ToArray(), UnalignedLimit, testToken)).Throws<PdfException>();
    }

    /// <summary>An exact-sized Brotli stage can finish without reserving another output byte.</summary>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrotliAtExactLimitSucceeds(CancellationToken testToken)
    {
        var decoded = Decode(CompressBrotli(new byte[UnalignedLimit], testToken), "/BrotliDecode"u8.ToArray(), UnalignedLimit, testToken);

        await Assert.That(decoded).IsEqualTo(UnalignedLimit);
    }

    /// <summary>The public array reader rejects a bomb instead of copying partial decoded bytes.</summary>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PublicStreamReaderRejectsBomb(CancellationToken testToken)
    {
        ReadOnlyMemory<byte> payload = CompressZeros(BombLength, testToken);
        var pdf = new RenderTestPdf(PageSize, PageSize);
        var streamNumber = pdf.AddStream("/Filter /FlateDecode", payload.ToArray());
        pdf.CatalogEntries = FormattableString.Invariant($"/Metadata {streamNumber} 0 R");
        using var document = PdfDocumentReader.OpenWith(pdf.ToBytes(), new() { MaxDecompressedBytes = Limit, CancellationToken = testToken });
        var stream = document.Catalog.GetStream(KnownName.Metadata)!;
        await Assert.That(() => stream.DecodeToArray(testToken)).Throws<PdfException>();
    }

    /// <summary>Runs the stream reader while checking pool capacity on failure before disposing it.</summary>
    /// <param name="input">The synthetic encoded bytes.</param>
    /// <param name="filters">The filter declaration.</param>
    /// <param name="limit">The output limit.</param>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>The decoded length.</returns>
    private static int Decode(ReadOnlyMemory<byte> input, byte[] filters, int limit, CancellationToken testToken) =>
        Decode(input, filters, limit, default, testToken);

    /// <summary>Runs the stream reader with filter parameters.</summary>
    /// <param name="input">The synthetic encoded bytes.</param>
    /// <param name="filters">The filter declaration.</param>
    /// <param name="limit">The output limit.</param>
    /// <param name="parameters">The decoding parameters.</param>
    /// <param name="testToken">Cancels decoding.</param>
    /// <returns>The decoded length.</returns>
    /// <exception cref="PdfException">The decoded bytes exceed the configured bound.</exception>
    /// <exception cref="InvalidOperationException">The decoder allocates or writes beyond the bounded output.</exception>
    private static int Decode(ReadOnlyMemory<byte> input, byte[] filters, int limit, PdfValue parameters, CancellationToken testToken)
    {
        var context = PdfOpenContext.Create(new() { MaxDecompressedBytes = limit }, testToken);
        var filter = new PdfParser(filters, 0, null, new()).ParseValue();
        var output = default(PooledBuffer);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            _ = PdfStreamDecoder.Apply(input.Span, filter, parameters, new(context, 0, testToken), ref output);
            return output.Length;
        }
        catch (PdfException)
        {
            if (output.Length > limit || output.Array is { Length: > Limit } || GC.GetAllocatedBytesForCurrentThread() - allocated > MaximumDecodeAllocation)
            {
                throw new InvalidOperationException("Decoding exceeded the bounded output buffer before rejecting the payload.");
            }

            throw;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Compresses zero chunks without allocating the expanded payload.</summary>
    /// <param name="length">The decoded byte count.</param>
    /// <param name="testToken">Cancels fixture generation.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] CompressZeros(int length, CancellationToken testToken) => CompressRun(length, 0, testToken);

    /// <summary>Compresses repeated bytes without allocating the expanded payload.</summary>
    /// <param name="length">The decoded byte count.</param>
    /// <param name="value">The repeated byte.</param>
    /// <param name="testToken">Cancels fixture generation.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] CompressRun(int length, byte value, CancellationToken testToken)
    {
        using var sink = new MemoryStream();
        using (var zlib = new ZLibStream(sink, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var chunk = new byte[ChunkLength];
            chunk.AsSpan().Fill(value);
            for (var written = 0; written < length; written += ChunkLength)
            {
                testToken.ThrowIfCancellationRequested();
                zlib.Write(chunk.AsSpan(0, Math.Min(ChunkLength, length - written)));
            }
        }

        return sink.ToArray();
    }

    /// <summary>Compresses a stage of a synthetic chain.</summary>
    /// <param name="input">The bytes to compress.</param>
    /// <param name="testToken">Cancels fixture generation.</param>
    /// <returns>The zlib bytes.</returns>
    private static byte[] Compress(ReadOnlySpan<byte> input, CancellationToken testToken)
    {
        testToken.ThrowIfCancellationRequested();
        using var sink = new MemoryStream();
        using (var zlib = new ZLibStream(sink, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(input);
        }

        return sink.ToArray();
    }

    /// <summary>Creates a Brotli stage outside the decoder measurement.</summary>
    /// <param name="input">The bytes to compress.</param>
    /// <param name="testToken">Cancels fixture generation.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] CompressBrotli(ReadOnlySpan<byte> input, CancellationToken testToken)
    {
        testToken.ThrowIfCancellationRequested();
        using var sink = new MemoryStream();
        using (var brotli = new BrotliStream(sink, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(input);
        }

        return sink.ToArray();
    }

    /// <summary>Creates an LZW expansion fixture with a valid end code.</summary>
    /// <param name="input">The uncompressed zeros.</param>
    /// <param name="earlyChange">The code-width convention.</param>
    /// <param name="testToken">Cancels encoding.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] EncodeLzw(byte[] input, int earlyChange, CancellationToken testToken)
    {
        var output = default(PooledBuffer);
        try
        {
            LzwEncoder.Encode(input, earlyChange, ref output, testToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
