// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for reading preseparated pages (ISO 32000-2 §14.11.4).</summary>
public sealed class SeparationTests
{
    /// <summary>The most separation pages the reader reads.</summary>
    private const int MaxSeparationPages = 10_000;

    /// <summary>More <c>/Pages</c> entries than the reader reads.</summary>
    private const int TooManyEntries = 1_100;

    /// <summary>In the malformed document, the page whose array lists a page and a non-page.</summary>
    private const int StrayPage = 0;

    /// <summary>In the malformed document, the page with no colourant.</summary>
    private const int NoColorantPage = 1;

    /// <summary>In the malformed document, the page whose colourant is not its colour space's, and which a later page also lists.</summary>
    private const int MismatchPage = 2;

    /// <summary>In the malformed document, the page with no <c>/Pages</c> array.</summary>
    private const int NoPagesPage = 3;

    /// <summary>In the malformed document, the page whose array lists an ordinary page.</summary>
    private const int ListsOrdinaryPage = 4;

    /// <summary>In the malformed document, the ordinary page that a separation lists.</summary>
    private const int ListedOrdinaryPage = 5;

    /// <summary>In the malformed document, the page whose colour space is a name.</summary>
    private const int InvalidSpacePage = 6;

    /// <summary>In the malformed document, the first of two pages listing each other in different orders.</summary>
    private const int ReorderedFirst = 7;

    /// <summary>In the malformed document, the second of two pages listing each other in different orders.</summary>
    private const int ReorderedSecond = 8;

    /// <summary>The findings in the malformed document.</summary>
    private const int MalformedFindings = 8;

    /// <summary>The groups in the document of self-listed pages.</summary>
    private const int SelfListedGroups = 2;

    /// <summary>The page of that document whose array is an indirect, self-including array.</summary>
    private const int LoopingArrayPage = 1;

