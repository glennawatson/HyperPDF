// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading preseparated pages (ISO 32000-2 §14.11.4): the uncached read of the hand-made two printed page
/// document and of 250 printed pages, the check of an ordinary document, the cached lookup, and planning a copy that
/// carries only part of a printed page.
/// </summary>
public class HyperPdfSeparationBenchmarks
{
    /// <summary>The printed pages of the large document.</summary>
    private const int LargePrintedPages = 250;

    /// <summary>The pages of the ordinary document.</summary>
    private const int OrdinaryPages = 100;

    /// <summary>The third page of printed page 1.</summary>
    private const int Yellow = 2;

    /// <summary>The hand-made preseparated document.</summary>
    private PdfDocument? _preseparated;

    /// <summary>The large preseparated document.</summary>
    private PdfDocument? _large;

    /// <summary>The ordinary document.</summary>
    private PdfDocument? _ordinary;

    /// <summary>The pages a partial copy carries: three separations of printed page 1 and the ordinary page.</summary>
    private PdfPage[] _partial = [];

    /// <summary>Opens the documents and checks they read as intended.</summary>
    /// <exception cref="InvalidOperationException">A fixture does not read as intended.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _preseparated = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        _large = PdfDocumentReader.Open(SeparationPdf.CreatePrintedPages(LargePrintedPages), null);
        _ordinary = PdfDocumentReader.Open(TestPdf.Create(OrdinaryPages), null);
        _partial = [.. new[] { 0, 1, Yellow, SeparationPdf.OrdinaryPage }.Select(index => PdfDocumentPages.GetPage(_preseparated, index))];
        if (PdfDocumentSeparations.GetSeparations(_preseparated).Groups.Length != SeparationPdf.PrintedPages
            || PdfDocumentSeparations.GetSeparations(_large) is not { Findings.Length: 0, Groups.Length: LargePrintedPages }
            || PdfDocumentSeparations.GetSeparations(_ordinary).Groups.Length != 0)
        {
            throw new InvalidOperationException("Separation fixtures did not read as expected.");
        }
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _preseparated?.Dispose();
        _large?.Dispose();
        _ordinary?.Dispose();
    }

    /// <summary>Reads and groups the hand-made document's separation dictionaries, without the cache.</summary>
    /// <returns>The groups.</returns>
    [Benchmark]
    public PdfSeparationInfo ReadPreseparated() => SeparationReader.Read(_preseparated!, CancellationToken.None);

    /// <summary>Reads and groups a thousand separation pages, without the cache.</summary>
    /// <returns>The groups.</returns>
    [Benchmark]
    public PdfSeparationInfo ReadLarge() => SeparationReader.Read(_large!, CancellationToken.None);

    /// <summary>Checks a hundred ordinary pages for separation dictionaries, without the cache.</summary>
    /// <returns>The empty result.</returns>
    [Benchmark]
    public PdfSeparationInfo ReadOrdinary() => SeparationReader.Read(_ordinary!, CancellationToken.None);

    /// <summary>Gets the cached groups.</summary>
    /// <returns>The groups.</returns>
    [Benchmark]
    public PdfSeparationInfo Cached() => PdfDocumentSeparations.GetSeparations(_preseparated!);

    /// <summary>Plans the separation dictionaries of a copy that carries three of a printed page's four separations.</summary>
    /// <returns>The pages left linked, so the plan is not optimised away.</returns>
    [Benchmark]
    public int PlanPartialCopy() => PdfCarrySeparations.Plan(_preseparated!, _partial, new())!.Links[0]!.Length;
}
