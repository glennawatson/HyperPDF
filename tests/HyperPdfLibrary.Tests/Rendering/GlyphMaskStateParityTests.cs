// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that remembering the canvas matrix and paint between glyphs, and converting outlines only when needed, draw the same pixels as setting everything for every glyph.</summary>
public sealed class GlyphMaskStateParityTests
{
    /// <summary>The descriptor flags of the non-symbolic test font.</summary>
    private const int FontFlags = 32;

    /// <summary>The side of the test canvas in pixels.</summary>
    private const int CanvasSize = 120;

    /// <summary>The side of a test glyph in device pixels.</summary>
    private const float GlyphSize = 12;

    /// <summary>The distance between two glyphs in a run.</summary>
    private const float Advance = 14;

    /// <summary>The number of glyphs in a run.</summary>
    private const int RunLength = 6;

    /// <summary>The middle of the unit square, which the round glyph is drawn in.</summary>
    private const float Middle = 0.5F;

    /// <summary>The shift a Save and Restore pair moves the canvas by in between runs.</summary>
    private const float Shift = 3;

    /// <summary>The settings that draw from masks.</summary>
    private static readonly PdfRenderQuality Masks = PdfRenderQuality.Default with { GlyphMasks = true };

    /// <summary>Verifies that one painter drawing runs of changing colour, blend and edges, with the canvas matrix changed between them, draws what a new painter for every glyph draws.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunsWithChangingState_DrawWhatAFreshPainterPerGlyphDraws()
    {
        using var kept = Paint(reuse: true);
        using var fresh = Paint(reuse: false);

        await Assert.That(kept.GetPixelSpan().SequenceEqual(fresh.GetPixelSpan())).IsTrue();
        await Assert.That(kept.GetPixelSpan().ToArray().Any(static b => b != 0)).IsTrue();
    }

    /// <summary>Verifies that a page of text with nested saves, scaled and moved text, spaces and a colour change draws the same with and without a save around every show.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedStatesPage_DrawsTheSameWithEveryShowSaved()
    {
        const string Plain = "0 g BT /F1 10 Tf 10 80 Td (Hello  world) Tj 0 -14 Td 1 0 0 rg (red text here) Tj ET";
        await AssertSameWhenSaved(Masks, Plain);
        await AssertSameWhenSaved(Masks, "q 1 0 0 1 4 4 cm BT /F1 10 Tf 10 80 Td (one) Tj ET q 0.9 0 0 0.9 0 0 cm BT /F1 10 Tf 10 60 Td (two) Tj ET Q BT /F1 10 Tf 10 40 Td (three) Tj ET Q");
    }

    /// <summary>Verifies that stroked and fill-and-stroke text draw the same with and without a save around every show.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StrokedText_DrawsTheSameWithEveryShowSaved()
    {
        await AssertSameWhenSaved(Masks, "1 0 0 RG 0.5 w BT /F1 10 Tf 1 Tr 10 80 Td (stroke) Tj 2 Tr 0 -14 Td (both) Tj ET");
        await AssertSameWhenSaved(Masks, "BT /F1 10 Tf 0 Tr 10 80 Td (fill) Tj 3 Tr (hidden) Tj 0 Tr (shown) Tj ET");
    }

    /// <summary>Verifies that LCD, sharp and hinted text draw the same with and without a save around every show.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LcdSharpAndHintedText_DrawTheSameWithEveryShowSaved()
    {
        const string Content = "BT /F1 10 Tf 10 80 Td (Hello world) Tj 0 -14 Td 0 0 1 rg (blue ink) Tj ET";
        await AssertSameWhenSaved(Masks with { TextEdges = PdfTextEdges.Lcd }, Content);
        await AssertSameWhenSaved(Masks with { TextEdges = PdfTextEdges.Sharp }, Content);
        await AssertSameWhenSaved(Masks with { TextHinting = PdfTextHinting.Full, TextContrast = 1 }, Content);
    }