    /// <summary>The hand-made document reports both printed pages, their colourants and colour spaces, in page order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreseparatedFixtureReportsGroupsAndColorants()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        var info = PdfDocumentSeparations.GetSeparations(document);

        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups.Length).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(Indexes(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstGroup);
        await Assert.That(Colorants(info.Groups[0])).IsEquivalentTo(SeparationPdf.FirstColorants);
        await Assert.That(Indexes(info.Groups[1])).IsEquivalentTo(SeparationPdf.SecondGroup);
        await Assert.That(Colorants(info.Groups[1])).IsEquivalentTo(SeparationPdf.SecondColorants);
        await Assert.That(info.Groups[0].Separations[0].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.Separation);
        await Assert.That(info.Groups[1].Separations[^1].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.DeviceN);
        await Assert.That(info.Groups[1].Separations[^1].SpaceColorants).IsEquivalentTo(["PANTONE 35 CV"]);
        await Assert.That(info.Groups[1].Separations[0].SpaceKind).IsEqualTo(PdfSeparationSpaceKind.None);
        await Assert.That(PdfDocumentSeparations.FindGroup(info, SeparationPdf.OrdinaryPage)).IsNull();
        await Assert.That(PdfDocumentSeparations.FindGroup(info, SeparationPdf.SecondGroupStart)!.Index).IsEqualTo(1);
        await Assert.That(document.PageCount).IsEqualTo(SeparationPdf.PreseparatedPages);
    }

    /// <summary>The async form gives the same groups, and a cancelled token stops it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncReadMatchesAndCancels()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePreseparated(), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var thrown = false;
        try
        {
            _ = await PdfDocumentSeparations.GetSeparationsAsync(document, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            thrown = true;
        }

        var info = await PdfDocumentSeparations.GetSeparationsAsync(document, CancellationToken.None);
        await Assert.That(thrown).IsTrue();
        await Assert.That(info.Groups.Length).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(PdfDocumentSeparations.GetSeparations(document)).IsSameReferenceAs(info);
    }

    /// <summary>A document of ordinary pages has no groups and nothing to report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryDocumentHasNoGroups()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePages([null, null]), null);
        await Assert.That(PdfDocumentSeparations.GetSeparations(document)).IsSameReferenceAs(PdfSeparationInfo.Empty);
    }

    /// <summary>Each kind of malformed entry is reported against the page that has it, and the usable pages still group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedEntriesAreReported()
    {
        string?[] pages =
        [
            "/Pages [3 0 R 12 0 R 4 0 R] /DeviceColorant /Cyan",
            "/Pages [3 0 R 4 0 R]",
            "/Pages [5 0 R] /DeviceColorant /Other /ColorSpace [/Separation /Spot /DeviceGray 12 0 R]",
            "/DeviceColorant /Black",
            "/Pages [7 0 R 8 0 R] /DeviceColorant /Black",
            null,
            "/Pages [9 0 R 5 0 R] /DeviceColorant /Gray /ColorSpace /DeviceGray",
            "/Pages [10 0 R 11 0 R] /DeviceColorant /Cyan",
            "/Pages [11 0 R 10 0 R] /DeviceColorant /Magenta",
        ];
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePages(pages, "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>"), null);
        var info = PdfDocumentSeparations.GetSeparations(document);

        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(StrayPage, PdfSeparationProblem.NotAPage));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(NoColorantPage, PdfSeparationProblem.MissingColorant));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(MismatchPage, PdfSeparationProblem.ColorantMismatch));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(MismatchPage, PdfSeparationProblem.PageInTwoGroups));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(NoPagesPage, PdfSeparationProblem.MissingPages));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(ListedOrdinaryPage, PdfSeparationProblem.MemberWithoutSeparationInfo));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(InvalidSpacePage, PdfSeparationProblem.InvalidColorSpace));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(ReorderedSecond, PdfSeparationProblem.PagesMismatch));
        await Assert.That(info.Findings.Length).IsEqualTo(MalformedFindings);
        await Assert.That(Indexes(info.Groups[0])).IsEquivalentTo([StrayPage, NoColorantPage]);
        await Assert.That(Indexes(PdfDocumentSeparations.FindGroup(info, MismatchPage)!)).IsEquivalentTo([MismatchPage]);
        await Assert.That(Indexes(PdfDocumentSeparations.FindGroup(info, ListsOrdinaryPage)!)).IsEquivalentTo([ListsOrdinaryPage]);
        await Assert.That(Indexes(PdfDocumentSeparations.FindGroup(info, InvalidSpacePage)!)).IsEquivalentTo([InvalidSpacePage]);
        await Assert.That(Indexes(PdfDocumentSeparations.FindGroup(info, ReorderedFirst)!)).IsEquivalentTo([ReorderedFirst, ReorderedSecond]);
        await Assert.That(PdfDocumentSeparations.FindGroup(info, NoPagesPage)).IsNull();
        await Assert.That(info.Groups[0].Separations[NoColorantPage].Colorant).IsEmpty();
    }

    /// <summary>Reference loops, a self-listing array and an over-long array stay inside the reader's limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclesAndLongArraysStayInsideLimits()
    {
        var self = string.Join(' ', Enumerable.Repeat("3 0 R", TooManyEntries));
        string?[] pages =
        [
            $"/Pages [{self}] /DeviceColorant /Black",
            "/Pages 6 0 R /DeviceColorant /Black",
        ];
        using var document = PdfDocumentReader.Open(SeparationPdf.CreatePages(pages, "7 0 R", "[4 0 R 6 0 R 5 0 R]", "5 0 R"), null);
        var info = PdfDocumentSeparations.GetSeparations(document);

        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(0, PdfSeparationProblem.TooManyEntries));
        await Assert.That(info.Findings).Contains(new PdfSeparationFinding(LoopingArrayPage, PdfSeparationProblem.NotAPage));
        await Assert.That(info.Groups.Length).IsEqualTo(SelfListedGroups);
        await Assert.That(Indexes(info.Groups[1])).IsEquivalentTo([LoopingArrayPage]);
    }

    /// <summary>A separation dictionary that is a reference loop is not read, and the page stays ordinary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopingSeparationDictionaryIsIgnored()
    {
        var bytes = HyperPdf.TestAssets.MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /Resources << >> /MediaBox [0 0 10 10] >>",
            "<< /Type /Page /Parent 2 0 R /SeparationInfo 4 0 R >>",
            "5 0 R",
            "4 0 R");
        using var document = PdfDocumentReader.Open(bytes, null);
        await Assert.That(PdfDocumentSeparations.GetSeparations(document).Groups).IsEmpty();
    }

    /// <summary>Only the first ten thousand separation pages are read; the limit is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationPageLimitIsReported()
    {
        using var document = PdfDocumentReader.Open(SeparationPdf.CreateMany(MaxSeparationPages + 1), null);
        var info = await PdfDocumentSeparations.GetSeparationsAsync(document, CancellationToken.None);
        await Assert.That(info.Groups.Length).IsEqualTo(MaxSeparationPages);
        await Assert.That(info.Findings).IsEquivalentTo([new PdfSeparationFinding(MaxSeparationPages, PdfSeparationProblem.TooManyEntries)]);
    }

    /// <summary>Gets the page indexes of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The indexes.</returns>
    internal static int[] Indexes(PdfSeparationGroup group) => [.. group.Separations.Select(static page => page.PageIndex)];

    /// <summary>Gets the colourants of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The colourants.</returns>
    internal static string[] Colorants(PdfSeparationGroup group) => [.. group.Separations.Select(static page => page.Colorant)];
}
