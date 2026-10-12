// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the memory of parsed PDF objects: arrays of numbers and a dictionary of small arrays. The retained
/// PdfValue arrays dominate the bytes, so the EventPipe allocation audit shows the cost of one value slot.
/// </summary>
public class HyperPdfValueMemoryBenchmarks
{
    /// <summary>The number of numbers in the large array.</summary>
    private const int LargeArrayLength = 256;

    /// <summary>The number of entries in the dictionary of small arrays.</summary>
    private const int DictionaryEntries = 32;

    /// <summary>The number of numbers in each small array.</summary>
    private const int SmallArrayLength = 16;

    /// <summary>The name table the parser interns into.</summary>
    private readonly PdfNameTable _names = new();

    /// <summary>The source of the large array of integers and reals.</summary>
    private byte[] _largeArray = [];

    /// <summary>The source of the dictionary of small arrays.</summary>
    private byte[] _dictionary = [];

    /// <summary>Builds the array and dictionary sources.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _largeArray = Encoding.ASCII.GetBytes(BuildArray(LargeArrayLength));

        var text = new StringBuilder("<<");
        for (var i = 0; i < DictionaryEntries; i++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $" /Key{i} {BuildArray(SmallArrayLength)}");
        }

        _dictionary = Encoding.ASCII.GetBytes(text.Append(" >>").ToString());
    }

    /// <summary>Parses one large array of numbers and returns it, so its value storage is allocated.</summary>
    /// <returns>The parsed array.</returns>
    [Benchmark]
    public PdfArray ParseLargeArray()
    {
        var parser = new PdfParser(_largeArray, 0, null, _names);
        return parser.ParseValue().AsArray()!;
    }

    /// <summary>Parses a dictionary of small number arrays and returns it.</summary>
    /// <returns>The parsed dictionary.</returns>
    [Benchmark]
    public PdfDictionary ParseDictionaryOfArrays()
    {
        var parser = new PdfParser(_dictionary, 0, null, _names);
        return parser.ParseValue().AsDictionary()!;
    }

    /// <summary>Builds the text of an array of alternating integers and reals.</summary>
    /// <param name="length">The number of elements.</param>
    /// <returns>The array text.</returns>
    private static string BuildArray(int length)
    {
        var text = new StringBuilder("[");
        for (var i = 0; i < length; i++)
        {
            _ = (i & 1) == 0
                ? text.Append(CultureInfo.InvariantCulture, $" {i}")
                : text.Append(CultureInfo.InvariantCulture, $" {i}.5");
        }

        return text.Append(" ]").ToString();
    }
}
