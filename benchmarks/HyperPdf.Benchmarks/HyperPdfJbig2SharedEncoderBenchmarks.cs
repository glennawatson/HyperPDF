// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Encodes three real scanned pages (decoded from the JBIG2 samples) with one shared symbol dictionary. The result is the
/// globals array and one array per page, which the call must allocate; everything else is pooled.
/// </summary>
public class HyperPdfJbig2SharedEncoderBenchmarks
{
    /// <summary>The pages, ready for the encoder.</summary>
    private Jbig2PageInput[] _pages = [];

    /// <summary>The encoder settings.</summary>
    private Jbig2EncodeOptions _options = Jbig2EncodeOptions.Default;

    /// <summary>Gets or sets how the pages are coded.</summary>
    [Params(Jbig2EncodeMode.SymbolText, Jbig2EncodeMode.SymbolTextRefined, Jbig2EncodeMode.LossySymbolMerging)]
    public Jbig2EncodeMode Mode { get; set; } = Jbig2EncodeMode.SymbolText;

    /// <summary>Decodes the sample pages to the rows the encoder takes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var names = new[] { nameof(Jbig2Samples.RealLibraryOfCongress), nameof(Jbig2Samples.RealGoogleBooks), nameof(Jbig2Samples.RealInternetArchiveText) };
        _pages = new Jbig2PageInput[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var sample = Jbig2Samples.Get(names[i]);
            var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
            _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows);
            _pages[i] = new(rows, sample.Width, sample.Height);
        }

        _options = new Jbig2EncodeOptions { Mode = Mode };
    }

    /// <summary>Encodes the pages with a shared dictionary.</summary>
    /// <returns>The coded size in bytes.</returns>
    [Benchmark]
    public int EncodeShared()
    {
        var coded = Jbig2Encoder.EncodeShared(_pages, _options);
        var total = coded.Globals.Length;
        foreach (var page in coded.Pages)
        {
            total += page.Length;
        }

        return total;
    }
}
