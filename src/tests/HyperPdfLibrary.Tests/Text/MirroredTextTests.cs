// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Text drawn with a negative or a zero font size is extracted line by line, in drawing order, and never mixed into the line it overlaps.</summary>
public sealed class MirroredTextTests
{
    /// <summary>The line drawn at the normal size.</summary>
    private const string Forward = "Forward line";

    /// <summary>The line drawn with a negative size, which runs backwards from where the forward line ends.</summary>
    private const string Mirrored = "Mirrored line";

    /// <summary>The line drawn with a size of zero, whose glyphs all sit at one point.</summary>
    private const string Hidden = "Hidden line";

    /// <summary>Each line keeps its own letters and its own line, in the order the content draws them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsEachLineTogether()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, $"BT 1 0 0 1 50 200 Tm /F1 20 Tf ({Forward}) Tj /F1 -20 Tf ({Mirrored}) Tj /F1 0 Tf ({Hidden}) Tj ET"),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            null);

        var lines = PdfDocumentText.GetTextPage(document, 0).Text.Split('\n', StringSplitOptions.TrimEntries);

        await Assert.That(lines).IsEquivalentTo([Forward, Mirrored, Hidden], CollectionOrdering.Matching);
    }
}