    /// <summary>Draws runs of round glyphs onto a bitmap.</summary>
    /// <param name="reuse">Whether one painter draws every glyph; otherwise each glyph gets a new painter, which remembers nothing.</param>
    /// <returns>The bitmap.</returns>
    private static SKBitmap Paint(bool reuse)
    {
        var masks = new SkiaGlyphMasks();
        var bitmap = new SKBitmap(new(CanvasSize, CanvasSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var shared = new GlyphMaskPainter(masks);
        using var builder = new SKPathBuilder();
        builder.AddCircle(Middle, Middle, Middle);
        using var path = builder.Detach();
        var font = new object();
        Run[] runs =
        [
            new(SKColors.Black, SKBlendMode.SrcOver, PdfTextEdges.Smooth),
            new(SKColors.Black, SKBlendMode.SrcOver, PdfTextEdges.Smooth),
            new(SKColors.Red, SKBlendMode.SrcOver, PdfTextEdges.Smooth),
            new(SKColors.Blue, SKBlendMode.Multiply, PdfTextEdges.Smooth),
            new(SKColors.Black, SKBlendMode.SrcOver, PdfTextEdges.Lcd),
            new(SKColors.Black, SKBlendMode.SrcOver, PdfTextEdges.Lcd),
            new(SKColors.Green, SKBlendMode.SrcOver, PdfTextEdges.Smooth),
        ];

        for (var row = 0; row < runs.Length; row++)
        {
            var (color, blend, edges) = runs[row];
            var quality = Masks with { TextEdges = edges };
            for (var i = 0; i < RunLength; i++)
            {
                DrawGlyph(canvas, reuse ? shared : null, masks, font, path, new(i, row), new(quality, color, blend, true));
            }

            // What the device does between runs: it changes the canvas matrix and tells the painter to forget its own.
            canvas.SetMatrix(SKMatrix.CreateTranslation(Shift, Shift));
            shared.ForgetMatrix();
        }

        return bitmap;
    }

    /// <summary>Draws one glyph with a shared painter, or with a new one.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="shared">The painter that remembers state, or <see langword="null"/> for a new painter.</param>
    /// <param name="masks">The shared masks.</param>
    /// <param name="font">What the glyph belongs to.</param>
    /// <param name="path">The glyph outline.</param>
    /// <param name="place">Where the glyph goes.</param>
    /// <param name="style">The style.</param>
    private static void DrawGlyph(SKCanvas canvas, GlyphMaskPainter? shared, SkiaGlyphMasks masks, object font, SKPath path, in GlyphPlace place, in GlyphMaskStyle style)
    {
        var toDevice = Matrix3x2.CreateScale(GlyphSize) * Matrix3x2.CreateTranslation(Advance + (place.Column * Advance), Advance + (place.Row * Advance));
        var glyph = new GlyphMaskGlyph<OutlineMaskSource>(font, 1, new(path), toDevice);
        if (shared is not null)
        {
            _ = shared.TryDraw(canvas, SKMatrix.Identity, glyph, style);
            return;
        }

        using var fresh = new GlyphMaskPainter(masks);
        _ = fresh.TryDraw(canvas, SKMatrix.Identity, glyph, style);
    }

    /// <summary>Checks that a page draws the same plainly and with every show saved and restored, which makes the device forget its state before each one.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <param name="content">The page content; shows are the <c>Tj</c> operators.</param>
    /// <returns>A task representing the asynchronous check.</returns>
    private static async Task AssertSameWhenSaved(PdfRenderQuality quality, string content)
    {
        var plain = RenderText(quality, content);

        // A save and restore that change nothing still make the device forget what it remembers about the canvas.
        var saved = RenderText(quality, content.Replace(") Tj", ") Tj q Q", StringComparison.Ordinal));

        await Assert.That(plain.AsSpan().SequenceEqual(saved)).IsTrue();
        await Assert.That(plain.AsSpan().ToArray().Any(static b => b != byte.MaxValue)).IsTrue();
    }

    /// <summary>Renders a text page at scale 1.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The pixels.</returns>
    private static byte[] RenderText(PdfRenderQuality quality, string content)
    {
        var bytes = FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = content });
        using var page = new RenderTestPage(bytes);
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        var pixels = new byte[FontTestDocument.PageWidth * FontTestDocument.PageHeight * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, FontTestDocument.PageWidth, FontTestDocument.PageHeight, FontTestDocument.PageWidth * RenderedImage.BytesPerPixel));
        return pixels;
    }

    /// <summary>One run of glyphs in the painter test.</summary>
    /// <param name="Color">The text colour.</param>
    /// <param name="Blend">The blend mode.</param>
    /// <param name="Edges">How the edges are made.</param>
    private readonly record struct Run(SKColor Color, SKBlendMode Blend, PdfTextEdges Edges);

    /// <summary>Where a glyph goes in the painter test.</summary>
    /// <param name="Column">The column.</param>
    /// <param name="Row">The row.</param>
    private readonly record struct GlyphPlace(int Column, int Row);
}
