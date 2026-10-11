// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Loads /Type0 fonts: Identity and embedded CMaps, CIDToGIDMap, CID-keyed CFF, widths and vertical metrics.</summary>
public sealed class CompositeFontTests
{
    /// <summary>The glyph id, and Identity CID, of A in the generated TrueType font.</summary>
    private const int GlyphA = 34;

    /// <summary>The glyph id of B in the generated TrueType font.</summary>
    private const int GlyphB = 35;

    /// <summary>A CID with no /W entry.</summary>
    private const int UnlistedCid = 40;

    /// <summary>The top of A in the generated TrueType font.</summary>
    private const float TopA = 565;

    /// <summary>The /W width of A in text space.</summary>
    private const float WidthA = 0.5F;

    /// <summary>The /W width of B in text space.</summary>
    private const float WidthB = 0.7F;

    /// <summary>The /DW in text space.</summary>
    private const float DefaultWidth = 0.9F;

    /// <summary>The default vertical advance.</summary>
    private const float DefaultAdvance = -1;

    /// <summary>The default vertical origin y.</summary>
    private const float DefaultOriginY = 0.88F;

    /// <summary>The /W2 vertical advance of A.</summary>
    private const float AdvanceA = -0.9F;

    /// <summary>The /W2 vertical origin x of A.</summary>
    private const float OriginXA = 0.25F;

    /// <summary>The /W2 vertical origin y of A.</summary>
    private const float OriginYA = 0.8F;

    /// <summary>The tolerance of comparisons.</summary>
    private const float Tolerance = 0.0001F;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The code of hiragana a in Shift-JIS.</summary>
    private const int ShiftJisA = 0x82A0;

    /// <summary>The widths of A and B.</summary>
    private const string Widths = "/DW 900 /W [34 [500] 35 35 700]";

    /// <summary>The resources owned by this test.</summary>
    private readonly PdfFontResources _resources = new();

