// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;

namespace HyperPdf.Core.Tests.Ocr;

/// <summary>Checks the language pack catalogue: English first, each pack pinned and fetched from the pinned release.</summary>
public sealed class OcrLanguageCatalogTests
{
    /// <summary>The length of a SHA-256 in hexadecimal.</summary>
    private const int Sha256HexLength = 64;

    /// <summary>The fewest languages offered.</summary>
    private const int MinimumLanguages = 20;

    /// <summary>English is offered first and is the default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffersEnglishFirst()
    {
        await Assert.That(OcrLanguageCatalog.Packs[0].Code).IsEqualTo("eng");
        await Assert.That(OcrLanguageCatalog.DefaultLanguage).IsEqualTo("eng");
        await Assert.That(OcrLanguageCatalog.Packs.Count).IsGreaterThanOrEqualTo(MinimumLanguages);
    }

    /// <summary>Every pack has a unique code, a size, a lowercase SHA-256 and an address in the pinned release.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryPackIsPinned()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pack in OcrLanguageCatalog.Packs)
        {
            await Assert.That(codes.Add(pack.Code)).IsTrue();
            await Assert.That(pack.Bytes).IsGreaterThan(0);
            await Assert.That(pack.Sha256.Length).IsEqualTo(Sha256HexLength);
            await Assert.That(pack.Sha256).IsEqualTo(pack.Sha256.ToLowerInvariant());
            await Assert.That(pack.FileName).IsEqualTo($"{pack.Code}.traineddata");
            await Assert.That(pack.Source.AbsoluteUri).IsEqualTo($"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/{pack.Code}.traineddata");
            await Assert.That(OcrLanguageCatalog.Find(pack.Code)).IsSameReferenceAs(pack);
        }

        await Assert.That(OcrLanguageCatalog.Find("xyz")).IsNull();
    }

    /// <summary>Language settings split into codes without blanks or repeats, and join back in Tesseract's form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesAndFormatsLanguages()
    {
        var codes = OcrLanguageCatalog.Parse(" eng + deu ++eng");

        await Assert.That(codes).IsEquivalentTo(["eng", "deu"]);
        await Assert.That(OcrLanguageCatalog.Format(codes)).IsEqualTo("eng+deu");
        await Assert.That(OcrLanguageCatalog.Parse(null)).IsEquivalentTo(["eng"]);
        await Assert.That(OcrLanguageCatalog.Parse("  ")).IsEquivalentTo(["eng"]);
        await Assert.That(OcrLanguageCatalog.Format([])).IsEqualTo("eng");
    }

    /// <summary>Sizes are described in whole megabytes, rounding up.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesSizes()
    {
        await Assert.That(OcrLanguageCatalog.DescribeSize(1)).IsEqualTo("about 1 MB");
        await Assert.That(OcrLanguageCatalog.DescribeSize(OcrLanguageCatalog.Packs[0].Bytes)).IsEqualTo("about 4 MB");
    }
}
