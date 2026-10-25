// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures numbered-type lookup and repeated heading-level access over a prepared structure tree.</summary>
public class NumberedHeadingBenchmarks
{
    /// <summary>The type's UTF-8 bytes.</summary>
    private byte[] _spelling = null!;

    /// <summary>The owned document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's single numbered heading.</summary>
    private PdfStructureElement _heading = null!;

    /// <summary>Gets or sets the numbered heading's spelling.</summary>
    [Params("H3", "H7", "H1247", "H2147483647", "H2147483648", "H1234567890123456789012345678901234567890")]
    public string Spelling { get; set; } = string.Empty;

    /// <summary>Loads a PDF 2.0 namespace tree outside the measured operations.</summary>
    /// <returns>The asynchronous setup.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _spelling = Encoding.ASCII.GetBytes(Spelling);
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R] >>",
            "<< /Type /StructElem /S /Document /NS 6 0 R /P 3 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /{Spelling} /NS 6 0 R /P 4 0 R >>",
            $"<< /Type /Namespace /NS ({PdfStructureTypes.Pdf20Namespace}) >>",
        ]);
        _document = await PdfDocumentReader.OpenAsync(bytes, null, CancellationToken.None);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(_document, CancellationToken.None);
        _heading = PdfDocumentTagged.GetStructureTree(_document)!.Roots[0].Kids[0].Element!;
    }

    /// <summary>Releases the prepared document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Looks up the type in its original UTF-8 bytes.</summary>
    /// <returns>The recognised structure type.</returns>
    [Benchmark]
    public PdfStructureType Find() => PdfStructureTypes.Find(_spelling);

    /// <summary>Reads the heading's level without reparsing its document.</summary>
    /// <returns>The reported heading level.</returns>
    [Benchmark]
    public int ReadLevel() => _heading.HeadingLevel;

    /// <summary>Checks the character spelling used by tag creation without formatting its number.</summary>
    /// <returns>Whether the spelling is a canonical numbered heading.</returns>
    [Benchmark]
    public bool ValidateName() => NumberedHeadings.IsName(Spelling.AsSpan());
}
