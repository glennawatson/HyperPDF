// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks the byte-bounded, least recently used image cache and its reference counting.</summary>
public sealed class ImageCacheTests
{
    /// <summary>The side of the test images in pixels.</summary>
    private const int Side = 8;

    /// <summary>The bytes one test image holds.</summary>
    private const long ImageBytes = Side * Side * RenderedImage.BytesPerPixel;

    /// <summary>The images a test cache holds, and the decode attempts made for a broken stream.</summary>
    private const int Attempts = 2;

    /// <summary>A capacity that fits two test images.</summary>
    private const long TwoImages = ImageBytes * Attempts;

    /// <summary>The first reduced display resolution.</summary>
    private const int ReducedLevel = 1;

    /// <summary>The cache keeps images within its byte limit, evicting the least recently used first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictsTheLeastRecentlyUsedImage()
    {
        var cache = new ImageCache { Capacity = TwoImages };
        var first = Stream();
        var second = Stream();
        var third = Stream();

        Use(cache, first);
        Use(cache, second);
        Use(cache, first);
        var thirdEntry = cache.Acquire(third, 0, static (stream, state) => Create())!;
        thirdEntry.Release();

        await Assert.That(cache.Count).IsEqualTo(Attempts);
        await Assert.That(cache.Bytes).IsEqualTo(TwoImages);
        var decodes = 0;
        var again = cache.Acquire(first, 0, (stream, state) =>
        {
            // Counts decodes to show the first image is still cached; a capturing lambda keeps the test short.
            decodes++;
            return Create();
        });
        again!.Release();
        await Assert.That(decodes).IsEqualTo(0);
    }

    /// <summary>An evicted image stays usable while a recording holds it, and is disposed when released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictedImagesWaitForTheirUsers()
    {
        var cache = new ImageCache { Capacity = TwoImages };
        var held = cache.Acquire(Stream(), 0, static (stream, state) => Create())!;
        var skiaImage = (SkiaRenderImage)held.Image;
        Use(cache, Stream());
        Use(cache, Stream());

        _ = skiaImage.Native.Handle;
        await Assert.That(held.IsDisposed).IsFalse();
        await Assert.That(held.Image.Width).IsEqualTo(Side);

        held.Release();

        await Assert.That(held.IsDisposed).IsTrue();
        await Assert.That(() => skiaImage.Native).Throws<ObjectDisposedException>();
    }

    /// <summary>Lowering the limit evicts at once, and a stream that fails to decode is not decoded again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoweringTheLimitEvictsAndFailuresAreRemembered()
    {
        var cache = new ImageCache();
        Use(cache, Stream());
        Use(cache, Stream());
        cache.Capacity = 0;
        var broken = Stream();
        var attempts = 0;
        for (var i = 0; i < Attempts; i++)
        {
            _ = cache.Acquire(broken, 0, (stream, state) =>
            {
                // Counts decode attempts; a capturing lambda keeps the test short.
                attempts++;
                return null;
            });
        }

        await Assert.That(cache.Count).IsEqualTo(0);
        await Assert.That(attempts).IsEqualTo(1);
    }

    /// <summary>The same stream keeps full and reduced decoded images as separate entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResolutionVariantsDoNotShareAnImageOrFailure()
    {
        var cache = new ImageCache();
        var stream = Stream();
        var reducedKey = new ImageKey(stream, ReducedLevel);
        var fullKey = new ImageKey(stream, 0);
        var attempts = 0;
        _ = cache.Acquire(reducedKey, 0, (key, state) =>
        {
            attempts++;
            return null;
        });
        var full = cache.Acquire(fullKey, 0, (key, state) =>
        {
            attempts++;
            return Create();
        });
        var again = cache.Acquire(fullKey, 0, static (key, state) => Create());

        await Assert.That(full is not null && ReferenceEquals(full, again)).IsTrue();
        await Assert.That(attempts).IsEqualTo(Attempts);
        await Assert.That(cache.Count).IsEqualTo(1);
        again!.Release();
        full!.Release();
    }

    /// <summary>Creates an empty stream to key the cache with.</summary>
    /// <returns>The stream.</returns>
    private static PdfStream Stream() => new(new PdfDictionary(null), []);

    /// <summary>Creates a small opaque image entry.</summary>
    /// <returns>The entry.</returns>
    private static ImageEntry Create()
    {
        var pixels = new byte[ImageBytes];
        var native = SKImage.FromPixelCopy(new(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, Side * RenderedImage.BytesPerPixel);
        return new(new SkiaRenderImage(native), false, false);
    }

    /// <summary>Acquires and releases an image.</summary>
    /// <param name="cache">The cache.</param>
    /// <param name="stream">The stream.</param>
    private static void Use(ImageCache cache, PdfStream stream) =>
        cache.Acquire(stream, 0, static (key, state) => Create())!.Release();
}
