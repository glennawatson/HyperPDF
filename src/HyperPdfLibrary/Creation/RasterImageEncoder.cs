// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Turns decoded pixels into image streams: Flate with PNG predictors, a gray soft mask for alpha, and CCITT Group 4 for one-bit pixels.</summary>
internal static class RasterImageEncoder
{
    /// <summary>The bits of each sample of a colour image.</summary>
    private const int EightBits = 8;

    /// <summary>The bits of a bilevel sample.</summary>
    private const int OneBit = 1;

    /// <summary>The components of a gray image.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB image.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The offset of blue in an RGB pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The entries of an image dictionary.</summary>
    private const int ImageEntries = 10;

    /// <summary>The entries of decode parameters.</summary>
    private const int ParameterEntries = 4;

    /// <summary>The <c>/Predictor</c> value that lets the encoder choose a PNG filter for every row (ISO 32000-2 7.4.4.4).</summary>
    private const int OptimumPredictor = 15;

    /// <summary>The <c>/K</c> value of CCITT Group 4 coding.</summary>
    private const int Group4 = -1;

    /// <summary>Adds an image and, when it has transparency, its soft mask.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The pixels.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels between steps.</param>
    /// <returns>The image's object.</returns>
    internal static PdfObjectId Add(IPdfObjectSink sink, PdfRasterImage image, PdfImageOptions options, CancellationToken cancellationToken)
    {
        var pixels = image.Pixels.Span;
        return image.Format switch
        {
            PdfRasterFormat.Gray8 => AddFlate(sink, image, pixels, GrayComponents, options),
            PdfRasterFormat.Rgb24 => AddFlate(sink, image, pixels, RgbComponents, options),
            PdfRasterFormat.Bgra32 => AddBgra(sink, image, options, cancellationToken),
            _ => AddBilevel(sink, image, options),
        };
    }

    /// <summary>Creates an image dictionary without a filter.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bits">The bits per component.</param>
    /// <param name="colorSpace">The colour space value.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary CreateImageDictionary(IPdfObjectSink sink, int width, int height, int bits, PdfValue colorSpace)
    {
        var dictionary = new PdfDictionary(sink.Owner, ImageEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Image));
        dictionary.Set(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Set(KnownName.Height, PdfValue.FromInteger(height));
        if (!colorSpace.IsNull)
        {
            dictionary.Set(KnownName.ColorSpace, colorSpace);
        }

        dictionary.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        return dictionary;
    }

    /// <summary>Gets the colour space value for an image.</summary>
    /// <param name="components">The components the image has.</param>
    /// <param name="options">The options, which may name an ICC colour space.</param>
    /// <returns>A reference to the ICC colour space, or the device colour space's name.</returns>
    /// <exception cref="ArgumentException">The ICC colour space has a different component count.</exception>
    internal static PdfValue ColorSpaceFor(int components, PdfImageOptions options)
    {
        if (options.IccColorSpace.IsValid)
        {
            return options.IccColorSpace.Components == components
                ? PdfValue.FromReference(options.IccColorSpace.Id)
                : throw new ArgumentException("The ICC colour space has a different number of components than the image.", nameof(options));
        }

        return PdfValue.FromName(components switch
        {
            GrayComponents => KnownName.DeviceGray,
            RgbComponents => KnownName.DeviceRGB,
            _ => KnownName.DeviceCMYK,
        });
    }

    /// <summary>Sets <c>/Interpolate</c> when the options ask for it.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="options">The options.</param>
    internal static void ApplyInterpolation(PdfDictionary dictionary, PdfImageOptions options)
    {
        if (options.Interpolate)
        {
            dictionary.Set(KnownName.Interpolate, PdfValue.FromBoolean(true));
        }
    }

    /// <summary>Flate-compresses data and marks the dictionary's <c>/Filter</c>.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="data">The data.</param>
    /// <returns>The compressed bytes.</returns>
    internal static byte[] Deflate(PdfDictionary dictionary, ReadOnlySpan<byte> data)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return compressed.ToArray();
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Adds a gray or RGB image.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The image.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="components">The components a pixel has.</param>
    /// <param name="options">The options.</param>
    /// <returns>The image's object.</returns>
    private static PdfObjectId AddFlate(IPdfObjectSink sink, PdfRasterImage image, ReadOnlySpan<byte> samples, int components, PdfImageOptions options)
    {
        var stream = CreatePredictedImage(sink, image.Width, image.Height, samples, components, ColorSpaceFor(components, options));
        ApplyInterpolation(stream.Dictionary, options);
        return sink.Add(PdfValue.FromStream(stream));
    }

