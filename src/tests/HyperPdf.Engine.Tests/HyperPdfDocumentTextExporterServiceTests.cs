// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Text;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests for <see cref="HyperPdfDocumentTextExporterService"/>.</summary>
public sealed class HyperPdfDocumentTextExporterServiceTests
{
    /// <summary>The pages of the text sample.</summary>
    private const int Pages = 2;

    /// <summary>The lines on each page of the text sample.</summary>
    private const int Lines = 3;

    /// <summary>Plain text, Markdown and HTML each carry the words of the pages; HTML marks each page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WriteAsync_WritesThePagesTextInEachFormat()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateTextFilled(Pages, Lines));
        var exporter = DocumentFeature<IDocumentTextExporter>.Cast(test.Document)!;

        var plain = await Write(exporter, DocumentTextFormat.PlainText);
        var markdown = await Write(exporter, DocumentTextFormat.Markdown);
        var html = await Write(exporter, DocumentTextFormat.Html);

        await Assert.That(plain).Contains("quick brown fox");
        await Assert.That(markdown).Contains("quick brown fox");
        await Assert.That(html).Contains("data-page=\"2\"");
    }

    /// <summary>Writes the document's text to a string.</summary>
    /// <param name="exporter">The exporter.</param>
    /// <param name="format">The kind of file.</param>
    /// <returns>What was written.</returns>
    private static async Task<string> Write(IDocumentTextExporter exporter, DocumentTextFormat format)
    {
        await using var output = new StringWriter();
        await exporter.WriteAsync(output, format, CancellationToken.None);
        return output.ToString();
    }
}
