// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>
/// Reads the packed predefined CMaps and CID-to-Unicode tables. The expected CIDs and Unicode values come from Adobe's
/// cmap-resources.
/// </summary>
public sealed class PredefinedCMapTests
{
    /// <summary>Hiragana a in Shift-JIS.</summary>
    private const int ShiftJisA = 0x82A0;

    /// <summary>The ideographic comma in Shift-JIS.</summary>
    private const int ShiftJisComma = 0x8141;

    /// <summary>The Adobe-Japan1 CID of hiragana a.</summary>
    private const int HiraganaACid = 843;

    /// <summary>The Adobe-Japan1 CID of the proportional Latin A.</summary>
    private const int LatinACid = 264;

    /// <summary>The Adobe-Japan1 CID of the horizontal ideographic comma.</summary>
    private const int CommaCid = 634;

    /// <summary>The Adobe-Japan1 CID of the vertical ideographic comma.</summary>
    private const int VerticalCommaCid = 7887;

    /// <summary>Hiragana a.</summary>
    private const int HiraganaA = 0x3042;

    /// <summary>The ideographic comma.</summary>
    private const int IdeographicComma = 0x3001;

    /// <summary>A (U+554A) in GB 2312.</summary>
    private const int GbA = 0xB0A1;

    /// <summary>The Adobe-GB1 CID of U+554A.</summary>
    private const int GbACid = 940;

    /// <summary>The code point of the hanzi ah.</summary>
    private const int Ah = 0x554A;

    /// <summary>Zhong (U+4E2D) in GB 2312.</summary>
    private const int GbZhong = 0xD6D0;

    /// <summary>The Adobe-GB1 CID of U+4E2D.</summary>
    private const int ZhongCid = 4559;

    /// <summary>The code point of the hanzi zhong.</summary>
    private const int Zhong = 0x4E2D;

    /// <summary>One (U+4E00) in Big5.</summary>
    private const int Big5One = 0xA440;

    /// <summary>The Adobe-CNS1 CID of U+4E00.</summary>
    private const int OneCid = 595;

    /// <summary>The code point of the hanzi one.</summary>
    private const int One = 0x4E00;

    /// <summary>A CNS 11643 plane 1 code in its two-byte EUC form.</summary>
    private const int CnsTwoByte = 0xA4A1;

    /// <summary>The same code in its four-byte EUC form.</summary>
    private const uint CnsFourByte = 0x8EA1A4A1;

    /// <summary>The Adobe-CNS1 CID of both CNS codes.</summary>
    private const int CnsCid = 333;

    /// <summary>Ga (U+AC00) in KS X 1001 EUC.</summary>
    private const int KscGa = 0xB0A1;

    /// <summary>The code point of the hangul syllable ga.</summary>
    private const int Ga = 0xAC00;

    /// <summary>The Adobe-Korea1 CID of U+AC00.</summary>
    private const int GaCid = 1086;

    /// <summary>The code of an unknown CMap name.</summary>
    private const int UnknownCode = 0x1234;

    /// <summary>The bytes of a four-byte code.</summary>
    private const int FourBytes = 4;

    /// <summary>The bytes of a two-byte code.</summary>
    private const int TwoBytes = 2;

    /// <summary>The CJK CMap names of ISO 32000-2 Table 116, with the UTF-32 ones.</summary>
    private static readonly string[] Names =
    [
        "GB-EUC-H", "GB-EUC-V", "GBpc-EUC-H", "GBpc-EUC-V", "GBK-EUC-H", "GBK-EUC-V", "GBKp-EUC-H", "GBKp-EUC-V", "GBK2K-H", "GBK2K-V",
        "UniGB-UCS2-H", "UniGB-UCS2-V", "UniGB-UTF16-H", "UniGB-UTF16-V", "UniGB-UTF32-H", "UniGB-UTF32-V",
        "B5pc-H", "B5pc-V", "HKscs-B5-H", "HKscs-B5-V", "ETen-B5-H", "ETen-B5-V", "ETenms-B5-H", "ETenms-B5-V", "CNS-EUC-H", "CNS-EUC-V",
        "UniCNS-UCS2-H", "UniCNS-UCS2-V", "UniCNS-UTF16-H", "UniCNS-UTF16-V", "UniCNS-UTF32-H", "UniCNS-UTF32-V",
        "83pv-RKSJ-H", "90ms-RKSJ-H", "90ms-RKSJ-V", "90msp-RKSJ-H", "90msp-RKSJ-V", "90pv-RKSJ-H", "Add-RKSJ-H", "Add-RKSJ-V",
        "EUC-H", "EUC-V", "Ext-RKSJ-H", "Ext-RKSJ-V", "H", "V", "UniJIS-UCS2-H", "UniJIS-UCS2-V", "UniJIS-UCS2-HW-H", "UniJIS-UCS2-HW-V",
        "UniJIS-UTF16-H", "UniJIS-UTF16-V", "UniJIS-UTF32-H", "UniJIS-UTF32-V",
        "KSC-EUC-H", "KSC-EUC-V", "KSCms-UHC-H", "KSCms-UHC-V", "KSCms-UHC-HW-H", "KSCms-UHC-HW-V", "KSCpc-EUC-H",
        "UniKS-UCS2-H", "UniKS-UCS2-V", "UniKS-UTF16-H", "UniKS-UTF16-V", "UniKS-UTF32-H", "UniKS-UTF32-V",
    ];

