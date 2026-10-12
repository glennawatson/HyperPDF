// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;

namespace HyperPdf.Core.Tests.Text;

/// <summary>Tests the record kept with text boxes and the standard free text entries.</summary>
public sealed class TextFormatCodecTests
{
    /// <summary>A font size.</summary>
    private const float Size = 14.5F;

    /// <summary>A wrap width.</summary>
    private const float Wrap = 180.25F;

    /// <summary>A line spacing multiple.</summary>
    private const float Lines = 1.5F;

    /// <summary>A character spacing in points.</summary>
    private const float Spacing = 1.25F;

    /// <summary>A comb cell count.</summary>
    private const int Cells = 9;

    /// <summary>A colour.</summary>
    private const uint Teal = 0x117788;

    /// <summary>Red as 0xRRGGBB.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>Mid grey as 0xRRGGBB.</summary>
    private const uint Grey = 0x808080;

    /// <summary>The size in an Acrobat style.</summary>
    private const float AcrobatSize = 10;

    /// <summary>A size too large to use.</summary>
    private const float Huge = 9000;

    /// <summary>Every setting survives writing and reading the record, including a family with separators in its name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoundTripsEverySetting()
    {
        var format = new TextFormat("Odd;Family=Name%", Size, Teal)
        {
            IsBold = true,
            IsItalic = true,
            IsUnderline = true,
            Alignment = TextBoxAlignment.Right,
            LineSpacing = Lines,
            CharacterSpacing = Spacing,
            CombCells = Cells,
        };

        var read = TextFormatCodec.TryRead(TextFormatCodec.Write(format, Wrap), out var back, out var wrap);

        await Assert.That(read).IsTrue();
        await Assert.That(back).IsEqualTo(format);
        await Assert.That(wrap).IsEqualTo(Wrap);
    }

    /// <summary>Records without the version, or empty, are not read; unknown keys and bad values are skipped and clamped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsWhatItCannotRead()
    {
        var unversioned = TextFormatCodec.TryRead("family=Times", out _, out _);
        var empty = TextFormatCodec.TryRead(string.Empty, out _, out _);
        var odd = TextFormatCodec.TryRead("v1;future=1;size=9000;align=7;bogus", out var format, out var wrap);

        await Assert.That(unversioned).IsFalse();
        await Assert.That(empty).IsFalse();
        await Assert.That(odd).IsTrue();
        await Assert.That(format.FontSize).IsEqualTo(TextFormat.MaxFontSize);
        await Assert.That(format.Alignment).IsEqualTo(TextBoxAlignment.Left);
        await Assert.That(wrap).IsEqualTo(0F);
        await Assert.That(new TextFormat(" ", Huge, 0).Clamped().FontFamily).IsEqualTo(StandardFontFamilies.Sans);
    }

    /// <summary>The default appearance names a standard font resource, the size and the colour, and reads back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesAndReadsTheDefaultAppearance()
    {
        var format = new TextFormat("Times", Size, Red);
        var appearance = FreeTextStyle.DefaultAppearance(format);
        var back = FreeTextStyle.ApplyDefaultAppearance(appearance, TextFormat.Default);
        var gray = FreeTextStyle.ApplyDefaultAppearance("/Helv 0 Tf 0.5 g", TextFormat.Default);
        var cmyk = FreeTextStyle.ApplyDefaultAppearance("/Helv 9 Tf 0 1 1 0 k", TextFormat.Default);

        await Assert.That(appearance).StartsWith("/TiRo 14.5 Tf 1 0 0 rg");
        await Assert.That(back.FontSize).IsEqualTo(Size);
        await Assert.That(back.Color).IsEqualTo(Red);
        await Assert.That(gray.Color).IsEqualTo(Grey);
        await Assert.That(gray.FontSize).IsEqualTo(TextFormat.Default.FontSize);
        await Assert.That(cmyk.Color).IsEqualTo(Red);
    }

    /// <summary>The default style is CSS that reads back, and Acrobat's font shorthand is understood.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesAndReadsTheDefaultStyle()
    {
        var format = new TextFormat("DejaVu Sans", Size, Teal) { IsBold = true, IsItalic = true, IsUnderline = true, Alignment = TextBoxAlignment.Center, CharacterSpacing = Spacing };
        var style = FreeTextStyle.DefaultStyle(format);
        var back = FreeTextStyle.ApplyDefaultStyle(style, TextFormat.Default);
        var acrobat = FreeTextStyle.ApplyDefaultStyle("font: bold italic 10.0pt Helvetica,sans-serif; text-align:right; color:#FF0000", TextFormat.Default);
        var generic = FreeTextStyle.ApplyDefaultStyle("font-family: serif; font-weight: 700", TextFormat.Default);

        await Assert.That(back with { LineSpacing = format.LineSpacing }).IsEqualTo(format);
        await Assert.That(acrobat.FontFamily).IsEqualTo("Helvetica");
        await Assert.That(acrobat.IsBold && acrobat.IsItalic).IsTrue();
        await Assert.That(acrobat.FontSize).IsEqualTo(AcrobatSize);
        await Assert.That(acrobat.Alignment).IsEqualTo(TextBoxAlignment.Right);
        await Assert.That(acrobat.Color).IsEqualTo(Red);
        await Assert.That(generic.FontFamily).IsEqualTo(StandardFontFamilies.Serif);
        await Assert.That(generic.IsBold).IsTrue();
    }

    /// <summary>Rich text has one paragraph per line with XML escaped, and hex strings are ASCII UTF-16.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesRichTextAndHexStrings()
    {
        var rich = FreeTextStyle.RichText("a<b\n&c", TextFormat.Default);
        var hex = TextFormatCodec.HexTextString("Aé");

        await Assert.That(rich).Contains("<p>a&lt;b</p><p>&amp;c</p>");
        await Assert.That(rich).EndsWith("</body>");
        await Assert.That(hex).IsEqualTo("<FEFF004100E9>");
    }

    /// <summary>Standard families map to base font names and the closest standard family is picked by name and class.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicksStandardFonts()
    {
        await Assert.That(StandardFontFamilies.BaseFontName("Times", true, true)).IsEqualTo("Times-BoldItalic");
        await Assert.That(StandardFontFamilies.BaseFontName("Courier", false, true)).IsEqualTo("Courier-Oblique");
        await Assert.That(StandardFontFamilies.BaseFontName("Anything", false, false)).IsEqualTo("Helvetica");
        await Assert.That(StandardFontFamilies.Closest("DejaVu Sans Mono", false, false)).IsEqualTo(StandardFontFamilies.Mono);
        await Assert.That(StandardFontFamilies.Closest("Noto Serif", false, false)).IsEqualTo(StandardFontFamilies.Serif);
        await Assert.That(StandardFontFamilies.Closest("Fancy", true, false)).IsEqualTo(StandardFontFamilies.Serif);
        await Assert.That(StandardFontFamilies.Closest("Noto Sans", false, false)).IsEqualTo(StandardFontFamilies.Sans);
    }
}
