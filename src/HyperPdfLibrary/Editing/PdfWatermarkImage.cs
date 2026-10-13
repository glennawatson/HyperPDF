// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>An image drawn as a watermark.</summary>
/// <param name="Pixels">The straight-alpha BGRA pixels, rows top to bottom.</param>
/// <param name="PixelWidth">The width in pixels.</param>
/// <param name="PixelHeight">The height in pixels.</param>
/// <param name="WidthPoints">The width it is drawn at, in points.</param>
/// <param name="HasAlpha">Whether any pixel is not opaque.</param>
[DebuggerDisplay("PdfWatermarkImage: {PixelWidth}x{PixelHeight}")]
internal sealed record PdfWatermarkImage(byte[] Pixels, int PixelWidth, int PixelHeight, float WidthPoints, bool HasAlpha) : PdfWatermarkContent
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The alpha of an opaque pixel.</summary>
    private const byte Opaque = byte.MaxValue;

    /// <inheritdoc/>
    internal override bool HasTransparency => HasAlpha;

    /// <inheritdoc/>
    internal override bool UsesUnembeddedFont => false;

    /// <summary>Gets the resource name of the image in the watermark's form.</summary>
    private static ReadOnlySpan<byte> ImageName => "WmImage"u8;

    /// <summary>Copies and checks the arguments of an image watermark.</summary>
    /// <param name="bitmap">The image.</param>
    /// <returns>The watermark image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive or the width in points is not finite.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    internal static PdfWatermarkImage Create(in PdfWatermarkBitmap bitmap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitmap.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitmap.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitmap.WidthPoints);
        if (!float.IsFinite(bitmap.WidthPoints))
        {
            throw new ArgumentOutOfRangeException(nameof(bitmap), bitmap.WidthPoints, "The width must be finite.");
        }

        var length = checked(bitmap.Width * bitmap.Height * BytesPerPixel);
        if (bitmap.Pixels.Length < length)
        {
            throw new ArgumentException("The pixels do not fill the image.", nameof(bitmap));
        }

        var copy = bitmap.Pixels.Span[..length].ToArray();
        return new(copy, bitmap.Width, bitmap.Height, bitmap.WidthPoints, ContainsTransparency(copy));
    }

    /// <inheritdoc/>
    internal override PdfWatermarkForm Build(PdfDocument document, PdfEditTransaction transaction, PdfObjectId group)
    {
        var store = document.Objects;
        var imageId = PdfImages.AddBgraImage(store, Pixels, PixelWidth, PixelHeight);
        var height = WidthPoints * PixelHeight / PixelWidth;
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(WidthPoints, 0, 0, height, 0, 0);
            builder.DrawXObject(ImageName);
            builder.RestoreState();
            var images = new PdfDictionary(store);
            images.Set(store.Names.Intern(ImageName), PdfValue.FromReference(imageId));
            var resources = new PdfDictionary(store);
            resources.Set(KnownName.XObject, PdfValue.FromDictionary(images));
            var form = builder.ToFormXObject(store, new(0, 0, WidthPoints, height), resources);
            return new(PdfWatermarkForms.AddForm(transaction, form, group), WidthPoints, height);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Determines whether any pixel is not opaque.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <returns><see langword="true"/> when one is not.</returns>
    private static bool ContainsTransparency(ReadOnlySpan<byte> pixels)
    {
        for (var at = AlphaOffset; at < pixels.Length; at += BytesPerPixel)
        {
            if (pixels[at] != Opaque)
            {
                return true;
            }
        }

        return false;
    }
}
