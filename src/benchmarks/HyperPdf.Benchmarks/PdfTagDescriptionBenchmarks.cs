// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the description walk used when a creator decides whether to write a PDF/UA claim.</summary>
public class PdfTagDescriptionBenchmarks
{
    /// <summary>The created tree, which owns its reusable scan state.</summary>
    private PdfTagTree _tags = null!;

    /// <summary>The creator's reusable working storage.</summary>
    private ScratchPools _pools = null!;

    /// <summary>Gets or sets the requested PDF/UA part.</summary>
    [Params(1, 2)]
    public int Part { get; set; }

    /// <summary>Gets or sets whether the formula has an alternative string.</summary>
    [Params(false, true)]
    public bool HasAlt { get; set; }

    /// <summary>Creates a formula and a described figure outside the measured operation.</summary>
    /// <exception cref="InvalidOperationException">The description scan reports an unexpected defect count.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        _tags = creator.Tags!;
        _pools = creator.Builder.ImageScratchPools;
        _ = _tags.AddElement(_tags.Root, "Formula", new() { AlternativeText = HasAlt ? "x plus y" : null, ActualText = "x plus y" });
        _ = _tags.AddElement(_tags.Root, "Figure", new() { AlternativeText = "A square" });
        var expected = Part == (int)PdfUaConformance.Part1 && !HasAlt ? 1 : 0;
        if (Count() != expected)
        {
            throw new InvalidOperationException("The description scan did not report the expected formula defect.");
        }
    }

    /// <summary>Checks descriptions using the creator's tag tree.</summary>
    /// <returns>The number of description defects.</returns>
    [Benchmark]
    public int Count() => PdfTagDescriptionScan.Count(_tags.Root, _pools, (PdfUaConformance)Part, CancellationToken.None);
}
