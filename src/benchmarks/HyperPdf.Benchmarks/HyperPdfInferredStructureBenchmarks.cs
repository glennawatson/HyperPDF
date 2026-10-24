// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the layout inference for untagged pages. <see cref="TextOnlyPage"/> is the baseline: a page of running text
/// with no table, list, figure or margin text, which the early exits let through. The other benchmarks add one feature
/// each on top of that: tables and lists, pagination artifacts with a document profile, a language guess, and the
/// optimiser writing the inferred tags. <see cref="ImageOnlyPage"/> measures the image-only check that decides whether
/// the recogniser is called. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfInferredStructureBenchmarks
{
    /// <summary>The pages of the article.</summary>
    private const int ArticlePages = 3;

    /// <summary>The width and height of the scanned page in pixels.</summary>
    private const int ScanPixels = 8;

    /// <summary>A sentence long enough to be guessed.</summary>
    private const string Sentence = "The cat is on the mat and it is not in the house with the dog.";

    /// <summary>What inference may use when asked for a language guess.</summary>
    private static readonly PdfLayoutInference Guessing = new() { GuessLanguage = true };

    /// <summary>The recogniser that finds nothing.</summary>
    private static readonly PdfPageRecogniser NoWords = static (_, _) => ValueTask.FromResult<IReadOnlyList<PdfOcrWord>?>(null);

    /// <summary>A page of running text.</summary>
    private PdfDocument _article = null!;

    /// <summary>A page with a ruled table and a list.</summary>
    private PdfDocument _structured = null!;

    /// <summary>A page that only draws an image.</summary>
    private PdfDocument _scan = null!;

    /// <summary>The structure of the document's repeating margins.</summary>
    private PdfPaginationProfile _profile = null!;

    /// <summary>What inference may use when given the profile.</summary>
    private PdfLayoutInference _paginating;

    /// <summary>The untagged table and list page's file.</summary>
    private byte[] _untagged = [];

    /// <summary>Opens the documents and warms the caches.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var options = new PdfOpenOptions { FontFactory = static dictionary => new StructureBenchmarkFont(dictionary) };
        _article = PdfDocumentReader.OpenWith(TestPdf.CreateArticle(ArticlePages), options);
        _untagged = StructuredTestPdf.CreateUntagged();
        _structured = PdfDocumentReader.OpenWith(_untagged, options);
        _scan = PdfDocumentReader.OpenWith(TestPdf.CreateScan(new byte[ScanPixels * ScanPixels], ScanPixels, ScanPixels), options);
        _profile = PdfPaginationProfile.Find(_article, CancellationToken.None);
        _paginating = new() { Pagination = _profile };
        _ = PdfDocumentTagged.GetMarkedContent(_article, 0);
        _ = PdfDocumentTagged.GetMarkedContent(_structured, 0);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _article.Dispose();
        _structured.Dispose();
        _scan.Dispose();
    }

    /// <summary>Infers a page of running text: the baseline, where every extra search exits early.</summary>
    /// <returns>The node count.</returns>
    [Benchmark(Baseline = true)]
    public int TextOnlyPage() => PdfReadingStructure.InferLayout(_article, 0).Nodes.Count;

    /// <summary>Infers a page with a ruled table and a numbered list.</summary>
    /// <returns>The node count.</returns>
    [Benchmark]
    public int TableAndListPage() => PdfReadingStructure.InferLayout(_structured, 0).Nodes.Count;

    /// <summary>Infers a page of running text with the document's repeating margin text set aside.</summary>
    /// <returns>The artifact count.</returns>
    [Benchmark]
    public int WithPaginationProfile() => PdfReadingStructure.InferLayout(_article, 0, _paginating, CancellationToken.None).Artifacts.Count;

    /// <summary>Infers a page of running text and guesses each node's language.</summary>
    /// <returns>The node count.</returns>
    [Benchmark]
    public int WithLanguageGuess() => PdfReadingStructure.InferLayout(_article, 0, Guessing, CancellationToken.None).Nodes.Count;

    /// <summary>Finds the text that repeats in the margins of the article's pages.</summary>
    /// <returns>The number of texts found.</returns>
    [Benchmark]
    public int FindPaginationProfile() => PdfPaginationProfile.Find(_article, CancellationToken.None).Count;

    /// <summary>Guesses the language of one sentence.</summary>
    /// <returns>The length of the guess.</returns>
    [Benchmark]
    public int GuessLanguage() => LanguageGuesser.Guess(Sentence)?.Length ?? 0;

    /// <summary>Checks an image-only page, and asks the recogniser, which finds nothing.</summary>
    /// <returns>The node count.</returns>
    [Benchmark]
    public async ValueTask<int> ImageOnlyPage() => (await PdfReadingStructure.ReadAsync(_scan, 0, NoWords, CancellationToken.None).ConfigureAwait(false)).Nodes.Count;

    /// <summary>Optimises the untagged table and list page, writing inferred tables, lists and figures as tags.</summary>
    /// <returns>The size written.</returns>
    [Benchmark]
    public long WriteInferredTags()
    {
        using var document = PdfDocumentReader.Open(_untagged, null);
        using var output = new MemoryStream();
        _ = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.KeepQuality with { AddInferredTags = true });
        return output.Length;
    }
}
