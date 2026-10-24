// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures arbitrary PDF names that share their length and first and last bytes.</summary>
public class HyperPdfNameCollisionBenchmarks
{
    /// <summary>A small name table.</summary>
    private const int SmallCount = 128;

    /// <summary>A larger name table.</summary>
    private const int LargeCount = 1024;

    /// <summary>The spellings prepared before measurement.</summary>
    private byte[][] _spellings = [];

    /// <summary>The table used for warm lookups.</summary>
    private PdfNameTable _names = new();

    /// <summary>Gets or sets the number of distinct names.</summary>
    [Params(SmallCount, LargeCount)]
    public int NameCount { get; set; }

    /// <summary>Builds names whose distinguishing bytes lie between identical edges.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _spellings = new byte[NameCount][];
        _names = new();
        for (var i = 0; i < _spellings.Length; i++)
        {
            _spellings[i] = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Name{i:D4}Suffix"));
            _ = _names.Intern(_spellings[i]);
        }
    }

    /// <summary>Looks up names whose middle bytes distinguish them.</summary>
    /// <returns>A checksum of the identifiers.</returns>
    [Benchmark]
    public int Lookup()
    {
        var total = 0;
        foreach (var spelling in _spellings)
        {
            total += _names.Intern(spelling).Id;
        }

        return total;
    }

    /// <summary>Builds a fresh table from the distinct names.</summary>
    /// <returns>The resulting name count.</returns>
    [Benchmark]
    public int Insert()
    {
        var names = new PdfNameTable();
        foreach (var spelling in _spellings)
        {
            _ = names.Intern(spelling);
        }

        return names.Count;
    }
}
