// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the claim, the document entries, page checks, the finding cap and messages.</summary>
[NotInParallel]
public sealed class AccessibilityDocumentTests
{
    /// <summary>PDF/UA part 2.</summary>
    private const int PartTwo = 2;

    /// <summary>A part number that does not exist.</summary>
    private const int PartNine = 9;

    /// <summary>The number of figures in the capped document.</summary>
    private const int ManyFigures = 250;

    /// <summary>The longest message, in words.</summary>
    private const int MaxMessageWords = 20;

    /// <summary>The number of words in the shortest message that is still a sentence.</summary>
    private const int MinMessageWords = 4;

    /// <summary>A document with no faults has no findings and reads its entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GoodDocumentHasNoFindings()
    {
        var report = await AccessibilityPdfs.ReportAsync(new());

        await Assert.That(report.Findings.Count).IsEqualTo(0);
        await Assert.That(report.TotalCount).IsEqualTo(0);
        await Assert.That(report.Claim.Part).IsEqualTo(1);
        await Assert.That(report.Claim.IsClaimed).IsTrue();
        await Assert.That(report.IsMarked).IsTrue();
        await Assert.That(report.HasStructureTree).IsTrue();
        await Assert.That(report.Language).IsEqualTo("en-AU");
        await Assert.That(report.HasTitle).IsTrue();
        await Assert.That(report.DisplayDocTitle).IsTrue();
        await Assert.That(report.HasSuspects).IsFalse();
        await Assert.That(report.Pages.Count).IsEqualTo(1);
    }

