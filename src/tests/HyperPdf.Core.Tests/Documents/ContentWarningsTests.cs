// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="ContentWarnings"/>, which explains content that cannot be shown.</summary>
public sealed class ContentWarningsTests
{
    /// <summary>Nothing is said when everything can be shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaysNothingWhenAllIsShown() => await Assert.That(ContentWarnings.Describe(UnsupportedContent.None)).IsNull();

    /// <summary>An XFA form is named, with what the reader is seeing and where to go instead.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsXfaForms()
    {
        var message = ContentWarnings.Describe(UnsupportedContent.XfaForm)!;

        await Assert.That(message).StartsWith("This document uses an XFA form, which HyperPDF cannot show or run.");
        await Assert.That(message).Contains("version made for other viewers");
        await Assert.That(message).EndsWith("open it in Adobe Acrobat Reader.");
    }

    /// <summary>Several parts are listed together in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsSeveralParts()
    {
        var message = ContentWarnings.Describe(UnsupportedContent.JavaScript | UnsupportedContent.Multimedia | UnsupportedContent.Portfolio)!;

        await Assert.That(message).StartsWith("This document uses form scripts, sound or video and a PDF portfolio,");
        await Assert.That(message).Contains("listed under Attachments");
    }

    /// <summary>A portfolio is a cover page that says so in front of attached files.</summary>
    /// <param name="attachments">The attached files.</param>
    /// <param name="text">The first page's text.</param>
    /// <param name="expected">Whether it is a portfolio.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2, "For the best experience, open this PDF Portfolio in Acrobat.", true)]
    [Arguments(0, "PDF Portfolio", false)]
    [Arguments(2, "Quarterly report", false)]
    public async Task SpotsPortfolios(int attachments, string text, bool expected) =>
        await Assert.That(ContentWarnings.LooksLikePortfolio(attachments, text)).IsEqualTo(expected);
}
