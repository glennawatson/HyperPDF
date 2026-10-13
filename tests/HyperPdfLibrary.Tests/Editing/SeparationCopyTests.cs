// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Tests.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that copying, extracting, deleting and reordering pages keep separation dictionaries valid.</summary>
public sealed class SeparationCopyTests
{
    /// <summary>The pages in the flat target document.</summary>
    private const int TargetPages = 2;

    /// <summary>The second page of the first printed page.</summary>
    private const int Magenta = 1;

    /// <summary>The third page of the first printed page.</summary>
    private const int Yellow = 2;

    /// <summary>The last page of the first printed page.</summary>
    private const int Black = 3;

    /// <summary>The separations of a printed page left after one is lost.</summary>
    private const int ThreeKept = 3;

    /// <summary>The separations of a printed page left after two are lost.</summary>
    private const int TwoKept = 2;

    /// <summary>The groups after part of a printed page is duplicated in place.</summary>
    private const int GroupsAfterDuplicate = 3;

    /// <summary>The pages of the damaged document's only group.</summary>
    private const int DamagedGroupPages = 2;

    /// <summary>The selection that copies the first three separations of printed page 1 and the ordinary page.</summary>
    private static readonly int[] PartialSelection = [0, Magenta, Yellow, SeparationPdf.OrdinaryPage];

    /// <summary>The selection that deletes the black separation of printed page 1.</summary>
    private static readonly int[] BlackOnly = [Black];

    /// <summary>Gets every page of the preseparated document in reverse.</summary>
    private static int[] Reversed => [.. SeparationPdf.AllPages.Reverse()];

    /// <summary>Extracting every page keeps both printed pages whole, with nothing to report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractingEveryPageKeepsGroups()
    {
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var report = new PdfPageEditReport();
        var bytes = PdfDocumentPageOperations.ExtractPages(source, SeparationPdf.AllPages, report, CancellationToken.None);
        using var copy = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentSeparations.GetSeparations(copy);

        await Assert.That(report.SeparationChanges).IsEmpty();
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups.Length).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(SeparationTests.Colorants(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstColorants);
        await Assert.That(SeparationTests.Colorants(info.Groups[1])).IsEquivalentTo(SeparationPdf.SecondColorants);
        await Assert.That(info.Groups[0].Separations[0].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.Separation);
        await Assert.That(info.Groups[1].Separations[^1].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.DeviceN);
    }

