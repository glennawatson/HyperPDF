// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Compares grouped glyph drawing with the original per-glyph recording.</summary>
[NotInParallel]
public sealed class GlyphBatchRenderingTests
{
    /// <summary>The page width.</summary>
    private const int Width = 160;

    /// <summary>The page height.</summary>
    private const int Height = 80;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>Separate opaque glyphs produce the same software pixels when grouped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparateOpaqueGlyphsKeepPixels()
    {
        var pdf = CreatePdf("BT /F1 20 Tf 12 Tc 10 20 Td (AAAAA) Tj ET");
        var original = RenderWithFont(pdf, false);
        var grouped = RenderWithFont(pdf, true);

        await Assert.That(original.AsSpan().SequenceEqual(grouped)).IsTrue();
    }

    /// <summary>Overlapping glyphs retain their separate paint operations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingGlyphsKeepPixels()
    {
        var pdf = CreatePdf("BT /F1 20 Tf 10 20 Td (AAAAA) Tj ET");
        var original = RenderWithFont(pdf, false);
        var grouped = RenderWithFont(pdf, true);

        await Assert.That(original.AsSpan().SequenceEqual(grouped)).IsTrue();
    }

    /// <summary>A shape between text shows and a later colour change preserve PDF paint order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InterleavedPaintKeepsPixels()
    {
        var pdf = CreatePdf("BT /F1 20 Tf 12 Tc 10 20 Td (AAAA) Tj ET 1 0 0 rg 20 20 24 20 re f 0 0 0 rg BT /F1 20 Tf 12 Tc 10 50 Td (AAAA) Tj ET");
        var original = RenderWithFont(pdf, false);
        var grouped = RenderWithFont(pdf, true);

        await Assert.That(original.AsSpan().SequenceEqual(grouped)).IsTrue();
    }

    /// <summary>Creates a page with a square test font.</summary>
    /// <param name="content">The page's graphics operators.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePdf(string content)
    {
        var pdf = new RenderTestPdf(Width, Height) { Content = content };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Installs the test font only while one recording is made.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="batchGlyphs">Whether compatible glyphs are grouped.</param>
    /// <returns>The software pixels.</returns>
    private static byte[] RenderWithFont(byte[] pdf, bool batchGlyphs)
    {
        var previous = PdfFont.Factory;
        PdfFont.Factory = static dictionary => new SquareFont(dictionary);
        try
        {
            return Render(pdf, batchGlyphs);
        }
        finally
        {
            PdfFont.Factory = previous;
        }
    }

    /// <summary>Records a page directly so the grouped and original modes can be compared.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="batchGlyphs">Whether compatible glyphs are grouped.</param>
    /// <returns>The software pixels.</returns>
    /// <exception cref="InvalidOperationException">The test surface cannot be read.</exception>
    private static unsafe byte[] Render(byte[] pdf, bool batchGlyphs)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var page = PdfDocumentPages.GetPage(document, 0);
        var bounds = new PdfRect(0, 0, page.Width, page.Height);
        using var device = new SkiaContentDevice(new(0, 0, page.Width, page.Height), batchGlyphs);
        var builder = new PdfPathBuilder();
        builder.AddRect(bounds);
        device.Clip(builder.Detach(), false, Matrix3x2.Identity);
        using (var interpreter = new ContentInterpreter(PdfDocumentRendering.GetRenderCache(document), device, 0))
        {
            ContentExecution.RunPage(interpreter, page);
        }

        using var picture = device.Finish();
        var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawPicture(((SkiaRenderPicture)picture).Native);
        using var image = surface.Snapshot();
        var pixels = new byte[Width * Height * PixelBytes];
        fixed (byte* pointer = pixels)
        {
            if (!image.ReadPixels(info, (nint)pointer, Width * PixelBytes))
            {
                throw new InvalidOperationException("Could not read the raster surface.");
            }
        }

        return pixels;
    }
}