    /// <summary>Adds a BGRA image as RGB with a gray soft mask when any pixel is not opaque.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The image.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels between steps.</param>
    /// <returns>The image's object.</returns>
    private static PdfObjectId AddBgra(IPdfObjectSink sink, PdfRasterImage image, PdfImageOptions options, CancellationToken cancellationToken)
    {
        var count = checked(image.Width * image.Height);
        var rgb = ArrayPool<byte>.Shared.Rent(count * RgbComponents);
        var alpha = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            var opaque = Split(image.Pixels.Span[..(count * BgraBytes)], rgb, alpha);
            cancellationToken.ThrowIfCancellationRequested();
            var color = CreatePredictedImage(sink, image.Width, image.Height, rgb.AsSpan(0, count * RgbComponents), RgbComponents, ColorSpaceFor(RgbComponents, options));
            ApplyInterpolation(color.Dictionary, options);
            if (!opaque)
            {
                var mask = CreatePredictedImage(sink, image.Width, image.Height, alpha.AsSpan(0, count), GrayComponents, PdfValue.FromName(KnownName.DeviceGray));
                color.Dictionary.Set(KnownName.SMask, PdfValue.FromReference(sink.Add(PdfValue.FromStream(mask))));
            }

            return sink.Add(PdfValue.FromStream(color));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rgb);
            ArrayPool<byte>.Shared.Return(alpha);
        }
    }

    /// <summary>Adds a one-bit image coded as CCITT Group 4.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The image.</param>
    /// <param name="options">The options.</param>
    /// <returns>The image's object.</returns>
    private static PdfObjectId AddBilevel(IPdfObjectSink sink, PdfRasterImage image, PdfImageOptions options)
    {
        var dictionary = CreateImageDictionary(sink, image.Width, image.Height, OneBit, options.AsStencil ? default : PdfValue.FromName(KnownName.DeviceGray));
        if (options.AsStencil)
        {
            dictionary.Set(KnownName.ImageMask, PdfValue.FromBoolean(true));
        }

        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.CCITTFaxDecode));
        var parameters = new PdfDictionary(sink.Owner, ParameterEntries);
        parameters.Set(KnownName.K, PdfValue.FromInteger(Group4));
        parameters.Set(KnownName.Columns, PdfValue.FromInteger(image.Width));
        parameters.Set(KnownName.Rows, PdfValue.FromInteger(image.Height));
        dictionary.Set(KnownName.DecodeParms, PdfValue.FromDictionary(parameters));
        ApplyInterpolation(dictionary, options);
        var encoded = default(PooledBuffer);
        try
        {
            // Without /BlackIs1 a clear bit is black, which matches the set-bit-is-white layout of DeviceGray.
            CcittG4Encoder.Encode(image.Pixels.Span, image.Width, image.Height, false, ref encoded, sink.Owner?.ScratchPools ?? new ScratchPools());
            return sink.Add(PdfValue.FromStream(new(dictionary, encoded.ToArray())));
        }
        finally
        {
            encoded.Dispose();
        }
    }

    /// <summary>Creates a Flate image whose rows use PNG predictors.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="samples">The 8-bit samples.</param>
    /// <param name="components">The components a pixel has.</param>
    /// <param name="colorSpace">The colour space value.</param>
    /// <returns>The stream.</returns>
    private static PdfStream CreatePredictedImage(IPdfObjectSink sink, int width, int height, ReadOnlySpan<byte> samples, int components, PdfValue colorSpace)
    {
        var dictionary = CreateImageDictionary(sink, width, height, EightBits, colorSpace);
        var rowBytes = width * components;
        var predicted = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            PngPredictorEncoder.Encode(samples[..(rowBytes * height)], rowBytes, PngPredictorEncoder.GetPixelBytes(components, EightBits), ref predicted);
            FlateFilter.Encode(predicted.WrittenSpan, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            var parameters = new PdfDictionary(sink.Owner, ParameterEntries);
            parameters.Set(KnownName.Predictor, PdfValue.FromInteger(OptimumPredictor));
            parameters.Set(KnownName.Colors, PdfValue.FromInteger(components));
            parameters.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(EightBits));
            parameters.Set(KnownName.Columns, PdfValue.FromInteger(width));
            dictionary.Set(KnownName.DecodeParms, PdfValue.FromDictionary(parameters));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            predicted.Dispose();
            compressed.Dispose();
        }
    }

    /// <summary>Splits BGRA pixels into RGB and alpha planes.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="rgb">Receives the colour.</param>
    /// <param name="alpha">Receives the alpha.</param>
    /// <returns><see langword="true"/> when every pixel is opaque.</returns>
    private static bool Split(ReadOnlySpan<byte> pixels, Span<byte> rgb, Span<byte> alpha)
    {
        var opaque = true;
        var count = pixels.Length / BgraBytes;
        for (var i = 0; i < count; i++)
        {
            var pixel = pixels.Slice(i * BgraBytes, BgraBytes);
            var target = rgb.Slice(i * RgbComponents, RgbComponents);
            target[0] = pixel[RedOffset];
            target[1] = pixel[1];
            target[BlueOffset] = pixel[0];
            alpha[i] = pixel[AlphaOffset];
            opaque &= pixel[AlphaOffset] == byte.MaxValue;
        }

        return opaque;
    }
}
