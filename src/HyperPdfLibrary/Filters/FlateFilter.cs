// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Decodes and encodes FlateDecode (zlib) data through <see cref="ZLibCodec"/>, which picks the fastest API the runtime
/// has. Streams written without a zlib header are read as raw deflate. Damaged data keeps whatever decoded before the
/// damage, as other viewers do.
/// </summary>
internal static class FlateFilter
{
    /// <summary>The length of a zlib header.</summary>
    private const int ZLibHeaderLength = 2;

    /// <summary>The compression method nibble of a zlib header that means deflate.</summary>
    private const int DeflateMethod = 8;

    /// <summary>The mask of the compression method nibble.</summary>
    private const int MethodMask = 0x0F;

    /// <summary>The divisor that a valid zlib header is a multiple of.</summary>
    private const int HeaderCheck = 31;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Decodes zlib data, or raw deflate data when the zlib header is missing.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken = default) =>
        _ = TryDecode(input, ref output, cancellationToken);

    /// <summary>Decodes zlib data, or raw deflate data when the zlib header is missing, and says whether the data ended cleanly.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="true"/> when the data ended cleanly; <see langword="false"/> when it was truncated or damaged and the part before the fault was kept.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken = default)
    {
        if (input.IsEmpty)
        {
            return true;
        }

        var start = output.Length;
        if (HasZLibHeader(input))
        {
            // A missing or wrong Adler-32 checksum is harmless: the deflate data itself ended properly, which the raw decoder sees.
            return ZLibCodec.Decompress(input, raw: false, ref output, cancellationToken) || PreferLongerSkippingHeader(input, start, false, ref output, cancellationToken);
        }

        var clean = ZLibCodec.Decompress(input, raw: true, ref output, cancellationToken);
        if (input.Length > ZLibHeaderLength && (input[0] & MethodMask) == DeflateMethod)
        {
            clean = PreferLongerSkippingHeader(input, start, clean, ref output, cancellationToken);
        }

        return clean;
    }

    /// <summary>Encodes data as zlib.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the compressed bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Encode(ReadOnlySpan<byte> input, ref PooledBuffer output) => ZLibCodec.Compress(input, ref output);

    /// <summary>Encodes data as zlib at a level.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="level">The level.</param>
    /// <param name="output">The buffer receiving the compressed bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Encode(ReadOnlySpan<byte> input, PdfCompressionLevel level, ref PooledBuffer output) => ZLibCodec.Compress(input, level, ref output);

    /// <summary>
    /// A damaged zlib header (a bad check value) still leaves a deflate body after two bytes. Decodes from there and keeps
    /// whichever of the two attempts produced more.
    /// </summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="start">Where this decode began in the output.</param>
    /// <param name="firstClean">Whether the first attempt ended cleanly.</param>
    /// <param name="output">The output, holding the first attempt.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>Whether the attempt kept ended cleanly.</returns>
    private static bool PreferLongerSkippingHeader(ReadOnlySpan<byte> input, int start, bool firstClean, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        var second = default(PooledBuffer);
        try
        {
            var secondClean = ZLibCodec.Decompress(input[ZLibHeaderLength..], raw: true, ref second, cancellationToken);
            if (second.Length > output.Length - start || (secondClean && second.Length == output.Length - start))
            {
                output.Length = start;
                output.Write(second.WrittenSpan);
                return secondClean;
            }
        }
        catch (InvalidDataException)
        {
            // The second attempt hit the size cap; the first attempt stands.
        }
        finally
        {
            second.Dispose();
        }

        return firstClean;
    }

    /// <summary>Determines whether data starts with a zlib header.</summary>
    /// <param name="input">The data.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasZLibHeader(ReadOnlySpan<byte> input) =>
        input.Length >= ZLibHeaderLength && (input[0] & MethodMask) == DeflateMethod && ((input[0] << ByteBits) | input[1]) % HeaderCheck == 0;
}