    /// <summary>An Identity font without ToUnicode extracts a supplementary scalar from its Adobe collection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentityCidFallbackExtractsSupplementaryText()
    {
        const int cid = 8061;
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);
        using var document = new FontTestDocument(TrueTypeSpec(string.Empty) with { CidOrdering = "Japan1" });
        await Assert.That(FontProbe.Text(document.Font, cid)).IsEqualTo("\U0001F100");
        await Assert.That(document.Font.GetUnicode(cid, new char[1])).IsEqualTo(0);
    }

    /// <summary>An Identity-H font reads two-byte codes, uses them as CIDs and glyph ids, and measures them by /W and /DW.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentityFontReadsTwoByteCids()
    {
        using var document = new FontTestDocument(TrueTypeSpec(Widths));
        var font = document.Font;

        await Assert.That(FontProbe.ReadCodes(font, [0, GlyphA, 0, GlyphB])).IsEquivalentTo([new CodeRead(GlyphA, 1 + 1), new CodeRead(GlyphB, 1 + 1)]);
        await Assert.That(font.GetWidth(GlyphA)).IsEqualTo(WidthA).Within(Tolerance);
        await Assert.That(font.GetWidth(GlyphB)).IsEqualTo(WidthB).Within(Tolerance);
        await Assert.That(font.GetWidth(UnlistedCid)).IsEqualTo(DefaultWidth).Within(Tolerance);
        await Assert.That(FontProbe.Bounds(font, GlyphA).Bottom).IsEqualTo(TopA).Within(Tolerance);
        await Assert.That(font.IsVertical).IsFalse();
        await Assert.That(font.IsWordSpace(' ', 1 + 1)).IsFalse();
    }

    /// <summary>A /CIDToGIDMap stream maps CIDs to glyph ids.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CidToGidMapStreamPicksGlyphs()
    {
        using var document = new FontTestDocument(TrueTypeSpec(string.Empty) with { CidToGidMap = [0, 0, 0, GlyphA] });

        await Assert.That(FontProbe.Bounds(document.Font, 1).Bottom).IsEqualTo(TopA).Within(Tolerance);
    }

    /// <summary>Identity-V writes vertically with /DW2 defaults, and /W2 entries override them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalFontUsesW2()
    {
        using var document = new FontTestDocument(TrueTypeSpec($"{Widths} /W2 [34 [-900 250 800]]") with { CidEncoding = "/Identity-V" });
        var font = document.Font;
        font.GetVerticalMetrics(GlyphA, out var advanceA, out var originXA, out var originYA);
        font.GetVerticalMetrics(GlyphB, out var advanceB, out var originXB, out var originYB);

        await Assert.That(font.IsVertical).IsTrue();
        await Assert.That(advanceA).IsEqualTo(AdvanceA).Within(Tolerance);
        await Assert.That(originXA).IsEqualTo(OriginXA).Within(Tolerance);
        await Assert.That(originYA).IsEqualTo(OriginYA).Within(Tolerance);
        await Assert.That(advanceB).IsEqualTo(DefaultAdvance).Within(Tolerance);
        await Assert.That(originXB).IsEqualTo(WidthB / (1 + 1)).Within(Tolerance);
        await Assert.That(originYB).IsEqualTo(DefaultOriginY).Within(Tolerance);
    }

    /// <summary>An embedded CMap with a one-byte codespace, a CID range and usecmap reads codes by its ranges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedCMapMapsCodesToCids()
    {
        const string cmap = "%!PS-Adobe-3.0 Resource-CMap\n/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Test def /Identity-H usecmap "
            + "1 begincodespacerange <00> <7F> endcodespacerange 1 begincidrange <41> <42> 34 endcidrange endcmap";
        using var document = new FontTestDocument(TrueTypeSpec(Widths) with { CidEncoding = cmap });
        var font = document.Font;

        await Assert.That(FontProbe.ReadCodes(font, "AB"u8.ToArray())).IsEquivalentTo([new CodeRead('A', 1), new CodeRead('B', 1)]);
        await Assert.That(font.GetWidth('B')).IsEqualTo(WidthB).Within(Tolerance);
        await Assert.That(FontProbe.Bounds(font, 'A').Bottom).IsEqualTo(TopA).Within(Tolerance);
    }

    /// <summary>A CIDFontType0 font with a CID-keyed CFF program finds glyphs through the CFF charset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CidKeyedCffFindsGlyphsByCid()
    {
        using var document = new FontTestDocument(new()
        {
            Subtype = "Type0",
            CidSubtype = "CIDFontType0",
            Program = TestFontPrograms.CidCff(),
            FileKey = "FontFile3",
            FileEntries = "/Subtype /CIDFontType0C",
            Flags = Symbolic,
        });
        var bounds = FontProbe.Bounds(document.Font, TestFontPrograms.CidB);

        await Assert.That(bounds.Right).IsEqualTo((float)(TestFontPrograms.Left + TestFontPrograms.BoxWidthB)).Within(Tolerance);
        await Assert.That(bounds.Bottom).IsEqualTo((float)TestFontPrograms.HeightB).Within(Tolerance);
        await Assert.That(document.Font.GetOutline(TestFontPrograms.CidA + 1 + 1)).IsNull();
    }

    /// <summary>A Uni*-UCS2 CMap, whose CID data is not bundled, still reads codes as Unicode for text and TrueType glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnicodeCMapFallsBackToTheUnicodeCmap()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "UniGB-UCS2-H", CancellationToken.None);

        using var document = new FontTestDocument(TrueTypeSpec(string.Empty) with { CidEncoding = "/UniGB-UCS2-H" });

        await Assert.That(FontProbe.Text(document.Font, 'A')).IsEqualTo("A");
        await Assert.That(FontProbe.Bounds(document.Font, 'A').Bottom).IsEqualTo(TopA).Within(Tolerance);
    }

    /// <summary>A Shift-JIS CMap splits strings into one- and two-byte codes and reads them through code page 932.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisCMapSplitsCodes()
    {
        await PredefinedCMaps.EnsureAsync(_resources, "90ms-RKSJ-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_resources, CjkScript.Japanese, CancellationToken.None);

        using var document = new FontTestDocument(TrueTypeSpec(string.Empty) with { CidEncoding = "/90ms-RKSJ-H" });
        var font = document.Font;

        await Assert.That(FontProbe.ReadCodes(font, [(byte)'A', 0x82, 0xA0])).IsEquivalentTo([new CodeRead('A', 1), new CodeRead(ShiftJisA, 1 + 1)]);
        await Assert.That(FontProbe.Text(font, ShiftJisA)).IsEqualTo("あ");
    }

    /// <summary>A composite font's /ToUnicode gives its text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToUnicodeGivesText()
    {
        const string cmap = "begincmap 1 begincodespacerange <0000> <FFFF> endcodespacerange 1 beginbfrange <0022> <0023> <0041> endbfrange endcmap";
        using var document = new FontTestDocument(TrueTypeSpec(string.Empty) with { ToUnicode = cmap });

        await Assert.That(FontProbe.Text(document.Font, GlyphA)).IsEqualTo("A");
        await Assert.That(FontProbe.Text(document.Font, GlyphB)).IsEqualTo("B");
    }

    /// <summary>Builds the spec of an Identity-H font embedding the generated TrueType font.</summary>
    /// <param name="cidEntries">Extra descendant entries.</param>
    /// <returns>The spec.</returns>
    private static FontSpec TrueTypeSpec(string cidEntries) => new()
    {
        Subtype = "Type0",
        CidSubtype = "CIDFontType2",
        Program = TestFont.Create(),
        FileKey = "FontFile2",
        Flags = Symbolic,
        CidEntries = cidEntries,
    };
}
