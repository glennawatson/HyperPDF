// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Decodes real JBIG2 pages from scanned court reports with the managed decoder: a Library of Congress page (symbol
/// dictionaries, refinement and aggregation, a refined text region and a generic region), a Google Books page (global
/// symbols and a text region) and an Internet Archive cover (one large generic region).
/// </summary>
public class HyperPdfJbig2Benchmarks
{
    /// <summary>The output rows, reused between runs.</summary>
    private byte[] _rows = [];

    /// <summary>The sample being decoded.</summary>
    private Jbig2Sample _current = Jbig2Samples.RealLibraryOfCongress;

    /// <summary>Gets or sets the sample decoded.</summary>
    [Params(nameof(Jbig2Samples.RealLibraryOfCongress), nameof(Jbig2Samples.RealGoogleBooks), nameof(Jbig2Samples.RealInternetArchiveGeneric))]
    public string Sample { get; set; } = nameof(Jbig2Samples.RealLibraryOfCongress);

    /// <summary>Picks the sample and allocates the output.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _current = Jbig2Samples.Get(Sample);
        _rows = new byte[Jbig2Decoder.GetRowBytes(_current.Width) * _current.Height];
    }

    /// <summary>Decodes the page into the reused rows.</summary>
    /// <returns><see langword="true"/> when the page decoded.</returns>
    [Benchmark]
    public bool Decode() => Jbig2Decoder.TryDecode(_current.Data, _current.Globals, _current.Width, _current.Height, _rows);
}
