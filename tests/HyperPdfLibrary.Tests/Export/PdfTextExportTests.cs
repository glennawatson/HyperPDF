// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Export;
using HyperPdfLibrary.Tests.Text;

namespace HyperPdfLibrary.Tests.Export;

/// <summary>Checks writing a page's text as HTML and Markdown.</summary>
public sealed class PdfTextExportTests
{
    /// <summary>Verifies that a bold run is wrapped in the format's bold markup.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BoldRun_IsMarkedInBothFormats()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a) Tj /F3 10 Tf (b) Tj /F1 10 Tf (a) Tj ET");

        await Assert.That(PdfTextExport.ToHtml(page)).IsEqualTo("<p>a<strong>b</strong>a</p>");
        await Assert.That(PdfTextExport.ToMarkdown(page)).IsEqualTo("a**b**a");
    }

    /// <summary>Verifies that text on two lines keeps its line break.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TwoLines_KeepTheirBreak()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a) Tj 0 -20 Td (b) Tj ET");

        await Assert.That(PdfTextExport.ToHtml(page)).IsEqualTo("<p>a<br>\nb</p>");
        await Assert.That(PdfTextExport.ToMarkdown(page)).IsEqualTo("a  \nb");
    }

    /// <summary>Verifies that a page with no text gives no output.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyPage_GivesNothing()
    {
        var page = TextTestDocument.Extract(string.Empty);

        await Assert.That(PdfTextExport.ToHtml(page)).IsEmpty();
        await Assert.That(PdfTextExport.ToMarkdown(page)).IsEmpty();
    }
}
