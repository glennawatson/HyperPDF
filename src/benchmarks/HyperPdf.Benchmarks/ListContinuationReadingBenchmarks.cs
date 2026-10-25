// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures continuation attribute decoding and prepared tagged-node construction.</summary>
public class ListContinuationReadingBenchmarks
{
    /// <summary>The document kept open during measurement.</summary>
    private PdfDocument _document = null!;

    /// <summary>The parsed structure tree.</summary>
    private PdfStructureTree _tree = null!;

    /// <summary>The reusable attribute reader.</summary>
    private StructureAttributeReader _reader = null!;

    /// <summary>The list's dictionary.</summary>
    private PdfDictionary _element = null!;

    /// <summary>The retained attributes used by the reading getters.</summary>
    private PdfStructureAttributes _attributes = null!;

    /// <summary>Gets or sets the attribute owner representation.</summary>
    [Params("Omitted", "List", "Namespace", "PrivateNamespace")]
    public string Owner { get; set; } = "Omitted";

    /// <summary>Prepares source objects outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var owner = Owner switch
        {
            "Namespace" => "/O /NSO /NS << /Type /Namespace /NS (List) >>",
            "PrivateNamespace" => "/O /NSO /NS << /Type /Namespace /NS (https://example.test/list-attributes) >>",
            _ => "/O /List",
        };
        var attributes = Owner == "Omitted" ? string.Empty : $"/A << {owner} /ContinuedList true /ContinuedFrom <00ff3132> >>";
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [7 0 R] >>",
            "<< /Type /StructTreeRoot /K 4 0 R >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
            "<< /Type /StructElem /S /Div /P 4 0 R /K 6 0 R >>",
            $"<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /ActualText (item) {attributes} >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>",
        ]);
        _document = PdfDocumentReader.Open(bytes, null);
        _tree = PdfDocumentTagged.GetStructureTree(_document)!;
        var list = _tree.Roots[0].Kids[0].Element!.Kids[0].Element!;
        _element = list.Dictionary;
        _attributes = list.Attributes;
        var names = _document.Objects.Names;
        _reader = new(new TaggedNames(names), names, null);
    }

    /// <summary>Releases the source document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Decodes the effective attributes into a fresh snapshot.</summary>
    /// <returns>The reading attributes.</returns>
    [Benchmark]
    public PdfStructureAttributes ReadAttributes() => _reader.Read(_element);

    /// <summary>Reads the retained flag without rebuilding the attributes.</summary>
    /// <returns>The specified flag, or null for omission.</returns>
    [Benchmark]
    public bool? ReadFlag() => _attributes.ContinuedList;

    /// <summary>Reads the retained byte-string span without decoding or copying it.</summary>
    /// <returns>The identity's byte length.</returns>
    [Benchmark]
    public int ReadIdLength() => _attributes.ContinuedFromId.Length;

    /// <summary>Builds fresh reading nodes using the prepared source tree.</summary>
    /// <returns>The number of top-level reading nodes.</returns>
    [Benchmark]
    public int BuildTagged()
    {
        var nodes = new List<PdfSemanticNode>();
        new TaggedNodeBuilder(_document, 0).Build(_tree, nodes, CancellationToken.None);
        return nodes.Count;
    }
}