    /// <summary>A PDF 2.0 file with part 2 and a namespace list gives claim 2 and no version finding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Part2On20FileIsClaim2()
    {
        var report = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(PartTwo, true), CatalogExtra = "/Version /2.0", RootExtra = "/Namespaces []" });

        await Assert.That(report.Claim.Part).IsEqualTo(PartTwo);
        await Assert.That(report.Claim.PdfVersion).IsEqualTo("2.0");
        await Assert.That(report.GetCount(PdfAccessibilityCode.ClaimPart2NotPdf2)).IsEqualTo(0);
    }

    /// <summary>The PDF 2.0 structure namespace is noticed when a tree element uses it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20NamespaceUseIsReported()
    {
        var report = await AccessibilityPdfs.ReportAsync(new()
        {
            Xmp = AccessibilityPdfs.Packet(PartTwo, true),
            CatalogExtra = "/Version /2.0",
            Layout = static (pdf, document) =>
            {
                var space = pdf.Add("<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>");
                return AccessibilityPdfs.Kids(pdf.Element("P", document, $"/NS {space} 0 R /K 0"));
            },
        });

        await Assert.That(report.Claim.UsesPdf20Namespaces).IsTrue();
        await Assert.That((await Plain()).Claim.UsesPdf20Namespaces).IsFalse();
    }

    /// <summary>Part 2 on a PDF 1.7 file is flagged; an unknown part is flagged; no XMP gives no claim and no title.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimProblemsAreFlagged()
    {
        var wrongVersion = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(PartTwo, true) });
        var unknown = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(PartNine, true) });
        var none = await AccessibilityPdfs.ReportAsync(new() { Xmp = null });

        await Assert.That(wrongVersion.GetCount(PdfAccessibilityCode.ClaimPart2NotPdf2)).IsEqualTo(1);
        await Assert.That(unknown.GetCount(PdfAccessibilityCode.ClaimPartUnknown)).IsEqualTo(1);
        await Assert.That(unknown.Claim.IsClaimed).IsFalse();
        await Assert.That(none.Claim.Part).IsNull();
        await Assert.That(none.GetCount(PdfAccessibilityCode.NoMetadata)).IsEqualTo(1);
        await Assert.That(none.GetCount(PdfAccessibilityCode.NoTitle)).IsEqualTo(0);
    }

    /// <summary>Missing entries are each flagged once, at document level.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingDocumentEntriesAreFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(new()
        {
            MarkInfo = "/MarkInfo << /Marked false /Suspects true >>",
            Lang = string.Empty,
            Tree = false,
            Xmp = AccessibilityPdfs.Packet(1, false),
            ViewerPreferences = string.Empty,
        });

        await Assert.That(report.IsMarked).IsFalse();
        await Assert.That(report.HasSuspects).IsTrue();
        await Assert.That(report.Language).IsNull();
        await Assert.That(report.HasStructureTree).IsFalse();
        PdfAccessibilityCode[] expected =
        [
            PdfAccessibilityCode.NotMarked,
            PdfAccessibilityCode.NoStructureTree,
            PdfAccessibilityCode.NoLanguage,
            PdfAccessibilityCode.NoTitle,
            PdfAccessibilityCode.DisplayDocTitleOff,
            PdfAccessibilityCode.SuspectsSet,
        ];
        foreach (var code in expected)
        {
            await Assert.That(report.GetCount(code)).IsEqualTo(1);
        }

        await Assert.That(report.Findings.All(static finding => finding.PageIndex == -1)).IsTrue();
    }

    /// <summary>A page with annotations needs <c>/Tabs /S</c>; pop-ups and hidden annotations do not count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabsAreCheckedOnPagesWithAnnotations()
    {
        var bad = await AccessibilityPdfs.ReportAsync(new() { Layout = LinkLayout });
        var good = await AccessibilityPdfs.ReportAsync(new() { Layout = LinkLayout, PageExtra = "/Tabs /S" });
        var popup = await AccessibilityPdfs.ReportAsync(new()
        {
            Layout = static (pdf, document) =>
            {
                _ = AccessibilityPdfs.AddAnnotation(pdf, "/Subtype /Popup /Rect [0 0 10 10]");
                _ = AccessibilityPdfs.AddAnnotation(pdf, "/Subtype /Text /F 2 /Rect [0 0 10 10]");
                return AccessibilityPdfs.DefaultLayout(pdf, document);
            },
        });

        await Assert.That(bad.GetCount(PdfAccessibilityCode.TabsNotStructure)).IsEqualTo(1);
        await Assert.That(bad.Pages[0].TabsFollowStructure).IsFalse();
        await Assert.That(bad.Pages[0].AnnotationCount).IsEqualTo(1);
        await Assert.That(good.GetCount(PdfAccessibilityCode.TabsNotStructure)).IsEqualTo(0);
        await Assert.That(good.Pages[0].TabsFollowStructure).IsTrue();
        await Assert.That(popup.GetCount(PdfAccessibilityCode.TabsNotStructure)).IsEqualTo(0);
        await Assert.That(popup.Pages[0].AnnotationCount).IsEqualTo(0);
    }

    /// <summary>Text outside marked content is untagged content; text marked as an artifact is only counted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedAndArtifactTextIsCounted()
    {
        const string loose = "BT /F1 12 Tf 72 300 Td (Loose) Tj ET\n";
        const string decoration = "/Artifact BMC BT /F1 9 Tf 72 760 Td (Header) Tj ET EMC\n";
        var untagged = await AccessibilityPdfs.ReportAsync(new() { Content = new AccessibilitySpec().Content + loose });
        var artifact = await AccessibilityPdfs.ReportAsync(new() { Content = new AccessibilitySpec().Content + decoration });

        await Assert.That(untagged.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(1);
        await Assert.That(untagged.Findings[0].PageIndex).IsEqualTo(0);
        await Assert.That(untagged.Pages[0].UntaggedGlyphs).IsEqualTo("Loose".Length);
        await Assert.That(artifact.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(0);
        await Assert.That(artifact.Pages[0].ArtifactGlyphs).IsEqualTo("Header".Length);
        await Assert.That(artifact.Pages[0].UntaggedGlyphs).IsEqualTo(0);
    }

    /// <summary>The list keeps the first findings of a code, the counts keep every one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindingsAreCappedButCountsAreNot()
    {
        var report = await AccessibilityPdfs.ReportAsync(new()
        {
            Layout = static (pdf, document) =>
            {
                var kids = new System.Text.StringBuilder();
                for (var i = 0; i < ManyFigures; i++)
                {
                    _ = kids.Append(AccessibilityPdfs.Kids(pdf.Element("Figure", document, string.Empty)));
                }

                return kids.ToString();
            },
        });

        await Assert.That(report.GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(ManyFigures);
        await Assert.That(report.Findings.Count(static finding => finding.Code == PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(AccessibilityFindings.MaxFindingsPerCode);
        await Assert.That(report.TotalCount).IsEqualTo(ManyFigures);
    }

    /// <summary>A cancelled token stops the report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsTheReport()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(async () => await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Every code has a short sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryCodeHasAShortMessage()
    {
        foreach (var code in Enum.GetValues<PdfAccessibilityCode>())
        {
            if (code == PdfAccessibilityCode.None)
            {
                continue;
            }

            var words = PdfAccessibilityMessages.For(code).Split(' ').Length;
            await Assert.That(words).IsBetween(MinMessageWords, MaxMessageWords);
        }

        await Assert.That(PdfAccessibilityMessages.CodeCount).IsEqualTo(Enum.GetValues<PdfAccessibilityCode>().Length);
    }

    /// <summary>A layout with one tagged link.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element.</param>
    /// <returns>The kids.</returns>
    private static string LinkLayout(TaggedPdfBuilder pdf, int document)
    {
        var link = AccessibilityPdfs.AddLink(pdf, "/Contents (Go)");
        return AccessibilityPdfs.DefaultLayout(pdf, document) + AccessibilityPdfs.Kids(AccessibilityPdfs.AddObjectElement(pdf, "Link", document, link, string.Empty));
    }

    /// <summary>Reads the default document's report.</summary>
    /// <returns>The report.</returns>
    private static Task<PdfAccessibilityReport> Plain() => AccessibilityPdfs.ReportAsync(new()).AsTask();
}
