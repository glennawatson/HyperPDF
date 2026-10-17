// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Compresses an image as JPEG 2000 (ISO/IEC 15444-1) for the <c>JPXDecode</c> filter (ISO 32000-2 section 7.4.9). The file
/// is a JP2 file with an enumerated or ICC colour specification and, for an image with opacity, a channel definition box.
/// A lossless file uses the reversible 5/3 wavelet and reversible colour transform; a lossy file the 9/7 wavelet with scalar
/// quantization steered by <see cref="PdfJpxOptions.Quality"/>. Tiles, decomposition levels, quality layers, code-block and
/// precinct sizes, packet order and the code-block mode switches are options. Every file decodes with any JPEG 2000 decoder.
/// The high-throughput coder (Part 15), regions of interest and progression order changes are not written.
/// </summary>
public static class PdfJpxEncoder
{
    /// <summary>The divisor that makes the initial capacity.</summary>
    private const int SizeDivisor = 5;

    /// <summary>The largest initial capacity.</summary>
    private const int MaxInitialCapacity = 1 << 26;

    /// <summary>Encodes an image losslessly.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The JP2 file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or depth is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit it.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encode(PdfJpxImage image) => Encode(image, PdfJpxOptions.Lossless, CancellationToken.None);

    /// <summary>Encodes an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">How to compress it.</param>
    /// <param name="cancellationToken">Cancels between code-blocks and tiles.</param>
    /// <returns>The JP2 file, or a bare codestream when <see cref="PdfJpxOptions.WriteFileFormat"/> is off.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, depth or option is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit it.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static byte[] Encode(PdfJpxImage image, PdfJpxOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var pools = new ScratchPools();
        var output = new PooledBuffer(InitialCapacity(image));
        try
        {
            JpxEncoder.Encode(image, image.Samples.Span, options, ref output, pools, cancellationToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
            pools.Close();
        }
    }

    /// <summary>Encodes an image. The work runs on the calling thread, in steps that check the token.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">How to compress it.</param>
    /// <param name="cancellationToken">Cancels between code-blocks and tiles.</param>
    /// <returns>The JP2 file, or a bare codestream when <see cref="PdfJpxOptions.WriteFileFormat"/> is off.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, depth or option is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit it.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<byte[]> EncodeAsync(PdfJpxImage image, PdfJpxOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Encode(image, options, cancellationToken));
    }

    /// <summary>Encodes an image and writes the file to a stream.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">How to compress it.</param>
    /// <param name="destination">The stream the file is written to.</param>
    /// <param name="cancellationToken">Cancels encoding and writing.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, depth or option is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit it.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask EncodeAsync(PdfJpxImage image, PdfJpxOptions options, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var file = await EncodeAsync(image, options, cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Guesses the size of the file, so the buffer rarely grows: a fifth of the raw samples.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The initial capacity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InitialCapacity(PdfJpxImage image) => Math.Min(image.Samples.Length / SizeDivisor, MaxInitialCapacity);
}
