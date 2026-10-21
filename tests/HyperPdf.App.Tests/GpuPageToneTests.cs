// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Rendering;
using HyperPdf.Core.Rendering;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks the GPU tone effect against the exact CPU page-tone mapping.</summary>
public sealed class GpuPageToneTests
{
    /// <summary>The side of the colour grid.</summary>
    private const int Side = 256;

    /// <summary>The bytes per premultiplied BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The eight-bit channel mask.</summary>
    private const int ChannelMask = 255;

    /// <summary>The source blue pattern multiplier.</summary>
    private const int BlueX = 37;

    /// <summary>The source blue pattern's other multiplier.</summary>
    private const int BlueY = 71;

    /// <summary>The shift for the green channel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The shift for the red channel.</summary>
    private const int RedOffset = 2;

    /// <summary>The shift for the alpha channel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The tone variants that exceed the bounded reusable filter cache.</summary>
    private const int ToneVariants = 12;

    /// <summary>The number of tiles toned at the same time.</summary>
    private const int ConcurrentTiles = 16;

    /// <summary>Common light, dark and contrast tones match CPU pixels across a colour grid.</summary>
    /// <param name="paper">The packed paper colour.</param>
    /// <param name="ink">The packed ink colour.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0xFAF7F0U, 0x282624U)]
    [Arguments(0x2A2826U, 0xD2CDC5U)]
    [Arguments(0x202326U, 0xE8E8E8U)]
    [Arguments(0x000000U, 0xFFFFFFU)]
    public async Task ShaderMatchesCpuTone(uint paper, uint ink)
    {
        var tone = new PageTone(paper, ink);
        using var tones = new GpuPageTone();
        var source = CreateSource();
        var expected = (byte[])source.Clone();
        tone.Apply(expected);
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul, srgb);
        using var image = SKImage.FromPixelCopy(info, source, Side * BytesPerPixel);
        using var toned = tones.TryApply(image, info, tone, null);
        var actual = new byte[source.Length];
        var read = ReadPixels(toned, info, actual);

        using (Assert.Multiple())
        {
            await Assert.That(tones.CanApply(tone)).IsTrue();
            await Assert.That(toned).IsNotNull();
            await Assert.That(read).IsTrue();
            await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
        }
    }

    /// <summary>Dynamic theme changes retain GPU output after the reusable filter cache fills.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DynamicTonesKeepGpuPathAfterCacheFills()
    {
        using var tones = new GpuPageTone();
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul, srgb);
        using var image = SKImage.FromPixelCopy(info, CreateSource(), Side * BytesPerPixel);
        var allSupported = true;
        for (var index = 0; index < ToneVariants; index++)
        {
            var tone = new PageTone(0xFAF700U + (uint)index, 0x282624U);
            using var result = tones.TryApply(image, info, tone, null);
            allSupported &= tones.CanApply(tone) && result is not null;
        }

        await Assert.That(allSupported).IsTrue();
    }

    /// <summary>Tiles toned from many threads at once all match the CPU tone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentTilesMatchCpuTone()
    {
        var tone = new PageTone(0x2A2826U, 0xD2CDC5U);
        using var tones = new GpuPageTone();
        var source = CreateSource();
        var expected = (byte[])source.Clone();
        tone.Apply(expected);
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul, srgb);
        using var image = SKImage.FromPixelCopy(info, source, Side * BytesPerPixel);
        var matches = 0;
        _ = Parallel.For(0, ConcurrentTiles, _ =>
        {
            using var toned = tones.TryApply(image, info, tone, null);
            var actual = new byte[source.Length];
            if (ReadPixels(toned, info, actual) && actual.AsSpan().SequenceEqual(expected))
            {
                _ = Interlocked.Increment(ref matches);
            }
        });

        await Assert.That(matches).IsEqualTo(ConcurrentTiles);
    }

    /// <summary>Closing one hub leaves another hub's tone resources usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToneCachesBelongToTheirRenderHub()
    {
        var first = new GpuPageTone();
        using var second = new GpuPageTone();
        var tone = new PageTone(0xFAF7F0U, 0x282624U);
        bool supported;
        using (first)
        {
            supported = first.CanApply(tone) && second.CanApply(tone);
        }

        using (Assert.Multiple())
        {
            await Assert.That(supported).IsTrue();
            await Assert.That(first.CanApply(tone)).IsFalse();
            await Assert.That(second.CanApply(tone)).IsTrue();
        }
    }

    /// <summary>Builds opaque pixels with varied RGB combinations and every red and green byte.</summary>
    /// <returns>The source pixels.</returns>
    private static byte[] CreateSource()
    {
        var pixels = new byte[Side * Side * BytesPerPixel];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var offset = ((y * Side) + x) * BytesPerPixel;
                pixels[offset] = (byte)(((x * BlueX) + (y * BlueY)) & ChannelMask);
                pixels[offset + GreenOffset] = (byte)y;
                pixels[offset + RedOffset] = (byte)x;
                pixels[offset + AlphaOffset] = ChannelMask;
            }
        }

        return pixels;
    }

    /// <summary>Reads the shader output into raw premultiplied BGRA bytes.</summary>
    /// <param name="image">The shader output.</param>
    /// <param name="info">The pixel layout.</param>
    /// <param name="pixels">The destination.</param>
    /// <returns>Whether the pixels were read.</returns>
    private static unsafe bool ReadPixels(SKImage? image, SKImageInfo info, byte[] pixels)
    {
        if (image is null)
        {
            return false;
        }

        fixed (byte* pointer = pixels)
        {
            return image.ReadPixels(info, (nint)pointer, Side * BytesPerPixel);
        }
    }
}
