// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks the faces embedded in the library assembly: present, whole, deterministic and covering the standard fonts' characters.</summary>
public sealed class EmbeddedFaceResourceTests
{
    /// <summary>The number of embedded faces.</summary>
    private const int FaceCount = 15;

    /// <summary>A family number no family has.</summary>
    private const PdfBundledFontFamily UndefinedFamily = (PdfBundledFontFamily)99;

    /// <summary>The Helvetica width of A in glyph units.</summary>
    private const float HelveticaWidthA = 667F;

    /// <summary>The Courier width of every glyph in glyph units.</summary>
    private const float CourierWidth = 600F;

    /// <summary>The tolerance of width comparisons, in glyph units.</summary>
    private const float WidthTolerance = 1F;

    /// <summary>The characters of Windows ANSI beyond ASCII that the text faces must cover.</summary>
    private const string WinAnsiExtras = "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ¡¢£¤¥¦§¨©ª«¬®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ";

    /// <summary>The resource names of every face, without the extension.</summary>
    private static readonly string[] Names =
    [
        "Arimo-Regular", "Arimo-Bold", "Arimo-Italic", "Arimo-BoldItalic",
        "Tinos-Regular", "Tinos-Bold", "Tinos-Italic", "Tinos-BoldItalic",
        "Cousine-Regular", "Cousine-Bold", "Cousine-Italic", "Cousine-BoldItalic",
        "NotoSansMath-Regular", "NotoSansSymbols2-Regular", "NotoEmoji-Regular",
    ];

    /// <summary>Gets every family with its four styles (the symbol and emoji families have one).</summary>
    /// <returns>The cases.</returns>
    public static IEnumerable<Func<FaceCase>> Faces() =>
    [
        static () => new(PdfBundledFontFamily.Sans, false, false),
        static () => new(PdfBundledFontFamily.Sans, true, false),
        static () => new(PdfBundledFontFamily.Sans, false, true),
        static () => new(PdfBundledFontFamily.Sans, true, true),
        static () => new(PdfBundledFontFamily.Serif, false, false),
        static () => new(PdfBundledFontFamily.Serif, true, false),
        static () => new(PdfBundledFontFamily.Serif, false, true),
        static () => new(PdfBundledFontFamily.Serif, true, true),
        static () => new(PdfBundledFontFamily.Fixed, false, false),
        static () => new(PdfBundledFontFamily.Fixed, true, false),
        static () => new(PdfBundledFontFamily.Fixed, false, true),
        static () => new(PdfBundledFontFamily.Fixed, true, true),
        static () => new(PdfBundledFontFamily.Symbol, false, false),
        static () => new(PdfBundledFontFamily.Dingbats, false, false),
        static () => new(PdfBundledFontFamily.Emoji, false, false),
    ];

    /// <summary>Every face is an assembly resource, so loading it needs no file or network.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryFaceIsAnAssemblyResource()
    {
        var resources = typeof(PdfBundledFonts).Assembly.GetManifestResourceNames();

        foreach (var name in Names)
        {
            await Assert.That(resources).Contains($"HyperPdfLibrary.Faces.{name}.ttf.z");
        }

        await Assert.That(Names.Length).IsEqualTo(FaceCount);
    }

    /// <summary>Every face opens as an embeddable font with glyphs, and the same instance is returned each time.</summary>
    /// <param name="make">Makes the family and style.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task EveryFaceOpensAndIsShared(FaceCase make)
    {
        var (family, bold, italic) = make;
        var services = new PdfDrawingServices(new());
        var first = PdfBundledFonts.Open(services, family, bold, italic);
        var second = PdfBundledFonts.Open(services, family, bold, italic);

        await Assert.That(first.IsEmbeddable).IsTrue().Because(first.Refusal ?? string.Empty);
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(first.Source!.GlyphCount).IsGreaterThan(1);
        await Assert.That(first.Source.CanSubset).IsTrue();
    }

    /// <summary>The styles of a family differ in the style flags of the opened font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StylesCarryTheirFlags()
    {
        var regular = PdfBundledFonts.Open(PdfBundledFontFamily.Serif, false, false).Source!;
        var boldItalic = PdfBundledFonts.Open(PdfBundledFontFamily.Serif, true, true).Source!;
        var fixedPitch = PdfBundledFonts.Open(PdfBundledFontFamily.Fixed, false, false).Source!;

        await Assert.That(regular.IsBold).IsFalse();
        await Assert.That(regular.IsItalic).IsFalse();
        await Assert.That(boldItalic.IsBold).IsTrue();
        await Assert.That(boldItalic.IsItalic).IsTrue();
        await Assert.That(fixedPitch.IsFixedPitch).IsTrue();
    }

