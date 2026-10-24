// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Skia.Bitmaps;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks direct access to raster image buffers.</summary>
public sealed class ImmutableBitmapTests
{
    /// <summary>The ImageSize used by the test workload.</summary>
    private const int ImageSize = 8;

    /// <summary>Verifies that locking a raster snapshot exposes its existing pixel memory.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Lock_RasterImage_UsesExistingBuffer()
    {
        using var surface = SKSurface.Create(new SKImageInfo(ImageSize, ImageSize));
        var image = surface.Snapshot();
        using var pixels = image.PeekPixels();
        using var bitmap = new ImmutableBitmap(image);
        using var framebuffer = bitmap.Lock();
        await Assert.That(framebuffer.Address).IsEqualTo(pixels.GetPixels());
        await Assert.That(bitmap.Format).IsNotNull();
    }
}
