// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures caller-owned JBIG2 selection and retained output boundaries.</summary>
public class BilevelJbig2Benchmarks
{
    /// <summary>The synthetic page width.</summary>
    private const int Width = 64;

    /// <summary>The synthetic page height.</summary>
    private const int Height = 8;

    /// <summary>The bits in a packed byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The caller's reusable encoding storage.</summary>
    private readonly ScratchPools _pools = new();

    /// <summary>The packed sample rows.</summary>
    private readonly byte[] _rows = new byte[Width * Height / ByteBits];

    /// <summary>Creates a white page without external fixtures.</summary>
    [GlobalSetup]
    public void Setup() => _rows.AsSpan().Fill(byte.MaxValue);

    /// <summary>Rejects a byte budget before doing any encoding work.</summary>
    /// <returns>No candidate.</returns>
    [Benchmark]
    public Jbig2EncodedImage? ZeroBudget() =>
        BilevelJbig2.Choose(_rows, Width, Height, PdfJbig2Mode.Lossless, 0, _pools, CancellationToken.None);

    /// <summary>Discards an encoded candidate without copying its pooled output.</summary>
    /// <returns>No candidate.</returns>
    [Benchmark]
    public Jbig2EncodedImage? RejectedLossless() =>
        BilevelJbig2.Choose(_rows, Width, Height, PdfJbig2Mode.Lossless, 1, _pools, CancellationToken.None);

    /// <summary>Returns detached bytes for the accepted coding.</summary>
    /// <returns>The retained coding snapshot.</returns>
    [Benchmark]
    public Jbig2EncodedImage? AcceptedLossless() =>
        BilevelJbig2.Choose(_rows, Width, Height, PdfJbig2Mode.Lossless, long.MaxValue, _pools, CancellationToken.None);

    /// <summary>Releases the caller's retained encoding storage.</summary>
    [GlobalCleanup]
    public void Cleanup() => _pools.Close();
}
