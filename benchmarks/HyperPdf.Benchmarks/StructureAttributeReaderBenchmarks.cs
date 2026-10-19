// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures Table attribute decoding from a prepared dictionary, including its retained snapshot.</summary>
public class StructureAttributeReaderBenchmarks
{
    /// <summary>The span larger than the reader's former ceiling.</summary>
    private const int LargeSpan = 1001;

    /// <summary>The owned source document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The reusable attribute reader.</summary>
    private StructureAttributeReader _reader = null!;

    /// <summary>The parsed structure element dictionary.</summary>
    private PdfDictionary _element = null!;

    /// <summary>Gets or sets the number of associated header IDs.</summary>
    [Params(1, 257)]
    public int HeaderCount { get; set; }

    /// <summary>Gets or sets how the attribute object identifies its owner.</summary>
    [Params("Table", "Namespace", "Private")]
    public string OwnerKind { get; set; } = "Table";

    /// <summary>Prepares the attribute dictionary outside the measured decoding operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var headers = new StringBuilder();
        for (var i = 0; i < HeaderCount; i++)
        {
            _ = headers.Append(CultureInfo.InvariantCulture, $"(header-{i}) ");
        }

        var owner = OwnerKind == "Namespace" ? "/O /NSO /NS 5 0 R" : $"/O /{OwnerKind}";
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            "<< /Type /StructTreeRoot /K 4 0 R >>",
            $"<< /Type /StructElem /S /TD /P 3 0 R /A << {owner} /RowSpan {LargeSpan} /ColSpan {LargeSpan} /Headers [{headers}] >> >>",
            "<< /Type /Namespace /NS (Table) >>",
        ]);
        _document = PdfDocumentReader.Open(bytes, null);
        _element = PdfDocumentTagged.GetStructureTree(_document)!.Roots[0].Dictionary;
        var names = _document.Objects.Names;
        _reader = new(new TaggedNames(names), names, null);
    }

    /// <summary>Releases the source document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads effective Table attributes according to their declared owner.</summary>
    /// <returns>The decoded attributes.</returns>
    [Benchmark]
    public PdfStructureAttributes Read() => _reader.Read(_element);
}
