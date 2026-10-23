// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the language findings (ISO 14289-1 7.2; ISO 14289-2 8.4.4).</summary>
public sealed class AccessibilityLanguageTests
{
    /// <summary>A catalog language that is not a language identifier is flagged; a valid one is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CatalogLanguageMustBeAnIdentifier()
    {
        var bad = await AccessibilityPdfs.ReportAsync(new() { Lang = "/Lang (1-pt)" });
        var tooLong = await AccessibilityPdfs.ReportAsync(new() { Lang = "/Lang (portugues)" });
        var good = await AccessibilityPdfs.ReportAsync(new() { Lang = "/Lang (pt-BR)" });

        await Assert.That(bad.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(1);
        await Assert.That(tooLong.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(1);
        await Assert.That(good.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(0);
    }

    /// <summary>A structure element's own language must be an identifier too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ElementLanguageMustBeAnIdentifier()
    {
        var bad = await AccessibilityPdfs.ReportAsync(new() { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("P", document, "/K 0 /Lang (1-pt)")) });
        var good = await AccessibilityPdfs.ReportAsync(new() { Layout = static (pdf, document) => AccessibilityPdfs.Kids(pdf.Element("P", document, "/K 0 /Lang (fr)")) });

        await Assert.That(bad.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(1);
        await Assert.That(good.GetCount(PdfAccessibilityCode.LanguageInvalid)).IsEqualTo(0);
    }
}
