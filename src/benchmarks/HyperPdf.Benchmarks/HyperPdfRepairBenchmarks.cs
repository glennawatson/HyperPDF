// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures HyperPDF's repair work: opening a clean file (which must cost no more than before repairs were reported),
/// opening a file whose cross-reference table is damaged, the whole-document check, and a compact save that writes
/// conforming structures. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfRepairBenchmarks
{
    /// <summary>The pages of the documents.</summary>
    private const int Pages = 20;

    /// <summary>A clean document.</summary>
    private byte[] _clean = [];

    /// <summary>The same document with a spoiled startxref.</summary>
    private byte[] _damaged = [];

    /// <summary>The clean document, open, for the check and the save.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _clean = TestPdf.Create(Pages);
        _damaged = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(_clean).Replace("startxref", "startxxxx", StringComparison.Ordinal));
        _document = PdfDocumentReader.Open(_clean, null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Opens a clean file and reads its repair flag.</summary>
    /// <returns>Whether the file was repaired.</returns>
    [Benchmark(Baseline = true)]
    public bool OpenClean()
    {
        using var document = PdfDocumentReader.Open(_clean, null);
        return PdfDocumentCheck.WasRepaired(document);
    }

    /// <summary>Opens a file whose cross-reference table must be rebuilt, and reads the repairs.</summary>
    /// <returns>The number of repairs.</returns>
    [Benchmark]
    public int OpenDamaged()
    {
        using var document = PdfDocumentReader.Open(_damaged, null);
        return PdfDocumentCheck.GetRepairs(document).Length;
    }

    /// <summary>Checks the whole document.</summary>
    /// <returns>The number of faults.</returns>
    [Benchmark]
    public int Check() => PdfDocumentCheck.Check(_document, PdfCheckOptions.Default).Faults.Count;

    /// <summary>Saves the document compactly, with the conforming-structure pass.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int CompactSave() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Classic).Length;
}
