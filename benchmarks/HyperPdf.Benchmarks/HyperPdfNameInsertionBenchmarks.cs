// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Loggers;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures constructor and insertion costs for serialized document-name writers.</summary>
public class HyperPdfNameInsertionBenchmarks
{
    /// <summary>The initial capacity also used by spelling storage.</summary>
    private const int InitialCapacity = 16;

    /// <summary>The shared spelling inputs, prepared before timing.</summary>
    private byte[][] _spellings = [];

    /// <summary>Gets or sets the number of inserted names, including constructor-only operation.</summary>
    [Params(0, 2, 128)]
    public int NameCount { get; set; }

    /// <summary>Creates the input names outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _spellings = new byte[NameCount][];
        for (var i = 0; i < _spellings.Length; i++)
        {
            _spellings[i] = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"DocumentName{i}"));
        }

        _ = DefaultConcurrency();
        _ = SingleWriter();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var defaultDictionary = DefaultConcurrency();
        var defaultBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(defaultDictionary);
        before = GC.GetAllocatedBytesForCurrentThread();
        var singleDictionary = SingleWriter();
        var singleBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(singleDictionary);
        ConsoleLogger.Default.WriteLine(LogKind.Info, string.Create(
            CultureInfo.InvariantCulture,
            $"Concurrent name dictionary: {NameCount} inserts, {Environment.ProcessorCount} processors, "
            + $"default {defaultBytes} allocated bytes, single writer {singleBytes} allocated bytes."));
    }

    /// <summary>Creates the dictionary with its default processor-count concurrency and inserts each name.</summary>
    /// <returns>The resulting dictionary.</returns>
    [Benchmark(Baseline = true)]
    public object DefaultConcurrency() => Insert(new(ByteArrayComparer.Instance));

    /// <summary>Creates one writer stripe with explicit capacity and inserts each name.</summary>
    /// <returns>The resulting dictionary.</returns>
    [Benchmark]
    public object SingleWriter() => Insert(new(1, InitialCapacity, ByteArrayComparer.Instance));

    /// <summary>Runs the same insertions for both constructor configurations.</summary>
    /// <param name="dictionary">The new dictionary.</param>
    /// <returns>The populated dictionary.</returns>
    private ConcurrentDictionary<byte[], int> Insert(ConcurrentDictionary<byte[], int> dictionary)
    {
        for (var i = 0; i < _spellings.Length; i++)
        {
            _ = dictionary.TryAdd(_spellings[i], i);
        }

        return dictionary;
    }
}