    /// <summary>The resources owned by this test.</summary>
    private readonly PdfFontResources _resources = new();

    /// <summary>Gets the name of the horizontal Microsoft Shift-JIS CMap.</summary>
    private static ReadOnlySpan<byte> ShiftJisName => "90ms-RKSJ-H"u8;

    /// <summary>Every CJK CMap of Table 116 loads, maps codes and writes in the direction its name says.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryTableCMapLoads()
    {
        foreach (var name in Names)
        {
            await PredefinedCMaps.EnsureAsync(_resources, name, CancellationToken.None);
            var cmap = PredefinedCMaps.Find(_resources, Encoding.ASCII.GetBytes(name));

            await Assert.That(cmap).IsNotNull();
            await Assert.That(cmap!.Collection).IsNotEqualTo(CjkScript.None);
            await Assert.That(cmap.IsVertical).IsEqualTo(name.EndsWith('V'));
        }
    }

    /// <summary>90ms-RKSJ-H splits Shift-JIS into one- and two-byte codes and maps them to Adobe-Japan1 CIDs and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisMapsToJapan1()
    {
        await PredefinedCMaps.EnsureAsync(_resources, Encoding.ASCII.GetString(ShiftJisName), CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        var cmap = PredefinedCMaps.Get(_resources, ShiftJisName);
        var japan1 = CidToUnicodeTable.Get(_resources, CjkScript.Japanese);

        await Assert.That(cmap.Map.ReadCode([(byte)'A', 0x82, 0xA0], out var latin)).IsEqualTo(1);
        await Assert.That(latin).IsEqualTo('A');
        await Assert.That(cmap.Map.ReadCode([0x82, 0xA0], out var kana)).IsEqualTo(TwoBytes);
        await Assert.That(kana).IsEqualTo(ShiftJisA);
        await Assert.That(cmap.ToCid(ShiftJisA)).IsEqualTo(HiraganaACid);
        await Assert.That(cmap.ToCid('A')).IsEqualTo(LatinACid);
        await Assert.That(cmap.Coding).IsEqualTo(CidCoding.Native);
        await Assert.That(cmap.Collection).IsEqualTo(CjkScript.Japanese);
        await Assert.That(japan1!.Lookup(HiraganaACid)).IsEqualTo(HiraganaA);
        await Assert.That(cmap.ToUnicode(ShiftJisA, japan1)).IsEqualTo(HiraganaA);
        await Assert.That(PredefinedCMaps.Get(_resources, ShiftJisName)).IsSameReferenceAs(cmap);
    }

    /// <summary>90ms-RKSJ-V overrides the vertical forms and finds the rest through its base, 90ms-RKSJ-H.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalCMapUsesItsBase()
    {
        await PredefinedCMaps.EnsureAsync(_resources, Encoding.ASCII.GetString(ShiftJisName), CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "90ms-RKSJ-V", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        var horizontal = PredefinedCMaps.Get(_resources, ShiftJisName);
        var vertical = PredefinedCMaps.Get(_resources, "90ms-RKSJ-V"u8);
        var japan1 = CidToUnicodeTable.Get(_resources, CjkScript.Japanese);

        await Assert.That(vertical.IsVertical).IsTrue();
        await Assert.That(horizontal.ToCid(ShiftJisComma)).IsEqualTo(CommaCid);
        await Assert.That(vertical.ToCid(ShiftJisComma)).IsEqualTo(VerticalCommaCid);
        await Assert.That(vertical.ToCid(ShiftJisA)).IsEqualTo(HiraganaACid);
        await Assert.That(vertical.ToUnicode(ShiftJisComma, japan1)).IsEqualTo(IdeographicComma);
    }

    /// <summary>GB-EUC-H maps GB 2312 codes to Adobe-GB1 CIDs and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GbEucMapsToGb1()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "GB-EUC-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.SimplifiedChinese, CancellationToken.None);

        var cmap = PredefinedCMaps.Get(_resources, "GB-EUC-H"u8);
        var gb1 = CidToUnicodeTable.Get(_resources, CjkScript.SimplifiedChinese);

        await Assert.That(cmap.ToCid(GbA)).IsEqualTo(GbACid);
        await Assert.That(cmap.ToCid(GbZhong)).IsEqualTo(ZhongCid);
        await Assert.That(gb1!.Lookup(GbACid)).IsEqualTo(Ah);
        await Assert.That(cmap.ToUnicode(GbZhong, gb1)).IsEqualTo(Zhong);
    }

    /// <summary>ETen-B5-H and CNS-EUC-H map Big5 and four-byte EUC codes to Adobe-CNS1 CIDs and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChineseTraditionalMapsToCns1()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "ETen-B5-H", CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "CNS-EUC-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.TraditionalChinese, CancellationToken.None);

        var big5 = PredefinedCMaps.Get(_resources, "ETen-B5-H"u8);
        var euc = PredefinedCMaps.Get(_resources, "CNS-EUC-H"u8);
        var cns1 = CidToUnicodeTable.Get(_resources, CjkScript.TraditionalChinese);

        await Assert.That(big5.ToCid(Big5One)).IsEqualTo(OneCid);
        await Assert.That(cns1!.Lookup(OneCid)).IsEqualTo(One);
        await Assert.That(euc.Map.ReadCode([0x8E, 0xA1, 0xA4, 0xA1], out var code)).IsEqualTo(FourBytes);
        await Assert.That((uint)code).IsEqualTo(CnsFourByte);
        await Assert.That(euc.ToCid(code)).IsEqualTo(CnsCid);
        await Assert.That(euc.ToCid(CnsTwoByte)).IsEqualTo(CnsCid);
    }

