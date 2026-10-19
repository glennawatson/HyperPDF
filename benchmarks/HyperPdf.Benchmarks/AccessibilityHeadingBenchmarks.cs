// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the heading-level structure check independently of page parsing and report caching.</summary>
public class AccessibilityHeadingBenchmarks
{
    /// <summary>The opened document, which owns reusable scan storage.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets or sets the PDF/UA part, or zero for an unclaimed document.</summary>
    [Params(0, 1, 2)]
    public int Part { get; set; }

    /// <summary>Opens a structure tree with an H1 followed by an H3.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
            "<< /Type /StructElem /S /Document /NS 7 0 R /P 3 0 R /K [5 0 R 6 0 R] >>",
            "<< /Type /StructElem /S /H1 /NS 7 0 R /P 4 0 R >>",
            "<< /Type /StructElem /S /H3 /NS 7 0 R /P 4 0 R >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        ]);
        _document = PdfDocumentReader.Open(bytes, null);
        _ = Scan();
    }

    /// <summary>Releases the document and its scan storage.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Checks the heading tree with caller-owned, reusable working storage.</summary>
    /// <returns>The number of skipped-level findings.</returns>
    [Benchmark]
    public int Scan()
    {
        var pools = _document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        var tree = PdfDocumentTagged.GetStructureTree(_document)!;
        var state = new AccessibilityStructureState(
            tree,
            tree.Root.GetDictionary(KnownName.RoleMap),
            Part,
            findings,
            pools,
            null,
            null,
            null,
            null,
            null,
            string.Empty,
            false,
            CancellationToken.None);
        try
        {
            AccessibilityStructureScan.Run(ref state);
            return findings.Counts[(int)PdfAccessibilityCode.HeadingLevelSkipped];
        }
        finally
        {
            AccessibilityStructureScan.Release(state);
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }
}
