// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Skia;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks the Skia-only software tile that a render head with no bitmap type of its own can use.</summary>
public sealed class SkiaBitmapRenderSurfaceTests
{
    /// <summary>The tile width.</summary>
    private const int TileWidth = 8;

    /// <summary>The tile height.</summary>
    private const int TileHeight = 4;

    /// <summary>The bytes in one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The blue byte written.</summary>
    private const byte Blue = 0x10;

    /// <summary>The green byte written.</summary>
    private const byte Green = 0x20;

    /// <summary>The red byte written.</summary>
    private const byte Red = 0x30;

    /// <summary>The alpha byte written.</summary>
    private const byte Opaque = 0xFF;

    /// <summary>The surface reports its size and the memory it uses.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsItsSize()
    {
        using var surface = new SkiaBitmapRenderSurface(TileWidth, TileHeight);

        await Assert.That(surface.Width).IsEqualTo(TileWidth);
        await Assert.That(surface.Height).IsEqualTo(TileHeight);
        await Assert.That(surface.ByteSize).IsEqualTo(TileWidth * TileHeight * BytesPerPixel);
    }

    /// <summary>What the writer puts in the target is what the Skia bitmap holds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WriterFillsTheBitmap()
    {
        using var surface = new SkiaBitmapRenderSurface(TileWidth, TileHeight);

        var result = surface.Write(0, static (target, in _) =>
        {
            target.Pixels[0] = Blue;
            target.Pixels[1] = Green;
            target.Pixels[2] = Red;
            target.Pixels[3] = Opaque;
            return target.Width == TileWidth && target.Height == TileHeight;
        });

        var pixel = surface.Bitmap.GetPixel(0, 0);

        await Assert.That(result).IsTrue();
        await Assert.That(pixel).IsEqualTo(new(Red, Green, Blue, Opaque));
    }
}
