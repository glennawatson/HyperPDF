// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Decodes JBIG2 data as the PDF JBIG2Decode filter defines it (ITU T.88 embedded stream, PDF 32000 section 7.4.7):
/// the segments of one page, optionally preceded by the global segments of a /JBIG2Globals stream. Generic,
/// refinement, text and halftone regions, symbol and pattern dictionaries and custom Huffman tables are supported.
/// </summary>
/// <remarks>
/// Output rows hold <c>(width + 7) / 8</c> bytes, most significant bit first, with 1 for white and 0 for black: the
/// inverse of JBIG2's own convention, as PDF expects. Damaged data never throws; the pixels decoded before the damage
/// are kept, and the rest of the page keeps its default colour.
/// </remarks>
public static class Jbig2Decoder
{
    /// <summary>The bytes of a JBIG2 file header without the page count.</summary>
    private const int ShortHeader = 9;

    /// <summary>The bytes of a JBIG2 file header with the page count.</summary>
    private const int LongHeader = 13;

    /// <summary>The file header flag that says the page count is unknown, so the field is absent.</summary>
    private const int UnknownPagesFlag = 0x02;

    /// <summary>Gets the signature of a JBIG2 file (T.88 annex D.4), which PDF streams should not have.</summary>
    private static ReadOnlySpan<byte> FileSignature => [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Gets the bytes in one output row.</summary>
    /// <param name="width">The image width in pixels.</param>
    /// <returns>The row length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRowBytes(int width) => Jbig2Bits.Stride(width);

    /// <summary>Decodes a JBIG2 page with no global segments.</summary>
    /// <param name="data">The page's segments: the data after the stream's other filters.</param>
    /// <param name="width">The image width from the PDF image dictionary.</param>
    /// <param name="height">The image height from the PDF image dictionary.</param>
    /// <param name="destination">Receives the rows; at least <see cref="GetRowBytes"/> times <paramref name="height"/> bytes.</param>
    /// <returns><see langword="true"/> when a page was decoded, even partly; <see langword="false"/> when nothing could be.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecode(ReadOnlySpan<byte> data, int width, int height, Span<byte> destination) =>
        TryDecode(data, default, width, height, destination, CancellationToken.None);

    /// <summary>Decodes a JBIG2 page while observing the caller's token.</summary>
    /// <param name="data">The page's segments.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="destination">Receives packed pixels.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>Whether a page decoded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecode(ReadOnlySpan<byte> data, int width, int height, Span<byte> destination, CancellationToken cancellationToken) =>
        TryDecode(data, default, width, height, destination, cancellationToken);

    /// <summary>Decodes a JBIG2 page.</summary>
    /// <param name="data">The page's segments: the data after the stream's other filters.</param>
    /// <param name="globals">The decoded /JBIG2Globals stream, or an empty span.</param>
    /// <param name="width">The image width from the PDF image dictionary.</param>
    /// <param name="height">The image height from the PDF image dictionary.</param>
    /// <param name="destination">Receives the rows; at least <see cref="GetRowBytes"/> times <paramref name="height"/> bytes.</param>
    /// <returns>
    /// <see langword="true"/> when a page was decoded, even partly, with the rest of the page white or in its default
    /// colour; <see langword="false"/> when the data holds no page, or when the image is larger than the decoder allows,
    /// in which case the destination is not written.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> globals, int width, int height, Span<byte> destination) =>
        TryDecode(data, globals, width, height, destination, CancellationToken.None);

    /// <summary>Decodes a page and its global segments while observing the caller's token.</summary>
    /// <param name="data">The page segments.</param>
    /// <param name="globals">The global segments.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="destination">Receives packed pixels.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>Whether a page decoded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is not positive.</exception>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> globals, int width, int height, Span<byte> destination, CancellationToken cancellationToken) =>
        TryDecode(data, globals, width, height, destination, new(), cancellationToken);

    /// <summary>Decodes a page with an explicitly owned reusable scratch cache.</summary>
    /// <param name="data">The page segments.</param>
    /// <param name="globals">The global segments.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="destination">Receives packed pixels.</param>
    /// <param name="scratchPools">The caller's bounded scratch cache.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>Whether a page decoded.</returns>
    /// <exception cref="ArgumentNullException">The scratch cache is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The dimensions are not positive.</exception>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> globals, int width, int height, Span<byte> destination, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scratchPools);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if ((long)width * height > ImageHeader.MaxPixels)
        {
            return false;
        }

        if (destination.Length < GetRowBytes(width) * height)
        {
            throw new ArgumentException("The destination is smaller than the image.", nameof(destination));
        }

        using var page = Jbig2Bitmap.Create(width, height, scratchPools);
        if (page is null)
        {
            return false;
        }

        var decoded = Decode(StripFileHeader(data), StripFileHeader(globals), page, scratchPools, cancellationToken);
        Jbig2Composer.CopyInverted(page.Data, destination);
        return decoded;
    }

    /// <summary>Processes the global segments, then the page's.</summary>
    /// <param name="data">The page's segments.</param>
    /// <param name="globals">The global segments, or an empty span.</param>
    /// <param name="page">The white page bitmap.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="true"/> when the page decoded, even partly.</returns>
    private static bool Decode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> globals, Jbig2Bitmap page, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        using var workspace = new Jbig2Workspace(scratchPools);
        using var global = globals.IsEmpty ? null : new Jbig2DecodeState(null, workspace, null, cancellationToken);
        if (global is not null)
        {
            // Damaged globals keep the segments that decoded; a page that needs a lost one fails on its own.
            _ = Jbig2Segments.DecodeSequential(global, globals);
        }

        using var context = new Jbig2DecodeState(page, workspace, global, cancellationToken);
        return Jbig2Segments.DecodeSequential(context, data) == Jbig2Status.Success || context.PageSeen;
    }

    /// <summary>Drops a JBIG2 file header, which PDF forbids (ISO 32000-1 section 7.4.7) but some writers leave in; reading leniently, we skip it.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The data after any file header.</returns>
    private static ReadOnlySpan<byte> StripFileHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < ShortHeader || !data.StartsWith(FileSignature))
        {
            return data;
        }

        var header = (data[FileSignature.Length] & UnknownPagesFlag) == 0 ? LongHeader : ShortHeader;
        return data.Length > header ? data[header..] : default;
    }
}
