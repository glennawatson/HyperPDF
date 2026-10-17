// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Encodes real scanned pages (decoded from the JBIG2 samples) with the managed JBIG2 encoder, once for each way of coding:
/// a generic region, MMR, and a symbol dictionary with a text region, lossless and lossy. The output goes to reused
/// pooled buffers, so the measured allocations are the encoder's own.
/// </summary>
public class HyperPdfJbig2EncoderBenchmarks
{
    /// <summary>The caller's reusable encoding scratch.</summary>
    private readonly ScratchPools _pools = new();

    /// <summary>The page's samples, 0 for black.</summary>
    private byte[] _rows = [];

    /// <summary>The page width.</summary>
    private int _width;

    /// <summary>The page height.</summary>
    private int _height;

    /// <summary>The encoder settings.</summary>
    private Jbig2EncodeOptions _options = Jbig2EncodeOptions.Default;

    /// <summary>Gets or sets the page encoded.</summary>
    [Params(nameof(Jbig2Samples.RealLibraryOfCongress), nameof(Jbig2Samples.RealGoogleBooks))]
    public string Sample { get; set; } = nameof(Jbig2Samples.RealLibraryOfCongress);

    /// <summary>Gets or sets how the page is coded.</summary>
    [Params(Jbig2EncodeMode.GenericRegion, Jbig2EncodeMode.GenericRegionMmr, Jbig2EncodeMode.SymbolText, Jbig2EncodeMode.SymbolTextRefined, Jbig2EncodeMode.LossySymbolMerging)]
    public Jbig2EncodeMode Mode { get; set; } = Jbig2EncodeMode.GenericRegion;

    /// <summary>Decodes the sample page to the rows the encoder takes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sample = Jbig2Samples.Get(Sample);
        _width = sample.Width;
        _height = sample.Height;
        _rows = new byte[Jbig2Decoder.GetRowBytes(_width) * _height];
        _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, _width, _height, _rows);
        _options = new Jbig2EncodeOptions { Mode = Mode };
    }

    /// <summary>Encodes the page into pooled buffers.</summary>
    /// <returns>The coded size in bytes.</returns>
    [Benchmark]
    public int Encode()
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            _ = Jbig2Encoder.EncodeInto(_rows, new(new(_width, _height), _options), ref data, ref globals, _pools, CancellationToken.None);
            return data.Length + globals.Length;
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Releases the encoder's retained scratch.</summary>
    [GlobalCleanup]
    public void Cleanup() => _pools.Close();
}
