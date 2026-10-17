// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Measures caller-owned rentals with runtime-derived element size and reference clearing.</summary>
public sealed class ScratchPoolBenchmarks
{
    /// <summary>The request served by the runtime's small-buffer pool.</summary>
    private const int SmallLength = 1024;

    /// <summary>The request retained within the caller's large-buffer budget.</summary>
    private const int LargeLength = 128 * 1024;

    /// <summary>The explicit rental owner.</summary>
    private readonly ScratchPools _owner = new();

    /// <summary>The caller's byte pool.</summary>
    private ScratchPool<byte> _bytes = null!;

    /// <summary>The caller's integer pool.</summary>
    private ScratchPool<int> _integers = null!;

    /// <summary>The caller's reference-element pool.</summary>
    private ScratchPool<string> _references = null!;

    /// <summary>Warms every rental path before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _bytes = _owner.Bytes;
        _integers = _owner.Ints;
        _references = _owner.Get<string>();
        _ = SmallBytes();
        _ = LargeBytes();
        _ = SmallIntegers();
        _ = References();
    }

    /// <summary>Releases the owner's retained arrays.</summary>
    [GlobalCleanup]
    public void Cleanup() => _owner.Close();

    /// <summary>Rents and returns a small byte array.</summary>
    /// <returns>The rental's length.</returns>
    [Benchmark]
    public int SmallBytes() => RentReturn(_bytes, SmallLength);

    /// <summary>Rents and returns an owner-retained large byte array.</summary>
    /// <returns>The rental's length.</returns>
    [Benchmark]
    public int LargeBytes() => RentReturn(_bytes, LargeLength);

    /// <summary>Rents and returns small integer storage.</summary>
    /// <returns>The rental's length.</returns>
    [Benchmark]
    public int SmallIntegers() => RentReturn(_integers, SmallLength);

    /// <summary>Rents and returns reference storage with explicit clearing.</summary>
    /// <returns>The rental's length.</returns>
    [Benchmark]
    public int References()
    {
        var array = _references.Rent(SmallLength);
        array[0] = "sample";
        var length = array.Length;
        _references.Return(array, true);
        return length;
    }

    /// <summary>Completes a rental without retaining the array in benchmark state.</summary>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <param name="pool">The caller's pool.</param>
    /// <param name="requested">The minimum elements.</param>
    /// <returns>The returned array's length.</returns>
    private static int RentReturn<T>(ScratchPool<T> pool, int requested)
    {
        var array = pool.Rent(requested);
        var length = array.Length;
        pool.Return(array);
        return length;
    }
}
