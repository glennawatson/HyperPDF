// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks which draws share a glyph mask: the same glyph at the same size and position within a pixel shares one, and the choices that change the pixels do not.</summary>
public sealed class GlyphMaskCacheTests
{
    /// <summary>The side of the test glyph in device pixels.</summary>
    private const float GlyphSize = 10;

    /// <summary>A glyph too big for a mask, in device pixels.</summary>
    private const float HugeSize = 300;

    /// <summary>The column the first draw puts the glyph at.</summary>
    private const float Column = 20;

    /// <summary>The row the glyphs are drawn at.</summary>
    private const float Row = 30;

    /// <summary>A quarter of a pixel, the step masks are shared at.</summary>
    private const float Quarter = 0.25F;

    /// <summary>A shift that stays within the same quarter pixel as the one before.</summary>
    private const float Sliver = 0.04F;

    /// <summary>A shift that moves the glyph about a third of a pixel, to another quarter.</summary>
    private const float Third = 0.35F;

    /// <summary>The size of the canvas glyphs are drawn on.</summary>
    private const float CanvasSize = 400;

    /// <summary>The size of the test image mask in pixels.</summary>
    private const int ImageSize = 8;

    /// <summary>The middle of the unit square, which the round glyph is drawn in.</summary>
    private const float Middle = 0.5F;

    /// <summary>The whole pixels the glyph moves by when its position changes by pixels.</summary>
    private const int WholePixels = 3;

    /// <summary>The masks made when a glyph is drawn at two different quarter pixel positions.</summary>
    private const int TwoMasks = 2;

    /// <summary>The masks the draws of different choices make: plain, contrast, sharp, LCD and another glyph.</summary>
    private const int DifferentMasks = 5;

    /// <summary>The colour the test text is drawn in.</summary>
    private static readonly SKColor Ink = SKColors.Black;

    /// <summary>The settings that draw from masks.</summary>
    private static readonly PdfRenderQuality Plain = PdfRenderQuality.Default with { GlyphMasks = true };

    /// <summary>The area of the canvas glyphs are drawn on.</summary>
    private static readonly SKRect Canvas = new(0, 0, CanvasSize, CanvasSize);

    /// <summary>Verifies that drawing the same glyph twice at the same place makes one mask.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SameGlyphAtTheSamePlace_SharesOneMask()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();

