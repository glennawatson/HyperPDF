// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks the faces bundled with the library and that non-embedded fonts use them before system fonts.</summary>
public sealed class BundledFaceTests
{
    /// <summary>The name of the bold sans face.</summary>
    private const string SansBoldFace = "Arimo-Bold";

    /// <summary>The name of the regular serif face.</summary>
    private const string SerifFace = "Tinos-Regular";

    /// <summary>A font name no system has.</summary>
    private const string UnknownFont = "NoSuchFontXyz";

    /// <summary>The name of the Symbol font.</summary>
    private const string SymbolFont = "Symbol";

    /// <summary>The font subtype under test.</summary>
    private const string Type1 = "Type1";

    /// <summary>The code of A.</summary>
    private const int CodeA = 'A';

    /// <summary>The code of a, which the Symbol font draws as alpha.</summary>
    private const int CodeAlpha = 'a';

    /// <summary>The code of the first ZapfDingbats glyph, a1.</summary>
    private const int CodeFirstDingbat = 0x21;

    /// <summary>The weight of a bold face.</summary>
    private const int BoldWeight = 700;

    /// <summary>The weight of a regular face.</summary>
    private const int RegularWeight = 400;

    /// <summary>The tolerance of width comparisons, in glyph units.</summary>
    private const float WidthTolerance = 1F;

    /// <summary>The Helvetica width of A in glyph units.</summary>
    private const float HelveticaWidthA = 667F;

    /// <summary>The Courier width of every glyph in glyph units.</summary>
    private const float CourierWidth = 600F;

    /// <summary>The resources owned by this test.</summary>
    private readonly PdfDrawingServices _services = new(new());

    /// <summary>The font resources owned by this test's services.</summary>
    private readonly PdfFontResources _resources;

    /// <summary>Initializes a new instance of the <see cref="BundledFaceTests"/> class.</summary>
    public BundledFaceTests() => _resources = _services.FontResources;

    /// <summary>The standard 14 names with the bundled face each uses.</summary>
    /// <returns>The cases.</returns>
    public static IEnumerable<Func<StandardFaceCase>> StandardFaces() =>
    [
        static () => new("Helvetica", "Arimo-Regular"),
        static () => new("Helvetica-Bold", SansBoldFace),
        static () => new("Helvetica-Oblique", "Arimo-Italic"),
        static () => new("Helvetica-BoldOblique", "Arimo-BoldItalic"),
        static () => new("Times-Roman", SerifFace),
        static () => new("Times-Bold", "Tinos-Bold"),
        static () => new("Times-Italic", "Tinos-Italic"),
        static () => new("Times-BoldItalic", "Tinos-BoldItalic"),
        static () => new("Courier", "Cousine-Regular"),
        static () => new("Courier-Bold", "Cousine-Bold"),
        static () => new("Courier-Oblique", "Cousine-Italic"),
        static () => new("Courier-BoldOblique", "Cousine-BoldItalic"),
        static () => new(SymbolFont, "NotoSansMath-Regular"),
        static () => new("ZapfDingbats", "NotoSansSymbols2-Regular"),
    ];

    /// <summary>Each of the standard 14 names uses its own bundled face, never a system font.</summary>
    /// <param name="make">Makes the name and the expected face.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(StandardFaces))]
    public async Task StandardFontUsesItsBundledFace(StandardFaceCase make)
    {
        var (baseFont, expected) = make;
        var standard = StandardFonts.Find(System.Text.Encoding.UTF8.GetBytes(baseFont));
        var matcher = new SystemFontMatcher(null, _resources);

        var face = matcher.Match(new(baseFont, standard, FontFlags.Nonsymbolic, 0, CjkScript.None));

        await Assert.That(face.IsBundled).IsTrue();
        await Assert.That(face.FamilyName).IsEqualTo(expected);
    }

    /// <summary>Common aliases of a standard font use the bundled face, as PDFium's name table does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AliasesUseTheBundledFace()
    {
        var matcher = new SystemFontMatcher(null, _resources);

        var arial = matcher.Match(new("Arial,Bold", StandardFont.None, FontFlags.Nonsymbolic, 0, CjkScript.None));
        var times = matcher.Match(new("TimesNewRoman", StandardFont.None, FontFlags.Nonsymbolic, 0, CjkScript.None));

        await Assert.That(arial.FamilyName).IsEqualTo(SansBoldFace);
        await Assert.That(times.FamilyName).IsEqualTo(SerifFace);
    }

