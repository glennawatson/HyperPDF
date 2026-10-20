// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Presentation;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading a document's parts and article threads, a page's transition and timing, the page's accessibility items,
/// and planning a presentation step; allocations come from the EventPipe trace.
/// </summary>
public class PageStructureSourceBenchmarks
{
    /// <summary>An ordinary blank page.</summary>
    private const string BlankPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>";

    /// <summary>The document with parts and an article.</summary>
    private BenchmarkPdfDocument _parts = null!;

    /// <summary>The document with page transitions.</summary>
    private BenchmarkPdfDocument _slides = null!;

    /// <summary>The accessible document with tags, fields and annotations.</summary>
    private BenchmarkPdfDocument _accessible = null!;

    /// <summary>The parts source.</summary>
    private IDocumentPartSource _partSource = null!;

    /// <summary>The transition source.</summary>
    private IPageTransitionSource _transitionSource = null!;

    /// <summary>The semantic source.</summary>
    private IPageSemanticSource _semanticSource = null!;

    /// <summary>The transition of the first slide.</summary>
    private PageTransition? _transition;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _parts = new(MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /DPartRoot 11 0 R /Threads [12 0 R] >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            BlankPage,
            BlankPage,
            BlankPage,
            "<< /Type /DPart /Parent 11 0 R /DParts [[7 0 R] [8 0 R]] /DPM << /Title (Mailing) >> >>",
            "<< /Type /DPart /Parent 6 0 R /DParts [[9 0 R 10 0 R]] /DPM << /Recipient (Ann) /Title (Letter A) >> >>",
            "<< /Type /DPart /Parent 6 0 R /Start 5 0 R /DPM << /Recipient (Bob) >> >>",
            "<< /Type /DPart /Parent 7 0 R /Start 3 0 R >>",
            "<< /Type /DPart /Parent 7 0 R /Start 4 0 R /End 5 0 R >>",
            "<< /Type /DPartRoot /DPartRootNode 6 0 R /RecordLevel 1 /NodeNameList [/Job /Recipient] >>",
            "<< /Type /Thread /F 13 0 R /I << /Title (Story) >> >>",
            "<< /T 12 0 R /N 14 0 R /V 15 0 R /P 3 0 R /R [72 500 300 700] >>",
            "<< /T 12 0 R /N 15 0 R /V 13 0 R /P 4 0 R /R [72 200 300 400] >>",
            "<< /T 12 0 R /N 13 0 R /V 14 0 R /P 5 0 R /R [320 500 540 700] >>"));
        _slides = new(MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Dur 5 /Trans << /Type /Trans /S /Wipe /D 2 /Di 90 >> >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Trans << /S /Split /D 0.5 /Dm /V /M /O >> >>"));
        _accessible = new(AccessibleTestPdf.CreateRich());
        _partSource = DocumentFeature<IDocumentPartSource>.Cast(_parts.Document)!;
        _transitionSource = DocumentFeature<IPageTransitionSource>.Cast(_slides.Document)!;
        _semanticSource = DocumentFeature<IPageSemanticSource>.Cast(_accessible.Document)!;
        _transition = _transitionSource.GetTransition(0);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _parts.Dispose();
        _slides.Dispose();
        _accessible.Dispose();
    }

    /// <summary>Reads the part tree.</summary>
    /// <returns>The count of top-level parts.</returns>
    [Benchmark]
    public int ReadParts() => _partSource.GetParts().Count;

    /// <summary>Reads the article threads.</summary>
    /// <returns>The count of threads.</returns>
    [Benchmark]
    public int ReadThreads() => _partSource.GetThreads().Count;

    /// <summary>Reads the transition and timing of the first slide.</summary>
    /// <returns>Whether the page has a transition.</returns>
    [Benchmark]
    public bool ReadTransition() => _transitionSource.GetTransition(0) is not null;

    /// <summary>Reads the accessibility items of the first page: fields, annotations and artifacts.</summary>
    /// <returns>The count of items.</returns>
    [Benchmark]
    public int ReadPageItems() => _semanticSource.GetItems(0).Count;

    /// <summary>Plans the presentation step for a slide with a transition.</summary>
    /// <returns>The step's duration.</returns>
    [Benchmark]
    public double PlanStep() => PresentationPlanner.Plan(0, _transition, new(true, true, false)).DurationSeconds;
}
