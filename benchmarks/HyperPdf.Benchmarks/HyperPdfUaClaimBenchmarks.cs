// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the PDF/UA-1 check every save runs: on an edited document that names no PDF/UA part (the cost every save
/// pays), on an edited document that names PDF/UA-1 and has nothing to repair, and on one with an annotation the check
/// repairs, followed by the undo of the repair so the next call repairs it again.
/// </summary>
public class HyperPdfUaClaimBenchmarks
{
    /// <summary>The width of the pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The pages in each document.</summary>
    private const int Pages = 20;

    /// <summary>The left edge of the annotation's area.</summary>
    private const float AreaLeft = 10F;

    /// <summary>The bottom of the annotation's area.</summary>
    private const float AreaBottom = 10F;

    /// <summary>The right edge of the annotation's area.</summary>
    private const float AreaRight = 100F;

    /// <summary>The top of the annotation's area.</summary>
    private const float AreaTop = 40F;

    /// <summary>The edit that makes a document changed.</summary>
    private static readonly PdfMetadataEdit TitleEdit = new() { Title = "Edited" };

    /// <summary>The report every check fills, kept so the benchmarks measure the check and not the report.</summary>
    private readonly PdfSaveReport _report = new();

    /// <summary>An edited document that names no PDF/UA part.</summary>
    private PdfDocument _plain = null!;

    /// <summary>An edited document that names PDF/UA-1 and has nothing to repair.</summary>
    private PdfDocument _clean = null!;

    /// <summary>An edited document that names PDF/UA-1 and has an untagged annotation.</summary>
    private PdfDocument _annotated = null!;

    /// <summary>Builds and edits the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = Open(PdfUaConformance.None);
        _clean = Open(PdfUaConformance.Part1);
        _annotated = Open(PdfUaConformance.Part1);
        var annotation = PdfRedactions.CreateAnnotation(_annotated.Objects, [new(AreaLeft, AreaBottom, AreaRight, AreaTop)], PdfRedactionAppearance.Black);
        annotation.Set(KnownName.Contents, PdfValue.FromString("Marked for redaction"u8.ToArray()));
        _ = PdfPageAnnotations.Append(_annotated.Objects, PdfDocumentPages.GetPage(_annotated, 0), annotation);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _clean.Dispose();
        _annotated.Dispose();
    }

    /// <summary>Runs the save check on an edited document that names no PDF/UA part.</summary>
    /// <returns>Whether a check was recorded, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public bool CheckUnclaimed()
    {
        PdfUaClaimGuard.Apply(_plain.Objects, true, _report, CancellationToken.None);
        return _report.UaClaim is null;
    }

    /// <summary>Runs the save check on an edited document that names PDF/UA-1 and has nothing to repair.</summary>
    /// <returns>The number of problems found.</returns>
    [Benchmark]
    public int CheckClaimedClean()
    {
        PdfUaClaimGuard.Apply(_clean.Objects, true, _report, CancellationToken.None);
        return _report.UaClaim!.Problems.Count;
    }

    /// <summary>Runs the save check on a document with an untagged annotation, repairs it, then undoes the repair.</summary>
    /// <returns>The number of problems found.</returns>
    [Benchmark]
    public int CheckAndRepairAnnotation()
    {
        PdfUaClaimGuard.Apply(_annotated.Objects, true, _report, CancellationToken.None);
        var found = _report.UaClaim!.Problems.Count;
        _ = PdfDocumentEditing.Undo(_annotated);
        return found;
    }

    /// <summary>Builds a document, opens it and makes an edit.</summary>
    /// <param name="claim">The PDF/UA part the document names.</param>
    /// <returns>The edited document.</returns>
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
        var document = PdfDocumentReader.Open(creator.ToArray(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        return document;
    }
}
