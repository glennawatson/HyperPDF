// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh opening and reporting of PDF/UA-1 optional-content configurations.</summary>
public class OptionalContentReportBenchmarks
{
    /// <summary>The generated source bytes.</summary>
    private byte[] _bytes = [];

    /// <summary>The document retained for warm scanner measurements.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets or sets the number of alternate configurations.</summary>
    [Params(0, 1, 64)]
    public int Configurations { get; set; }

    /// <summary>Gets or sets whether the configurations include required names.</summary>
    [Params(false, true)]
    public bool Named { get; set; }

    /// <summary>Generates named or unnamed configuration dictionaries before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var name = Named ? "/Name (Configuration)" : string.Empty;
        var configurations = new StringBuilder();
        for (var index = 0; index < Configurations; index++)
        {
            _ = configurations.Append("<< ").Append(name).Append(" >> ");
        }

        const string metadata = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/" pdfuaid:part="1"/>
            </rdf:RDF>
            """;
        _bytes = MiniPdf.Build(
        [
            $"<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R /OCProperties << /OCGs [] /D << {name} >> /Configs [{configurations}] >> >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", metadata),
        ]);
        _document = PdfDocumentReader.Open(_bytes, null);
        _ = Scan();
        _ = Read();
    }

    /// <summary>Releases the warm document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Opens fresh object state and builds an uncached accessibility report.</summary>
    /// <returns>The independently owned report.</returns>
    [Benchmark]
    public PdfAccessibilityReport Read()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfDocumentAccessibility.GetAccessibilityReport(document);
    }

    /// <summary>Checks prepared configurations using exclusively leased finding storage.</summary>
    /// <returns>The number of configuration findings.</returns>
    [Benchmark]
    public int Scan()
    {
        var pools = _document.Objects.ScratchPools;
        var findings = AccessibilityFindingCollection.Rent(pools);
        try
        {
            AccessibilityOptionalContentScan.Run(_document.Catalog, PdfUaClaim.Part1, findings, CancellationToken.None);
            return findings.Findings.Count;
        }
        finally
        {
            AccessibilityFindingCollection.Release(pools, findings);
        }
    }
}