    /// <summary>A font with only descriptor flags picks the bundled serif, sans or fixed face.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlagsChooseTheGenericFace()
    {
        var matcher = new SystemFontMatcher(null, _resources);

        var serif = matcher.Match(new(UnknownFont, StandardFont.None, FontFlags.Serif | FontFlags.Nonsymbolic, RegularWeight, CjkScript.None));
        var fixedPitch = matcher.Match(new(UnknownFont, StandardFont.None, FontFlags.FixedPitch | FontFlags.Nonsymbolic, RegularWeight, CjkScript.None));
        var sansBold = matcher.Match(new(UnknownFont, StandardFont.None, FontFlags.Nonsymbolic, BoldWeight, CjkScript.None));
        var sansItalic = matcher.Match(new("NoSuchFontXyz-Italic", StandardFont.None, FontFlags.Nonsymbolic, RegularWeight, CjkScript.None));

        await Assert.That(serif.FamilyName).IsEqualTo(SerifFace);
        await Assert.That(fixedPitch.FamilyName).IsEqualTo("Cousine-Regular");
        await Assert.That(sansBold.FamilyName).IsEqualTo(SansBoldFace);
        await Assert.That(sansItalic.FamilyName).IsEqualTo("Arimo-Italic");
    }

    /// <summary>The bundled faces keep the metrics of the fonts they stand in for, so layout does not change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BundledFacesMatchStandardWidths()
    {
        var sans = BundledFaces.Get(_resources, BundledFamily.Sans, false, false)!;
        var mono = BundledFaces.Get(_resources, BundledFamily.Fixed, false, false)!;

        await Assert.That(sans.GetAdvance(sans.GetGlyph(CodeA))).IsEqualTo(HelveticaWidthA).Within(WidthTolerance);
        await Assert.That(mono.GetAdvance(mono.GetGlyph(CodeA))).IsEqualTo(CourierWidth).Within(WidthTolerance);
    }

    /// <summary>Every bundled face loads, has glyphs and draws a letter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryBundledFaceLoads()
    {
        foreach (var family in new[] { BundledFamily.Sans, BundledFamily.Serif, BundledFamily.Fixed })
        {
            foreach (var bold in new[] { false, true })
            {
                foreach (var italic in new[] { false, true })
                {
                    var face = BundledFaces.Get(_resources, family, bold, italic);
                    await Assert.That(face).IsNotNull();
                    var path = face!.BuildOutline(face.GetGlyph(CodeA));
                    await Assert.That(path).IsNotNull();
                    await Assert.That(path!.Bounds.IsEmpty).IsFalse();
                }
            }
        }
    }

    /// <summary>The Symbol and ZapfDingbats fonts draw from the bundled faces through their own encodings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolFontsDrawFromBundledFaces()
    {
        using var symbol = new FontTestDocument(new() { Subtype = Type1, BaseFont = SymbolFont, Flags = -1 }, _services);
        using var dingbats = new FontTestDocument(new() { Subtype = Type1, BaseFont = "ZapfDingbats", Flags = -1 }, _services);

        await Assert.That(FontProbe.Bounds(symbol.Font, CodeAlpha).IsEmpty).IsFalse();
        await Assert.That(FontProbe.Bounds(dingbats.Font, CodeFirstDingbat).IsEmpty).IsFalse();
        await Assert.That(FontProbe.Text(symbol.Font, CodeAlpha)).IsEqualTo("α");
    }

    /// <summary>A standard font draws without any system font, from the bundled face.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HelveticaDrawsFromTheBundledFace()
    {
        using var document = new FontTestDocument(new() { Subtype = Type1, BaseFont = "Helvetica", Flags = -1 }, _services);

        await Assert.That(FontProbe.Bounds(document.Font, CodeA).IsEmpty).IsFalse();
    }

    /// <summary>Private-use text a Symbol font's /ToUnicode map gives is remapped to the characters its codes mean.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolPrivateUseTextIsRemapped()
    {
        const string cmap = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap 1 begincodespacerange <00> <FF> endcodespacerange "
            + "1 beginbfchar <61> <F061> endbfchar endcmap end end";
        using var symbol = new FontTestDocument(new() { Subtype = Type1, BaseFont = SymbolFont, Flags = -1, ToUnicode = cmap }, _services);

        await Assert.That(FontProbe.Text(symbol.Font, CodeAlpha)).IsEqualTo("α");
    }

    /// <summary>A standard font name and the bundled face expected for it.</summary>
    /// <param name="BaseFont">The /BaseFont name.</param>
    /// <param name="Face">The bundled face's name.</param>
    public sealed record StandardFaceCase(string BaseFont, string Face);
}
