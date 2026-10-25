// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures compact image keys with caller-owned transfer identities.</summary>
public sealed class ImageKeyBenchmarks
{
    /// <summary>The caller-owned lookup table.</summary>
    private readonly Dictionary<ImageKey, int> _images = [];

    /// <summary>The cached transfer variant.</summary>
    private ImageKey _same;

    /// <summary>A distinct transfer with the same samples.</summary>
    private ImageKey _different;

    /// <summary>The stream without a transfer.</summary>
    private ImageKey _none;

    /// <summary>Creates keys and warms dictionary hashing outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var stream = new PdfStream(new PdfDictionary(null), []);
        var transfer = new PdfTransfer(new byte[PdfTransfer.Entries * PdfTransfer.Channels]);
        var other = new PdfTransfer(new byte[PdfTransfer.Entries * PdfTransfer.Channels]);
        _same = new(stream, 0, IccRendering.Default, transfer);
        _different = new(stream, 0, IccRendering.Default, other);
        _none = new(stream, 0);
        _images.Clear();
        _images.Add(_same, 1);
        _ = SameTransfer();
        _ = DifferentTransfer();
        _ = NoTransfer();
    }

    /// <summary>Looks up the retained transfer instance.</summary>
    /// <returns>Whether the image is cached.</returns>
    [Benchmark]
    public bool SameTransfer() => _images.ContainsKey(_same);

    /// <summary>Rejects a transfer instance with equal samples.</summary>
    /// <returns>Whether the image is cached.</returns>
    [Benchmark]
    public bool DifferentTransfer() => _images.ContainsKey(_different);

    /// <summary>Rejects the stream's unmodified variant.</summary>
    /// <returns>Whether the image is cached.</returns>
    [Benchmark]
    public bool NoTransfer() => _images.ContainsKey(_none);
}
