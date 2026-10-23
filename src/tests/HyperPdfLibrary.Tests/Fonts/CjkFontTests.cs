// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Loads CJK /Type0 fonts with predefined CMaps: CID widths, CID-to-Unicode text and CID-keyed CFF glyphs.</summary>
public sealed class CjkFontTests
{
    /// <summary>Hiragana a in Shift-JIS.</summary>
    private const int ShiftJisA = 0x82A0;

    /// <summary>Hiragana i in Shift-JIS.</summary>
    private const int ShiftJisI = 0x82A2;

    /// <summary>Zhong (U+4E2D) in GB 2312.</summary>
    private const int GbZhong = 0xD6D0;

    /// <summary>Han (U+D55C), the UCS-2 code.</summary>
    private const int Han = 0xD55C;

    /// <summary>The Adobe-Japan1 CID of hiragana a.</summary>
    private const int HiraganaACid = 843;

    /// <summary>The /W width of hiragana a in text space.</summary>
    private const float HiraganaAWidth = 0.5F;

    /// <summary>The /DW in text space.</summary>
    private const float DefaultWidth = 1;

    /// <summary>The tolerance of comparisons.</summary>
    private const float Tolerance = 0.0001F;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The resources owned by this test.</summary>
    private readonly PdfFontResources _resources = new();

    /// <summary>A Shift-JIS font measures codes by their CIDs' /W widths and gives their text through Adobe-Japan1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisFontUsesCidWidthsAndText()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "90ms-RKSJ-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        using var document = PdfDocumentReader.Open(CjkFontPdf.Build("90ms-RKSJ-H", "Japan1", CjkFontPdf.ShiftJisContent, CjkFontPdf.HiraganaAWidth), null);
        var font = CjkFontPdf.LoadFont(document);

        await Assert.That(font.GetWidth(ShiftJisA)).IsEqualTo(HiraganaAWidth).Within(Tolerance);
        await Assert.That(font.GetWidth(ShiftJisI)).IsEqualTo(DefaultWidth).Within(Tolerance);
        await Assert.That(FontProbe.Text(font, ShiftJisA)).IsEqualTo("あ");
        await Assert.That(FontProbe.Text(font, 'A')).IsEqualTo("A");
    }

    /// <summary>An Identity-H font of the Adobe-Japan1 collection without /ToUnicode gives text from its CIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentityFontUsesItsCollection()
    {
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        using var document = PdfDocumentReader.Open(CjkFontPdf.Build("Identity-H", "Japan1", CjkFontPdf.IdentityJapan1Content, string.Empty), null);
        var font = CjkFontPdf.LoadFont(document);

        await Assert.That(FontProbe.Text(font, HiraganaACid)).IsEqualTo("あ");
    }

    /// <summary>A GB-EUC-H font and a UniKS-UCS2-H font give their text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChineseAndKoreanFontsGiveText()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "GB-EUC-H", CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "UniKS-UCS2-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.SimplifiedChinese, CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Korean, CancellationToken.None);

        using var gb = PdfDocumentReader.Open(CjkFontPdf.Build("GB-EUC-H", "GB1", CjkFontPdf.GbEucContent, string.Empty), null);
        using var ks = PdfDocumentReader.Open(CjkFontPdf.Build("UniKS-UCS2-H", "Korea1", CjkFontPdf.UniKsContent, string.Empty), null);

        await Assert.That(FontProbe.Text(CjkFontPdf.LoadFont(gb), GbZhong)).IsEqualTo("中");
        await Assert.That(FontProbe.Text(CjkFontPdf.LoadFont(ks), Han)).IsEqualTo("한");
    }

    /// <summary>An embedded CID-keyed CFF program with a predefined CMap finds glyphs through the CMap's CIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CidKeyedCffDrawsThroughAPredefinedCMap()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "UniGB-UCS2-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.SimplifiedChinese, CancellationToken.None);

        using var document = new FontTestDocument(new()
        {
            Subtype = "Type0",
            CidSubtype = "CIDFontType0",
            Program = TestFontPrograms.CidCff(),
            FileKey = "FontFile3",
            FileEntries = "/Subtype /CIDFontType0C",
            Flags = Symbolic,
            CidEncoding = "/UniGB-UCS2-H",
        });

        // UniGB-UCS2-H maps U+0042 to Adobe-GB1 CID 35, the test program's B.
        var bounds = FontProbe.Bounds(document.Font, 'B');

        await Assert.That(bounds.Right).IsEqualTo((float)(TestFontPrograms.Left + TestFontPrograms.BoxWidthB)).Within(Tolerance);
        await Assert.That(bounds.Bottom).IsEqualTo((float)TestFontPrograms.HeightB).Within(Tolerance);
    }
}
