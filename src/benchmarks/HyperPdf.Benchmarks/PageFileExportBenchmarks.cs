// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Exporting;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures exporting a text page as pictures and vector files; allocations come from the EventPipe trace.</summary>
public class PageFileExportBenchmarks
{
    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 30;

    /// <summary>The pages in the document.</summary>
    private const int Pages = 3;

    /// <summary>The resolution of the pictures, in dots to the inch.</summary>
    private const int Dpi = 72;

    /// <summary>The destination, reused so only the exporter's allocations are measured.</summary>
    private readonly MemoryStream _destination = new();

    /// <summary>The first page.</summary>
    private readonly int[] _first = [0];

    /// <summary>Every page.</summary>
    private readonly int[] _all = [0, 1, 2];

    /// <summary>The document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The document's exporter.</summary>
    private IPageFileExporter _exporter = null!;

    /// <summary>Opens a text document and draws it once.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _document = new(TestPdf.CreateTextFilled(Pages, Lines));
        _exporter = DocumentFeature<IPageFileExporter>.Cast(_document.Document)!;
        _ = await Export(_first, PageFileFormat.Png);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _destination.Dispose();
    }

    /// <summary>Exports a page as a PNG picture.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public Task<long> Png() => Export(_first, PageFileFormat.Png);

    /// <summary>Exports a page as a JPEG picture.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public Task<long> Jpeg() => Export(_first, PageFileFormat.Jpeg);

    /// <summary>Exports a page as an SVG drawing.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public Task<long> Svg() => Export(_first, PageFileFormat.Svg);

    /// <summary>Exports a page as Encapsulated PostScript.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public Task<long> Eps() => Export(_first, PageFileFormat.EncapsulatedPostScript);

    /// <summary>Exports every page as one PostScript file.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public Task<long> PostScriptDocument() => Export(_all, PageFileFormat.PostScript);

    /// <summary>Exports pages to the reused destination.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="format">The format.</param>
    /// <returns>The file size.</returns>
    private async Task<long> Export(int[] pages, PageFileFormat format)
    {
        _destination.SetLength(0);
        _ = await _exporter.ExportAsync(pages, format, new(Dpi, true), _destination, CancellationToken.None);
        return _destination.Length;
    }
}
