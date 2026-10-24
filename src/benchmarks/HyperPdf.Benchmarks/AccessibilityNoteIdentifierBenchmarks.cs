// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures warm scans with missing, unique and repeated Note IDs.</summary>
public class AccessibilityNoteIdentifierBenchmarks
{
    /// <summary>The first Note object's number.</summary>
    private const int FirstNote = 5;

    /// <summary>The root's zero-based slot in the object array.</summary>
    private const int RootSlot = 2;

    /// <summary>The Document element's zero-based slot.</summary>
    private const int DocumentSlot = 3;

    /// <summary>The opened document, including reusable scan storage.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets or sets the number of Notes.</summary>
    [Params(0, 64)]
    public int Notes { get; set; }

    /// <summary>Gets or sets whether IDs are omitted, unique or repeated.</summary>
    [Params("Missing", "Unique", "Repeated")]
    public string Identifiers { get; set; } = string.Empty;

    /// <summary>Prepares the tree and leases its first-use buffers outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var objects = new string[FirstNote - 1 + Notes];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>";
        objects[1] = "<< /Type /Pages /Count 0 /Kids [] >>";
        objects[RootSlot] = "<< /Type /StructTreeRoot /K 4 0 R >>";
        var kids = new StringBuilder();
        for (var index = 0; index < Notes; index++)
        {
            var number = FirstNote + index;
            _ = kids.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
            var id = Identifiers switch
            {
                "Unique" => string.Create(CultureInfo.InvariantCulture, $"/ID ({index})"),
                "Repeated" => "/ID (same)",
                _ => string.Empty,
            };
            objects[number - 1] = $"<< /Type /StructElem /S /Note /P 4 0 R {id} >>";
        }

        objects[DocumentSlot] = $"<< /Type /StructElem /S /Document /P 3 0 R /K [{kids}] >>";
        _document = PdfDocumentReader.Open(MiniPdf.Build(objects), null);
        _ = Scan();
    }

    /// <summary>Releases the prepared document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Runs the full structure scan with caller-owned pooled storage.</summary>
    /// <returns>The number of Note identifier findings.</returns>
    [Benchmark]
    public int Scan()
    {
        var pools = _document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        var tree = PdfDocumentTagged.GetStructureTree(_document)!;
        var state = new AccessibilityStructureState(tree, null, PdfUaClaim.Part1, findings, pools, null, null, null, null, null, string.Empty, false, CancellationToken.None);
        try
        {
            AccessibilityStructureScan.Run(ref state);
            return findings.Counts[(int)PdfAccessibilityCode.NoteIdentifierInvalid];
        }
        finally
        {
            AccessibilityStructureScan.Release(state);
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }
}
