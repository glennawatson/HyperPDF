// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures scanning formula alternatives without report-cache or page-parsing costs.</summary>
public class AccessibilityFormulaBenchmarks
{
    /// <summary>The document and its reusable finding storage.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets or sets the claimed PDF/UA part.</summary>
    [Params(1, 2)]
    public int Part { get; set; }

    /// <summary>Gets or sets whether the formula has Alt in addition to ActualText.</summary>
    [Params(false, true)]
    public bool HasAlt { get; set; }

    /// <summary>Opens a tree whose formula has replacement text and an optional alternate string.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var alternative = HasAlt ? "/Alt (x plus y)" : string.Empty;
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R] >>",
            "<< /Type /StructElem /S /Document /NS 6 0 R /P 3 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /Formula /NS 6 0 R /P 4 0 R /ActualText (x plus y) {alternative} >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        ]);
        _document = PdfDocumentReader.Open(bytes, null);
        _ = Scan();
    }

    /// <summary>Releases the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Walks the formula tree using exclusively leased, reusable working storage.</summary>
    /// <returns>The formula finding count.</returns>
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
            return findings.Counts[(int)PdfAccessibilityCode.FormulaNoAlternative];
        }
        finally
        {
            AccessibilityStructureScan.Release(state);
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }
}
