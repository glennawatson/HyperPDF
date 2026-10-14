// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Encodes pixels as a JPEG that <c>/DCTDecode</c> can carry (ISO 32000-2, 7.4.8): 8 bits a sample, one gray, three YCbCr
/// or four CMYK or YCCK components, baseline sequential or progressive Huffman coding. The encoder is managed code and
/// needs no image codec from the host.
/// </summary>
public static class PdfJpegEncoder
{
    /// <summary>The pixels per byte of the capacity estimate.</summary>
    private const int EstimateDivisor = 8;

    /// <summary>The smallest starting capacity.</summary>
    private const int MinCapacity = 4096;

    /// <summary>The largest starting capacity.</summary>
    private const int MaxCapacity = 1 << 24;

    /// <summary>The working arrays that calls without a document share, so repeated calls reuse them.</summary>
    private static readonly ScratchPools SharedPools = new();

    /// <summary>Encodes pixels as a JPEG.</summary>
    /// <param name="pixels">The pixels, rows packed with no padding.</param>
    /// <param name="format">The layout of the pixels.</param>
    /// <param name="width">The width in pixels, from 1 to 65535.</param>
    /// <param name="height">The height in pixels, from 1 to 65535.</param>
    /// <param name="options">The coding options.</param>
    /// <param name="cancellationToken">Cancels the encode between MCU rows and scans.</param>
    /// <returns>The JPEG file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not from 1 to 65535, or the format is not known.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encode(ReadOnlySpan<byte> pixels, PdfJpegPixelFormat format, int width, int height, PdfJpegOptions options, CancellationToken cancellationToken) =>
        Encode(pixels, format, width, height, options, SharedPools, cancellationToken);

    /// <summary>Encodes a raster image as a JPEG. A BGRA image is composited over white, because JPEG has no alpha.</summary>
    /// <param name="image">The pixels: gray, RGB or BGRA.</param>
    /// <param name="options">The coding options.</param>
    /// <param name="cancellationToken">Cancels the encode between MCU rows and scans.</param>
    /// <returns>The JPEG file's bytes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not from 1 to 65535.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image, or the image is bilevel.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static byte[] Encode(PdfRasterImage image, PdfJpegOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.Validate();
        return Encode(image.Pixels.Span, FormatOf(image.Format), image.Width, image.Height, options, SharedPools, cancellationToken);
    }

    /// <summary>Encodes pixels as a JPEG.</summary>
    /// <param name="pixels">The pixels, rows packed with no padding.</param>
    /// <param name="format">The layout of the pixels.</param>
    /// <param name="width">The width in pixels, from 1 to 65535.</param>
    /// <param name="height">The height in pixels, from 1 to 65535.</param>
    /// <param name="options">The coding options.</param>
    /// <param name="cancellationToken">Cancels the encode between MCU rows and scans.</param>
    /// <returns>The JPEG file's bytes. The encode runs inline; the result is ready when the call returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not from 1 to 65535, or the format is not known.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<byte[]> EncodeAsync(ReadOnlyMemory<byte> pixels, PdfJpegPixelFormat format, int width, int height, PdfJpegOptions options, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Encode(pixels.Span, format, width, height, options, SharedPools, cancellationToken));

    /// <summary>Encodes a raster image as a JPEG and writes it to a stream.</summary>
    /// <param name="image">The pixels: gray, RGB or BGRA.</param>
    /// <param name="options">The coding options.</param>
    /// <param name="destination">The stream that receives the JPEG file.</param>
    /// <param name="cancellationToken">Cancels the encode between MCU rows and scans, and the write.</param>
    /// <returns>A task that completes when the stream has the bytes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not from 1 to 65535.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image, or the image is bilevel.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask EncodeAsync(PdfRasterImage image, PdfJpegOptions options, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var jpeg = Encode(image, options, cancellationToken);
        await destination.WriteAsync(jpeg, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Encodes pixels with working arrays from a given cache.</summary>
    /// <param name="pixels">The pixels, rows packed with no padding.</param>
    /// <param name="format">The layout of the pixels.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The coding options.</param>
    /// <param name="scratchPools">The bounded cache for the working arrays.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The JPEG file's bytes.</returns>
    internal static byte[] Encode(ReadOnlySpan<byte> pixels, PdfJpegPixelFormat format, int width, int height, PdfJpegOptions options, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var output = new PooledBuffer(EstimateBytes(width, height));
        try
        {
            JpegEncoder.Encode(pixels, new(width, height, format, options), ref output, scratchPools, cancellationToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Maps a raster image format to a JPEG source format.</summary>
    /// <param name="format">The raster format.</param>
    /// <returns>The JPEG pixel format.</returns>
    /// <exception cref="ArgumentException">The image is bilevel or has no format.</exception>
    private static PdfJpegPixelFormat FormatOf(PdfRasterFormat format) => format switch
    {
        PdfRasterFormat.Gray8 => PdfJpegPixelFormat.Gray8,
        PdfRasterFormat.Rgb24 => PdfJpegPixelFormat.Rgb24,
        PdfRasterFormat.Bgra32 => PdfJpegPixelFormat.Bgra32,
        _ => throw new ArgumentException("Only gray, RGB and BGRA images can be encoded as JPEG.", nameof(format)),
    };

    /// <summary>Estimates the size of the output, so the buffer rarely grows.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A starting capacity: an eighth of a byte a pixel, clamped to a sensible range.</returns>
    private static int EstimateBytes(int width, int height) =>
        (int)Math.Clamp((long)width * height / EstimateDivisor, MinCapacity, MaxCapacity);
}
