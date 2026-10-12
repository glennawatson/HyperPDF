// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks Roman page labels through the public document API.</summary>
public sealed class RomanPageLabelTests
{
    /// <summary>The Roman label wrap used by PDFium.</summary>
    private const int MaxRoman = 1_000_000;

    /// <summary>The second zero-based page index.</summary>
    private const int SecondPage = 2;

    /// <summary>The third zero-based page index.</summary>
    private const int ThirdPage = 3;

    /// <summary>The number of thousands in the last Roman number before wrapping.</summary>
    private const int LastThousands = 999;

    /// <summary>The number of thousands in int.MaxValue after wrapping.</summary>
    private const int LargestThousands = 483;

    /// <summary>A page dictionary shared by the four-page fixture.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>Subtractive symbols and case are kept across label ranges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsSubtractiveSymbolsAndCase()
    {
        using var document = Open("[0 << /S /r /St 4 >> 1 << /S /R /St 9 >> 2 << /S /r /St 49 >> 3 << /S /R /St 944 >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("iv");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsEqualTo("IX");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsEqualTo("xlix");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("CMXLIV");
    }

    /// <summary>Zero, negative and wrapped numbers keep their existing empty-label behavior.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesNonPositiveAndWrappedNumbers()
    {
        using var document = Open($"[0 << /S /r /St 0 >> 1 << /S /R /St -1 >> 2 << /S /r /St {MaxRoman} >> 3 << /S /R /St {MaxRoman + 1} >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsNull();
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsNull();
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsNull();
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("I");
    }

    /// <summary>The last number before wrapping and the largest PDF integer keep their modulo behavior.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsLargeValuesAtTheWrapBoundary()
    {
        using var document = Open($"[0 << /S /r /St {MaxRoman - 1} >> 2 << /S /R /St {int.MaxValue} >>]");
        var last = PdfDocumentLabels.GetPageLabel(document, 0);
        var largest = PdfDocumentLabels.GetPageLabel(document, SecondPage);

        await Assert.That(last).IsEqualTo($"{new string('m', LastThousands)}cmxcix");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsNull();
        await Assert.That(largest).IsEqualTo($"{new string('M', LargestThousands)}DCXLVII");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsNull();
    }

    /// <summary>Prefix and offset apply to the selected range, while pages before it use decimal labels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesPrefixAndRangeOffset()
    {
        using var document = Open("[1 << /S /R /P (Part-) /St 3 >> 3 << /S /r /P (Appendix-) /St 8 >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("1");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsEqualTo("Part-III");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsEqualTo("Part-IV");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, ThirdPage)).IsEqualTo("Appendix-viii");
    }

    /// <summary>Empty and UTF-16BE prefixes keep their text while Roman letters use the selected case.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsEmptyAndUnicodePrefixes()
    {
        using var document = Open("[0 << /S /r /P () /St 9 >> 1 << /S /R /P <FEFF7AE0> /St 4 >> 2 << /S /r /P <FEFF7B2C002D00310032> /St 9 >>]");

        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("ix");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 1)).IsEqualTo("章IV");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, SecondPage)).IsEqualTo("第-12ix");
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
