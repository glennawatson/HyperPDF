// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks caller-buffer stride, boundaries and lifetime during tile rendering.</summary>
public sealed class PdfPageRendererBufferTests
{
    /// <summary>The tile width.</summary>
    private const int Width = 91;

    /// <summary>The tile height.</summary>
    private const int Height = 67;

    /// <summary>The bytes in a pixel row.</summary>
    private const int RowBytes = Width * RenderedImage.BytesPerPixel;

    /// <summary>The sentinel bytes before and after the target.</summary>
    private const int GuardBytes = 16;

    /// <summary>The tile's horizontal page offset.</summary>
    private const int OffsetX = 13;

    /// <summary>The tile's vertical page offset.</summary>
    private const int OffsetY = 19;

    /// <summary>The render scale.</summary>
    private const float Scale = 1.25F;

    /// <summary>The sentinel value that drawing must leave untouched.</summary>
    private const byte Sentinel = 0xA5;

    /// <summary>The page edge in points.</summary>
    private const int PageEdge = 200;

    /// <summary>The deliberately unaligned row stride.</summary>
    private const int UnalignedStride = RowBytes + 1;

    /// <summary>Rotated, offset tiles preserve every pixel while leaving row padding and span guards untouched.</summary>
    /// <param name="turns">The clockwise quarter turns.</param>
    /// <param name="padding">The bytes between rows.</param>
    /// <param name="progressive">Whether to use progressive rendering.</param>
    /// <param name="flags">The drawing options.</param>
    /// <returns>A task.</returns>
    [Test]
    [MatrixDataSource]
    public async Task StridedSubspanMatchesPackedPixels(
        [Matrix(0, 1, 2, 3)] int turns,
        [Matrix(4, 12)] int padding,
        [Matrix(false, true)] bool progressive,
        [Matrix(PdfRenderFlags.None, PdfRenderFlags.Grayscale, PdfRenderFlags.Annotations, PdfRenderFlags.Annotations | PdfRenderFlags.Grayscale)] PdfRenderFlags flags)
    {
        using var page = new RenderTestPage(CreatePage());
        var request = new PdfTileRequest(0, Scale, turns, OffsetX, OffsetY, flags);
        var expected = CopiedPixels(page, request, Width, Height);
        var stride = RowBytes + padding;
        var length = ((Height - 1) * stride) + RowBytes;
        var guarded = new byte[GuardBytes + length + GuardBytes];
        guarded.AsSpan().Fill(Sentinel);

        var rendered = Render(page.Renderer, request, guarded.AsSpan(GuardBytes, length), stride, progressive);

        await Assert.That(rendered).IsTrue();
        await Assert.That(MatchesPixelsAndGuards(expected, guarded, stride, length)).IsTrue();
    }

