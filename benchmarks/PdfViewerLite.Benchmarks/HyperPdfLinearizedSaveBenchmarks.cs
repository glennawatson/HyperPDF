// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures classic compact and linearized saves of an open form and outline document.</summary>
public class HyperPdfLinearizedSaveBenchmarks
{
    /// <summary>The source document shared by each benchmark operation.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the representative document outside the measured save operation.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);

    /// <summary>Releases the source document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Rewrites the document with a classic cross-reference table.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark(Baseline = true)]
    public byte[] CompactClassic() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Classic);

    /// <summary>Rewrites the document with first-page and hint sections.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] Linearized() => PdfLinearizedWriter.Save(_document);
}
