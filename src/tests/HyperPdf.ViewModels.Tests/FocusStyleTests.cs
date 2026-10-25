// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the Focus Mode type scale and page colours, which every render head reads the same way.</summary>
public sealed class FocusStyleTests
{
    /// <summary>The body text size used by the type tests.</summary>
    private const double Size = 20;

    /// <summary>The line spacing used by the type tests.</summary>
    private const double Lines = 1.5;

    /// <summary>The paragraph spacing used by the type tests.</summary>
    private const double Paragraphs = 1;

    /// <summary>The column width in characters used by the type tests.</summary>
    private const double Width = 60;

    /// <summary>A heading's size as a multiple of the text size.</summary>
    private const double HeadingScale = 1.35;

    /// <summary>Captions and footnotes as a multiple of the text size.</summary>
    private const double SmallScale = 0.85;

    /// <summary>The list indent as a multiple of the text size.</summary>
    private const double IndentScale = 1.2;

    /// <summary>A character's average width as a share of the text size.</summary>
    private const double CharacterShare = 0.5;

    /// <summary>The divisor that halves a space.</summary>
    private const double Halves = 2;

    /// <summary>A colour choice past the last one.</summary>
    private const int UnknownChoice = 99;

    /// <summary>How close two brightness values must be to be equal.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The page colour that follows the app, when the app's page is white.</summary>
    private const uint White = 0xFFFFFF;

    /// <summary>The page colour that follows the app, when the app's page is black.</summary>
    private const uint Black = 0x000000;

    /// <summary>The dark text used on a light page.</summary>
    private const uint DarkText = 0x222222;

    /// <summary>The light text used on a dark page.</summary>
    private const uint LightText = 0xE4E0D8;

    /// <summary>The warm paper choice's page colour.</summary>
    private const uint WarmPage = 0xF4ECD8;

    /// <summary>The type follows the text size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TypeScalesWithTheTextSize()
    {
        var type = FocusTypography.From(Size, Lines, Paragraphs, Width);

        await Assert.That(type.FontSize).IsEqualTo(Size);
        await Assert.That(type.LineHeight).IsEqualTo(Size * Lines);
        await Assert.That(type.ParagraphMargin).IsEqualTo(Size * Paragraphs);
        await Assert.That(type.HeadingSize).IsEqualTo(Size * HeadingScale);
        await Assert.That(type.SmallSize).IsEqualTo(Size * SmallScale);
        await Assert.That(type.ItemIndent).IsEqualTo(Size * IndentScale);
        await Assert.That(type.MaxWidth).IsEqualTo(Width * Size * CharacterShare);
    }

    /// <summary>Headings never get a line height under their own size, however tight the body lines are.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadingLinesNeverGoUnderTheHeadingSize()
    {
        var type = FocusTypography.From(Size, 1, Paragraphs, Width);

        await Assert.That(type.HeadingLineHeight).IsEqualTo(type.HeadingSize);
    }

    /// <summary>Spaces around headings and list items are half the paragraph space where they trail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrailingSpacesAreHalfAParagraph()
    {
        var type = FocusTypography.From(Size, Lines, Paragraphs, Width);

        await Assert.That(type.HeadingTopMargin).IsEqualTo(type.ParagraphMargin);
        await Assert.That(type.HeadingBottomMargin).IsEqualTo(type.ParagraphMargin / Halves);
        await Assert.That(type.ItemBottomMargin).IsEqualTo(type.ParagraphMargin / Halves);
    }

    /// <summary>A numbered choice gives its own colours whatever the app's page colour is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NumberedChoiceIgnoresTheAppPage()
    {
        var colours = FocusColours.Resolve(1, Black);

        await Assert.That(colours.Page).IsEqualTo(WarmPage);
    }

    /// <summary>The first choice follows the app's page, with dark text on a light page and light text on a dark one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowingTheAppPageChoosesReadableText()
    {
        FocusColours expectedLight = new(White, DarkText);
        FocusColours expectedDark = new(Black, LightText);

        await Assert.That(FocusColours.Resolve(0, White)).IsEqualTo(expectedLight);
        await Assert.That(FocusColours.Resolve(0, Black)).IsEqualTo(expectedDark);
    }

    /// <summary>A choice past the last one follows the app's page too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownChoiceFollowsTheAppPage()
    {
        var colours = FocusColours.Resolve(UnknownChoice, White);

        await Assert.That(colours.Page).IsEqualTo(White);
    }

    /// <summary>Brightness runs from black at 0 to white at 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LuminanceSpansBlackToWhite()
    {
        await Assert.That(FocusColours.Luminance(Black)).IsEqualTo(0);
        await Assert.That(FocusColours.Luminance(White)).IsEqualTo(1).Within(Tolerance);
    }
}
