// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures prepared tagged and inferred list numbering without rebuilding reading nodes.</summary>
public class ListNumberingReadingBenchmarks
{
    /// <summary>The inferred list, retained by this benchmark instance.</summary>
    private readonly PdfSemanticNode _inferred = new(PdfSemanticRole.List, PdfNodeOrigin.Inferred, 0) { ListNumbering = PdfListNumbering.Decimal };

    /// <summary>The parsed document retained during measurement.</summary>
    private PdfDocument? _document;

    /// <summary>The prepared tagged reading node.</summary>
    private PdfSemanticNode? _tagged;

    /// <summary>Gets or sets whether the numbering belongs to the list's parent.</summary>
    [Params(false, true)]
    public bool Inherited { get; set; }

    /// <summary>Parses the source and prepares the nodes outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        const string attributes = "/A << /O /List /ListNumbering /Decimal >>";
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [7 0 R] >>",
            "<< /Type /StructTreeRoot /K 4 0 R >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /Div /P 4 0 R /K 6 0 R {(Inherited ? attributes : string.Empty)} >>",
            $"<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /ActualText (item) {(Inherited ? string.Empty : attributes)} >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>",
        ]);
        _document = PdfDocumentReader.Open(bytes, null);
        var nodes = new List<PdfSemanticNode>();
        new TaggedNodeBuilder(_document, 0).Build(PdfDocumentTagged.GetStructureTree(_document)!, nodes);
        _tagged = nodes[0].Children[0].Children[0];
    }

    /// <summary>Closes the source document after measurement.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Reads numbering from a prepared tagged list.</summary>
    /// <returns>The numbering scheme.</returns>
    [Benchmark]
    public PdfListNumbering ReadTagged() => _tagged!.ListNumbering;

    /// <summary>Reads the numbering inferred from the layout.</summary>
    /// <returns>The numbering scheme.</returns>
    [Benchmark]
    public PdfListNumbering ReadInferred() => _inferred.ListNumbering;

    /// <summary>Builds tagged reading nodes, including their inherited list attributes.</summary>
    /// <returns>The list's numbering scheme.</returns>
    [Benchmark]
    public PdfListNumbering BuildTagged()
    {
        var nodes = new List<PdfSemanticNode>();
        new TaggedNodeBuilder(_document!, 0).Build(PdfDocumentTagged.GetStructureTree(_document!)!, nodes);
        return nodes[0].Children[0].Children[0].ListNumbering;
    }
}
