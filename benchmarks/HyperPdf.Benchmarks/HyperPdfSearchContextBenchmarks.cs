// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the text around 500 search hits on one page through the engine, as the search panel does to show each hit in context.</summary>
public class HyperPdfSearchContextBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 60;

    /// <summary>The hits read.</summary>
    private const int Hits = 500;

    /// <summary>The characters of context kept on each side of a hit.</summary>
    private const int ContextLength = 40;

    /// <summary>The length of a hit.</summary>
    private const int HitLength = 5;

    /// <summary>The managed document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The engine document.</summary>
    private HyperPdfDocument _engine = null!;

    /// <summary>The characters on the page.</summary>
    private int _characters;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTextFilled(1, Lines), null);
        _engine = new(_document, "search-context-benchmark.pdf");
        _characters = _engine.GetCharacterCount(0);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the context of each hit with a count and a text call, as the search panel does.</summary>
    /// <returns>The characters read.</returns>
    [Benchmark]
    public int ContextOfEachHit()
    {
        var total = 0;
        for (var i = 0; i < Hits; i++)
        {
            var start = (i * (_characters - HitLength)) / Hits;
            var from = Math.Max(0, start - ContextLength);
            var to = Math.Min(_engine.GetCharacterCount(0), start + HitLength + ContextLength);
            total += _engine.GetText(0, from, to - from).ReplaceLineEndings(" ").Trim().Length;
        }

        return total;
    }
}
