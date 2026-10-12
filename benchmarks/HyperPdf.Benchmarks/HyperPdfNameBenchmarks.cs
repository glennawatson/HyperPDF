// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdf.Benchmarks;

/// <summary>Measures known-name lookup, repeated unknown-name interning and dictionary parsing.</summary>
public class HyperPdfNameBenchmarks
{
    /// <summary>The document-specific names in the lookup batch.</summary>
    private const int CustomNames = 128;

    /// <summary>A dictionary mixing known keys and values with document-specific names.</summary>
    private static readonly byte[] Dictionary =
        "<< /Type /Catalog /Pages 1 0 R /Lang (en) /PageMode /UseOutlines /CustomKey /CustomValue /ViewerPreferences << /HideToolbar true /Direction /L2R >> >>"u8.ToArray();

    /// <summary>Known spellings copied outside the measured workload.</summary>
    private byte[][] _known = [];

    /// <summary>Document-specific spellings copied outside the measured workload.</summary>
    private byte[][] _custom = [];

    /// <summary>The table shared by repeated lookups and parses.</summary>
    private PdfNameTable _names = new();

    /// <summary>Warms the name table before measuring repeated lookups.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var known = Enum.GetValues<KnownName>();
        _known = new byte[known.Length - 1][];
        for (var i = 1; i < known.Length; i++)
        {
            _known[i - 1] = PdfNameTable.GetKnownSpelling(known[i]).ToArray();
        }

        _custom = new byte[CustomNames][];
        _names = new();
        for (var i = 0; i < _custom.Length; i++)
        {
            _custom[i] = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"DocumentSpecificName{i}"));
            _ = _names.Intern(_custom[i]);
        }

        _ = ParseDictionary();
    }

    /// <summary>Looks up every known spelling once without allocating.</summary>
    /// <returns>The sum of the known ids.</returns>
    [Benchmark]
    public int LookupKnown()
    {
        var total = 0;
        foreach (var spelling in _known)
        {
            _ = PdfNameTable.TryGetKnown(spelling, out var name);
            total += name.Id;
        }

        return total;
    }

    /// <summary>Looks up already interned unknown names from UTF-8 bytes.</summary>
    /// <returns>The sum of the document-specific ids.</returns>
    [Benchmark]
    public int InternExisting()
    {
        var total = 0;
        foreach (var spelling in _custom)
        {
            total += _names.Intern(spelling).Id;
        }

        return total;
    }

    /// <summary>Parses a dictionary that repeatedly interns known and document-specific names.</summary>
    /// <returns>The number of dictionary entries.</returns>
    [Benchmark]
    public int ParseDictionary()
    {
        var parser = new PdfParser(Dictionary, 0, null, _names);
        return parser.ParseValue().AsDictionary()!.Count;
    }

    /// <summary>Parses the same dictionary with a fresh table, including new document-specific name insertion.</summary>
    /// <returns>The number of dictionary entries.</returns>
    [Benchmark]
    public int ParseDictionaryNewNames()
    {
        var names = new PdfNameTable();
        var parser = new PdfParser(Dictionary, 0, null, names);
        return parser.ParseValue().AsDictionary()!.Count;
    }
}