    /// <summary>Extracting part of a printed page links the copied separations to each other and reports the loss.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractingPartOfAGroupRelinksAndReports()
    {
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var report = new PdfPageEditReport();
        var bytes = await PdfDocumentPageEditing.ExtractPagesAsync(source, PartialSelection, report, CancellationToken.None);
        using var copy = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentSeparations.GetSeparations(copy);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new PdfSeparationChange(0, SeparationPdf.ColorantsPerPage, ThreeKept, 0)]);
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups.Length).IsEqualTo(1);
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([0, Magenta, Yellow]);
    }

    /// <summary>A separation copied twice stays linked once; the second copy loses its dictionary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedCopyIsUnlinked()
    {
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var report = new PdfPageEditReport();
        var bytes = PdfDocumentPageOperations.ExtractPages(source, [0, .. SeparationPdf.FirstGroup], report, CancellationToken.None);
        using var copy = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentSeparations.GetSeparations(copy);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new PdfSeparationChange(0, SeparationPdf.ColorantsPerPage, SeparationPdf.ColorantsPerPage, 1)]);
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([0, Yellow, Black, SeparationPdf.SecondGroupStart]);
        await Assert.That(PdfDocumentSeparations.FindGroup(info, 1)).IsNull();
    }

    /// <summary>Inserting a whole printed page into another document keeps it linked through a save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertingAWholeGroupSurvivesSave()
    {
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        var report = new PdfPageEditReport();
        await PdfDocumentPageEditing.InsertPagesAsync(target, 1, source, SeparationPdf.SecondGroup, report, CancellationToken.None);
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        var info = PdfDocumentSeparations.GetSeparations(saved);

        await Assert.That(report.SeparationChanges).IsEmpty();
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([.. Enumerable.Range(1, SeparationPdf.ColorantsPerPage)]);
        await Assert.That(SeparationTests.Colorants(info.Groups[0])).IsEquivalentTo(SeparationPdf.SecondColorants);
    }

    /// <summary>Duplicating part of a printed page in place leaves the original linked and links the copies to each other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DuplicatingPartOfAGroupKeepsOriginal()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var report = new PdfPageEditReport();
        PdfDocumentPageOperations.InsertPages(document, SeparationPdf.PreseparatedPages, document, [0, Magenta], report, CancellationToken.None);
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var info = PdfDocumentSeparations.GetSeparations(saved);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new PdfSeparationChange(0, SeparationPdf.ColorantsPerPage, TwoKept, 0)]);
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups.Length).IsEqualTo(GroupsAfterDuplicate);
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstGroup);
        await Assert.That(SeparationTests.Indexes(info.Groups[^1])).IsEquivalentTo([.. Enumerable.Range(SeparationPdf.PreseparatedPages, TwoKept)]);
    }

    /// <summary>Deleting a separation relinks the rest of its printed page and reports it; the saved file reads cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingASeparationRelinksTheRest()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var report = new PdfPageEditReport();
        await PdfDocumentPageEditing.DeletePagesAsync(document, BlackOnly, report, CancellationToken.None);
        var live = PdfDocumentSeparations.GetSeparations(document);
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var info = PdfDocumentSeparations.GetSeparations(saved);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new PdfSeparationChange(0, SeparationPdf.ColorantsPerPage, ThreeKept, 0)]);
        await Assert.That(live.Findings).IsEmpty();
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([0, Magenta, Yellow]);
        await Assert.That(SeparationTests.Indexes(info.Groups[1])).IsEquivalentTo([.. Enumerable.Range(Black, SeparationPdf.ColorantsPerPage)]);
        await Assert.That(SeparationTests.Colorants(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstColorants[..ThreeKept]);
    }

    /// <summary>Undoing a deletion restores the original groups.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoingADeletionRestoresGroups()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        PdfDocumentPageOperations.DeletePages(document, [0, SeparationPdf.SecondGroupStart]);
        _ = PdfDocumentEditing.Undo(document);
        var info = PdfDocumentSeparations.GetSeparations(document);

        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstGroup);
        await Assert.That(SeparationTests.Indexes(info.Groups[1])).IsEquivalentTo(SeparationPdf.SecondGroup);
    }

    /// <summary>Reordering moves the separation pages without breaking their links.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderingKeepsGroups()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        PdfDocumentPageOperations.ReorderPages(document, Reversed);
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var info = PdfDocumentSeparations.GetSeparations(saved);

        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups.Length).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([.. Enumerable.Range(1, SeparationPdf.ColorantsPerPage).Reverse()]);
        await Assert.That(PdfDocumentSeparations.FindGroup(info, 0)).IsNull();
    }

    /// <summary>A damaged separation dictionary is not copied, a page without a colourant is unlinked, and both are reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDictionaryIsDroppedOnCopy()
    {
        string?[] pages = ["/DeviceColorant /Black", "/Pages [4 0 R 5 0 R] /DeviceColorant /Cyan /ColorSpace [/Separation /Other /DeviceGray 6 0 R]", "/Pages [4 0 R 5 0 R]"];
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePages(pages, "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>"), null);
        var report = new PdfPageEditReport();
        var bytes = PdfDocumentPageOperations.ExtractPages(source, [.. Enumerable.Range(0, pages.Length)], report, CancellationToken.None);
        using var copy = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentSeparations.GetSeparations(copy);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new PdfSeparationChange(0, DamagedGroupPages, 1, 1), new PdfSeparationChange(-1, 0, 0, 1)]);
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(SeparationTests.Indexes(info.Groups[0])).IsEquivalentTo([1]);
        await Assert.That(info.Groups[0].Separations[0].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.None);
    }

    /// <summary>The single-page copy used for printing leaves the separation dictionary out, so no link names a page left behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SinglePageCopyLeavesDictionaryOut()
    {
        using var source = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var builder = new PdfDocumentBuilder();
        _ = new PdfPageImporter(builder, source).ImportPage(PdfDocumentPages.GetPage(source, 0), PdfAnnotationFilter.All);
        using var copy = PdfDocumentReader.Open(builder.ToArray(), null);
        await Assert.That(PdfDocumentSeparations.GetSeparations(copy)).IsSameReferenceAs(PdfSeparationInfo.Empty);
    }
}
