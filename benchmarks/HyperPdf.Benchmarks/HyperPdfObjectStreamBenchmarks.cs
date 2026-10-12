// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures HyperPDF on a document whose page dictionaries live in many small object streams: opening it, reading its
/// objects in a scattered order, and rendering scattered pages. The decoded object streams are kept in a bounded cache,
/// so these show what decoding them again costs. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfObjectStreamBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 600;

    /// <summary>The objects packed in each object stream.</summary>
    private const int ObjectsPerStream = 10;

    /// <summary>The step that walks the pages in a scattered order; it shares no factor with the page count.</summary>
    private const int ScatterStep = 197;

    /// <summary>The pages rendered by <see cref="RenderScatteredPages"/>.</summary>
    private const int RenderedPages = 6;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The render scale (100% at 96 DPI).</summary>
    private const float Scale = 96F / 72F;

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[TileGrid.TileSize * TileGrid.TileSize * BytesPerPixel];

    /// <summary>The HyperPDF engine.</summary>
    private readonly HyperPdfEngine _engine = new();

    /// <summary>The document bytes.</summary>
    private byte[] _file = [];

    /// <summary>The document on disk.</summary>
    private string _path = string.Empty;

    /// <summary>Writes the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _file = ObjectStreamPdf.Create(PageCount, ObjectsPerStream);
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-objstm-bench-{Environment.ProcessId}.pdf");
        File.WriteAllBytes(_path, _file);
    }

    /// <summary>Deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup() => File.Delete(_path);

    /// <summary>Opens the document, which reads the page tree out of the object streams.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int OpenDocument()
    {
        using var document = PdfDocumentReader.Open(_file, null);
        return document.PageCount;
    }

    /// <summary>Opens the object store and reads every page dictionary and its content stream in a scattered order.</summary>
    /// <returns>The pages read.</returns>
    [Benchmark]
    public int RandomObjectAccess()
    {
        using var store = StoreOpening.Open(_file, null);
        var read = 0;
        for (var i = 0; i < PageCount; i++)
        {
            if (StoreReading.GetObject(
        store,
        new(
        ObjectStreamPdf.PageObject((i * ScatterStep) % PageCount),
        0)).AsDictionary() is { } dictionary
        && dictionary.Get(KnownName.Contents).AsStream() is not null)
            {
                read++;
            }
        }

        return read;
    }

    /// <summary>Opens the file and renders the first tile of pages scattered through the document.</summary>
    /// <returns>The tiles rendered.</returns>
    [Benchmark]
    public int RenderScatteredPages()
    {
        using var document = _engine.Open(_path, null);
        var rendered = 0;
        var tileSize = TileGrid.TileSize;
        for (var i = 0; i < RenderedPages; i++)
        {
            var info = new PageRenderInfo((i * ScatterStep) % PageCount, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
            if (document.Render(info, new(_pixels, tileSize, tileSize, tileSize * BytesPerPixel)))
            {
                rendered++;
            }
        }

        return rendered;
    }
}
