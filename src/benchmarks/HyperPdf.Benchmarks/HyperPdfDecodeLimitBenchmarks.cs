// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdf.Benchmarks;

/// <summary>Measures bounded Flate, LZW and nested filter decoding, including bomb rejection.</summary>
public class HyperPdfDecodeLimitBenchmarks
{
    /// <summary>The strict limit for each filter output.</summary>
    private const int Limit = 4096;

    /// <summary>The ordinary decoded byte count.</summary>
    private const int SampleLength = 1024;

    /// <summary>The expanded byte count of the high-ratio payload.</summary>
    private const int BombLength = 1 << 23;

    /// <summary>The context carrying the strict byte limit.</summary>
    private readonly PdfOpenContext _context = PdfOpenContext.Create(new() { MaxDecompressedBytes = Limit });

    /// <summary>The normal compressed Flate bytes.</summary>
    private ReadOnlyMemory<byte> _flate;

    /// <summary>The normal compressed LZW bytes.</summary>
    private ReadOnlyMemory<byte> _lzw;

    /// <summary>The nested Flate and ASCII85 bytes.</summary>
    private ReadOnlyMemory<byte> _chain;

    /// <summary>The high-ratio compressed bomb.</summary>
    private ReadOnlyMemory<byte> _bomb;

    /// <summary>The single Flate declaration.</summary>
    private PdfValue _flateFilter;

    /// <summary>The single LZW declaration.</summary>
    private PdfValue _lzwFilter;

    /// <summary>The nested filter declaration.</summary>
    private PdfValue _chainFilters;

    /// <summary>Creates all compressed data and parses filter names outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sample = new byte[SampleLength];
        _flate = Compress(sample);
        _lzw = EncodeLzw(sample);
        _chain = Compress(Compress(EncodeAscii85(sample)));
        _bomb = CompressZeros();
        _flateFilter = Parse("/FlateDecode"u8);
        _lzwFilter = Parse("/LZWDecode"u8);
        _chainFilters = Parse("[/FlateDecode /FlateDecode /ASCII85Decode]"u8);
    }

    /// <summary>Decodes a normal Flate stream within the byte limit.</summary>
    /// <returns>The decoded byte count.</returns>
    [Benchmark]
    public int FlateWithinLimit() => Decode(_flate, _flateFilter);

    /// <summary>Decodes a normal LZW stream within the byte limit.</summary>
    /// <returns>The decoded byte count.</returns>
    [Benchmark]
    public int LzwWithinLimit() => Decode(_lzw, _lzwFilter);

    /// <summary>Decodes all three stages within the byte limit.</summary>
    /// <returns>The decoded byte count.</returns>
    [Benchmark]
    public int ChainWithinLimit() => Decode(_chain, _chainFilters);

    /// <summary>Rejects a high-ratio Flate payload before allocating its expanded output.</summary>
    /// <returns>-1 when the byte bound rejects the payload.</returns>
    [Benchmark]
    public int FlateBomb() => Decode(_bomb, _flateFilter);

    /// <summary>Creates a compressed expansion payload without storing all expanded bytes.</summary>
    /// <returns>The compressed payload.</returns>
    private static byte[] CompressZeros()
    {
        using var sink = new MemoryStream();
        using (var zlib = new ZLibStream(sink, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var chunk = new byte[Limit];
            for (var written = 0; written < BombLength; written += Limit)
            {
                zlib.Write(chunk);
            }
        }

        return sink.ToArray();
    }

    /// <summary>Creates one zlib stage outside the measurement.</summary>
    /// <param name="input">The bytes to encode.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(ReadOnlySpan<byte> input)
    {
        using var sink = new MemoryStream();
        using (var zlib = new ZLibStream(sink, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(input);
        }

        return sink.ToArray();
    }

    /// <summary>Creates a valid LZW payload outside measurement.</summary>
    /// <param name="input">The bytes to encode.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] EncodeLzw(ReadOnlySpan<byte> input)
    {
        var output = default(PooledBuffer);
        try
        {
            LzwEncoder.Encode(input, 1, ref output, CancellationToken.None);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Creates ASCII85 bytes for the final chain stage.</summary>
    /// <param name="input">The bytes to encode.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] EncodeAscii85(ReadOnlySpan<byte> input)
    {
        var output = default(PooledBuffer);
        try
        {
            Ascii85Filter.Encode(input, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Parses a filter declaration outside measurement.</summary>
    /// <param name="filters">The UTF-8 filter declaration.</param>
    /// <returns>The parsed value.</returns>
    private static PdfValue Parse(ReadOnlySpan<byte> filters) => new PdfParser(filters.ToArray(), 0, null, new()).ParseValue();

    /// <summary>Runs one decoder with the configured bound and returns its output buffers.</summary>
    /// <param name="input">The encoded bytes.</param>
    /// <param name="filters">The parsed filter declaration.</param>
    /// <returns>The decoded byte count, or -1 for a rejected payload.</returns>
    private int Decode(ReadOnlyMemory<byte> input, PdfValue filters)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = PdfStreamDecoder.Apply(input.Span, filters, default, new(_context, 0, CancellationToken.None), ref output);
            return output.Length;
        }
        catch (PdfException)
        {
            return -1;
        }
        finally
        {
            output.Dispose();
        }
    }
}