    /// <summary>KSC-EUC-H and UniKS-UCS2-H map to the same Adobe-Korea1 CID; the UCS-2 code is its own text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KoreanMapsToKorea1()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "KSC-EUC-H", CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "UniKS-UCS2-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Korean, CancellationToken.None);

        var euc = PredefinedCMaps.Get(_resources, "KSC-EUC-H"u8);
        var ucs2 = PredefinedCMaps.Get(_resources, "UniKS-UCS2-H"u8);
        var korea1 = CidToUnicodeTable.Get(_resources, CjkScript.Korean);

        await Assert.That(euc.ToCid(KscGa)).IsEqualTo(GaCid);
        await Assert.That(ucs2.ToCid(Ga)).IsEqualTo(GaCid);
        await Assert.That(korea1!.Lookup(GaCid)).IsEqualTo(Ga);
        await Assert.That(ucs2.Coding).IsEqualTo(CidCoding.Ucs2);
        await Assert.That(ucs2.ToUnicode(Ga, null)).IsEqualTo(Ga);
    }

    /// <summary>The UTF-16 and UTF-32 CMaps read their codes and map them like the UCS-2 one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnicodeEncodingsAgree()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "UniJIS-UTF16-H", CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_resources, "UniJIS-UTF32-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        var utf16 = PredefinedCMaps.Get(_resources, "UniJIS-UTF16-H"u8);
        var utf32 = PredefinedCMaps.Get(_resources, "UniJIS-UTF32-H"u8);

        await Assert.That(utf32.Map.ReadCode([0, 0, 0x30, 0x42], out var code)).IsEqualTo(FourBytes);
        await Assert.That(code).IsEqualTo(HiraganaA);
        await Assert.That(utf32.ToCid(HiraganaA)).IsEqualTo(HiraganaACid);
        await Assert.That(utf32.ToUnicode(HiraganaA, null)).IsEqualTo(HiraganaA);
        await Assert.That(utf16.ToCid(HiraganaA)).IsEqualTo(HiraganaACid);
        await Assert.That(utf16.Coding).IsEqualTo(CidCoding.Utf16);
    }

    /// <summary>An unknown name reads two-byte codes as CIDs with no text, as PDFium does; a final V makes it vertical.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownNameUsesCodesAsCids()
    {
        var cmap = PredefinedCMaps.Get(_resources, "Unknown-V"u8);

        await Assert.That(PredefinedCMaps.Find(_resources, "Unknown-V"u8)).IsNull();
        await Assert.That(cmap.IsVertical).IsTrue();
        await Assert.That(cmap.Coding).IsEqualTo(CidCoding.Unknown);
        await Assert.That(cmap.ToCid(UnknownCode)).IsEqualTo(UnknownCode);
        await Assert.That(cmap.ToUnicode(UnknownCode, CidToUnicodeTable.Get(_resources, CjkScript.Japanese))).IsEqualTo(0);
        await Assert.That(PredefinedCMaps.Get(_resources, "Identity-H"u8).Coding).IsEqualTo(CidCoding.Cid);
    }
}
