// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Drawing;

/// <summary>Checks page replay into a Skia surface against caller-buffer rendering.</summary>
public sealed class SkiaSurfaceRenderTargetTests
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 128;

    /// <summary>The bytes in a premultiplied BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>A GPU-capable surface route gives the same raster output without a CPU target buffer.</summary>
    /// <param name="grayscale">Whether the replay includes the grayscale display effect.</param>
    /// <param name="colorType">The Skia surface channel order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, SKColorType.Bgra8888)]
    [Arguments(false, SKColorType.Rgba8888)]
    [Arguments(true, SKColorType.Bgra8888)]
    [Arguments(true, SKColorType.Rgba8888)]
    public async Task SurfaceReplayMatchesCallerPixels(bool grayscale, SKColorType colorType)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        using var renderer = new PdfPageRenderer(document);
        var flags = grayscale ? PdfRenderFlags.Grayscale : PdfRenderFlags.None;
        var request = new PdfTileRequest(0, 1, 0, 0, 0, flags);
        var cpuPixels = new byte[TileSide * TileSide * BytesPerPixel];
        var prepared = renderer.Prepare(request, CancellationToken.None);
        var countAfterPrepare = renderer.PictureCount;
        var cpuTarget = new PdfTileTarget(cpuPixels, TileSide, TileSide, TileSide * BytesPerPixel);
        var cpuDrawn = renderer.Render(request, cpuTarget);
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(TileSide, TileSide, colorType, SKAlphaType.Premul, srgb);
        using var target = new SkiaSurfaceRenderTarget(SKSurface.Create(info), info);
        var surfaceDrawn = renderer.RenderToTarget(request, target);
        var progressiveStatus = renderer.RenderProgressiveToTarget(request, target, null, CancellationToken.None);
        using var image = target.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var matches = true;
        for (var y = 0; y < TileSide && matches; y++)
        {
            for (var x = 0; x < TileSide; x++)
            {
                var offset = ((y * TileSide) + x) * BytesPerPixel;
                var expected = new SKColor(cpuPixels[offset + RedOffset], cpuPixels[offset + 1], cpuPixels[offset], cpuPixels[offset + AlphaOffset]);
                if (bitmap.GetPixel(x, y) == expected)
                {
                    continue;
                }

                matches = false;
                break;
            }
        }

        using (Assert.Multiple())
        {
            await Assert.That(prepared).IsTrue();
            await Assert.That(countAfterPrepare).IsEqualTo(1);
            await Assert.That(cpuDrawn).IsTrue();
            await Assert.That(surfaceDrawn).IsTrue();
            await Assert.That(progressiveStatus).IsEqualTo(PdfRenderStatus.Done);
            await Assert.That(renderer.PictureCount).IsEqualTo(countAfterPrepare);
            await Assert.That(matches).IsTrue();
        }
    }

    /// <summary>Disposal invalidates the target without invalidating an image snapshot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SnapshotRetainsPixelsAfterTargetDisposal()
    {
        var info = new SKImageInfo(TileSide, TileSide, SKColorType.Bgra8888, SKAlphaType.Premul);
        var target = new SkiaSurfaceRenderTarget(SKSurface.Create(info), info);
        target.Canvas.Clear(SKColors.Red);
        using var image = target.Snapshot();
        target.Dispose();
        using var bitmap = SKBitmap.FromImage(image);

        using (Assert.Multiple())
        {
            await Assert.That(target.IsValid).IsFalse();
            await Assert.That(bitmap.GetPixel(0, 0)).IsEqualTo(SKColors.Red);
            await Assert.That(() => target.Snapshot()).Throws<ObjectDisposedException>();
        }
    }

    /// <summary>Accepts common GPU colour layouts while rejecting non-colour coverage surfaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TargetAcceptsRgbaAndBgraButRejectsGray()
    {
        var bgra = new SKImageInfo(TileSide, TileSide, SKColorType.Bgra8888, SKAlphaType.Premul);
        var rgba = new SKImageInfo(TileSide, TileSide, SKColorType.Rgba8888, SKAlphaType.Premul);
        var gray = new SKImageInfo(TileSide, TileSide, SKColorType.Gray8, SKAlphaType.Opaque);
        using var linearSpace = SKColorSpace.CreateSrgbLinear();
        var linear = new SKImageInfo(TileSide, TileSide, SKColorType.Rgba8888, SKAlphaType.Premul, linearSpace);
        using var bgraTarget = new SkiaSurfaceRenderTarget(SKSurface.Create(bgra), bgra);
        using var rgbaTarget = new SkiaSurfaceRenderTarget(SKSurface.Create(rgba), rgba);
        using var graySurface = SKSurface.Create(gray);
        using var linearSurface = SKSurface.Create(linear);

        using (Assert.Multiple())
        {
            await Assert.That(bgraTarget.IsValid).IsTrue();
            await Assert.That(rgbaTarget.IsValid).IsTrue();
            await Assert.That(() => new SkiaSurfaceRenderTarget(graySurface, gray)).Throws<ArgumentException>();
            await Assert.That(() => new SkiaSurfaceRenderTarget(linearSurface, linear)).Throws<ArgumentException>();
        }
    }
}
