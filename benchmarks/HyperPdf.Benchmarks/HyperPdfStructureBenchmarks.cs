// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Reading;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures HyperPDF's accessibility structure: reading the structure tree, recording a page's marked content, building
/// the tagged reading nodes, inferring an untagged page's order, and producing the reading view's tagged blocks.
/// <see cref="ReadingOrderBenchmarks.ReadTags"/> measures PDFium's tagged blocks on the same document. Allocations come
/// from the EventPipe trace.
/// </summary>
public class HyperPdfStructureBenchmarks
{
    /// <summary>The pages of the untagged article.</summary>
    private const int ArticlePages = 2;

    /// <summary>The tagged blocks, reused.</summary>
    private readonly List<TaggedBlock> _blocks = [];

    /// <summary>The tagged document.</summary>
    private PdfDocument _tagged = null!;

    /// <summary>The untagged article.</summary>
    private PdfDocument _article = null!;

    /// <summary>The tagged document's first page.</summary>
    private PdfPage _page = null!;

    /// <summary>The tagged document's structure tree.</summary>
    private PdfStructureTree _tree = null!;

    /// <summary>The tagged document's file, for the adapter.</summary>
    private string _path = string.Empty;

    /// <summary>The tagged document opened through the adapter.</summary>
    private HyperPdfDocument _adapter = null!;

    /// <summary>Opens the documents and installs a font that gives glyphs their text.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = TestPdf.CreateTagged();
        var options = new PdfOpenOptions { FontFactory = static dictionary => new StructureBenchmarkFont(dictionary) };
        _tagged = PdfDocumentReader.OpenWith(bytes, options);
        _article = PdfDocumentReader.OpenWith(TestPdf.CreateArticle(ArticlePages), options);
        _page = PdfDocumentPages.GetPage(_tagged, 0);
        _tree = PdfDocumentTagged.GetStructureTree(_tagged)!;
        _path = Path.Combine(Path.GetTempPath(), $"structure-benchmark-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, bytes);
        _adapter = (HyperPdfDocument)new HyperPdfEngine(static dictionary => new StructureBenchmarkFont(dictionary)).Open(_path, null);
        _ = HyperPdfTagged.GetTaggedBlocksNative(_adapter, 0, _blocks);
        _ = PdfDocumentTagged.GetMarkedContent(_article, 0);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _adapter.Dispose();
        _tagged.Dispose();
        _article.Dispose();
        File.Delete(_path);
    }

    /// <summary>Reads the whole structure tree: elements, role maps, attributes and ids.</summary>
    /// <returns>The element count.</returns>
    [Benchmark]
    public int LoadTree() => PdfStructureTree.Load(_tagged)!.ElementCount;

    /// <summary>Runs a page's content and records each glyph with its marked content.</summary>
    /// <returns>The glyph count.</returns>
    [Benchmark]
    public int RecordMarkedContent() => MarkedContentRecorder.Record(_tagged, _page).GlyphCount;

    /// <summary>Builds a tagged page's reading nodes from the tree and its recorded content.</summary>
    /// <returns>The top-level node count.</returns>
    [Benchmark]
    public int BuildTaggedNodes()
    {
        var nodes = new List<PdfSemanticNode>();
        new TaggedNodeBuilder(_tagged, 0).Build(_tree, nodes);
        return nodes.Count;
    }

    /// <summary>Works out an untagged page's reading order from its recorded layout.</summary>
    /// <returns>The node count.</returns>
    [Benchmark]
    public int InferLayout() => PdfReadingStructure.InferLayout(_article, 0).Nodes.Count;

    /// <summary>Produces the reading view's tagged blocks natively, including matching glyphs to the page's characters.</summary>
    /// <returns>The block count.</returns>
    [Benchmark]
    public int NativeTaggedBlocks()
    {
        _blocks.Clear();
        _ = HyperPdfTagged.GetTaggedBlocksNative(_adapter, 0, _blocks);
        return _blocks.Count;
    }
}
