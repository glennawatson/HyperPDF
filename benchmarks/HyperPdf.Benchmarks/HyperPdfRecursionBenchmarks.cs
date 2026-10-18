// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdf.Benchmarks;

/// <summary>Measures parsing within and beyond the PDF container nesting limit.</summary>
public class HyperPdfRecursionBenchmarks
{
    /// <summary>The container depth of the ordinary input.</summary>
    private const int ShallowDepth = 4;

    /// <summary>The container depth of the hostile input.</summary>
    private const int HostileDepth = 10_000;

    /// <summary>The shared name table, which the parsed values do not modify.</summary>
    private readonly PdfNameTable _names = new();

    /// <summary>A shallow array tree with a following dictionary entry.</summary>
    private byte[] _shallow = [];

    /// <summary>A ten thousand-deep array tree with a following entry.</summary>
    private byte[] _arrays = [];

    /// <summary>A ten thousand-deep dictionary tree with a following entry.</summary>
    private byte[] _dictionaries = [];

    /// <summary>Creates the raw syntax outside the measured parsing operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _shallow = Nested(ShallowDepth, false);
        _arrays = Nested(HostileDepth, false);
        _dictionaries = Nested(HostileDepth, true);
    }

    /// <summary>Parses ordinary nesting.</summary>
    /// <returns>The trailing count.</returns>
    [Benchmark]
    public long ParseShallowContainers() => Parse(_shallow);

    /// <summary>Parses and bounds an over-deep array tree.</summary>
    /// <returns>The trailing count.</returns>
    [Benchmark]
    public long ParseDeepArrays() => Parse(_arrays);

    /// <summary>Parses and bounds an over-deep dictionary tree.</summary>
    /// <returns>The trailing count.</returns>
    [Benchmark]
    public long ParseDeepDictionaries() => Parse(_dictionaries);

    /// <summary>Creates nested containers inside a dictionary.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="dictionary">Whether to use dictionaries instead of arrays.</param>
    /// <returns>The syntax bytes.</returns>
    private static byte[] Nested(int depth, bool dictionary)
    {
        var text = new StringBuilder("<< /A ");
        for (var level = 0; level < depth; level++)
        {
            _ = text.Append(dictionary ? "<< /A " : "[");
        }

        _ = text.Append('1');
        for (var level = 0; level < depth; level++)
        {
            _ = text.Append(dictionary ? " >>" : "]");
        }

        return Encoding.UTF8.GetBytes(text.Append(" /Count 1 >>").ToString());
    }

    /// <summary>Parses one raw dictionary.</summary>
    /// <param name="bytes">The syntax.</param>
    /// <returns>The trailing count.</returns>
    private long Parse(byte[] bytes)
    {
        var parser = new PdfParser(bytes, 0, null, _names);
        return parser.ParseValue().AsDictionary()!.GetInteger(KnownName.Count);
    }
}
