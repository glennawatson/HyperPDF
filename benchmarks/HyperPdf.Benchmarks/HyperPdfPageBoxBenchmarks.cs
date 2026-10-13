// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures setting and undoing the five page boundaries of a page, and reading them from the page.</summary>
public class HyperPdfPageBoxBenchmarks
{
    /// <summary>The pages in the fixture.</summary>
    private const int Pages = 100;

    /// <summary>The page edited.</summary>
    private const int EditedPage = 50;

    /// <summary>The boxes set, in order.</summary>
    private static readonly PdfPageBox[] Kinds = [PdfPageBox.Media, PdfPageBox.Crop, PdfPageBox.Bleed, PdfPageBox.Trim, PdfPageBox.Art];

    /// <summary>The box written for every kind; it lies inside the fixture's media box.</summary>
    private static readonly PdfRectangle Box = new(20, 20, 500, 700);

    /// <summary>The document edited and restored by each operation.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the fixture and warms the edit.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _ = SetAndUndoFiveBoxes();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Sets all five boxes of a page, then undoes each edit.</summary>
    /// <returns>The art box width after the undo.</returns>
    [Benchmark]
    public float SetAndUndoFiveBoxes()
    {
        foreach (var kind in Kinds)
        {
            _ = PdfDocumentPageBoxes.SetBox(_document, EditedPage, kind, Box);
        }

        foreach (var kind in Kinds)
        {
            _ = kind;
            _ = PdfDocumentEditing.Undo(_document);
        }

        return PdfDocumentPages.GetPage(_document, EditedPage).ArtBox.Width;
    }

    /// <summary>Reads the five boundaries from an already loaded page.</summary>
    /// <returns>The sum of their widths.</returns>
    [Benchmark]
    public float ReadFiveBoxes()
    {
        var page = PdfDocumentPages.GetPage(_document, EditedPage);
        var width = 0F;
        foreach (var kind in Kinds)
        {
            width += page.GetBox(kind).Width;
        }

        return width;
    }
}
