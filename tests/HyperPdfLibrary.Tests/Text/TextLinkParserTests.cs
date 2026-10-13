// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks which words in page text become web and email links.</summary>
[NotInParallel]
public sealed class TextLinkParserTests
{
    /// <summary>The links in the range test.</summary>
    private const int TwoLinks = 2;

    /// <summary>The start of "www.one.org" in the range test.</summary>
    private const int WebStart = 2;

    /// <summary>The length of "www.one.org".</summary>
    private const int WebLength = 11;

    /// <summary>The start of "me@two.org" in the range test.</summary>
    private const int MailStart = 16;

    /// <summary>The length of "me@two.org".</summary>
    private const int MailLength = 10;

    /// <summary>A web address followed by a full stop is linked without the stop.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="expected">The expected link address.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("See https://example.com/a_(b).", "https://example.com/a_(b)")]
    [Arguments("(http://x.org/p)", "http://x.org/p")]
    [Arguments("Go to [http://x.org/p] now", "http://x.org/p")]
    [Arguments("ftp://host/file.txt;", "ftp://host/file.txt")]
    [Arguments("HTTP://EXAMPLE.COM", "HTTP://EXAMPLE.COM")]
    [Arguments("visit www.example.com/path?x=1, next", "http://www.example.com/path?x=1")]
    [Arguments("mailto:a@b.org,", "mailto:a@b.org")]
    [Arguments("write user.name+tag@mail.example.co.uk!", "mailto:user.name+tag@mail.example.co.uk")]
    [Arguments("<http://x.org/q>", "http://x.org/q")]
    public async Task AddressIsFoundWithoutTrailingPunctuation(string text, string expected)
    {
        var links = Links(text);

        await Assert.That(links.Count).IsEqualTo(1);
        await Assert.That(links[0].Url).IsEqualTo(expected);
    }

    /// <summary>Words that only look like addresses are not linked.</summary>
    /// <param name="text">The page text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("www.localhost")]
    [Arguments("http://")]
    [Arguments("x@y")]
    [Arguments("a..b@c.org")]
    [Arguments(".a@c.org")]
    [Arguments("a@c.o")]
    [Arguments("a@-c.org")]
    [Arguments("awww.example.com")]
    [Arguments("plain words only")]
    public async Task NonAddressesAreNotLinked(string text)
    {
        var links = Links(text);

        await Assert.That(links.Count).IsEqualTo(0);
    }

    /// <summary>The link covers exactly the characters of the address, and several links in one text are found in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkRangesCoverTheAddress()
    {
        var links = Links("a www.one.org b me@two.org c");

        await Assert.That(links.Count).IsEqualTo(TwoLinks);
        await Assert.That(links[0]).IsEqualTo(new("http://www.one.org", WebStart, WebLength));
        await Assert.That(links[1]).IsEqualTo(new("mailto:me@two.org", MailStart, MailLength));
    }

    /// <summary>Finds the links in a line of text.</summary>
    /// <param name="text">The text, drawn in the test font.</param>
    /// <returns>The links.</returns>
    private static IReadOnlyList<PdfWebLink> Links(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        return TextTestDocument.Extract($"BT /F1 10 Tf 10 100 Td ({escaped}) Tj ET").GetWebLinks();
    }
}
