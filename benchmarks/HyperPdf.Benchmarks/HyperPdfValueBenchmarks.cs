// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Loggers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdf.Benchmarks;

/// <summary>Measures mixed-kind dispatch and parsing through the compact PDF value container.</summary>
public class HyperPdfValueBenchmarks
{
    /// <summary>The number of mixed values read in each dispatch pass.</summary>
    private const int ValueCount = 1024;

    /// <summary>The number of entries in the parsed dictionary.</summary>
    private const int DictionaryEntries = 40;

    /// <summary>The integer payload in the dispatch sample.</summary>
    private const int SampleInteger = 17;

    /// <summary>The real payload in the dispatch sample.</summary>
    private const double SampleReal = 17.5;

    /// <summary>The generation in the dispatch sample reference.</summary>
    private const int SampleGeneration = 2;

    /// <summary>The mixed values used to exercise kind dispatch.</summary>
    private PdfValue[] _values = [];

    /// <summary>The dictionary containing all value kinds.</summary>
    private byte[] _dictionary = [];

    /// <summary>Builds one dictionary with mixed values and a repeated value array.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _values = new PdfValue[ValueCount];
        var array = new PdfArray(null);
        var dictionary = new PdfDictionary(null);
        var stream = new PdfStream(dictionary, []);
        PdfValue[] kinds =
        [
            PdfValue.Null,
            PdfValue.FromBoolean(true),
            PdfValue.FromInteger(SampleInteger),
            PdfValue.FromReal(SampleReal),
            PdfValue.FromName(KnownName.Type),
            PdfValue.FromString("value"u8.ToArray()),
            PdfValue.FromArray(array),
            PdfValue.FromDictionary(dictionary),
            PdfValue.FromStream(stream),
            PdfValue.FromReference(new(SampleInteger, SampleGeneration)),
        ];
        for (var i = 0; i < _values.Length; i++)
        {
            _values[i] = kinds[i % kinds.Length];
        }

        var text = new System.Text.StringBuilder("<<");
        for (var i = 0; i < DictionaryEntries; i++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $" /Key{i} [{i} 0 R {i}.5 (text {i}) /Name{i}]");
        }

        _dictionary = System.Text.Encoding.ASCII.GetBytes(text.Append(" >>").ToString());

        var warmByteArray = new byte[ValueCount];
        var warmValueArray = new PdfValue[ValueCount];
        GC.KeepAlive(warmByteArray);
        GC.KeepAlive(warmValueArray);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var measuredByteArray = new byte[ValueCount];
        var byteArrayAllocation = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(measuredByteArray);
        before = GC.GetAllocatedBytesForCurrentThread();
        var measuredValueArray = new PdfValue[ValueCount];
        var valueArrayAllocation = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(measuredValueArray);
        var valueSize = Unsafe.SizeOf<PdfValue>();
        ConsoleLogger.Default.WriteLine(LogKind.Info, string.Create(
            CultureInfo.InvariantCulture,
            $"PdfValue layout: {valueSize} bytes/value, {ValueCount * valueSize} retained value payload bytes; "
            + $"byte[{ValueCount}] allocation {byteArrayAllocation} bytes, "
            + $"PdfValue[{ValueCount}] allocation {valueArrayAllocation} bytes."));
    }

    /// <summary>Reads the discriminator of values stored densely in an array.</summary>
    /// <returns>A sum of the kinds to retain the reads.</returns>
    [Benchmark]
    public int DispatchKinds()
    {
        var sum = 0;
        for (var i = 0; i < _values.Length; i++)
        {
            sum += (int)_values[i].Kind;
        }

        return sum;
    }

    /// <summary>Parses a dictionary containing references, reals, strings and names.</summary>
    /// <returns>The dictionary entry count.</returns>
    [Benchmark]
    public int ParseMixedDictionary()
    {
        var parser = new PdfParser(_dictionary, 0, null, new PdfNameTable());
        return parser.ParseValue().AsDictionary()!.Count;
    }
}
