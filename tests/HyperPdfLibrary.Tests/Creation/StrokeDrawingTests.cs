// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Stroked lines and rectangle outlines on the page canvas, checked by rendering the created page.</summary>
public sealed class StrokeDrawingTests
{
    /// <summary>The side of the square test page.</summary>
    private const int Side = 200;

    /// <summary>The width of a thick stroke.</summary>
    private const float Thick = 6F;

    /// <summary>The width of a thin stroke.</summary>
    private const float Thin = 1F;

    /// <summary>The y of the horizontal line, from the bottom.</summary>
    private const float LineY = 100F;

    /// <summary>The start of the line along its length.</summary>
    private const float LineStart = 40F;

    /// <summary>The end of the line along its length.</summary>
    private const float LineEnd = 160F;

    /// <summary>The middle of the line along its length.</summary>
    private const int LineMiddle = 100;

    /// <summary>A pixel offset that is clear of a thick line but near it.</summary>
    private const int Clear = 10;

    /// <summary>A pixel offset inside a thick line.</summary>
    private const int Inside = 2;

    /// <summary>A pixel offset that is outside a thin line.</summary>
    private const int OutsideThin = 4;

    /// <summary>The rectangle's left edge.</summary>
    private const float RectLeft = 50F;

    /// <summary>The rectangle's bottom edge.</summary>
    private const float RectBottom = 60F;

    /// <summary>The rectangle's width.</summary>
    private const float RectWidth = 100F;

    /// <summary>The rectangle's height.</summary>
    private const float RectHeight = 80F;

    /// <summary>The rectangle's right edge as a pixel column.</summary>
    private const int RectRight = 150;

    /// <summary>The middle of the rectangle across, as a pixel column.</summary>
    private const int RectMiddleX = 100;

    /// <summary>The middle of the rectangle up, in points from the bottom.</summary>
    private const int RectMiddleY = 100;

    /// <summary>The size of the test text.</summary>
    private const float TextSize = 11F;

    /// <summary>The level above which a channel counts as fully on.</summary>
    private const int Bright = 200;

    /// <summary>The level below which a channel counts as off.</summary>
    private const int Dark = 60;

    /// <summary>The tolerance for a pixel that should match a colour.</summary>
    private const int Tolerance = 8;

    /// <summary>Draws on a page.</summary>
    /// <param name="canvas">The page.</param>
    private delegate void DrawPage(ref PdfPageCanvas canvas);

    /// <summary>A horizontal line is dark along its length and centred on its y, and nothing is drawn past its ends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HorizontalLineIsStrokedAtItsPosition()
    {
        var render = RenderPage(static (ref PdfPageCanvas canvas) => canvas.DrawLine(PdfStroke.Black(Thick), LineStart, LineY, LineEnd, LineY));

        await Assert.That(IsDark(render, LineMiddle, Row(LineY))).IsTrue();
        await Assert.That(IsDark(render, LineMiddle, Row(LineY) - Inside)).IsTrue();
        await Assert.That(IsDark(render, LineMiddle, Row(LineY) + Inside)).IsTrue();
        await Assert.That(IsDark(render, LineMiddle, Row(LineY) - Clear)).IsFalse();
        await Assert.That(IsDark(render, LineMiddle, Row(LineY) + Clear)).IsFalse();
        await Assert.That(IsDark(render, (int)LineStart - Clear, Row(LineY))).IsFalse();
        await Assert.That(IsDark(render, (int)LineEnd + Clear, Row(LineY))).IsFalse();
    }

    /// <summary>A vertical line has the colour it was given.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalLineHasTheRequestedColour()
    {
        var render = RenderPage(static (ref PdfPageCanvas canvas) => canvas.DrawLine(new(1F, 0F, 0F, Thick), LineY, LineStart, LineY, LineEnd));

        await Assert.That(render.IsNear(LineMiddle, Row(LineMiddle), new(byte.MaxValue, 0, 0), Tolerance)).IsTrue();
        await Assert.That(render.IsNear(LineMiddle + Clear, Row(LineMiddle), new(byte.MaxValue, byte.MaxValue, byte.MaxValue), Tolerance)).IsTrue();
        await Assert.That(render.IsNear(LineMiddle, Row(LineStart) + Clear, new(byte.MaxValue, byte.MaxValue, byte.MaxValue), Tolerance)).IsTrue();
    }

    /// <summary>A diagonal line passes through its midpoint and leaves the opposite corner empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DiagonalLinePassesThroughItsMidpoint()
    {
        var render = RenderPage(static (ref PdfPageCanvas canvas) => canvas.DrawLine(PdfStroke.Black(Thick), LineStart, LineStart, LineEnd, LineEnd));

        await Assert.That(IsDark(render, LineMiddle, Row(LineMiddle))).IsTrue();
        await Assert.That(IsDark(render, (int)LineStart + Clear, Row(LineStart + Clear))).IsTrue();
        await Assert.That(IsDark(render, (int)LineStart + Clear, Row(LineEnd - Clear))).IsFalse();
        await Assert.That(IsDark(render, (int)LineEnd - Clear, Row(LineStart + Clear))).IsFalse();
    }

    /// <summary>A thin stroke covers fewer pixels than a thick one drawn at the same place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeWidthControlsTheThickness()
    {
        var thin = RenderPage(static (ref PdfPageCanvas canvas) => canvas.DrawLine(PdfStroke.Black(Thin), LineStart, LineY, LineEnd, LineY));
        var thick = RenderPage(static (ref PdfPageCanvas canvas) => canvas.DrawLine(PdfStroke.Black(Thick), LineStart, LineY, LineEnd, LineY));

        await Assert.That(IsDark(thin, LineMiddle, Row(LineY) - OutsideThin)).IsFalse();
        await Assert.That(IsDark(thin, LineMiddle, Row(LineY) + OutsideThin)).IsFalse();
        await Assert.That(IsDark(thick, LineMiddle, Row(LineY) - Inside)).IsTrue();
        await Assert.That(CountDark(thick)).IsGreaterThan(CountDark(thin));
    }

