// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Checks that a warmed JPEG decode reuses its sample planes, including chroma planes of about 250 KiB.</summary>
[NotInParallel]
public sealed class JpegAllocationTests
{
    /// <summary>The image side in pixels; 4:2:0 chroma planes of this size are about 250 KiB.</summary>
    private const int Side = 1000;

    /// <summary>The JPEG quality.</summary>
    private const int Quality = 90;

    /// <summary>The bytes of an RGB output pixel.</summary>
    private const int RgbComponents = 3;

    /// <summary>The decodes made before measuring.</summary>
    private const int Warmup = 3;

    /// <summary>The measured decodes.</summary>
    private const int Runs = 40;

    /// <summary>The most managed bytes a warmed decode may allocate; the planes alone are over 1 MB.</summary>
    private const long MaxBytes = 16 * 1024;

    /// <summary>The multiplier that scrambles pixel positions into texture.</summary>
    private const uint NoiseMultiplier = 2_654_435_761;

    /// <summary>The bits of noise added to each channel.</summary>
    private const int NoiseMask = 0x0F;

    /// <summary>The shift that takes the high bits of the scrambled position.</summary>
    private const int NoiseShift = 24;

    /// <summary>A warmed decode of a 1000 by 1000 colour image allocates only bookkeeping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmDecodeAllocatesOnlyBookkeeping()
    {
        var jpeg = Encode();
        var scratch = new ScratchPools();
        var samples = new byte[Side * Side * RgbComponents];
        for (var i = 0; i < Warmup; i++)
        {
            _ = JpegDecoder.TryDecode(jpeg, true, samples, scratch, CancellationToken.None);
        }

        // Small buffers still use ArrayPool, whose Gen2 trimming can cause a rent miss; the smallest run distinguishes
        // that miss from allocation repeated on every decode.
        var smallest = long.MaxValue;
        for (var i = 0; i < Runs && smallest >= MaxBytes; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            _ = JpegDecoder.TryDecode(jpeg, true, samples, scratch, CancellationToken.None);
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        await Assert.That(smallest).IsLessThan(MaxBytes);
    }

    /// <summary>Encodes a textured colour gradient.</summary>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] Encode()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var noise = (int)(((uint)((y * Side) + x) * NoiseMultiplier) >> NoiseShift) & NoiseMask;
                bitmap.SetPixel(x, y, new((byte)((x * byte.MaxValue / Side) + noise), (byte)((y * byte.MaxValue / Side) + noise), (byte)(((x + y) * byte.MaxValue / (Side + Side)) + noise)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
        return encoded.ToArray();
    }
}
