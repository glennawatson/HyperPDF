// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures HyperPDF's byte-level hot paths: tokenising and reading a content stream, parsing a dictionary-heavy object
/// and decoding Flate data. The Flate decode uses the span-based ZLibDecoder on .NET 11 and ZLibStream on .NET 10, so
/// running on both runtimes compares the two.
/// </summary>
public class HyperPdfSyntaxBenchmarks
{
    /// <summary>The lines in the sample content stream.</summary>
    private const int ContentLines = 4000;

    /// <summary>The x positions the sample lines cycle through.</summary>
    private const int LineWrap = 500;

    /// <summary>The entries in the sample dictionary.</summary>
    private const int DictionaryEntries = 40;

    /// <summary>The Brotli quality of the sample.</summary>
    private const int BrotliQuality = 5;

    /// <summary>The Brotli window of the sample, as a power of two.</summary>
    private const int BrotliWindow = 22;

    /// <summary>The sample content stream.</summary>
    private byte[] _content = [];

    /// <summary>The sample content stream, Flate compressed.</summary>
    private byte[] _compressed = [];

    /// <summary>The document that owns reusable Flate decoders.</summary>
    private PdfDocument? _ownedDocument;

    /// <summary>A Flate stream with the document as its owner.</summary>
    private PdfStream? _ownedFlate;

    /// <summary>The sample content stream, Brotli compressed.</summary>
    private byte[] _brotli = [];

    /// <summary>The sample dictionary.</summary>
    private byte[] _dictionary = [];

    /// <summary>The name table operand names are interned in.</summary>
    private PdfNameTable _names = new();

    /// <summary>Builds the samples.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder();
        for (var i = 0; i < ContentLines; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"BT /F1 12 Tf {i % LineWrap} 700 Td [(Hello) -250 (world {i})] TJ ET 0.5 0 0 rg {i} 10 20 30 re f\n");
        }

        _content = Encoding.ASCII.GetBytes(content.ToString());
        _compressed = Compress(_content);
        _ownedDocument = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var streamDictionary = new PdfDictionary(_ownedDocument.Objects);
        streamDictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        _ownedFlate = new(streamDictionary, _compressed);
        _brotli = CompressBrotli(_content);
        var dictionary = new StringBuilder("<<");
        for (var i = 0; i < DictionaryEntries; i++)
        {
            _ = dictionary.Append(CultureInfo.InvariantCulture, $" /Key{i} [{i} 0 R {i}.5 (text {i}) /Name{i}]");
        }

        _dictionary = Encoding.ASCII.GetBytes(dictionary.Append(" >>").ToString());
        _names = new();
    }

    /// <summary>Releases the document and its idle decoders.</summary>
    [GlobalCleanup]
    public void Cleanup() => _ownedDocument?.Dispose();

    /// <summary>Reads every operator and its operands from a content stream.</summary>
    /// <returns>The operator count.</returns>
    [Benchmark]
    public int ReadContentStream()
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(_content, _names, operands);
        var count = 0;
        while (reader.Next(out _))
        {
            count++;
        }

        return count;
    }

    /// <summary>Parses a dictionary holding arrays, references, reals, strings and names.</summary>
    /// <returns>The entry count.</returns>
    [Benchmark]
    public int ParseDictionary()
    {
        var parser = new PdfParser(_dictionary, 0, null, _names);
        return parser.ParseValue().AsDictionary()!.Count;
    }

    /// <summary>Decodes Flate-compressed content into a pooled buffer.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int DecodeFlate()
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Decode(_compressed, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes a Flate stream using the document's independently leased decoder state.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int DecodeOwnedFlate()
    {
        var output = default(PooledBuffer);
        try
        {
            _ = _ownedFlate!.Decode(ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes the same content Brotli compressed, for comparison with <see cref="DecodeFlate"/>.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int DecodeBrotli()
    {
        var output = default(PooledBuffer);
        try
        {
            BrotliFilter.Decode(_brotli, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Compresses data with Brotli at a typical writer's settings.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] CompressBrotli(byte[] data)
    {
        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        _ = BrotliEncoder.TryCompress(data, buffer, out var written, BrotliQuality, BrotliWindow);
        return buffer.AsSpan(0, written).ToArray();
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
}
