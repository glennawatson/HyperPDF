// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Tests for the standard 14 font metrics and the built-in encodings.</summary>
public sealed class StandardFontsTests
{
    /// <summary>The Helvetica width of A.</summary>
    private const float HelveticaA = 667;

    /// <summary>The Times-Roman width of a.</summary>
    private const float TimesA = 444;

    /// <summary>The width of every Courier glyph.</summary>
    private const float CourierWidth = 600;

    /// <summary>The Helvetica ascender.</summary>
    private const float HelveticaAscent = 718;

    /// <summary>The Helvetica descender.</summary>
    private const float HelveticaDescent = -207;

    /// <summary>The slant of Times-Italic.</summary>
    private const float TimesItalicAngle = -15.5F;

    /// <summary>The code of A.</summary>
    private const int CodeA = 65;

    /// <summary>The WinAnsi code of the euro sign.</summary>
    private const int WinAnsiEuro = 0x80;

    /// <summary>The PDFDocEncoding code of the euro sign.</summary>
    private const int PdfDocEuro = 0xA0;

    /// <summary>The StandardEncoding code of quoteright.</summary>
    private const int StandardQuoteRight = 0x27;

    /// <summary>The Symbol code of alpha.</summary>
    private const int SymbolAlpha = 0x61;

    /// <summary>The MacRoman code of Adieresis.</summary>
    private const int MacAdieresis = 0x80;

    /// <summary>Glyph widths come from the AFM files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidthsMatchAfm()
    {
        await Assert.That(Width("Helvetica"u8, "A"u8)).IsEqualTo(HelveticaA);
        await Assert.That(Width("Times-Roman"u8, "a"u8)).IsEqualTo(TimesA);
        await Assert.That(Width("Courier-Bold"u8, "W"u8)).IsEqualTo(CourierWidth);
    }

    /// <summary>A code reaches its width through the built-in encoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeWidthUsesBuiltInEncoding()
    {
        var metrics = StandardFonts.Get(StandardFont.Helvetica);
        var found = metrics.TryGetWidth(CodeA, out var width);

        await Assert.That(found).IsTrue();
        await Assert.That(width).IsEqualTo(HelveticaA);
    }

    /// <summary>Font metrics and flags come from the AFM files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetricsAndFlagsMatchAfm()
    {
        var helvetica = StandardFonts.Get(StandardFont.Helvetica);
        var courier = StandardFonts.Get(StandardFont.Courier);
        var timesItalic = StandardFonts.Get(StandardFont.TimesItalic);
        var symbol = StandardFonts.Get(StandardFont.Symbol);

        await Assert.That(helvetica.Ascent).IsEqualTo(HelveticaAscent);
        await Assert.That(helvetica.Descent).IsEqualTo(HelveticaDescent);
        await Assert.That(helvetica.IsSerif).IsFalse();
        await Assert.That(courier.IsFixedPitch).IsTrue();
        await Assert.That(timesItalic.ItalicAngle).IsEqualTo(TimesItalicAngle);
        await Assert.That(timesItalic.IsItalic).IsTrue();
        await Assert.That(timesItalic.IsSerif).IsTrue();
        await Assert.That(StandardFonts.Get(StandardFont.HelveticaBold).IsBold).IsTrue();
        await Assert.That(symbol.IsSymbolic).IsTrue();
        await Assert.That(symbol.BuiltInEncoding).IsEqualTo(FontEncoding.Symbol);
        await Assert.That((symbol.Flags & FontFlags.Nonsymbolic) == 0).IsTrue();
    }

    /// <summary>Common aliases and subset tags resolve to the standard fonts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AliasesResolve()
    {
        await Assert.That(StandardFonts.Find("Arial"u8)).IsEqualTo(StandardFont.Helvetica);
        await Assert.That(StandardFonts.Find("Arial,Bold"u8)).IsEqualTo(StandardFont.HelveticaBold);
        await Assert.That(StandardFonts.Find("Arial-BoldItalicMT"u8)).IsEqualTo(StandardFont.HelveticaBoldOblique);
        await Assert.That(StandardFonts.Find("TimesNewRoman"u8)).IsEqualTo(StandardFont.TimesRoman);
        await Assert.That(StandardFonts.Find("TimesNewRoman,BoldItalic"u8)).IsEqualTo(StandardFont.TimesBoldItalic);
        await Assert.That(StandardFonts.Find("CourierNew"u8)).IsEqualTo(StandardFont.Courier);
        await Assert.That(StandardFonts.Find("ABCDEF+Helvetica"u8)).IsEqualTo(StandardFont.Helvetica);
        await Assert.That(StandardFonts.Find("ZapfDingbats"u8)).IsEqualTo(StandardFont.ZapfDingbats);
        await Assert.That(StandardFonts.Find("Wingdings"u8)).IsEqualTo(StandardFont.None);
        await Assert.That(StandardFonts.TryGet("Helvetica-Oblique"u8, out var metrics)).IsTrue();
        await Assert.That(metrics.Name).IsEqualTo("Helvetica-Oblique");
    }

    /// <summary>The encodings map codes to the glyph names of PDF 32000-1 Annex D.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncodingsMapCodesToNames()
    {
        await Assert.That(Name(FontEncoding.WinAnsi, WinAnsiEuro)).IsEqualTo("Euro");
        await Assert.That(Name(FontEncoding.PdfDoc, PdfDocEuro)).IsEqualTo("Euro");
        await Assert.That(Name(FontEncoding.Standard, StandardQuoteRight)).IsEqualTo("quoteright");
        await Assert.That(Name(FontEncoding.WinAnsi, StandardQuoteRight)).IsEqualTo("quotesingle");
        await Assert.That(Name(FontEncoding.Symbol, SymbolAlpha)).IsEqualTo("alpha");
        await Assert.That(Name(FontEncoding.MacRoman, MacAdieresis)).IsEqualTo("Adieresis");
        await Assert.That(Name(FontEncoding.ZapfDingbats, CodeA)).IsEqualTo("a10");
        await Assert.That(FontEncodings.GetCode(FontEncoding.Standard, "A"u8)).IsEqualTo(CodeA);
        await Assert.That(FontEncodings.GetCode(FontEncoding.Standard, "Euro"u8)).IsEqualTo(-1);
    }

    /// <summary>Gets a glyph width.</summary>
    /// <param name="font">The base font name.</param>
    /// <param name="glyph">The glyph name.</param>
    /// <returns>The width, or -1.</returns>
    private static float Width(ReadOnlySpan<byte> font, ReadOnlySpan<byte> glyph) =>
        StandardFonts.TryGet(font, out var metrics) && metrics.TryGetWidth(glyph, out var width) ? width : -1;

    /// <summary>Gets the glyph name of a code.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name.</returns>
    private static string Name(FontEncoding encoding, int code) => Encoding.ASCII.GetString(FontEncodings.GetGlyphName(encoding, code));
}
