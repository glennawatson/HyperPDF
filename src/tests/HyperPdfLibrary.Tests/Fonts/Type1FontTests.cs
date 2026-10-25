// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Fonts;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Loads Type 1 fonts: embedded Type 1 and CFF programs, and the standard 14 fonts without programs.</summary>
public sealed class Type1FontTests
{
    /// <summary>The code of A.</summary>
    private const int CodeA = 'A';

    /// <summary>The code of B.</summary>
    private const int CodeB = 'B';

    /// <summary>The code of a, which the Symbol font draws as alpha.</summary>
    private const int CodeAlpha = 'a';

    /// <summary>The code of the first ZapfDingbats glyph, a1.</summary>
    private const int CodeFirstDingbat = 0x21;

    /// <summary>The Helvetica width of A in text space.</summary>
    private const float HelveticaWidthA = 0.667F;

    /// <summary>The Times-Roman width of the space in text space.</summary>
    private const float TimesWidthSpace = 0.25F;

    /// <summary>The Courier width of every glyph in text space.</summary>
    private const float CourierWidth = 0.6F;

    /// <summary>The Symbol width of alpha in text space.</summary>
    private const float SymbolWidthAlpha = 0.631F;

    /// <summary>The ZapfDingbats width of a1 in text space.</summary>
    private const float DingbatWidth = 0.974F;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The tolerance of comparisons.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The glyph units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The font subtype under test.</summary>
    private const string Type1Subtype = "Type1";

    /// <summary>The descriptor key of a Type 1 program.</summary>
    private const string Type1FileKey = "FontFile";

    /// <summary>An embedded Type 1 program maps codes through its built-in encoding and draws its rectangles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedType1UsesItsBuiltInEncoding()
    {
        var program = TestFontPrograms.Type1(out var length1);
        using var document = new FontTestDocument(new()
        {
            Subtype = Type1Subtype,
            Program = program,
            FileKey = Type1FileKey,
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1} /Length2 {program.Length - length1} /Length3 0"),
            Flags = Symbolic,
        });

        await AssertRectangles(document.Font);
        await Assert.That(FontProbe.Text(document.Font, CodeA)).IsEqualTo("A");
        await Assert.That(document.Font.GetWidth(CodeA)).IsEqualTo(TestFontPrograms.WidthA / GlyphUnits).Within(Tolerance);
    }

    /// <summary>An embedded non-symbolic Type 1 program finds glyphs by the names of /Encoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedType1FindsGlyphsByName()
    {
        var program = TestFontPrograms.Type1(out var length1);
        using var document = new FontTestDocument(new()
        {
            Subtype = Type1Subtype,
            Program = program,
            FileKey = Type1FileKey,
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Nonsymbolic,
            Entries = "/Encoding << /Differences [1 /B 2 /A] >>",
        });

        await Assert.That(FontProbe.Bounds(document.Font, 1).Right).IsEqualTo(FontProbe.Bounds(document.Font, CodeB).Right);
        await Assert.That(FontProbe.Bounds(document.Font, 1).Right).IsEqualTo((float)(TestFontPrograms.Left + TestFontPrograms.BoxWidthB)).Within(Tolerance);
        await Assert.That(FontProbe.Text(document.Font, 1)).IsEqualTo("B");
        await Assert.That(FontProbe.Text(document.Font, 1 + 1)).IsEqualTo("A");
    }

    /// <summary>An embedded CFF program (FontFile3 /Type1C) maps codes through StandardEncoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedCffDrawsItsGlyphs()
    {
        using var document = new FontTestDocument(new() { Subtype = Type1Subtype, Program = TestFontPrograms.Cff(), FileKey = "FontFile3", FileEntries = "/Subtype /Type1C", Flags = Nonsymbolic, });

        await AssertRectangles(document.Font);
        await Assert.That(document.Font.GetWidth(CodeB)).IsEqualTo(TestFontPrograms.WidthB / GlyphUnits).Within(Tolerance);
    }

    /// <summary>A program under the wrong key is still found by its signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgramUnderTheWrongKeyIsSniffed()
    {
        using var document = new FontTestDocument(new() { Subtype = Type1Subtype, Program = TestFontPrograms.Cff(), FileKey = Type1FileKey, Flags = Nonsymbolic, });

        await AssertRectangles(document.Font);
    }

    /// <summary>The standard 14 fonts take their widths from the built-in metrics.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardFontsUseBuiltInMetrics()
    {
        using var helvetica = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "Helvetica", Flags = -1 });
        using var times = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "Times-Roman", Flags = -1, Entries = "/Encoding /WinAnsiEncoding" });
        using var courier = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "Courier-Bold", Flags = -1 });

        await Assert.That(helvetica.Font.GetWidth(CodeA)).IsEqualTo(HelveticaWidthA).Within(Tolerance);
        await Assert.That(times.Font.GetWidth(' ')).IsEqualTo(TimesWidthSpace).Within(Tolerance);
        await Assert.That(courier.Font.GetWidth(CodeA)).IsEqualTo(CourierWidth).Within(Tolerance);
        await Assert.That(courier.Font.IsBold).IsTrue();
        await Assert.That(FontProbe.Text(helvetica.Font, CodeA)).IsEqualTo("A");
    }

    /// <summary>The Symbol and ZapfDingbats fonts read codes through their own encodings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolFontsUseTheirOwnEncodings()
    {
        using var symbol = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "Symbol", Flags = -1 });
        using var dingbats = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "ZapfDingbats", Flags = -1 });

        await Assert.That(FontProbe.Text(symbol.Font, CodeAlpha)).IsEqualTo("α");
        await Assert.That(symbol.Font.GetWidth(CodeAlpha)).IsEqualTo(SymbolWidthAlpha).Within(Tolerance);
        await Assert.That(FontProbe.Text(dingbats.Font, CodeFirstDingbat)).IsEqualTo("✁");
        await Assert.That(dingbats.Font.GetWidth(CodeFirstDingbat)).IsEqualTo(DingbatWidth).Within(Tolerance);
    }

    /// <summary>A standard font draws with a system font when the machine has any font at all.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardFontDrawsWithASystemFont()
    {
        if (SKFontManager.Default.FontFamilyCount == 0)
        {
            Skip.Test("The machine has no system fonts.");
        }

        using var document = new FontTestDocument(new() { Subtype = Type1Subtype, BaseFont = "Helvetica", Flags = -1 });
        var bounds = FontProbe.Bounds(document.Font, CodeA);

        await Assert.That(bounds.IsEmpty).IsFalse();
        await Assert.That(bounds.Bottom).IsGreaterThan(0F);
    }

    /// <summary>Checks the A and B rectangles of the test programs.</summary>
    /// <param name="font">The font.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRectangles(PdfFont font)
    {
        var a = FontProbe.Bounds(font, CodeA);
        var b = FontProbe.Bounds(font, CodeB);

        await Assert.That(a.Left).IsEqualTo((float)TestFontPrograms.Left).Within(Tolerance);
        await Assert.That(a.Right).IsEqualTo((float)(TestFontPrograms.Left + TestFontPrograms.BoxWidthA)).Within(Tolerance);
        await Assert.That(a.Bottom).IsEqualTo((float)TestFontPrograms.HeightA).Within(Tolerance);
        await Assert.That(b.Bottom).IsEqualTo((float)TestFontPrograms.HeightB).Within(Tolerance);
    }
}
