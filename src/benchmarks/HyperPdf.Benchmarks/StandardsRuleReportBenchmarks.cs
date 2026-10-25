// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the rule reports that map veraPDF's accessibility profiles to HyperPdf's checks, on a small tagged document.</summary>
public class StandardsRuleReportBenchmarks
{
    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the tagged sample and reads the first report, so each run measures a warm document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        _ = PdfDocumentStandards.Check(_document, PdfStandardsProfile.PdfUa1);
    }

    /// <summary>Releases the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Gets the rules of the largest profile, which are read once and shared.</summary>
    /// <returns>The number of rules.</returns>
    [Benchmark]
    public int Rules() => PdfDocumentStandards.GetRules(PdfStandardsProfile.WtpdfAccessibility).Count;

    /// <summary>Reports the PDF/UA-1 rules.</summary>
    /// <returns>The number of failed rules.</returns>
    [Benchmark]
    public int PdfUa1() => PdfDocumentStandards.Check(_document, PdfStandardsProfile.PdfUa1).Count(PdfStandardsRuleStatus.Failed);

    /// <summary>Reports the PDF/UA-2 rules, which also read the Well-Tagged PDF check.</summary>
    /// <returns>The number of failed rules.</returns>
    [Benchmark]
    public int PdfUa2() => PdfDocumentStandards.Check(_document, PdfStandardsProfile.PdfUa2).Count(PdfStandardsRuleStatus.Failed);

    /// <summary>Reports the ISO/TS 32005 rules, with their parent-child pairs.</summary>
    /// <returns>The number of failed rules.</returns>
    [Benchmark]
    public int Iso32005() => PdfDocumentStandards.Check(_document, PdfStandardsProfile.Iso32005).Count(PdfStandardsRuleStatus.Failed);

    /// <summary>Reports the Well-Tagged PDF accessibility rules.</summary>
    /// <returns>The number of failed rules.</returns>
    [Benchmark]
    public int WtpdfAccessibility() => PdfDocumentStandards.Check(_document, PdfStandardsProfile.WtpdfAccessibility).Count(PdfStandardsRuleStatus.Failed);

    /// <summary>Reports the complete WCAG 2.2 profile and groups it by success criterion.</summary>
    /// <returns>The number of criteria.</returns>
    [Benchmark]
    public int WcagByCriterion() => PdfDocumentStandards.Check(_document, PdfStandardsProfile.Wcag22Complete).GetBySuccessCriterion().Length;
}