    /// <summary>A rectangle outline is dark on its four edges and empty inside and outside.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RectangleOutlineStrokesTheEdgesOnly()
    {
        var render = RenderPage(static (ref PdfPageCanvas canvas) => canvas.StrokeRectangle(PdfStroke.Black(Thick), RectLeft, RectBottom, RectWidth, RectHeight));
        await Assert.That(IsDark(render, (int)RectLeft, Row(RectMiddleY))).IsTrue();
        await Assert.That(IsDark(render, RectRight, Row(RectMiddleY))).IsTrue();
        await Assert.That(IsDark(render, RectMiddleX, Row(RectBottom))).IsTrue();
        await Assert.That(IsDark(render, RectMiddleX, Row(RectBottom + RectHeight))).IsTrue();
        await Assert.That(IsDark(render, RectMiddleX, Row(RectMiddleY))).IsFalse();
        await Assert.That(IsDark(render, (int)RectLeft - Clear, Row(RectMiddleY))).IsFalse();
        await Assert.That(IsDark(render, RectRight + Clear, Row(RectMiddleY))).IsFalse();
        await Assert.That(IsDark(render, RectMiddleX, Row(RectBottom) + Clear)).IsFalse();
        await Assert.That(IsDark(render, RectMiddleX, Row(RectBottom + RectHeight) - Clear)).IsFalse();
    }

    /// <summary>The outline's colour is the one given and a fill drawn before it stays inside.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineColourDiffersFromTheFill()
    {
        var render = RenderPage(static (ref PdfPageCanvas canvas) =>
        {
            canvas.FillRectangle(0F, 1F, 0F, RectLeft, RectBottom, RectWidth, RectHeight);
            canvas.StrokeRectangle(new(0F, 0F, 1F, Thick), RectLeft, RectBottom, RectWidth, RectHeight);
        });
        await Assert.That(render.IsNear((int)RectLeft, Row(RectMiddleY), new(0, 0, byte.MaxValue), Tolerance)).IsTrue();
        await Assert.That(render.IsNear(RectMiddleX, Row(RectMiddleY), new(0, byte.MaxValue, 0), Tolerance)).IsTrue();
    }

    /// <summary>A zero width draws the thinnest line rather than being refused, and a negative width is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeWidthIsRefused()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var canvas = creator.BeginPage(Side, Side);
        var refused = false;
        try
        {
            canvas.DrawLine(PdfStroke.Black(0F), LineStart, LineY, LineEnd, LineY);
            try
            {
                canvas.StrokeRectangle(PdfStroke.Black(-1F), RectLeft, RectBottom, RectWidth, RectHeight);
            }
            catch (ArgumentOutOfRangeException)
            {
                refused = true;
            }
        }
        finally
        {
            canvas.Dispose();
        }

        await Assert.That(refused).IsTrue();
    }

    /// <summary>In a tagged document a line is refused outside marked content and passes the accessibility checks as an artifact.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentDrawsLinesAsArtifacts()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Lines", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var canvas = creator.BeginPage(Side, Side);
        var refused = false;
        try
        {
            try
            {
                canvas.DrawLine(PdfStroke.Black(Thin), LineStart, LineY, LineEnd, LineY);
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }

            var tags = creator.Tags!;
            canvas.BeginContent(tags.AddElement(tags.Root, "P"));
            canvas.DrawText(font, TextSize, LineStart, LineEnd, "Text");
            canvas.EndContent();
            canvas.BeginArtifact(PdfArtifactKind.Layout);
            canvas.DrawLine(PdfStroke.Black(Thin), LineStart, LineY, LineEnd, LineY);
            canvas.StrokeRectangle(PdfStroke.Black(Thin), RectLeft, RectBottom, RectWidth, RectHeight);
            canvas.EndArtifact();
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None);

        await Assert.That(refused).IsTrue();
        await Assert.That(accessibility.Findings.Select(static finding => finding.Code).ToArray()).IsEmpty();
    }

    /// <summary>Draws on a page with a callback and renders the first page.</summary>
    /// <param name="draw">Draws on the canvas.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderPage(DrawPage draw)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var canvas = creator.BeginPage(Side, Side);
        try
        {
            draw(ref canvas);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var pdf = creator.ToArray();
        CreatedFiles.Keep("strokes.pdf", pdf);
        return OptimizerTestKit.Render(pdf);
    }

    /// <summary>Gets the row of a pixel for a y measured up from the page's bottom.</summary>
    /// <param name="y">The y in points.</param>
    /// <returns>The row from the top.</returns>
    private static int Row(float y) => Side - (int)y;

    /// <summary>Determines whether a pixel is nearly black.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row from the top.</param>
    /// <returns><see langword="true"/> when every channel is low.</returns>
    private static bool IsDark(RenderedImage image, int x, int y)
    {
        var pixel = CreationTestKit.PixelAt(image, x, y);
        return pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark;
    }

    /// <summary>Counts the pixels that are not nearly white.</summary>
    /// <param name="image">The render.</param>
    /// <returns>The count.</returns>
    private static int CountDark(RenderedImage image)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                count += pixel.Red < Bright || pixel.Green < Bright || pixel.Blue < Bright ? 1 : 0;
            }
        }

        return count;
    }
}
