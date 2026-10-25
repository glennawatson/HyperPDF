// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures opening and async reporting through the stream adapter with uncached configuration objects.</summary>
public class OptionalContentReadAheadBenchmarks
{
    /// <summary>The padded source bytes shared by successive opens.</summary>
    private byte[] _bytes = [];

    /// <summary>Gets or sets whether the configuration array is indirect.</summary>
    [Params(false, true)]
    public bool IndirectArray { get; set; }

    /// <summary>Builds the large file before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        const string metadata = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/" pdfuaid:part="1"/>
            </rdf:RDF>
            """;
        _bytes = OptionalContentPdf.Create(metadata, IndirectArray);
    }

    /// <summary>Opens a fresh bounded stream cache and reads the configuration report.</summary>
    /// <returns>The number of prohibited automatic-state entries.</returns>
    [Benchmark]
    public async ValueTask<int> OpenAndReadAsync()
    {
        await using var stream = new MemoryStream(_bytes, false);
        using var document = await PdfDocumentReader.OpenAsync(stream, null, CancellationToken.None).ConfigureAwait(false);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None).ConfigureAwait(false);
        return report.GetCount(PdfAccessibilityCode.OptionalContentAutomaticState);
    }
}
