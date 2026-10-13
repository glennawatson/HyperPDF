// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationImages annotation operations.</summary>
internal static class HyperPdfAnnotationImages
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    internal const int BgraBytes = 4;

    /// <summary>Gets ImageResource.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetImageResource() => "Im0"u8;

    /// <summary>Places a picture as a stamp, keeping its transparency.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="bounds">The picture's bounds in page space.</param>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels, in rows from top to bottom.</param>
    /// <param name="width">The picture width in pixels.</param>
    /// <param name="height">The picture height in pixels.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddImageStamp(HyperPdfAnnotations annotationState, int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < (long)width * height * BgraBytes)
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is { } page ? AddImage(
                annotationState,
                pageIndex,
                page,
                bounds,
                pixels,
                new(width, height),
                HyperPdfAnnotationKinds.GetStampSubject()) : -1;
        }
    }

    /// <summary>Copies an image into a removable signature annotation.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="bounds">The image bounds in page points, with a top-left origin.</param>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels, in rows from top to bottom.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The annotation index, or -1 when placement fails.</returns>
    /// <exception cref="ArgumentException">The pixel buffer length does not match the image dimensions.</exception>
    internal static int AddImageSignature(HyperPdfAnnotations annotationState, int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Height);
        if (pixels.Length != checked(width * height * BgraBytes))
        {
            throw new ArgumentException("The image dimensions must match its BGRA pixels.", nameof(pixels));
        }

        lock (annotationState.Gate)
        {
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is { } page ? AddImage(
                annotationState,
                pageIndex,
                page,
                bounds,
                pixels,
                new(width, height),
                HyperPdfAnnotationKinds.GetSignatureSubject()) : -1;
        }
    }

    /// <summary>Adds the image and a stamp whose appearance maps the image's unit square onto the bounds, page rotation included.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The bounds in page space.</param>
    /// <param name="pixels">The straight-alpha BGRA pixels.</param>
    /// <param name="size">The image size in pixels.</param>
    /// <param name="subject">What the stamp is: a signature or a picture stamp.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddImage(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, PageRect bounds, ReadOnlySpan<byte> pixels, PixelSize size, ReadOnlySpan<byte> subject)
    {
        var image = PdfImages.AddBgraImage(annotationState.Store, pixels[..(size.Width * size.Height * BgraBytes)], size.Width, size.Height);
        var bottomLeft = HyperPdfAnnotationReading.ToUser(page, new(bounds.Left, bounds.Bottom));
        var bottomRight = HyperPdfAnnotationReading.ToUser(page, new(bounds.Right, bounds.Bottom));
        var topLeft = HyperPdfAnnotationReading.ToUser(page, new(bounds.Left, bounds.Top));
        var rectangle = HyperPdfAnnotationReading.ToUserRectangle(page, bounds);
        var stamp = PdfAnnotations.Create(annotationState.Store, KnownName.Stamp, rectangle);
        var images = new PdfDictionary(annotationState.Store, 1);
        images.Set(annotationState.Store.Names.Intern(GetImageResource()), PdfValue.FromReference(image));
        var resources = new PdfDictionary(annotationState.Store, 1);
        resources.Set(KnownName.XObject, PdfValue.FromDictionary(images));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            var across = bottomRight - bottomLeft;
            var up = topLeft - bottomLeft;
            builder.Transform(across.X, across.Y, up.X, up.Y, bottomLeft.X, bottomLeft.Y);
            builder.DrawXObject(GetImageResource());
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, stamp, builder.ToFormXObject(annotationState.Store, rectangle, resources));
        }
        finally
        {
            builder.Dispose();
        }

        // A picture keeps its own colours, so it records none; readers show the kind's default colour.
        return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, stamp, null, string.Empty, subject);
    }
}
