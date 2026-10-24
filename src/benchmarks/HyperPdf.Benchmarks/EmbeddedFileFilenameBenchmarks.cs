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

/// <summary>Measures fresh accessibility reports with absent or valid embedded-file specifications.</summary>
public class EmbeddedFileFilenameBenchmarks
{
    /// <summary>The generated document bytes.</summary>
    private byte[] _bytes = [];

    /// <summary>Gets or sets the number of file specifications.</summary>
    [Params(0, 32)]
    public int Files { get; set; }

    /// <summary>Builds the input before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var names = new StringBuilder();
        for (var index = 0; index < Files; index++)
        {
            _ = names.Append("(file").Append(index.ToString("D2", CultureInfo.InvariantCulture))
                .Append(") << /Type /Filespec /F (file.txt) /UF (file.txt) /EF << /F 4 0 R >> >> ");
        }

        const string metadata = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/" pdfuaid:part="1"/>
            </rdf:RDF>
            """;
        var tree = Files == 0 ? string.Empty : $"/Names << /EmbeddedFiles << /Names [{names}] >> >>";
        _bytes = MiniPdf.Build(
        [
            $"<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R {tree} >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", metadata),
            MiniPdf.Stream("/Type /EmbeddedFile", "payload"),
        ]);
        _ = Read();
    }

    /// <summary>Opens fresh object state and builds an independently owned report.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public PdfAccessibilityReport Read()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfDocumentAccessibility.GetAccessibilityReport(document);
    }
}
