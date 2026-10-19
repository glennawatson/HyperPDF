// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the colours, direction, matrix and font facts a text page keeps for each character.</summary>
public sealed class TextCharStyleTests
{
    /// <summary>Red, as the fill colour.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>Green, as the fill colour.</summary>
    private const uint Green = 0x00FF00;

    /// <summary>Blue, as the stroke colour.</summary>
    private const uint Blue = 0x0000FF;

    /// <summary>The weight of the regular test font.</summary>
    private const int RegularWeight = 400;

    /// <summary>The weight of the bold test font.</summary>
    private const int BoldWeight = 700;

    /// <summary>A quarter turn in degrees.</summary>
    private const float QuarterTurn = 90;

    /// <summary>The allowed difference in an angle, in degrees.</summary>
    private const float AngleTolerance = 0.01F;

    /// <summary>The index of the character drawn after the colour changes.</summary>
    private const int Third = 2;

    /// <summary>Verifies the fill and stroke colours of each character, which follow the colour operators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Colours_FollowTheColourOperators()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 1 0 0 rg 0 0 1 RG 2 Tr 10 100 Td (ab) Tj 0 1 0 rg (c) Tj ET");

        await Assert.That(page.GetCharStyle(0).FillColor).IsEqualTo(Red);
        await Assert.That(page.GetCharStyle(1).FillColor).IsEqualTo(Red);
        await Assert.That(page.GetCharStyle(0).StrokeColor).IsEqualTo(Blue);
        await Assert.That(page.GetCharStyle(Third).FillColor).IsEqualTo(Green);
        await Assert.That(page.GetCharStyle(Third).StrokeColor).IsEqualTo(Blue);
    }

    /// <summary>Verifies that text drawn without a colour operator is black.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Text_WithoutAColour_IsBlack()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a) Tj ET");

        await Assert.That(page.GetCharStyle(0).FillColor).IsEqualTo(0U);
    }

    /// <summary>Verifies the angle of rotated and unrotated text, and that the matrix carries the rotation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Angle_FollowsTheTextMatrix()
    {
        var flat = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (ab) Tj ET");
        var turned = TextTestDocument.Extract("BT /F1 10 Tf 0 1 -1 0 100 100 Tm (ab) Tj ET");

        await Assert.That(flat.GetCharStyle(0).Angle).IsEqualTo(0).Within(AngleTolerance);
        await Assert.That(turned.GetCharStyle(0).Angle).IsEqualTo(QuarterTurn).Within(AngleTolerance);
        await Assert.That(turned.GetCharStyle(1).Matrix.M12).IsGreaterThan(0);
    }

    /// <summary>Verifies the font name, weight and bold flag, from a regular and a bold font.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Font_NameAndWeightComeFromTheFont()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a) Tj /F3 10 Tf (b) Tj ET");

        await Assert.That(page.GetCharStyle(0).FontName).IsEqualTo("TextTestHorizontal");
        await Assert.That(page.GetCharStyle(0).FontWeight).IsEqualTo(RegularWeight);
        await Assert.That(page.GetCharStyle(0).IsBold).IsFalse();
        await Assert.That(page.GetCharStyle(1).FontName).IsEqualTo("TextTestBold");
        await Assert.That(page.GetCharStyle(1).FontWeight).IsEqualTo(BoldWeight);
        await Assert.That(page.GetCharStyle(1).IsBold).IsTrue();
    }

    /// <summary>Verifies that an index outside the page is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetCharStyle_RefusesAnIndexOutsideThePage()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a) Tj ET");

        await Assert.That(() => page.GetCharStyle(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.GetCharStyle(page.CharCount)).Throws<ArgumentOutOfRangeException>();
    }
}
