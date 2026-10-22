// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>Splits text into runs by font: text a single font covers, the same text with a fallback font listed, and text where the fallback has to answer.</summary>
public class HyperPdfFontRunBenchmarks
{
    /// <summary>The words repeated to make the text.</summary>
    private const string Words = "The quick brown fox jumps over the lazy dog. ";

    /// <summary>The times the words repeat.</summary>
    private const int Repeats = 50;

    /// <summary>A character the standard fonts do not have.</summary>
    private const string Missing = "中文";

    /// <summary>The runs, reused between invocations.</summary>
    private readonly List<PdfFontFallbackRun> _runs = [];

    /// <summary>The font with no fallback.</summary>
    private PdfCanvasFont _alone = null!;

    /// <summary>The font with a fallback listed.</summary>
    private PdfCanvasFont _withFallback = null!;

    /// <summary>The text the primary font covers.</summary>
    private string _covered = string.Empty;

    /// <summary>The text with characters the primary font lacks.</summary>
    private string _mixed = string.Empty;

    /// <summary>Makes the fonts and the text.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        _alone = creator.AddBuiltInFont(AppearanceFont.Helvetica);
        _withFallback = _alone.WithFallbacks([creator.AddBuiltInFont(AppearanceFont.TimesRoman)]);
        _covered = string.Concat(Enumerable.Repeat(Words, Repeats));
        var mixed = new StringBuilder();
        for (var i = 0; i < Repeats; i++)
        {
            _ = mixed.Append(Words).Append(Missing);
        }

        _mixed = mixed.ToString();
    }

    /// <summary>Splits text with no fallback listed.</summary>
    /// <returns>The runs.</returns>
    [Benchmark(Baseline = true)]
    public int NoFallback()
    {
        _alone.SplitRuns(_covered, _runs);
        return _runs.Count;
    }

    /// <summary>Splits text the primary font covers, with a fallback listed.</summary>
    /// <returns>The runs.</returns>
    [Benchmark]
    public int CoveredWithFallback()
    {
        _withFallback.SplitRuns(_covered, _runs);
        return _runs.Count;
    }

    /// <summary>Splits text where the primary font lacks some characters.</summary>
    /// <returns>The runs.</returns>
    [Benchmark]
    public int MixedWithFallback()
    {
        _withFallback.SplitRuns(_mixed, _runs);
        return _runs.Count;
    }
}
