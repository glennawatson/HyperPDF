// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading keys from a parsed dictionary, for dictionaries of a few, several and many entries.</summary>
public class HyperPdfDictionaryLookupBenchmarks
{
    /// <summary>The keys every dictionary is built from, in order.</summary>
    private static readonly KnownName[] Keys =
    [
        KnownName.Type, KnownName.Subtype, KnownName.Parent, KnownName.Kids, KnownName.Count, KnownName.Contents,
        KnownName.Resources, KnownName.MediaBox, KnownName.Length, KnownName.Filter, KnownName.Font, KnownName.Rotate,
        KnownName.CropBox, KnownName.Annots, KnownName.Name, KnownName.Width,
    ];

    /// <summary>The dictionary.</summary>
    private PdfDictionary _dictionary = new(null);

    /// <summary>Gets or sets the number of entries in the dictionary.</summary>
    [Params(4, 8, 16)]
    public int Entries { get; set; }

    /// <summary>Builds the dictionary.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _dictionary = new(null, Entries);
        for (var i = 0; i < Entries; i++)
        {
            _dictionary.Add(Keys[i], PdfValue.FromInteger(i));
        }
    }

    /// <summary>Reads every key once, each a hit.</summary>
    /// <returns>The sum of the values.</returns>
    [Benchmark]
    public long ReadEveryKey()
    {
        var sum = 0L;
        for (var i = 0; i < Entries; i++)
        {
            sum += _dictionary.GetInteger(Keys[i]);
        }

        return sum;
    }

    /// <summary>Reads a key the dictionary does not have.</summary>
    /// <returns>The value, which is zero.</returns>
    [Benchmark]
    public long ReadMissingKey() => _dictionary.GetInteger(KnownName.Height);
}
