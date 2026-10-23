// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests resource names and cached field font lifetimes.</summary>
public sealed class FormFontCacheTests
{
    /// <summary>The first resource alias.</summary>
    private const string FirstName = "First";

    /// <summary>The second resource alias.</summary>
    private const string SecondName = "Second";

    /// <summary>A standard font can appear under different resource names in one document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardFontKeepsEachRequestedResourceName()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var cache = new PdfFontCache(null);
        var first = cache.GetStandardFormFont(document.Objects, StandardFont.Helvetica, FirstName, false);
        var second = cache.GetStandardFormFont(document.Objects, StandardFont.Helvetica, SecondName, false);

        await Assert.That(first.Name).IsEqualTo(FirstName);
        await Assert.That(second.Name).IsEqualTo(SecondName);
        await Assert.That(second.GetResources(document.Objects).GetDictionary(KnownName.Font)!.ContainsKey(document.Objects.Names.Intern(SecondName))).IsTrue();
        await Assert.That(cache.GetStandardFormFont(document.Objects, StandardFont.Helvetica, FirstName, false)).IsSameReferenceAs(first);
    }

    /// <summary>Two resource aliases for one font dictionary are cached independently.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DictionaryFontReusesEachResourceAlias()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var cache = new PdfFontCache(null);
        var dictionary = new PdfDictionary(document.Objects);
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        dictionary.Set(KnownName.BaseFont, PdfValue.FromName(document.Objects.Names.Intern("Helvetica"u8)));
        var firstRequest = new FormFontRequest(document.Objects, document.Objects.Names.Intern(FirstName), PdfValue.FromDictionary(dictionary), cache);
        var secondRequest = firstRequest with { Key = document.Objects.Names.Intern(SecondName) };
        var first = cache.GetFormFont(dictionary, firstRequest);
        var second = cache.GetFormFont(dictionary, secondRequest);

        await Assert.That(first!.Name).IsEqualTo(FirstName);
        await Assert.That(second!.Name).IsEqualTo(SecondName);
        await Assert.That(cache.GetFormFont(dictionary, secondRequest)).IsSameReferenceAs(second);
    }

    /// <summary>Cached font encodings do not retain text or line lengths from a previous rental.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusedEncodingKeepsOnlyTheCurrentText()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var font = FormFont.CreateFallback(document.Objects, new(null));
        var first = font.Encode("longer\r\nfirst\ntext", CancellationToken.None);
        using (first)
        {
            await Assert.That(first.IndexOfLineFeed(0)).IsEqualTo("longer".Length);
        }

        using var second = font.Encode("x", CancellationToken.None);
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(second.Length).IsEqualTo(1);
        await Assert.That(second.IndexOfLineFeed(0)).IsEqualTo(-1);
        await Assert.That(second.Slice(0, 1).SequenceEqual("x"u8)).IsTrue();
    }

    /// <summary>Encoded-text pools are reused within a cache and isolated from other documents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncodingPoolsStayWithTheirDocument()
    {
        var left = new PdfFontCache(null);
        var right = new PdfFontCache(null);
        var returned = left.RentFormCodes();
        returned.Dispose();
        using var other = right.RentFormCodes();
        using var reused = left.RentFormCodes();

        await Assert.That(reused).IsSameReferenceAs(returned);
        await Assert.That(ReferenceEquals(other, returned)).IsFalse();
    }

    /// <summary>Shared resource combinations change when their fonts or icon change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppearanceResourcesFollowFontsAndIcons()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var cache = new PdfFontCache(null);
        var first = cache.GetStandardFormFont(document.Objects, StandardFont.Helvetica, FirstName, false);
        var second = cache.GetStandardFormFont(document.Objects, StandardFont.TimesRoman, SecondName, false);
        var resources = cache.GetAppearanceResources(document.Objects, [first, second], default);
        await Assert.That(cache.GetAppearanceResources(document.Objects, [first, second], default)).IsSameReferenceAs(resources);

        var icon = PdfValue.FromStream(new(new(document.Objects), []));
        var withIcon = cache.GetAppearanceResources(document.Objects, [first, second], icon);
        await Assert.That(ReferenceEquals(withIcon, resources)).IsFalse();
        await Assert.That(withIcon.GetDictionary(KnownName.XObject)!.Get(document.Objects.Names.Intern("Icon"u8)).Equals(icon)).IsTrue();
        await Assert.That(resources.ContainsKey(KnownName.XObject)).IsFalse();
    }

    /// <summary>Trailing-space searches stop at the requested range and handle empty text.</summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="expected">The number of units before trailing spaces.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", 0)]
    [Arguments("  ", 0)]
    [Arguments("abc  ", 3)]
    public async Task EncodedTextTrimsOnlyTrailingSpaces(string text, int expected)
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        using var codes = FormFont.CreateFallback(document.Objects).Encode(text, CancellationToken.None);

        await Assert.That(codes.TrimmedLength(0, codes.Length)).IsEqualTo(expected);
    }
}
