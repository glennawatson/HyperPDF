// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Draws shapes, transforms and text boxes on a page and reads back the pixels.</summary>
public sealed class CanvasShapeTests
{
    /// <summary>The page side in points and pixels.</summary>
    private const int Side = 100;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The most a channel may differ from white.</summary>
    private const int WhiteTolerance = 8;

    /// <summary>The weakest a red channel may be to count as red.</summary>
    private const int StrongRed = 200;

    /// <summary>The strongest a green or blue channel may be in a red pixel.</summary>
    private const int WeakChannel = 60;

    /// <summary>The line width used for strokes.</summary>
    private const float PenWidth = 6F;

    /// <summary>The radius of the rounded corners.</summary>
    private const float CornerRadius = 20F;

    /// <summary>A distance just inside the edge of a shape.</summary>
    private const int Near = 2;

    /// <summary>The side of the shapes' boxes.</summary>
    private const float Box = 60F;

    /// <summary>The margin before the shapes' boxes.</summary>
    private const float Margin = 20F;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The sweep of the half circle arc, in degrees.</summary>
    private const float HalfCircle = 180F;

    /// <summary>The length of a dash and of a gap, in points.</summary>
    private const float DashLength = 10F;

    /// <summary>A point in the first gap of a dashed line.</summary>
    private const int InGap = 15;

    /// <summary>A point in the first dash of a dashed line.</summary>
    private const int InDash = 5;

    /// <summary>The turn of the rotated square, in degrees.</summary>
    private const float Turn = 45F;

    /// <summary>Half the side of the rotated square.</summary>
    private const float HalfSquare = 10F;

    /// <summary>The side of the rotated square.</summary>
    private const float Square = 20F;

    /// <summary>How far above the middle a turned square still paints, where an upright one stops short.</summary>
    private const int TurnedReach = 12;

    /// <summary>How far diagonally from the middle a turned square has left bare.</summary>
    private const int TurnedCorner = 9;

    /// <summary>The font size of the text box test.</summary>
    private const float FontSize = 10F;

    /// <summary>The width of the text box.</summary>
    private const float TextWidth = 40F;

    /// <summary>The height of the text box, one line.</summary>
    private const float TextHeight = 12F;

    /// <summary>The height of a box too short for a line.</summary>
    private const float TooShort = 2F;

    /// <summary>The text drawn into the box.</summary>
    private const string Words = "aaa bbb ccc ddd";

    /// <summary>The pixel rows searched for the underline, from the top.</summary>
    private const int RowsSearched = 16;

    /// <summary>A byte value below which a pixel channel is dark.</summary>
    private const int DarkChannel = 100;

    /// <summary>The cyan fill's blue channel must be at least this much.</summary>
    private const int CyanBlue = 200;

    /// <summary>The cyan fill's red channel must be less than this.</summary>
    private const int CyanRed = 50;

    /// <summary>Gets the red used in the tests.</summary>
    private static PdfCanvasColor Red => PdfCanvasColor.Rgb(1, 0, 0);