    /// <summary>Changing the caller's target cannot cause a later render to write an earlier target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompletedRenderDoesNotRetainCallerBuffer()
    {
        using var page = new RenderTestPage(CreatePage());
        var request = new PdfTileRequest(0, Scale, 0, OffsetX, OffsetY, PdfRenderFlags.None);
        var first = new byte[RowBytes * Height];
        var second = new byte[first.Length];
        var firstRendered = Render(page.Renderer, request, first, RowBytes, false);
        first.AsSpan().Fill(Sentinel);
        var secondRendered = Render(page.Renderer, request, second, RowBytes, false);

        await Assert.That(firstRendered && secondRendered).IsTrue();
        await Assert.That(first.AsSpan().IndexOfAnyExcept(Sentinel)).IsEqualTo(-1);
        await Assert.That(second.AsSpan().IndexOfAnyExcept(Sentinel)).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>Growing and shrinking targets keep their own pixels and never change an earlier caller's buffer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlternatingDimensionsMatchCopiedPixels()
    {
        using var page = new RenderTestPage(CreatePage());
        var completed = new List<byte[]>();
        int[] steps = [2, 0, 1, 0];
        var matched = true;
        foreach (var step in steps)
        {
            var width = Width + step;
            var height = Height + step;
            var stride = width * RenderedImage.BytesPerPixel;
            var pixels = new byte[stride * height];
            var request = new PdfTileRequest(0, Scale, step, OffsetX, OffsetY, PdfRenderFlags.Annotations | PdfRenderFlags.Grayscale);
            var expected = CopiedPixels(page, request, width, height);
            matched &= page.Renderer.Render(request, new(pixels, width, height, stride)) && pixels.AsSpan().SequenceEqual(expected);
            foreach (var prior in completed)
            {
                matched &= prior.AsSpan().IndexOfAnyExcept(Sentinel) < 0;
            }

            pixels.AsSpan().Fill(Sentinel);
            completed.Add(pixels);
        }

        await Assert.That(matched).IsTrue();
    }

    /// <summary>A stride not aligned to BGRA pixels fails without writing caller memory.</summary>
    /// <param name="progressive">Whether to use progressive rendering.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnalignedStrideFailsWithoutWriting(bool progressive)
    {
        using var page = new RenderTestPage(CreatePage());
        var request = new PdfTileRequest(0, Scale, 0, OffsetX, OffsetY, PdfRenderFlags.None);
        var pixels = new byte[UnalignedStride * Height];
        pixels.AsSpan().Fill(Sentinel);

        var rendered = Render(page.Renderer, request, pixels, UnalignedStride, progressive);

        await Assert.That(rendered).IsFalse();
        await Assert.That(pixels.AsSpan().IndexOfAnyExcept(Sentinel)).IsEqualTo(-1);
    }

    /// <summary>Creates overlapping translucent shapes for comparing tile pixels.</summary>
    /// <returns>The generated PDF.</returns>
    private static byte[] CreatePage()
    {
        var pdf = new RenderTestPdf(PageEdge, PageEdge)
        {
            Content = "1 0 0 rg 10 10 150 150 re f /GS gs 0 0 1 rg 40 40 150 150 re f q 100 0 0 100 20 20 cm /Im Do Q BT /F1 12 Tf 20 120 Td (Pixel parity) Tj ET",
        };
        var image = pdf.AddStream(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode",
            "FF0000 00FF00 0000FF FFFF00>");
        var appearance = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", "0 1 0 rg 0 0 10 10 re f");
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [100 100 140 140] /F 4 /AP << /N {appearance} 0 R >> >>");
        pdf.Resources = $"/ExtGState << /GS << /Type /ExtGState /ca 0.5 >> >> /XObject << /Im {image} 0 R >> /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        return pdf.ToBytes();
    }

    /// <summary>Replays identical page recordings through the original allocated surface and copy path.</summary>
    /// <param name="testPage">The test document.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The packed reference pixels.</returns>
    /// <exception cref="InvalidOperationException">The reference surface copy fails.</exception>
    private static unsafe byte[] CopiedPixels(RenderTestPage testPage, PdfTileRequest request, int width, int height)
    {
        var page = PdfDocumentPages.GetPage(testPage.Document, 0);
        var cache = PdfDocumentRendering.GetRenderCache(testPage.Document);
        var printing = (request.Flags & PdfRenderFlags.Printing) != 0;
        using var content = PageRecorder.RecordContent(cache, page, printing, out _, out _);
        using var annotations = (request.Flags & PdfRenderFlags.Annotations) != 0
            ? PageRecorder.RecordAnnotations(cache, page, printing, out _, out _)
            : null;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var paints = new RenderSurface();
        var canvas = surface.Canvas;
        var saved = canvas.Save();
        canvas.ClipRect(new(0, 0, width, height));
        canvas.Clear(SKColors.White);
        if ((request.Flags & PdfRenderFlags.Grayscale) != 0)
        {
            _ = canvas.SaveLayer(paints.GrayPaint);
        }

        var matrix = SkiaConversions.ToSkMatrix(PdfPageRenderer.GetMatrix(page, request));
        canvas.DrawPicture(SkiaResources.Picture(content), in matrix);
        if (annotations is not null)
        {
            canvas.DrawPicture(SkiaResources.Picture(annotations), in matrix);
        }

        canvas.RestoreToCount(saved);
        var rowBytes = width * RenderedImage.BytesPerPixel;
        var pixels = new byte[rowBytes * height];
        fixed (byte* pointer = pixels)
        {
            if (!surface.ReadPixels(new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), (nint)pointer, rowBytes, 0, 0))
            {
                throw new InvalidOperationException("The reference surface copy failed.");
            }
        }

        return pixels;
    }

    /// <summary>Runs either render entry point with the same borrowed span.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="request">The tile selection.</param>
    /// <param name="pixels">The borrowed target.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <param name="progressive">Whether to use progressive rendering.</param>
    /// <returns>Whether rendering completed.</returns>
    private static bool Render(PdfPageRenderer renderer, in PdfTileRequest request, Span<byte> pixels, int stride, bool progressive) => progressive
        ? renderer.RenderProgressive(request, new(pixels, Width, Height, stride), null, CancellationToken.None) == PdfRenderStatus.Done
        : renderer.Render(request, new(pixels, Width, Height, stride));

    /// <summary>Compares row pixels and all bytes outside the target's visible rows.</summary>
    /// <param name="expected">The packed pixel rows.</param>
    /// <param name="guarded">The guarded target buffer.</param>
    /// <param name="stride">The target row stride.</param>
    /// <param name="length">The target span length, excluding outer guards.</param>
    /// <returns>Whether the pixels match and every guard byte survived.</returns>
    private static bool MatchesPixelsAndGuards(byte[] expected, byte[] guarded, int stride, int length)
    {
        if (guarded.AsSpan(0, GuardBytes).IndexOfAnyExcept(Sentinel) >= 0
            || guarded.AsSpan(GuardBytes + length).IndexOfAnyExcept(Sentinel) >= 0)
        {
            return false;
        }

        for (var row = 0; row < Height; row++)
        {
            var start = GuardBytes + (row * stride);
            if (!guarded.AsSpan(start, RowBytes).SequenceEqual(expected.AsSpan(row * RowBytes, RowBytes)))
            {
                return false;
            }

            if (row < Height - 1 && guarded.AsSpan(start + RowBytes, stride - RowBytes).IndexOfAnyExcept(Sentinel) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
