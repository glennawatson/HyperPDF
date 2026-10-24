// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks decimal and letter page labels, and prefixes in every text string encoding.</summary>
public sealed class PageLabelStyleTests
{
    /// <summary>The second zero-based page index.</summary>
    private const int SecondPage = 2;

    /// <summary>The third zero-based page index.</summary>
    private const int ThirdPage = 3;

    /// <summary>A page dictionary shared by the four-page fixture.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>Decimal and letter styles write their numbers after PDFDocEncoding and UTF-8 prefixes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsDecimalAndLetterStyles()
    {
        using var document = Open("[0 << /S /D /P (p.) /St 7 >> 1 << /S /A /St 28 >> 2 << /S /a /P <EFBBBFC3A9> /St 3 >> 3 << /S /D /P <80> >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("p.7");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsEqualTo("BB");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsEqualTo("éc");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("•1");
    }

    /// <summary>Negative decimals, empty letter labels, UTF-16LE prefixes and unknown styles keep their behaviour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsEdgeCases()
    {
        using var document = Open("[0 << /S /D /St -5 >> 1 << /S /a /St 0 >> 2 << /S /r /P <FFFE4100> /St 2 >> 3 << /S /Q /P <FEFF0041004241> >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("-5");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsNull();
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsEqualTo("Aii");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("AB");
    }

    /// <summary>Opens four pages with the given page-label number tree.</summary>
    /// <param name="numbers">The /Nums array.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string numbers)
    {
        var bytes = MiniPdf.Build(
            $"<< /Type /Catalog /Pages 2 0 R /PageLabels << /Nums {numbers} >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R] /Count 4 >>",
            Page,
            Page,
            Page,
            Page);
        return PdfDocumentReader.Open(bytes, null);
    }
}