    /// <summary>A filled ellipse covers its centre and leaves the corners of its box bare.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilledEllipseCoversTheCentreNotTheCorners()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawEllipse(PdfShapeStyle.Filled(Red), Margin, Margin, Box, Box);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Middle)).IsTrue();
        await Assert.That(IsWhite(pixels, Margin + Near, Margin + Near)).IsTrue();
    }

    /// <summary>A rounded rectangle is filled in the middle and bare at its corner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoundedRectangleLeavesItsCornersBare()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawRoundedRectangle(PdfShapeStyle.Filled(Red), Margin, Margin, Box, Box, CornerRadius);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Middle)).IsTrue();
        await Assert.That(IsWhite(pixels, Margin + 1, Margin + 1)).IsTrue();
        await Assert.That(IsRed(pixels, Margin + CornerRadius, Margin + 1)).IsTrue();
    }

    /// <summary>A stroked arc paints its line and nothing inside.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArcStrokesOnlyItsLine()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawArc(new(Red, PenWidth), Margin, Margin, Box, Box, 0F, HalfCircle);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Margin + Box - 1)).IsTrue();
        await Assert.That(IsWhite(pixels, Middle, Middle)).IsTrue();
    }

    /// <summary>A triangle polygon fills inside its corners.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolygonFillsInsideItsCorners()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawPolygon(PdfShapeStyle.Filled(Red), [new(Margin, Margin), new(Margin + Box, Margin), new(Middle, Margin + Box)]);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Margin + Near)).IsTrue();
        await Assert.That(IsWhite(pixels, Margin + Near, Margin + Box - Near)).IsTrue();
    }

    /// <summary>A dashed pen leaves gaps along its line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DashedPenLeavesGaps()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        PdfPen pen = new(Red, PenWidth) { Dash = new[] { DashLength, DashLength } };
        canvas.DrawPolyline(pen, [new(0, Middle), new(Side, Middle)]);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, InDash, Middle)).IsTrue();
        await Assert.That(IsWhite(pixels, InGap, Middle)).IsTrue();
    }

    /// <summary>A rotated square reaches further up than an upright one and leaves its diagonal corner bare.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationTurnsWhatFollows()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.SaveState();
        canvas.Translate(Middle, Middle);
        canvas.Rotate(Turn);
        canvas.DrawRectangle(PdfShapeStyle.Filled(Red), -HalfSquare, -HalfSquare, Square, Square);
        canvas.RestoreState();
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Middle + TurnedReach)).IsTrue();
        await Assert.That(IsWhite(pixels, Middle + TurnedCorner, Middle + TurnedCorner)).IsTrue();
    }

    /// <summary>A clip keeps a fill inside an ellipse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipLimitsWhatFollows()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.SaveState();
        canvas.ClipEllipse(Margin, Margin, Box, Box);
        canvas.DrawRectangle(PdfShapeStyle.Filled(Red), 0, 0, Side, Side);
        canvas.RestoreState();
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(IsRed(pixels, Middle, Middle)).IsTrue();
        await Assert.That(IsWhite(pixels, Near, Near)).IsTrue();
    }

    /// <summary>A cyan CMYK fill paints blue-green, with little red.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykColourPaints()
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawRectangle(PdfShapeStyle.Filled(PdfCanvasColor.Cmyk(1, 0, 0, 0)), Margin, Margin, Box, Box);
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        const int offset = ((Middle * Side) + Middle) * BytesPerPixel;
        await Assert.That(pixels[offset + RedIndex]).IsLessThan((byte)CyanRed);
        await Assert.That(pixels[offset]).IsGreaterThan((byte)CyanBlue);
    }

    /// <summary>Text in a box returns how much of it fit, and an underline paints under the baseline.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextBoxStopsWhereTheBoxEndsAndUnderlines()
    {
        var creator = new PdfDocumentCreator(new());
        var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        var canvas = creator.BeginPage(Side, Side);
        var style = new PdfTextStyle(font, FontSize, PdfCanvasColor.Black) { Format = PdfTextFormat.Default with { Underline = true } };
        var drawn = canvas.DrawTextBox(style, new(0, Side - TextHeight, TextWidth, TextHeight), Words);
        var none = canvas.DrawTextBox(style, new(0, 0, TextWidth, TooShort), "aaa");
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());

        await Assert.That(drawn).IsGreaterThan(0);
        await Assert.That(drawn).IsLessThan(Words.Length);
        await Assert.That(none).IsEqualTo(0);
        await Assert.That(pixels.Take(Side * BytesPerPixel * RowsSearched).Any(static value => value < DarkChannel)).IsTrue();
    }

    /// <summary>Renders the first page at one pixel to the point.</summary>
    /// <param name="file">The PDF file.</param>
    /// <returns>The premultiplied BGRA pixels.</returns>
    private static byte[] Render(byte[] file)
    {
        using var document = SkiaTestRegistration.Open(file, null);
        using var renderer = new PdfPageRenderer(document);
        var pixels = new byte[Side * Side * BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Side, Side, Side * BytesPerPixel));
        return pixels;
    }

    /// <summary>Determines whether a pixel is red; the y counts up from the bottom of the page.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="x">The x in points.</param>
    /// <param name="y">The y in points from the bottom.</param>
    /// <returns><see langword="true"/> for red.</returns>
    private static bool IsRed(byte[] pixels, float x, float y) => Channels(pixels, x, y) is { Blue: < WeakChannel, Green: < WeakChannel, Red: > StrongRed };

    /// <summary>Determines whether a pixel is white; the y counts up from the bottom of the page.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="x">The x in points.</param>
    /// <param name="y">The y in points from the bottom.</param>
    /// <returns><see langword="true"/> for white.</returns>
    private static bool IsWhite(byte[] pixels, float x, float y) =>
        Channels(pixels, x, y) is { Blue: > byte.MaxValue - WhiteTolerance, Green: > byte.MaxValue - WhiteTolerance, Red: > byte.MaxValue - WhiteTolerance };

    /// <summary>Reads the colour of a pixel; the y given counts up from the bottom of the page.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="x">The x in points.</param>
    /// <param name="y">The y in points from the bottom.</param>
    /// <returns>The channels.</returns>
    private static PixelColor Channels(byte[] pixels, float x, float y)
    {
        var offset = (((Side - 1 - (int)y) * Side) + (int)x) * BytesPerPixel;
        return new(pixels[offset + RedIndex], pixels[offset + 1], pixels[offset]);
    }

    /// <summary>A pixel's colour.</summary>
    /// <param name="Red">The red channel.</param>
    /// <param name="Green">The green channel.</param>
    /// <param name="Blue">The blue channel.</param>
    private readonly record struct PixelColor(byte Red, byte Green, byte Blue);
}
