// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks how the text device groups glyphs into runs, with and without text object starts from the interpreter.</summary>
public sealed class TextDeviceTests
{
    /// <summary>The font size.</summary>
    private const float FontSize = 10;

    /// <summary>The x of the first glyph.</summary>
    private const float FirstX = 10;

    /// <summary>The baseline.</summary>
    private const float Baseline = 100;

    /// <summary>The advance of one glyph at the font size.</summary>
    private const float GlyphAdvance = TextTestFont.Advance * FontSize;

    /// <summary>A kerning gap after the second glyph, in points.</summary>
    private const float KerningGap = 3;

    /// <summary>The x of the second glyph, one advance after the first.</summary>
    private const float SecondX = FirstX + GlyphAdvance;

    /// <summary>The x of the third glyph, one advance and the kerning gap after the second.</summary>
    private const float ThirdX = SecondX + GlyphAdvance + KerningGap;

    /// <summary>The pen position of the third glyph in the run: its x less the first glyph's x.</summary>
    private const float ExpectedThirdPen = ThirdX - FirstX;

    /// <summary>The tolerance for kerning values.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The glyphs of the second run when each glyph starts its own text object.</summary>
    private const int SecondRunGlyphs = 1;

    /// <summary>The runs when the interpreter reports a text object for each glyph pair.</summary>
    private const int HookedRuns = 2;

    /// <summary>Without text object starts, glyphs moving forward along one baseline form one run, and a gap leaves the next glyph's pen further along.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlyphsOnABaselineFormOneRun()
    {
        var device = Collect(false);

        await Assert.That(device.Runs.Count).IsEqualTo(1);
        await Assert.That(device.Glyphs[2].Pen).IsEqualTo(ExpectedThirdPen).Within(Tolerance);
    }

    /// <summary>With text object starts, each text object is its own run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextObjectStartsSplitRuns()
    {
        var device = Collect(true);

        await Assert.That(device.Runs.Count).IsEqualTo(HookedRuns);
        await Assert.That(device.Runs[1].GlyphCount).IsEqualTo(SecondRunGlyphs);
    }

    /// <summary>Draws three glyphs, the third after a kerning gap, and returns the device.</summary>
    /// <param name="hooked">Whether to report a text object start before the first and third glyphs.</param>
    /// <returns>The device with its runs closed.</returns>
    private static TextDevice Collect(bool hooked)
    {
        var document = PdfDocumentReader.Open(TextTestDocument.Create(string.Empty).ToBytes(), null);
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new TextTestFont(new PdfDictionary(document.Objects), false, false);
        var device = new TextDevice();
        device.Reset(page);
        var state = new GraphicsState { Ctm = page.ViewerTransform, FontSize = FontSize };
        float[] positions = [FirstX, SecondX, ThirdX];
        for (var i = 0; i < positions.Length; i++)
        {
            if (hooked && i != 1)
            {
                device.BeginTextObject();
            }

            Draw(device, font, positions[i], ref state);
        }

        device.Finish();
        return device;
    }

    /// <summary>Draws one glyph of code A at a position.</summary>
    /// <param name="device">The device.</param>
    /// <param name="font">The font.</param>
    /// <param name="x">The glyph x.</param>
    /// <param name="state">The graphics state.</param>
    private static void Draw(TextDevice device, TextTestFont font, float x, ref GraphicsState state)
    {
        var textMatrix = Matrix3x2.CreateTranslation(x, Baseline);
        var glyphMatrix = font.FontMatrix * Matrix3x2.CreateScale(FontSize) * textMatrix;
        var glyph = new GlyphEvent(font, 'A', "A", new(glyphMatrix, textMatrix, GlyphAdvance), FontSize, false);
        device.DrawGlyph(glyph, ref state);
    }
}
