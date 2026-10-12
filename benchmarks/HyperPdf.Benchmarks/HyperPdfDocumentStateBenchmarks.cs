// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures page lookup, navigation, metadata reading and page edit state in HyperPDF.</summary>
public class HyperPdfDocumentStateBenchmarks
{
    /// <summary>The pages in the page lookup and editing fixture.</summary>
    private const int Pages = 50;

    /// <summary>The page used for warm lookup.</summary>
    private const int MiddlePage = Pages / 2;

    /// <summary>The expected title in the XMP fixture.</summary>
    private const string XmpTitle = "State benchmark";

    /// <summary>A small XMP packet with a title.</summary>
    private const string XmpPacket = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
          <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/">
              <dc:title><rdf:Alt><rdf:li xml:lang="x-default">State benchmark</rdf:li></rdf:Alt></dc:title>
            </rdf:Description>
          </rdf:RDF>
        </x:xmpmeta>
        """;

    /// <summary>The order used for page edits.</summary>
    private readonly int[] _reverse = new int[Pages];

    /// <summary>The rich navigation fixture, also used by cold navigation.</summary>
    private byte[] _navigationBytes = [];

    /// <summary>The page lookup and information fixture.</summary>
    private PdfDocument? _pages;

    /// <summary>The warmed outline, link and label fixture.</summary>
    private PdfDocument? _navigation;

    /// <summary>The XMP fixture.</summary>
    private PdfDocument? _metadata;

    /// <summary>The page edit fixture.</summary>
    private PdfDocument? _editing;

    /// <summary>Opens the fixtures and warms the cached paths.</summary>
    /// <exception cref="InvalidOperationException">A fixture does not contain the expected data.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var pageBytes = TestPdf.Create(Pages);
        _navigationBytes = CarryTestDocuments.CreateBook();
        var metadataBytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", XmpPacket));

        _pages = PdfDocumentReader.Open(pageBytes, null);
        _navigation = PdfDocumentReader.Open(_navigationBytes, null);
        _metadata = PdfDocumentReader.Open(metadataBytes, null);
        _editing = PdfDocumentReader.Open(pageBytes, null);

        for (var i = 0; i < Pages; i++)
        {
            _reverse[i] = Pages - 1 - i;
        }

        if (PdfDocumentPages.GetPageIndex(_pages, PdfDocumentPages.GetPage(_pages, MiddlePage).Id) != MiddlePage
            || PdfDocumentNavigation.GetOutline(_navigation).Count != 2
            || PdfDocumentLinks.GetLinks(_navigation, 0).Count != 2
            || PdfDocumentLabels.GetPageLabel(_navigation, 0) != "i"
            || PdfDocumentMetadata.GetInfo(_pages).Title != TestPdf.Title
            || PdfDocumentMetadata.GetXmp(_metadata)?.Title != XmpTitle)
        {
            throw new InvalidOperationException("The document state benchmark fixtures did not load as expected.");
        }
    }

    /// <summary>Closes the fixtures.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pages?.Dispose();
        _navigation?.Dispose();
        _metadata?.Dispose();
        _editing?.Dispose();
    }

    /// <summary>Gets a cached page and resolves its object id to an index.</summary>
    /// <returns>The page index.</returns>
    [Benchmark(Baseline = true)]
    public int WarmPageLookup()
    {
        var page = PdfDocumentPages.GetPage(_pages!, MiddlePage);
        return PdfDocumentPages.GetPageIndex(_pages!, page.Id);
    }

    /// <summary>Reads cached outlines, links and page labels.</summary>
    /// <returns>The total number of entries and label characters.</returns>
    [Benchmark]
    public int WarmNavigation() => ReadNavigation(_navigation!);

    /// <summary>Opens a document and reads its outlines, links and page labels before their caches are warm.</summary>
    /// <returns>The total number of entries and label characters.</returns>
    [Benchmark]
    public int ColdNavigation()
    {
        using var document = PdfDocumentReader.Open(_navigationBytes, null);
        return ReadNavigation(document);
    }

    /// <summary>Reads document information from a warm document.</summary>
    /// <returns>The title and author length.</returns>
    [Benchmark]
    public int ReadInfo()
    {
        var info = PdfDocumentMetadata.GetInfo(_pages!);
        return (info.Title?.Length ?? 0) + (info.Author?.Length ?? 0);
    }

    /// <summary>Parses the catalog's XMP packet from a warm document.</summary>
    /// <returns>The title length.</returns>
    [Benchmark]
    public int ReadXmp() => PdfDocumentMetadata.GetXmp(_metadata!)?.Title?.Length ?? 0;

    /// <summary>Reorders pages, resolves the edited page set, undoes the edit and resolves the restored page set.</summary>
    /// <returns>The two page indexes and restored page count.</returns>
    /// <exception cref="InvalidOperationException">The edit cannot be undone.</exception>
    [Benchmark]
    public int PageEditUndo()
    {
        var document = _editing!;
        PdfDocumentPageOperations.ReorderPages(document, _reverse);
        var movedIndex = PdfDocumentPages.GetPageIndex(document, PdfDocumentPages.GetPage(document, 0).Id);
        if (!PdfDocumentEditing.Undo(document))
        {
            throw new InvalidOperationException("The page reorder could not be undone.");
        }

        return movedIndex + PdfDocumentPages.GetPageIndex(document, PdfDocumentPages.GetPage(document, 0).Id) + document.PageCount;
    }

    /// <summary>Reads all three navigation structures in the book fixture.</summary>
    /// <param name="document">The book document.</param>
    /// <returns>The entry count and label length.</returns>
    private static int ReadNavigation(PdfDocument document) =>
        PdfDocumentNavigation.GetOutline(document).Count + PdfDocumentLinks.GetLinks(document, 0).Count + (PdfDocumentLabels.GetPageLabel(document, 0)?.Length ?? 0);
}
