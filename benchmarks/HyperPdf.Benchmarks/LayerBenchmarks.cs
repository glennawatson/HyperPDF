// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading layers, switching one, and drawing a page while layers differ; allocations come from the EventPipe trace.</summary>
public class LayerBenchmarks
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The tile size rendered.</summary>
    private const int Tile = 512;

    /// <summary>The layered document's bytes.</summary>
    private byte[] _file = [];

    /// <summary>The open document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The document's layers.</summary>
    private ILayerSource _layers = null!;

    /// <summary>The hidden layer's id.</summary>
    private int _notes;

    /// <summary>The tile buffer.</summary>
    private byte[] _pixels = [];

    /// <summary>The visibility the toggle benchmark sets next.</summary>
    private bool _show;

    /// <summary>Opens the layered document and shows its hidden layer, so pages draw from the layer view.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _file = TestPdf.CreateWithLayers();
        _document = new(_file);
        _layers = DocumentFeature<ILayerSource>.Cast(_document.Document)!;
        _notes = _layers.GetLayers()[1].Id;
        _ = _layers.SetLayerVisible(_notes, true);
        _pixels = new byte[Tile * Tile * BytesPerPixel];
        _ = RenderTile();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads a document's layers from its bytes.</summary>
    /// <returns>The layer count.</returns>
    [Benchmark]
    public int ReadLayers() => PdfLayers.Read(_file).Count;

    /// <summary>Shows or hides a layer, rebuilding the view pages are drawn from.</summary>
    /// <returns>Whether it applied.</returns>
    [Benchmark]
    public bool ToggleLayer()
    {
        _show = !_show;
        return _layers.SetLayerVisible(_notes, _show);
    }

    /// <summary>Draws a tile while a layer differs from the document's setting.</summary>
    /// <returns>Whether it rendered.</returns>
    [Benchmark]
    public bool RenderTileWithLayers() => RenderTile();

    /// <summary>Renders the top-left tile of the page.</summary>
    /// <returns>Whether it rendered.</returns>
    private bool RenderTile() =>
        _document.Document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(_pixels, Tile, Tile, Tile * BytesPerPixel));
}
