// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for how the page tree, page boxes and rotation are read.</summary>
public sealed class PageTreeTests
{
    /// <summary>The width inherited from the parent node.</summary>
    private const int InheritedWidth = 300;

    /// <summary>The height inherited from the parent node.</summary>
    private const int InheritedHeight = 400;

    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>Three quarter turns.</summary>
    private const int ThreeQuarterTurns = 270;

    /// <summary>The width of the default US Letter page.</summary>
    private const int LetterWidth = 612;

    /// <summary>A rotation that is not a quarter turn and rounds toward zero.</summary>
    private const int AwkwardTurn = 100;

    /// <summary>A negative rotation that is not a quarter turn.</summary>
    private const int AwkwardNegativeTurn = -100;

    /// <summary>A page found by scanning inherits its box and rotation from its /Parent chain, even when the chain loops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScannedPagesInheritFromTheirParents()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 300 400] /Rotate 90 /Parent 2 0 R >>",
                "<< /Type /Page /Parent 2 0 R >>"), null);
        var page = PdfDocumentPages.GetPage(document, 0);

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(page.MediaBox.Right).IsEqualTo(InheritedWidth);
        await Assert.That(page.Rotation).IsEqualTo(QuarterTurn);
        await Assert.That(page.Width).IsEqualTo(InheritedHeight);
        await Assert.That(page.Height).IsEqualTo(InheritedWidth);
    }

    /// <summary>Adding inherited attributes to one document leaves the shared empty state unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InheritedAttributesStayWithinTheirDocument()
    {
        using var inherited = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 300 400] /Rotate 90 >>",
                "<< /Type /Page /Parent 2 0 R >>"), null);
        using var plain = PdfDocumentReader.Open(Single(string.Empty), null);

        await Assert.That(PdfDocumentPages.GetPage(inherited, 0).Rotation).IsEqualTo(QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(plain, 0).Rotation).IsEqualTo(0);
        await Assert.That(PdfDocumentPages.GetPage(plain, 0).Width).IsEqualTo(LetterWidth);
    }

    /// <summary>A media box of absurd size is ignored in favour of the default page size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AbsurdMediaBoxIsIgnored()
    {
        using var document = PdfDocumentReader.Open(Single("/MediaBox [0 0 2000000 100]"), null);

        await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(LetterWidth);
    }

    /// <summary>Rotations that are not quarter turns truncate toward zero like PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationTruncatesTowardZero()
    {
        using var positive = PdfDocumentReader.Open(Single(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"/Rotate {AwkwardTurn}")), null);
        using var negative = PdfDocumentReader.Open(Single(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"/Rotate {AwkwardNegativeTurn}")), null);

        await Assert.That(PdfDocumentPages.GetPage(positive, 0).Rotation).IsEqualTo(QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(negative, 0).Rotation).IsEqualTo(ThreeQuarterTurns);
    }

    /// <summary>Builds a one page document whose page carries extra entries.</summary>
    /// <param name="entries">The extra page dictionary entries.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Single(string entries) =>
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R {entries} >>");
}