        var first = Draw(masks, font, Column, Row, Plain);
        var second = Draw(masks, font, Column, Row, Plain);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
        await Assert.That(masks.Count).IsEqualTo(1);
        await Assert.That(masks.Bytes).IsGreaterThan(0);
    }

    /// <summary>Verifies that a move within the same quarter pixel shares the mask, and a move to another quarter makes a new one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MovesWithinAQuarterPixel_ShareTheMask()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();

        _ = Draw(masks, font, Column, Row, Plain);
        _ = Draw(masks, font, Column + Sliver, Row, Plain);
        var shared = masks.Count;
        _ = Draw(masks, font, Column + Third, Row, Plain);

        await Assert.That(shared).IsEqualTo(1);
        await Assert.That(masks.Count).IsEqualTo(TwoMasks);
    }

    /// <summary>Verifies that a move by whole pixels shares the mask, because the mask is placed on the pixel grid.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MovesByWholePixels_ShareTheMask()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();

        _ = Draw(masks, font, Column, Row, Plain);
        _ = Draw(masks, font, Column + (WholePixels * GlyphSize), Row + GlyphSize, Plain);

        await Assert.That(masks.Count).IsEqualTo(1);
    }

    /// <summary>Verifies that full hinting puts glyphs on whole pixels, so positions that round to one pixel share a mask.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FullHinting_SharesMasksOnWholePixels()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();
        var full = Plain with { TextHinting = PdfTextHinting.Full };

        _ = Draw(masks, font, Column, Row, full);
        _ = Draw(masks, font, Column + Quarter, Row + Quarter, full);
        _ = Draw(masks, font, Column + Third, Row + Third, full);

        await Assert.That(masks.Count).IsEqualTo(1);
    }

    /// <summary>Verifies that slight hinting shares masks along a row but not across quarter pixels of the line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SlightHinting_SnapsRowsOnly()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();
        var slight = Plain with { TextHinting = PdfTextHinting.Slight };

        _ = Draw(masks, font, Column, Row, slight);
        _ = Draw(masks, font, Column, Row + Third, slight);
        var sameRow = masks.Count;
        _ = Draw(masks, font, Column + Third, Row, slight);

        await Assert.That(sameRow).IsEqualTo(1);
        await Assert.That(masks.Count).IsEqualTo(TwoMasks);
    }

    /// <summary>Verifies that contrast, LCD edges and sharp edges each make their own mask, and other glyphs do too.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChoicesThatChangeThePixels_MakeTheirOwnMasks()
    {
        var masks = new SkiaGlyphMasks();
        var font = new object();

        _ = Draw(masks, font, Column, Row, Plain);
        _ = Draw(masks, font, Column, Row, Plain with { TextContrast = 1 });
        _ = Draw(masks, font, Column, Row, Plain with { TextEdges = PdfTextEdges.Sharp });
        _ = Draw(masks, font, Column, Row, Plain with { TextEdges = PdfTextEdges.Lcd });
        _ = Draw(masks, new(), Column, Row, Plain);

        await Assert.That(masks.Count).IsEqualTo(DifferentMasks);
    }

    /// <summary>Verifies that a glyph too big for a mask is left to be filled from its outline.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HugeGlyph_IsLeftToTheOutline()
    {
        var masks = new SkiaGlyphMasks();

        var drawn = Draw(masks, new(), Column, Row, Plain, HugeSize);

        await Assert.That(drawn).IsFalse();
        await Assert.That(masks.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that a glyph with no outline has nothing to draw and keeps no mask.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyGlyph_HasNothingToDraw()
    {
        var masks = new SkiaGlyphMasks();
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(Canvas);
        using var painter = new GlyphMaskPainter(masks);
        using var empty = new SKPath();

        var handled = painter.TryDraw(canvas, SKMatrix.Identity, new GlyphMaskGlyph<OutlineMaskSource>(new(), 1, new(empty), Matrix3x2.CreateScale(GlyphSize)), Style(Plain));

        await Assert.That(handled).IsTrue();
        await Assert.That(masks.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that an image mask draws from a mask too, and is shared like a glyph.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImageMask_SharesOneMask()
    {
        var masks = new SkiaGlyphMasks();
        var owner = new object();
        using var bitmap = new SKBitmap(new(ImageSize, ImageSize, SKColorType.Alpha8, SKAlphaType.Premul));
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(Canvas);
        using var painter = new GlyphMaskPainter(masks);
        var toDevice = Matrix3x2.CreateTranslation(Column, Row);

        var first = painter.TryDraw(canvas, SKMatrix.Identity, new GlyphMaskGlyph<ImageMaskSource>(owner, 1, new(image, default), toDevice), Style(Plain));
        var second = painter.TryDraw(canvas, SKMatrix.Identity, new GlyphMaskGlyph<ImageMaskSource>(owner, 1, new(image, default), toDevice), Style(Plain));

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
        await Assert.That(masks.Count).IsEqualTo(1);
    }

    /// <summary>Draws a round glyph from a mask.</summary>
    /// <param name="masks">The shared masks.</param>
    /// <param name="font">What the glyph belongs to.</param>
    /// <param name="x">The device column of the glyph's origin.</param>
    /// <param name="y">The device row of the glyph's origin.</param>
    /// <param name="quality">The quality settings.</param>
    /// <returns><see langword="true"/> when the glyph was drawn from a mask.</returns>
    private static bool Draw(SkiaGlyphMasks masks, object font, float x, float y, PdfRenderQuality quality) => Draw(masks, font, x, y, quality, GlyphSize);

    /// <summary>Draws a round glyph of a size from a mask.</summary>
    /// <param name="masks">The shared masks.</param>
    /// <param name="font">What the glyph belongs to.</param>
    /// <param name="x">The device column of the glyph's origin.</param>
    /// <param name="y">The device row of the glyph's origin.</param>
    /// <param name="quality">The quality settings.</param>
    /// <param name="size">The glyph's side in device pixels.</param>
    /// <returns><see langword="true"/> when the glyph was drawn from a mask.</returns>
    private static bool Draw(SkiaGlyphMasks masks, object font, float x, float y, PdfRenderQuality quality, float size)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(Canvas);
        using var painter = new GlyphMaskPainter(masks);
        using var builder = new SKPathBuilder();
        builder.AddCircle(Middle, Middle, Middle);
        using var path = builder.Detach();
        var toDevice = Matrix3x2.CreateScale(size) * Matrix3x2.CreateTranslation(x, y);

        return painter.TryDraw(canvas, SKMatrix.Identity, new GlyphMaskGlyph<OutlineMaskSource>(font, 1, new(path), toDevice), Style(quality));
    }

    /// <summary>Makes the style a glyph is drawn with.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The style.</returns>
    private static GlyphMaskStyle Style(PdfRenderQuality quality) => new(quality, Ink, SKBlendMode.SrcOver, true);
}
