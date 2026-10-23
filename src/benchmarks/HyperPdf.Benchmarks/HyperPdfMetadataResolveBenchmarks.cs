// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the document information a viewer shows when a PDF 2.0 file keeps its metadata in XMP, and a packet with structures.</summary>
public class HyperPdfMetadataResolveBenchmarks
{
    /// <summary>The title in the XMP packet.</summary>
    private const string XmpTitle = "XMP title";

    /// <summary>An XMP packet with a title, creators, dates, and a structure.</summary>
    private const string Packet = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
          <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:xmp="http://ns.adobe.com/xap/1.0/"
              xmlns:pdf="http://ns.adobe.com/pdf/1.3/" xmlns:xmpMM="http://ns.adobe.com/xap/1.0/mm/"
              xmlns:stRef="http://ns.adobe.com/xap/1.0/sType/ResourceRef#" pdf:Producer="Producer" xmp:CreatorTool="Tool">
              <dc:title><rdf:Alt><rdf:li xml:lang="x-default">XMP title</rdf:li></rdf:Alt></dc:title>
              <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
              <xmp:CreateDate>2024-01-02T03:04:05Z</xmp:CreateDate>
              <xmp:ModifyDate>2024-05-06T07:08:09Z</xmp:ModifyDate>
              <xmpMM:DerivedFrom rdf:parseType="Resource"><stRef:documentID>uuid:1</stRef:documentID></xmpMM:DerivedFrom>
              <xmpMM:History><rdf:Seq><rdf:li rdf:parseType="Resource"><stRef:instanceID>a</stRef:instanceID></rdf:li></rdf:Seq></xmpMM:History>
            </rdf:Description>
          </rdf:RDF>
        </x:xmpmeta>
        """;

    /// <summary>The PDF 2.0 fixture with /Info and XMP.</summary>
    private PdfDocument? _document;

    /// <summary>Opens the fixture.</summary>
    /// <exception cref="InvalidOperationException">The fixture does not resolve to the XMP title.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.BuildWithTrailer(
            "/Info 5 0 R",
            "<< /Type /Catalog /Pages 2 0 R /Version /2.0 /Metadata 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", Packet),
            "<< /Title (Info title) /Author (Info author) >>");
        _document = PdfDocumentReader.Open(bytes, null);
        if (PdfDocumentMetadata.GetResolvedInfo(_document).Title != XmpTitle)
        {
            throw new InvalidOperationException("The metadata benchmark fixture did not resolve to the XMP title.");
        }
    }

    /// <summary>Closes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Reads the /Info dictionary alone.</summary>
    /// <returns>The title and author length.</returns>
    [Benchmark(Baseline = true)]
    public int ReadInfo()
    {
        var info = PdfDocumentMetadata.GetInfo(_document!);
        return (info.Title?.Length ?? 0) + (info.Author?.Length ?? 0);
    }

    /// <summary>Reads the information a viewer shows: /Info combined with the XMP packet.</summary>
    /// <returns>The title and author length.</returns>
    [Benchmark]
    public int ReadResolvedInfo()
    {
        var info = PdfDocumentMetadata.GetResolvedInfo(_document!);
        return (info.Title?.Length ?? 0) + (info.Author?.Length ?? 0);
    }

    /// <summary>Parses a packet with a language alternative, arrays, dates and structures.</summary>
    /// <returns>The title length.</returns>
    [Benchmark]
    public int ReadStructuredXmp() => PdfDocumentMetadata.GetXmp(_document!)?.Title?.Length ?? 0;
}
