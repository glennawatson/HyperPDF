// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Colors;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures a warm scalar colour-space row that reads the cached sample decode range.</summary>
public class HyperPdfSampleDecodeBenchmarks
{
    /// <summary>A mid-range sample for each Lab component.</summary>
    private const byte MidSample = 128;

    /// <summary>The one-pixel Lab sample.</summary>
    private readonly byte[] _samples = [MidSample, MidSample, MidSample];

    /// <summary>The converted BGRA pixel.</summary>
    private readonly byte[] _bgra = new byte[PixelConverter.BytesPerPixel];

    /// <summary>The parsed colour space.</summary>
    private PdfColorSpace _space = null!;

    /// <summary>Parses and warms the colour conversion before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _space = LabColorSpace.Parse(null);
        _space.ConvertRow(_samples, _bgra, 1);
    }

    /// <summary>Converts one Lab pixel after the sample range is cached.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte ConvertOneLabPixel()
    {
        _space.ConvertRow(_samples, _bgra, 1);
        return _bgra[0];
    }
}
