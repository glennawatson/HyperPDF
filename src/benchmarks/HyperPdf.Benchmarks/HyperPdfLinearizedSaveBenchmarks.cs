// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures classic compact and linearized saves of an open form and outline document.</summary>
public class HyperPdfLinearizedSaveBenchmarks
{
    /// <summary>The source document shared by each benchmark operation.</summary>
    private PdfDocument _document = null!;

    /// <summary>The source bytes reused when measuring a cold open and save.</summary>
    private byte[] _bytes = [];

    /// <summary>Opens the representative document outside the measured save operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _bytes = CarryTestDocuments.CreateBook();
        _document = PdfDocumentReader.Open(_bytes, null);
    }

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

    /// <summary>Opens a fresh document and writes its first classic compact file.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] ColdCompactClassic()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic);
    }

    /// <summary>Opens a fresh document and writes its first linearized file.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] ColdLinearized()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfLinearizedWriter.Save(document);
    }
}
