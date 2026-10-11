// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures warm adapter text bounds and search queries.</summary>
public class HyperPdfTextQueryBenchmarks
{
    /// <summary>The caller-owned rectangle results.</summary>
    private readonly List<PageRect> _bounds = [];

    /// <summary>The caller-owned search results.</summary>
    private readonly List<TextMatch> _matches = [];

    /// <summary>The open tagged document.</summary>
    private HyperPdfDocument _document = null!;

    /// <summary>The file opened by the adapter.</summary>
    private string _path = string.Empty;

    /// <summary>Opens a tagged page and warms its text page and caller-owned result lists.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-text-query-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(_path, TestPdf.CreateTagged(), CancellationToken.None);
        _document = (HyperPdfDocument)await new HyperPdfEngine().OpenAsync(_path, null, CancellationToken.None);
        _document.GetTextBounds(0, 0, 1, _bounds);
        _document.Find(0, TestPdf.TaggedHeading, default, _matches);
    }

    /// <summary>Closes and removes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Gets the first character's page rectangle.</summary>
    /// <returns>The number of rectangles.</returns>
    [Benchmark]
    public int TextBounds()
    {
        _bounds.Clear();
        _document.GetTextBounds(0, 0, 1, _bounds);
        return _bounds.Count;
    }

    /// <summary>Finds a tagged heading on the warmed page.</summary>
    /// <returns>The number of matches.</returns>
    [Benchmark]
    public int FindHeading()
    {
        _matches.Clear();
        _document.Find(0, TestPdf.TaggedHeading, default, _matches);
        return _matches.Count;
    }
}
