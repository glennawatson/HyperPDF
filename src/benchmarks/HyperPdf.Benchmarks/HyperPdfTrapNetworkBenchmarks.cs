// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the trap network check every save runs: on an edited 100 page document with no trap network (the cost
/// every save pays), on a trapped page whose network still matches, checking one network, and removing an out of date
/// network then undoing the removal.
/// </summary>
public class HyperPdfTrapNetworkBenchmarks
{
    /// <summary>The pages in the document without trap networks.</summary>
    private const int Pages = 100;

    /// <summary>The content written by the edits.</summary>
    private static readonly byte[] EditedContent = "BT /F1 24 Tf 72 650 Td (Edited) Tj ET"u8.ToArray();

    /// <summary>An edited document without trap networks.</summary>
    private PdfDocument _plain = null!;

    /// <summary>A trapped document edited on its untrapped page, so its network is kept.</summary>
    private PdfDocument _kept = null!;

    /// <summary>A trapped document whose trapped page content was edited, so its network is removed.</summary>
    private PdfDocument _stale = null!;

    /// <summary>The trapped page of <see cref="_kept"/>.</summary>
    private PdfTrapNetworkPage _page = null!;

    /// <summary>The edits of <see cref="_kept"/>.</summary>
    private PdfTrapNetworkEdits _edits = null!;

    /// <summary>Opens and edits the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var page = PdfDocumentPages.GetPage(_plain, 0).Dictionary.Clone();
        page.Set(KnownName.Rotate, PdfValue.FromInteger(0));
        StoreEditing.Replace(_plain.Objects, PdfDocumentPages.GetPage(_plain, 0).Id, PdfValue.FromDictionary(page));
        _kept = Open(TrapNetworkPdf.SecondContentObject);
        _stale = Open(TrapNetworkPdf.ContentObject);
        _page = PdfTrapNetworkPages.Find(_kept.Objects, CancellationToken.None)![0];
        _edits = new(_kept.Objects);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _kept.Dispose();
        _stale.Dispose();
    }

    /// <summary>Runs the save check on an edited document with no trap network.</summary>
    /// <returns>The number of changes, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int CheckWithoutNetworks()
    {
        var report = new PdfSaveReport();
        PdfTrapNetworkGuard.Apply(_plain.Objects, true, report, CancellationToken.None);
        return report.TrapNetworks.Count;
    }

    /// <summary>Runs the save check on a trapped document whose network still matches.</summary>
    /// <returns>The number of changes.</returns>
    [Benchmark]
    public int CheckKeptNetwork()
    {
        var report = new PdfSaveReport();
        PdfTrapNetworkGuard.Apply(_kept.Objects, true, report, CancellationToken.None);
        return report.TrapNetworks.Count;
    }

    /// <summary>Checks one page's trap network.</summary>
    /// <returns>The problems found.</returns>
    [Benchmark]
    public PdfTrapNetworkProblems ValidateNetwork() => PdfTrapNetworkValidator.Check(_page, _edits).Problems;

    /// <summary>Removes an out of date network as a save does, then undoes the removal so the next call removes it again.</summary>
    /// <returns>Whether the removal was undone.</returns>
    [Benchmark]
    public bool RemoveStaleNetwork()
    {
        var report = new PdfSaveReport();
        PdfTrapNetworkGuard.Apply(_stale.Objects, true, report, CancellationToken.None);
        return PdfDocumentEditing.Undo(_stale);
    }

    /// <summary>Opens the trapped sample and edits one content stream.</summary>
    /// <param name="content">The object number of the content stream edited.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(int content)
    {
        var document = PdfDocumentReader.Open(TrapNetworkPdf.Build(TrapNetworkPdf.Versioned), null);
        StoreEditing.Replace(document.Objects, new(content, 0), PdfValue.FromStream(new(new PdfDictionary(document.Objects), EditedContent)));
        return document;
    }
}
