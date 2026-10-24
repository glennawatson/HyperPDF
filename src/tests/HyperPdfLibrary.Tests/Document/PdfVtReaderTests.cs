// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks the PDF/VT claim and record structure report.</summary>
public sealed class PdfVtReaderTests
{
    /// <summary>The number of records in the sound sample.</summary>
    private const int TwoRecords = 2;

    /// <summary>The object number of the first object after the catalog, page tree and three pages.</summary>
    private const int FirstExtraObject = 6;

    /// <summary>A blank page object.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>A file with a claim and records that cover every page once reports no findings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundRecordsHaveNoFindings()
    {
        using var document = Open(
            "PDF/VT-1",
            "<< /DParts [[7 0 R 8 0 R]] >>",
            "<< /Start 3 0 R /End 4 0 R /DPM << /Name (Ann) >> >>",
            "<< /Start 5 0 R /DPM << /Name (Bob) >> >>",
            "<< /DPartRootNode 6 0 R /RecordLevel 1 /NodeNameList [/Job /Record] >>");

        var report = PdfVtReader.Read(document);

        await Assert.That(report.Flavor).IsEqualTo(PdfVtFlavor.Vt1);
        await Assert.That(report.IsClaimed).IsTrue();
        await Assert.That(report.IsStructureSound).IsTrue();
        await Assert.That(report.Records.Length).IsEqualTo(TwoRecords);
        await Assert.That(report.Records[0].Metadata["Name"]).IsEqualTo("Ann");
        await Assert.That(report.Records[1].StartPage).IsEqualTo(TwoRecords);
    }

    /// <summary>A page that no record holds is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncoveredPageIsReported()
    {
        using var document = Open(
            "PDF/VT-2",
            "<< /DParts [[7 0 R]] >>",
            "<< /Start 3 0 R /End 4 0 R >>",
            "<< /DPartRootNode 6 0 R /RecordLevel 1 >>");

        var report = PdfVtReader.Read(document);

        await Assert.That(report.Flavor).IsEqualTo(PdfVtFlavor.Vt2);
        await Assert.That(report.Findings.Length).IsEqualTo(1);
        await Assert.That(report.Findings[0]).Contains("Pages 3 to 3");
    }

    /// <summary>A claim without a document part root is reported and keeps the raw claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimWithoutPartsIsReported()
    {
        using var document = Open("PDF/VT-9");

        var report = PdfVtReader.Read(document);

        await Assert.That(report.Claim).IsEqualTo("PDF/VT-9");
        await Assert.That(report.Flavor).IsEqualTo(PdfVtFlavor.Unknown);
        await Assert.That(report.IsStructureSound).IsFalse();
    }

    /// <summary>A file with no claim and no parts reports nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryFileIsNotClaimed()
    {
        using var document = Open(null);

        var report = PdfVtReader.Read(document);

        await Assert.That(report.IsClaimed).IsFalse();
        await Assert.That(report.Flavor).IsEqualTo(PdfVtFlavor.None);
        await Assert.That(report.IsStructureSound).IsTrue();
    }

    /// <summary>Overlapping ranges are reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingRecordsAreReported()
    {
        using var document = Open(
            "PDF/VT-2s",
            "<< /DParts [[7 0 R 8 0 R]] >>",
            "<< /Start 3 0 R /End 4 0 R >>",
            "<< /Start 4 0 R /End 5 0 R >>",
            "<< /DPartRootNode 6 0 R /RecordLevel 1 >>");

        var report = PdfVtReader.Read(document);

        await Assert.That(report.Flavor).IsEqualTo(PdfVtFlavor.Vt2s);
        await Assert.That(report.Findings.Length).IsEqualTo(1);
        await Assert.That(report.Findings[0]).Contains("Record 2");
    }

    /// <summary>Opens a three-page file; the last extra object is the DPartRoot when any are given.</summary>
    /// <param name="claim">The <c>GTS_PDFVTVersion</c> value, or null for none.</param>
    /// <param name="extra">The objects numbered from 6.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string? claim, params string[] extra)
    {
        var root = extra.Length > 0 ? $" /DPartRoot {FirstExtraObject - 1 + extra.Length} 0 R" : string.Empty;
        string[] header =
        [
            $"<< /Type /Catalog /Pages 2 0 R{root} >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            PageObject,
            PageObject,
            PageObject,
        ];
        var infoNumber = FirstExtraObject + extra.Length;
        string[] info = claim is null ? [] : [$"<< /GTS_PDFVTVersion ({claim}) >>"];
        var trailer = claim is null ? string.Empty : $"/Info {infoNumber} 0 R";
        return PdfDocumentReader.Open(MiniPdf.BuildWithTrailer(trailer, [.. header, .. extra, .. info]), null);
    }
}
