// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;

namespace HyperPdf.Benchmarks;

/// <summary>Measures cache table allocation across source lifetimes and cache budgets.</summary>
public class HyperPdfStreamCacheBenchmarks
{
    /// <summary>The page count of the larger cache.</summary>
    private const int LargePages = 8;

    /// <summary>The caller-owned input shared by consecutive source lifetimes.</summary>
    private MemoryStream _stream = null!;

    /// <summary>The read destination shared between calls.</summary>
    private byte[] _buffer = [];

    /// <summary>The host's pool, or null when each source owns its tables.</summary>
    private ScratchPools? _pools;

    /// <summary>Gets or sets whether the sources share a host's pool.</summary>
    [Params(false, true)]
    public bool UseHostPool { get; set; }

    /// <summary>Creates the input and destination outside the measured source lifetimes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _stream = new(new byte[1], false);
        _buffer = new byte[1];
        _pools = UseHostPool ? new() : null;
    }

    /// <summary>Closes the input and releases any tables retained by the host.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _stream.Dispose();
        _pools?.Close();
    }

    /// <summary>Opens and reads sources with equal cache budgets.</summary>
    /// <returns>The bytes read.</returns>
    [Benchmark(Baseline = true)]
    public int EqualBudgets()
    {
        using var first = new StreamPdfByteSource(_stream, StreamPdfByteSource.PageSize, false, _pools);
        using var second = new StreamPdfByteSource(_stream, StreamPdfByteSource.PageSize, false, _pools);
        return first.Read(0, _buffer) + second.Read(0, _buffer);
    }

    /// <summary>Opens and reads sources with different cache budgets.</summary>
    /// <returns>The bytes read.</returns>
    [Benchmark]
    public int MixedBudgets()
    {
        using var first = new StreamPdfByteSource(_stream, StreamPdfByteSource.PageSize, false, _pools);
        using var second = new StreamPdfByteSource(_stream, (long)StreamPdfByteSource.PageSize * LargePages, false, _pools);
        return first.Read(0, _buffer) + second.Read(0, _buffer);
    }
}
