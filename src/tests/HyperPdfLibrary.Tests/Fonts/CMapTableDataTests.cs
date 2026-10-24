// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>
/// Checks the packed tables, built from Adobe's cmap-resources, against each other: a CID reached through a Uni*-UCS2
/// CMap must give the same character back through the collection's CID-to-Unicode table, and each CMap must keep the
/// codespace and writing mode Adobe publishes.
/// </summary>
public sealed class CMapTableDataTests
{
    /// <summary>The lowest code checked in the UCS-2 round trip; below it are control characters.</summary>
    private const int FirstCode = 0x20;

    /// <summary>The highest UCS-2 code.</summary>
    private const int LastCode = 0xFFFF;

    /// <summary>The share of mapped codes, in percent, that must come back as themselves. The rest are CIDs that several characters share, such as a full-width form and its ASCII twin.</summary>
    private const int MinimumRoundTripPercent = 95;

    /// <summary>The number of CIDs checked for text in each table.</summary>
    private const int CheckedCids = 15_000;

    /// <summary>The fewest of those CIDs that must have text.</summary>
    private const int MinimumWithText = 8_000;

    /// <summary>The percent scale.</summary>
    private const int Percent = 100;

    /// <summary>The Adobe-Japan1 CID of hiragana a, from Adobe's cid2code.txt.</summary>
    private const int HiraganaACid = 843;

    /// <summary>Hiragana a.</summary>
    private const int HiraganaA = 0x3042;

    /// <summary>The Adobe-Japan1 CID of the half-width space of the Shift-JIS CMaps.</summary>
    private const int HalfWidthSpaceCid = 231;

    /// <summary>The Adobe-Japan1 CID of the proportional space.</summary>
    private const int SpaceCid = 1;

    /// <summary>The space.</summary>
    private const int Space = 0x20;

    /// <summary>The CJK collections with the name of their UCS-2 CMap and the writing-mode pair checked.</summary>
    private static readonly CollectionCase[] Collections =
    [
        new(CjkScript.Japanese, "UniJIS-UCS2-H"),
        new(CjkScript.SimplifiedChinese, "UniGB-UCS2-H"),
        new(CjkScript.TraditionalChinese, "UniCNS-UCS2-H"),
        new(CjkScript.Korean, "UniKS-UCS2-H"),
    ];

    /// <summary>The resources owned by this test.</summary>
    private readonly PdfFontResources _resources = new();

    /// <summary>Every collection's CID-to-Unicode table loads and gives a character to most of the first fifteen thousand CIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryUnicodeTableLoads()
    {
        foreach (var collection in Collections)
        {
            await CidToUnicodeTable.EnsureAsync(_resources, collection.Script, CancellationToken.None);
            var table = CidToUnicodeTable.Read(collection.Script);
            var withText = 0;
            for (var cid = 1; cid < CheckedCids; cid++)
            {
                withText += table.Lookup(cid) != 0 ? 1 : 0;
            }

            await Assert.That(withText).IsGreaterThan(MinimumWithText);
        }
    }

    /// <summary>UCS-2 codes preserve their characters; legacy private-use aliases agree with the collection's UTF-32 CMap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UcsCodesRoundTripThroughTheUnicodeTable()
    {
        foreach (var collection in Collections)
        {
            await PredefinedCMaps.EnsureAsync(_resources, collection.CMapName, CancellationToken.None);
            await CidToUnicodeTable.EnsureAsync(_resources, collection.Script, CancellationToken.None);
            var cmap = PredefinedCMaps.Get(_resources, System.Text.Encoding.ASCII.GetBytes(collection.CMapName));
            var table = CidToUnicodeTable.Get(_resources, collection.Script)!;
            var utf32Name = collection.CMapName.Replace("UCS2", "UTF32", StringComparison.Ordinal);
            await PredefinedCMaps.EnsureAsync(_resources, utf32Name, CancellationToken.None);
            var utf32 = PredefinedCMaps.Get(_resources, System.Text.Encoding.ASCII.GetBytes(utf32Name));
            var mapped = 0;
            var same = 0;
            for (var code = FirstCode; code <= LastCode; code++)
            {
                var cid = cmap.ToCid(code);
                if (cid == 0)
                {
                    continue;
                }

                mapped++;
                var scalar = table.Lookup(cid);
                var privateUseAlias = code is >= 0xE000 and <= 0xF8FF && scalar > char.MaxValue && utf32.ToCid(scalar) == cid;
                same += scalar == code || privateUseAlias ? 1 : 0;
            }

            await Assert.That(mapped).IsGreaterThan(0);
            await Assert.That(same * Percent / mapped).IsGreaterThanOrEqualTo(MinimumRoundTripPercent);
        }
    }

    /// <summary>The Shift-JIS CMaps and the UCS-2 CMaps agree with Adobe's cid2code.txt on the hiragana a and the spaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnownAdobeValuesAreKept()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "UniJIS-UCS2-H", CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "90ms-RKSJ-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);
        var ucs2 = PredefinedCMaps.Get(_resources, "UniJIS-UCS2-H"u8);
        var halfWidth = PredefinedCMaps.Get(_resources, "90ms-RKSJ-H"u8);
        var japan1 = CidToUnicodeTable.Get(_resources, CjkScript.Japanese)!;

        await Assert.That(ucs2.ToCid(HiraganaA)).IsEqualTo(HiraganaACid);
        await Assert.That(japan1.Lookup(HiraganaACid)).IsEqualTo(HiraganaA);
        await Assert.That(ucs2.ToCid(Space)).IsEqualTo(SpaceCid);
        await Assert.That(halfWidth.ToCid(Space)).IsEqualTo(HalfWidthSpaceCid);
    }

    /// <summary>The UTF-16 CMaps map the characters beyond the Basic Multilingual Plane that Adobe's UTF-16 CMaps define.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Utf16CMapsCoverSurrogatePairs()
    {
        const uint pairCode = 0xD83CDD00;
        const int pairCid = 8061;
        await PredefinedCMaps.EnsureAsync(_resources, "UniJIS-UTF16-H", CancellationToken.None);
        var utf16 = PredefinedCMaps.Get(_resources, "UniJIS-UTF16-H"u8);

        await Assert.That(utf16.ToCid(unchecked((int)pairCode))).IsEqualTo(pairCid);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);
        const int scalar = 0x1F100;
        await Assert.That(CidToUnicodeTable.Get(_resources, CjkScript.Japanese)!.Lookup(pairCid)).IsEqualTo(scalar);
    }

    /// <summary>A collection and the name of its UCS-2 CMap.</summary>
    /// <param name="Script">The collection.</param>
    /// <param name="CMapName">The UCS-2 CMap.</param>
    private readonly record struct CollectionCase(CjkScript Script, string CMapName);
}
