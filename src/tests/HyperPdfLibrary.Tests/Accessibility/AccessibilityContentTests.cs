// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for page content outside the structure: untagged paths and images, and languages in marked content (ISO 14289-1 7.1, 7.2; ISO 14289-2 8.2.2, 8.4.4).</summary>
public sealed class AccessibilityContentTests
{
    /// <summary>A filled box.</summary>
    private const string Path = "0 0 0 rg 300 300 40 40 re f\n";

    /// <summary>A one pixel inline image drawn at 40 by 40 points.</summary>
    private const string InlineImage = "q 40 0 0 40 300 300 cm BI /W 1 /H 1 /CS /G /BPC 8 ID A EI Q\n";

    /// <summary>The marked content operator of the default page's paragraph.</summary>
    private const string ParagraphMark = "/P <</MCID 1>>";

    /// <summary>Gets the default page content.</summary>
    private static string Default => new AccessibilitySpec().Content;

    /// <summary>A path or an image outside marked content is untagged content; the same drawn as an artifact is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedGraphicsAreFlagged()
    {
        var path = await AccessibilityPdfs.ReportAsync(new() { Content = Default + Path });
        var image = await AccessibilityPdfs.ReportAsync(new() { Content = Default + InlineImage });
        var artifact = await AccessibilityPdfs.ReportAsync(new() { Content = $"{Default}/Artifact BMC\n{Path}{InlineImage}EMC\n" });
        var tagged = await AccessibilityPdfs.ReportAsync(new() { Content = Default });

        await Assert.That(path.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(1);
        await Assert.That(path.Pages[0].UntaggedGraphics).IsEqualTo(1);
        await Assert.That(image.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(1);
        await Assert.That(image.Pages[0].UntaggedGraphics).IsEqualTo(1);
        await Assert.That(artifact.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(0);
        await Assert.That(tagged.Pages[0].UntaggedGraphics).IsEqualTo(0);
    }

    /// <summary>An image inside marked content that has an id is tagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedImagesAreNotFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(new() { Content = $"{Default}/Figure <</MCID 3>> BDC\n{InlineImage}EMC\n" });

        await Assert.That(report.Pages[0].UntaggedGraphics).IsEqualTo(0);
    }

    /// <summary>A marked content property list whose language is not an identifier is flagged, inline or named.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkedContentLanguageMustBeAnIdentifier()
    {
        var bad = await AccessibilityPdfs.ReportAsync(new() { Content = Default.Replace(ParagraphMark, "/P <</MCID 1 /Lang (1-pt)>>", StringComparison.Ordinal) });
        var empty = await AccessibilityPdfs.ReportAsync(new() { Content = Default.Replace(ParagraphMark, "/P <</MCID 1 /Lang ()>>", StringComparison.Ordinal) });
        var good = await AccessibilityPdfs.ReportAsync(new() { Content = Default.Replace(ParagraphMark, "/P <</MCID 1 /Lang (fr-CA)>>", StringComparison.Ordinal) });

        await Assert.That(bad.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(1);
        await Assert.That(empty.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(1);
        await Assert.That(good.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(0);
    }
}
