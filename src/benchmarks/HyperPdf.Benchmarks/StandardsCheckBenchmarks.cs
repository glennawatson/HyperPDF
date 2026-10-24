// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the Well-Tagged PDF check, the Best Practice Guide advice, and two attribute edits with their undo, on a small tagged document.</summary>
public class StandardsCheckBenchmarks
{
    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The open document with a table of header and data cells.</summary>
    private PdfDocument _table = null!;

    /// <summary>Opens the tagged samples.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        _table = PdfDocumentReader.Open(StructuredTestPdf.CreateTagged(), null);
    }

    /// <summary>Releases the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _table.Dispose();
    }

    /// <summary>Checks a document with a table against Well-Tagged PDF, which includes the row group and header cell rules.</summary>
    /// <returns>The number of findings.</returns>
    [Benchmark]
    public int WtpdfTableCheck() => PdfDocumentWtpdf.Check(_table).Findings.Length;

    /// <summary>Checks the document against Well-Tagged PDF.</summary>
    /// <returns>The number of findings.</returns>
    [Benchmark]
    public int WtpdfCheck() => PdfDocumentWtpdf.Check(_document).Findings.Length;

    /// <summary>Reads the Best Practice Guide advice.</summary>
    /// <returns>The number of suggestions.</returns>
    [Benchmark]
    public int TagAdvice() => PdfDocumentTagAdvice.Get(_document).Length;

    /// <summary>Maps a finding code to its WCAG criteria.</summary>
    /// <returns>The number of criteria.</returns>
    [Benchmark]
    public int WcagLookup() => PdfAccessibilityWcag.GetSuccessCriteria(PdfAccessibilityCode.FormFieldNoLabel).Length;

    /// <summary>Sets a role mapping and takes it back.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool RoleMapAndUndo()
    {
        PdfDocumentStructureEditing.SetRoleMapping(_document, "Sidebar", "Div");
        return PdfDocumentEditing.Undo(_document);
    }
}
