// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks that the ranges of page labels can be read, set and saved.</summary>
public sealed class PageLabelRangeEditTests
{
    /// <summary>The third zero-based page index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The fourth zero-based page index.</summary>
    private const int FourthPage = 3;

    /// <summary>The number the front matter of the book starts at.</summary>
    private const int StartAtThree = 3;

    /// <summary>A page dictionary shared by the four-page fixture.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>Setting a Roman front matter and a prefixed body gives every page the label a reader shows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetRangesLabelEveryPage()
    {
        using var document = Open();

        PdfDocumentPageLabels.SetRanges(
            document,
            [new(0, PdfPageLabelStyle.LowerRoman, string.Empty, StartAtThree), new(ThirdPage, PdfPageLabelStyle.Decimal, "A-", 1)]);

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("iii");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsEqualTo("iv");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("A-1");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, FourthPage)).IsEqualTo("A-2");
    }

    /// <summary>The ranges read back are the ranges that were set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RangesReadBackAsSet()
    {
        using var document = Open();
        PdfPageLabelRange[] set = [new(0, PdfPageLabelStyle.UpperLetters, "Ch ", 1), new(ThirdPage, PdfPageLabelStyle.None, "Index", 1)];

        PdfDocumentPageLabels.SetRanges(document, set);
        var read = PdfDocumentPageLabels.GetRanges(document);

        await Assert.That(read.Length).IsEqualTo(set.Length);
        await Assert.That(read[0]).IsEqualTo(set[0]);
        await Assert.That(read[1].StartPage).IsEqualTo(ThirdPage);
        await Assert.That(read[1].Style).IsEqualTo(PdfPageLabelStyle.None);
        await Assert.That(read[1].Prefix).IsEqualTo("Index");
    }

    /// <summary>A document with no labels reports no ranges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADocumentWithoutLabelsHasNoRanges()
    {
        using var document = Open();

        await Assert.That(PdfDocumentPageLabels.GetRanges(document).Length).IsEqualTo(0);
    }

    /// <summary>Ranges that do not start on the first page, or start on missing pages, are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadRangesAreRefused()
    {
        using var document = Open();

        await Assert.That(() => PdfDocumentPageLabels.SetRanges(document, [new(1, PdfPageLabelStyle.Decimal, string.Empty, 1)])).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentPageLabels.SetRanges(document, [new(0, PdfPageLabelStyle.Decimal, string.Empty, 1), new(FourthPage + 1, PdfPageLabelStyle.Decimal, string.Empty, 1)]))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentPageLabels.SetRanges(document, [new(0, PdfPageLabelStyle.Decimal, string.Empty, 0)])).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Opens four pages with no page labels.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R] /Count 4 >>",
            Page,
            Page,
            Page,
            Page);
        return PdfDocumentReader.Open(bytes, null);
    }
}
