// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Optimises a tagged document that names PDF/UA-1 and the same document that names nothing. The claimed file is read back
/// and checked after the rewrite, so the difference between the two is the cost of keeping a claim.
/// </summary>
public class HyperPdfClaimedOptimizeBenchmarks
{
    /// <summary>The width of the pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The pages in each document.</summary>
    private const int Pages = 20;

    /// <summary>The document that names no PDF/UA part.</summary>
    private PdfDocument _plain = null!;

    /// <summary>The document that names PDF/UA-1.</summary>
    private PdfDocument _claimed = null!;

    /// <summary>Builds the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = Open(PdfUaConformance.None);
        _claimed = Open(PdfUaConformance.Part1);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _claimed.Dispose();
    }

    /// <summary>Optimises the document that names nothing.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public long OptimizeUnclaimed() => PdfOptimizer.Optimize(_plain, Stream.Null, PdfOptimizeOptions.KeepQuality).BytesAfter;

    /// <summary>Optimises the document that names PDF/UA-1.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long OptimizeClaimed() => PdfOptimizer.Optimize(_claimed, Stream.Null, PdfOptimizeOptions.KeepQuality).BytesAfter;

    /// <summary>Builds a tagged document and opens it.</summary>
    /// <param name="claim">The PDF/UA part the document names.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(PdfUaConformance claim)
    {
        var creator = new PdfDocumentCreator(new() { Title = "Benchmark", Language = "en", Tagged = true, UaClaim = claim });
        var source = PdfFontSource.Open(TestFont.Create(), 0).Source!;
        var font = creator.EmbedFont(source);
        PdfFlowBlock[] blocks = [new PdfFlowHeading(1, "A Title"), new PdfFlowParagraph("A paragraph of text.")];
        for (var page = 0; page < Pages; page++)
        {
            _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);
        }

        _ = creator.Finish();
        return PdfDocumentReader.Open(creator.ToArray(), null);
    }
}