    /// <summary>The text faces cover ASCII and the Windows ANSI repertoire, so built-in font text can move to them unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextFacesCoverWindowsAnsi()
    {
        foreach (var family in new[] { PdfBundledFontFamily.Sans, PdfBundledFontFamily.Serif, PdfBundledFontFamily.Fixed })
        {
            var source = PdfBundledFonts.Open(family, false, false).Source!;
            for (var c = ' '; c <= '~'; c++)
            {
                await Assert.That(source.GetGlyph(c)).IsNotEqualTo(0).Because($"{family} lacks U+{(int)c:X4}");
            }

            foreach (var c in WinAnsiExtras)
            {
                await Assert.That(source.GetGlyph(c)).IsNotEqualTo(0).Because($"{family} lacks U+{(int)c:X4}");
            }
        }
    }

    /// <summary>The faces keep the widths of the fonts they replace: Helvetica 667 for A and Courier 600 for every glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FacesKeepStandardWidths()
    {
        var sans = PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!;
        var fixedPitch = PdfBundledFonts.Open(PdfBundledFontFamily.Fixed, false, false).Source!;

        await Assert.That(sans.GetAdvance(sans.GetGlyph('A'))).IsEqualTo(HelveticaWidthA).Within(WidthTolerance);
        await Assert.That(fixedPitch.GetAdvance(fixedPitch.GetGlyph('A'))).IsEqualTo(CourierWidth).Within(WidthTolerance);
    }

    /// <summary>The Symbol and Dingbats faces cover the characters of their standard fonts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolFacesCoverTheirStandardCharacters()
    {
        var symbol = PdfBundledFonts.Open(PdfBundledFontFamily.Symbol, false, false).Source!;
        var dingbats = PdfBundledFonts.Open(PdfBundledFontFamily.Dingbats, false, false).Source!;

        // Alpha, Omega, the integral and infinity from Symbol; the scissors and the check mark from ZapfDingbats.
        foreach (var codePoint in new[] { 0x03B1, 0x03A9, 0x222B, 0x221E })
        {
            await Assert.That(symbol.GetGlyph(codePoint)).IsNotEqualTo(0).Because($"Symbol lacks U+{codePoint:X4}");
        }

        foreach (var codePoint in new[] { 0x2702, 0x2713, 0x2740 })
        {
            await Assert.That(dingbats.GetGlyph(codePoint)).IsNotEqualTo(0).Because($"Dingbats lacks U+{codePoint:X4}");
        }
    }

    /// <summary>Decoding a face twice gives the same bytes, so output built from it is reproducible.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodingIsDeterministic()
    {
        var resources = new PdfDrawingServices(new()).FontResources;
        var first = BundledFaces.Decode(BundledFamily.Sans, false, false);
        var second = BundledFaces.Decode(BundledFamily.Sans, false, false);
        var shared = BundledFaces.Program(resources, BundledFamily.Sans, false, false);

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsEquivalentTo(first!);
        await Assert.That(shared).IsEquivalentTo(first!);
        await Assert.That(ReferenceEquals(shared, BundledFaces.Program(resources, BundledFamily.Sans, false, false))).IsTrue();
    }

    /// <summary>Opened sources belong to the host service that requested them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenedFontsStayWithTheirServices()
    {
        var left = new PdfDrawingServices(new());
        var right = new PdfDrawingServices(new());
        var first = PdfBundledFonts.Open(left, PdfBundledFontFamily.Sans, false, false);
        var second = PdfBundledFonts.Open(right, PdfBundledFontFamily.Sans, false, false);

        await Assert.That(ReferenceEquals(first, second)).IsFalse();
        await Assert.That(ReferenceEquals(first.Source, second.Source)).IsFalse();
        await Assert.That(PdfBundledFonts.Open(left, PdfBundledFontFamily.Sans, false, false)).IsSameReferenceAs(first);
    }

    /// <summary>A family that does not exist is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFamilyIsRefused() =>
        await Assert.That(static () => PdfBundledFonts.Open(UndefinedFamily, false, false)).Throws<ArgumentOutOfRangeException>();

    /// <summary>A family and style under test.</summary>
    /// <param name="Family">The family.</param>
    /// <param name="Bold">Whether the style is bold.</param>
    /// <param name="Italic">Whether the style is italic.</param>
    public sealed record FaceCase(PdfBundledFontFamily Family, bool Bold, bool Italic);
}
