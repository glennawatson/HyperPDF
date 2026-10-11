// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Combines a decoded image with its soft mask or stencil mask, which may differ in size.</summary>
internal static class ImageMasks
{
    /// <summary>The index of the green byte in a BGRA pixel, which holds a gray soft mask's value.</summary>
    private const int GreenIndex = 1;

    /// <summary>
    /// Applies a mask to an opaque image. When the mask is larger the image is enlarged to the mask's size with bilinear
    /// interpolation; otherwise the mask is resampled to the image (pixel centres when growing, averages when shrinking).
    /// A /Matte colour is removed before the colours are premultiplied.
    /// </summary>
    /// <param name="image">The decoded, opaque image.</param>
    /// <param name="mask">The decoded mask: coverage, or gray BGRA for a soft mask.</param>
    /// <param name="matte">The matte colour, or <see langword="null"/>.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image with alpha; the same instance when no resize was needed.</returns>
    internal static PdfImageData Apply(PdfImageData image, PdfImageData mask, MatteColor? matte, ScratchPools scratchPools)
    {
        var width = Math.Max(image.Width, mask.Width);
        var height = Math.Max(image.Height, mask.Height);
        if ((long)width * height * PixelConverter.BytesPerPixel > ImageHeader.MaxDecodedBytes)
        {
            width = image.Width;
            height = image.Height;
        }

        var pixels = image.Pixels;
        var pinned = image.IsPinned;
        if (width != image.Width || height != image.Height)
        {
            pixels = PixelMemory.Allocate((long)width * height * PixelConverter.BytesPerPixel, out pinned);
            ImageAlpha.UpscaleBgra(image.Pixels, image.Width, image.Height, pixels, width, height);
        }

        var count = mask.Width * mask.Height;
        var pool = scratchPools.Get<byte>();
        var plane = pool.Rent(count);
        var alpha = pool.Rent(width * height);
        try
        {
            ExtractPlane(mask, plane.AsSpan(0, count));
            var alphaSpan = alpha.AsSpan(0, width * height);
            ImageAlpha.ResamplePlane(plane, mask.Width, mask.Height, alphaSpan, width, height);
            if (matte is { } color)
            {
                ImageAlpha.RemoveMatte(pixels, alphaSpan, color);
            }

            ImageAlpha.Multiply(pixels, alphaSpan);
        }
        finally
        {
            pool.Return(plane);
            pool.Return(alpha);
        }

        return ReferenceEquals(pixels, image.Pixels) ? image : new(width, height, pixels, false, image.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
    }

    /// <summary>Reads a soft mask's /Matte colour as RGB.</summary>
    /// <param name="softMask">The soft mask dictionary.</param>
    /// <param name="space">The image's colour space, or <see langword="null"/>.</param>
    /// <returns>The matte colour, or <see langword="null"/> when absent or not matching the colour space.</returns>
    internal static MatteColor? ReadMatte(PdfDictionary softMask, PdfColorSpace? space)
    {
        var array = ColorSpaceParser.FindByName(softMask, "Matte"u8).AsArray();
        if (array is null || space is null || space.Kind == PdfColorSpaceKind.Indexed || array.Count != space.Components)
        {
            return null;
        }

        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        if (array.ReadNumbers(components) != array.Count)
        {
            return null;
        }

        space.ToRgb(components, rgb);
        return new(PixelConverter.ToByte(rgb[0]), PixelConverter.ToByte(rgb[1]), PixelConverter.ToByte(rgb[PdfColorSpace.RgbComponents - 1]));
    }

    /// <summary>Gets one alpha byte per pixel from a decoded mask.</summary>
    /// <param name="mask">The decoded mask: coverage, gray bytes, or gray BGRA for a soft mask.</param>
    /// <param name="plane">Receives the alpha bytes.</param>
    private static void ExtractPlane(PdfImageData mask, Span<byte> plane)
    {
        if (mask.IsStencilMask || mask.IsGray)
        {
            mask.Pixels.AsSpan(0, plane.Length).CopyTo(plane);
            return;
        }

        for (var i = 0; i < plane.Length; i++)
        {
            plane[i] = mask.Pixels[(i * PixelConverter.BytesPerPixel) + GreenIndex];
        }
    }
}
