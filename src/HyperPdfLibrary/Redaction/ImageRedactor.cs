// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Blanks the pixels of an image that lie under redacted areas, keeping the rest of the image and its masks valid.</summary>
internal static class ImageRedactor
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the green byte in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The highest 8-bit value.</summary>
    private const int Opaque = 255;

    /// <summary>The samples of an RGB pixel.</summary>
    private const int RgbChannels = 3;

    /// <summary>The offset of the blue sample in an RGB pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>Blanks the pixels under the areas, replacing the image's data when any pixel changed.</summary>
    /// <param name="image">The image object; it receives the replacement.</param>
    /// <param name="regions">The areas in user space.</param>
    /// <param name="resources">The resources a named colour space is looked up in, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Checked once per pixel row.</param>
    /// <returns>What happened to the image.</returns>
    internal static BlankResult Blank(PdfImageObject image, ReadOnlySpan<PdfRectangle> regions, PdfDictionary? resources, CancellationToken cancellationToken)
    {
        var data = image.DecodeCompactPixels(resources);
        if (data is null || data.UnsupportedCodec != PdfImageCodec.None || data.Width < 1 || data.Height < 1 || !Matrix3x2.Invert(image.Matrix, out var inverse))
        {
            // An image that cannot be read pixel by pixel cannot be blanked, so it goes whole.
            return BlankResult.Removed;
        }

        var plan = new PixelPlan(data.Width, data.Height, image.Matrix, inverse, cancellationToken);
        if (data.IsStencilMask)
        {
            return BlankStencil(image, data, regions, plan);
        }

        return data.IsGray ? BlankGray(image, data, regions, plan) : BlankColor(image, data, regions, plan);
    }

    /// <summary>Blanks a stencil mask: covered pixels stop painting.</summary>
    /// <param name="image">The image object.</param>
    /// <param name="data">The decoded mask, which this call owns and changes in place.</param>
    /// <param name="regions">The areas.</param>
    /// <param name="plan">Where pixels fall on the page.</param>
    /// <returns>The result.</returns>
    private static BlankResult BlankStencil(PdfImageObject image, PdfImageData data, ReadOnlySpan<PdfRectangle> regions, in PixelPlan plan)
    {
        var blanked = 0;
        foreach (var region in regions)
        {
            blanked += plan.Blank(region, data.Pixels, 1, []);
        }

        if (blanked == 0)
        {
            return BlankResult.Unchanged;
        }

        image.SetStencilCoverage(data.Width, data.Height, data.Pixels);
        return BlankResult.Blanked;
    }

    /// <summary>Blanks an opaque grey image, which decodes to one byte per pixel. Pixels become black.</summary>
    /// <param name="image">The image object.</param>
    /// <param name="data">The decoded image, which this call owns and changes in place.</param>
    /// <param name="regions">The areas.</param>
    /// <param name="plan">Where pixels fall on the page.</param>
    /// <returns>The result.</returns>
    private static BlankResult BlankGray(PdfImageObject image, PdfImageData data, ReadOnlySpan<PdfRectangle> regions, in PixelPlan plan)
    {
        var blanked = 0;
        foreach (var region in regions)
        {
            blanked += plan.Blank(region, data.Pixels, 1, []);
        }

        if (blanked == 0)
        {
            return BlankResult.Unchanged;
        }

        image.SetPixels(data.Width, data.Height, PdfImagePixelFormat.Gray8, data.Pixels);
        return BlankResult.Blanked;
    }

    /// <summary>Blanks a colour or grey image. Pixels become black, and transparent where the image has transparency.</summary>
    /// <param name="image">The image object.</param>
    /// <param name="data">The decoded image.</param>
    /// <param name="regions">The areas.</param>
    /// <param name="plan">Where pixels fall on the page.</param>
    /// <returns>The result.</returns>
    private static BlankResult BlankColor(PdfImageObject image, PdfImageData data, ReadOnlySpan<PdfRectangle> regions, in PixelPlan plan)
    {
        var traits = Inspect(data.Pixels);
        var channels = traits.Gray ? 1 : RgbChannels;
        var count = data.Width * data.Height;
        var pool = ArrayPool<byte>.Shared;
        var samples = pool.Rent(count * channels);
        var alpha = traits.Alpha ? pool.Rent(count) : null;
        try
        {
            var alphaSpan = alpha is null ? [] : alpha.AsSpan(0, count);
            Unpack(data.Pixels, samples, alphaSpan, channels);
            var blanked = 0;
            foreach (var region in regions)
            {
                blanked += plan.Blank(region, samples, channels, alphaSpan);
            }

            if (blanked == 0)
            {
                return BlankResult.Unchanged;
            }

            image.SetPixels(data.Width, data.Height, traits.Gray ? PdfImagePixelFormat.Gray8 : PdfImagePixelFormat.Rgb24, samples.AsSpan(0, count * channels), alphaSpan);
            return BlankResult.Blanked;
        }
        finally
        {
            pool.Return(samples);
            if (alpha is not null)
            {
                pool.Return(alpha);
            }
        }
    }

    /// <summary>Finds out whether every pixel is grey and whether any pixel is not opaque.</summary>
    /// <param name="bgra">The premultiplied BGRA pixels.</param>
    /// <returns>The traits.</returns>
    private static ImageTraits Inspect(byte[] bgra)
    {
        var gray = true;
        var alpha = false;
        for (var i = 0; i + BgraBytes <= bgra.Length; i += BgraBytes)
        {
            gray &= bgra[i] == bgra[i + GreenOffset] && bgra[i] == bgra[i + RedOffset];
            alpha |= bgra[i + AlphaOffset] != Opaque;
        }

        return new(gray, alpha);
    }

    /// <summary>Splits premultiplied BGRA pixels into straight colour samples and an alpha plane.</summary>
    /// <param name="bgra">The pixels.</param>
    /// <param name="samples">Receives grey or RGB samples.</param>
    /// <param name="alpha">Receives alpha when it has room.</param>
    /// <param name="channels">1 for grey, 3 for RGB.</param>
    private static void Unpack(byte[] bgra, byte[] samples, Span<byte> alpha, int channels)
    {
        var pixel = 0;
        for (var i = 0; i + BgraBytes <= bgra.Length; i += BgraBytes)
        {
            var a = bgra[i + AlphaOffset];
            samples[pixel * channels] = Straighten(bgra[i + RedOffset], a);
            if (channels == RgbChannels)
            {
                samples[(pixel * channels) + 1] = Straighten(bgra[i + GreenOffset], a);
                samples[(pixel * channels) + BlueOffset] = Straighten(bgra[i], a);
            }

            if (!alpha.IsEmpty)
            {
                alpha[pixel] = a;
            }

            pixel++;
        }
    }

    /// <summary>Undoes premultiplication of one channel.</summary>
    /// <param name="value">The premultiplied value.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The straight value.</returns>
    private static byte Straighten(byte value, byte alpha) => alpha is Opaque or 0 ? value : (byte)Math.Min(Opaque, value * Opaque / alpha);

    /// <summary>What an image's pixels look like.</summary>
    /// <param name="Gray">Whether every pixel is grey.</param>
    /// <param name="Alpha">Whether any pixel is not opaque.</param>
    private readonly record struct ImageTraits(bool Gray, bool Alpha);

    /// <summary>Where an image's pixels fall on the page, for finding the pixels under an area.</summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    /// <param name="Matrix">The matrix from the unit square to user space.</param>
    /// <param name="Inverse">The inverse of <paramref name="Matrix"/>.</param>
    /// <param name="Token">Checked once per pixel row.</param>
    private readonly record struct PixelPlan(int Width, int Height, Matrix3x2 Matrix, Matrix3x2 Inverse, CancellationToken Token)
    {
        /// <summary>Half a pixel, the distance from a pixel's corner to its centre.</summary>
        private const float Half = 0.5F;

        /// <summary>Blanks every pixel whose centre lies in an area: its samples become zero and its alpha, when there is one, becomes transparent.</summary>
        /// <param name="region">The area in user space.</param>
        /// <param name="samples">The samples of all pixels.</param>
        /// <param name="channels">The samples per pixel.</param>
        /// <param name="alpha">The alpha plane, or an empty array.</param>
        /// <returns>The number of pixels blanked.</returns>
        internal int Blank(in PdfRectangle region, Span<byte> samples, int channels, Span<byte> alpha)
        {
            var blanked = 0;
            GetPixelRange(region, out var firstX, out var lastX, out var firstY, out var lastY);
            for (var y = firstY; y <= lastY; y++)
            {
                Token.ThrowIfCancellationRequested();
                for (var x = firstX; x <= lastX; x++)
                {
                    if (!CentreIsIn(region, x, y))
                    {
                        continue;
                    }

                    var pixel = (y * Width) + x;
                    samples.Slice(pixel * channels, channels).Clear();
                    if (!alpha.IsEmpty)
                    {
                        alpha[pixel] = 0;
                    }

                    blanked++;
                }
            }

            return blanked;
        }

        /// <summary>Gets the pixels whose boxes the area could cover.</summary>
        /// <param name="region">The area.</param>
        /// <param name="firstX">Receives the first column.</param>
        /// <param name="lastX">Receives the last column.</param>
        /// <param name="firstY">Receives the first row.</param>
        /// <param name="lastY">Receives the last row.</param>
        private void GetPixelRange(in PdfRectangle region, out int firstX, out int lastX, out int firstY, out int lastY)
        {
            var low = new Vector2(float.MaxValue, float.MaxValue);
            var high = new Vector2(float.MinValue, float.MinValue);
            Span<Vector2> corners = [new(region.Left, region.Bottom), new(region.Right, region.Bottom), new(region.Right, region.Top), new(region.Left, region.Top)];
            foreach (var corner in corners)
            {
                var unit = Vector2.Transform(corner, Inverse);
                low = Vector2.Min(low, unit);
                high = Vector2.Max(high, unit);
            }

            firstX = Math.Clamp((int)MathF.Floor(low.X * Width), 0, Width - 1);
            lastX = Math.Clamp((int)MathF.Ceiling(high.X * Width), 0, Width - 1);
            firstY = Math.Clamp((int)MathF.Floor((1 - high.Y) * Height), 0, Height - 1);
            lastY = Math.Clamp((int)MathF.Ceiling((1 - low.Y) * Height), 0, Height - 1);
        }

        /// <summary>Determines whether the centre of a pixel lies in an area.</summary>
        /// <param name="region">The area in user space.</param>
        /// <param name="x">The column.</param>
        /// <param name="y">The row, counted from the top.</param>
        /// <returns><see langword="true"/> when the centre is inside.</returns>
        private bool CentreIsIn(in PdfRectangle region, int x, int y)
        {
            var point = Vector2.Transform(new((x + Half) / Width, 1 - ((y + Half) / Height)), Matrix);
            return point.X >= region.Left && point.X <= region.Right && point.Y >= region.Bottom && point.Y <= region.Top;
        }
    }
}
