// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Composition;

namespace HyperPdfLibrary.Tests.Composition;

/// <summary>Checks units, page sizes, number formats and the style sheet's inheritance.</summary>
public sealed class CompositionBasicsTests
{
    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72F;

    /// <summary>The width of ISO A4 in points.</summary>
    private const float A4Width = 595.276F;

    /// <summary>The height of ISO A4 in points.</summary>
    private const float A4Height = 841.89F;

    /// <summary>The points in a pica.</summary>
    private const float PointsPerPica = 12F;

    /// <summary>The millimetres in an inch.</summary>
    private const float MillimetersPerInch = 25.4F;

    /// <summary>The centimetres in an inch.</summary>
    private const float CentimetersPerInch = 2.54F;

    /// <summary>The width of US Letter in points.</summary>
    private const float LetterWidth = 612F;

    /// <summary>The height of US Letter in points.</summary>
    private const float LetterHeight = 792F;

    /// <summary>A number written in Roman numerals as iv.</summary>
    private const int Four = 4;

    /// <summary>A number written in decimal in the test.</summary>
    private const int Twelve = 12;

    /// <summary>The level of the heading in the test.</summary>
    private const int HeadingLevel = 2;

    /// <summary>How far a converted length may be from the exact one.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The size of the style that overrides the size.</summary>
    private const float BigSize = 20F;

    /// <summary>The year written in Roman numerals in the test.</summary>
    private const int RomanYear = 1994;

    /// <summary>The number that turns the alphabet over to double letters.</summary>
    private const int AlphabetWrap = 27;

    /// <summary>Units convert to points and back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnitsConvertToPoints()
    {
        await Assert.That(PdfUnits.ToPoints(1F, PdfUnit.Inch)).IsEqualTo(PointsPerInch);
        await Assert.That(PdfUnits.ToPoints(1F, PdfUnit.Pica)).IsEqualTo(PointsPerPica);
        await Assert.That(PdfUnits.FromPoints(PdfUnits.ToPoints(MillimetersPerInch, PdfUnit.Millimeter), PdfUnit.Inch)).IsEqualTo(1F).Within(Tolerance);
        await Assert.That(PdfUnits.ToPoints(CentimetersPerInch, PdfUnit.Centimeter)).IsEqualTo(PointsPerInch).Within(Tolerance);
    }

    /// <summary>Named page sizes have the right shape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedPageSizesAreCorrect()
    {
        await Assert.That(PdfPageSize.A4.Width).IsEqualTo(A4Width).Within(Tolerance);
        await Assert.That(PdfPageSize.A4.Height).IsEqualTo(A4Height).Within(Tolerance);
        await Assert.That(PdfPageSize.Letter.Width).IsEqualTo(LetterWidth);
        await Assert.That(PdfPageSize.Letter.Height).IsEqualTo(LetterHeight);
        await Assert.That(PdfPageSize.A3.Width).IsGreaterThan(PdfPageSize.A4.Width);
    }

    /// <summary>A landscape page swaps the paper's width and height.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LandscapeSwapsTheSides()
    {
        var setup = new PdfPageSetup { Orientation = PdfOrientation.Landscape };

        await Assert.That(setup.PageWidth).IsEqualTo(PdfPageSize.A4.Height);
        await Assert.That(setup.PageHeight).IsEqualTo(PdfPageSize.A4.Width);
    }

    /// <summary>Roman numerals and letters are written in each case.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NumbersAreWrittenInEveryFormat()
    {
        await Assert.That(NumberText.Format(RomanYear, PdfNumberFormat.UpperRoman)).IsEqualTo("MCMXCIV");
        await Assert.That(NumberText.Format(Four, PdfNumberFormat.LowerRoman)).IsEqualTo("iv");
        await Assert.That(NumberText.Format(1, PdfNumberFormat.LowerAlpha)).IsEqualTo("a");
        await Assert.That(NumberText.Format(AlphabetWrap, PdfNumberFormat.UpperAlpha)).IsEqualTo("AA");
        await Assert.That(NumberText.Format(Twelve, PdfNumberFormat.Decimal)).IsEqualTo("12");
    }

    /// <summary>A style takes what it does not set from the style it is based on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StylesInherit()
    {
        var creator = CompositionKit.CreateCreator(false);
        var styles = CompositionKit.CreateStyles(creator);
        styles.Add(new("Big") { BasedOn = PdfStyleNames.Normal, Character = new() { Size = BigSize, Bold = true } });
        styles.Add(new("Bigger") { BasedOn = "Big", Paragraph = new() { Alignment = HyperPdfLibrary.Creation.PdfTextAlignment.Center } });

        var resolved = styles.Resolve("Bigger");

        await Assert.That(resolved.Character.Size).IsEqualTo(BigSize);
        await Assert.That(resolved.Character.Bold).IsTrue();
        await Assert.That(resolved.Character.Italic).IsFalse();
        await Assert.That(resolved.Paragraph.Alignment).IsEqualTo(HyperPdfLibrary.Creation.PdfTextAlignment.Center);
        await Assert.That(resolved.Paragraph.SpaceAfter).IsEqualTo(styles.Resolve(PdfStyleNames.Normal).Paragraph.SpaceAfter);
    }

    /// <summary>A heading style keeps with the next paragraph and is in the outline.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadingStylesAreOutlined()
    {
        var styles = CompositionKit.CreateStyles(CompositionKit.CreateCreator(false));

        var heading = styles.Resolve(PdfStyleNames.GetHeading(HeadingLevel)).Paragraph;

        await Assert.That(heading.KeepWithNext).IsTrue();
        await Assert.That(heading.OutlineLevel).IsEqualTo(HeadingLevel);
    }

    /// <summary>Styles that are based on each other in a circle are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACircleOfStylesIsRefused()
    {
        var styles = CompositionKit.CreateStyles(CompositionKit.CreateCreator(false));
        styles.Add(new("A") { BasedOn = "B" });
        styles.Add(new("B") { BasedOn = "A" });

        await Assert.That(() => styles.Resolve("A")).Throws<InvalidOperationException>();
    }

    /// <summary>A style that does not exist is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AMissingStyleIsRefused()
    {
        var styles = CompositionKit.CreateStyles(CompositionKit.CreateCreator(false));

        await Assert.That(() => styles.Resolve("Nowhere")).Throws<InvalidOperationException>();
    }
}
