// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Encodes a 1-bit image as JBIG2 data for a PDF /JBIG2Decode stream (T.88; ISO 32000-2 section 7.4.7): the page's segments
/// with no file header, and optionally the global segments for /JBIG2Globals. Generic regions are coded with arithmetic coding
/// (templates 0 to 3, typical prediction) or MMR; the symbol modes code connected components as a symbol dictionary and a text
/// region. The output decodes with <see cref="Jbig2Decoder"/> and with any conforming JBIG2 decoder.
/// </summary>
/// <remarks>
/// Input rows use the layout <see cref="Jbig2Decoder"/> writes and PDF samples use: <c>(width + 7) / 8</c> bytes a row, most
/// significant bit first, 0 for black. Decoding gives back the same samples, whatever colour space or /Decode array the image
/// has. Encoding is CPU-bound work on memory, so there are no async forms; pass a token to cancel a long encode.
/// </remarks>
public static class Jbig2Encoder
{
    /// <summary>The smallest accepted merge similarity.</summary>
    private const double MinimumSimilarity = 0.5;

    /// <summary>The number of generic templates.</summary>
    private const int TemplateCount = 4;

    /// <summary>The largest symbol size accepted.</summary>
    private const int MaximumSymbolLimit = 65_535;

    /// <summary>The size of the output buffers, as a divisor of the raw bitmap size.</summary>
    private const int OutputDivisor = 8;

    /// <summary>The smallest size of the output buffers.</summary>
    private const int MinimumOutput = 1024;

    /// <summary>Encodes an image with the default options.</summary>
    /// <param name="rows">The packed rows, 0 for black.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The coded image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or <paramref name="rows"/> is too short.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Jbig2EncodedImage Encode(ReadOnlySpan<byte> rows, int width, int height) =>
        Encode(rows, width, height, Jbig2EncodeOptions.Default, CancellationToken.None);

    /// <summary>Encodes an image.</summary>
    /// <param name="rows">The packed rows, 0 for black.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The settings.</param>
    /// <returns>The coded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size or an option is out of range, or <paramref name="rows"/> is too short.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Jbig2EncodedImage Encode(ReadOnlySpan<byte> rows, int width, int height, Jbig2EncodeOptions options) =>
        Encode(rows, width, height, options, CancellationToken.None);

    /// <summary>Encodes an image while observing a token.</summary>
    /// <param name="rows">The packed rows, 0 for black.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The settings.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The coded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size or an option is out of range, or <paramref name="rows"/> is too short.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Jbig2EncodedImage Encode(ReadOnlySpan<byte> rows, int width, int height, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            var mode = Encode(rows, width, height, options, ref data, ref globals, cancellationToken);
            return new(data.ToArray(), globals.Length == 0 ? null : globals.ToArray(), mode, mode != Jbig2EncodeMode.LossySymbolMerging);
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Encodes an image into pooled buffers, so a caller that reuses the buffers allocates nothing.</summary>
    /// <param name="rows">The packed rows, 0 for black.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The settings.</param>
    /// <param name="data">Receives the image stream's data, replacing any contents.</param>
    /// <param name="globals">Receives the /JBIG2Globals data, replacing any contents; empty when the image has none.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode used; for <see cref="Jbig2EncodeMode.Smallest"/> the one it picked, and a generic region when the requested mode does not suit the image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size or an option is out of range, or <paramref name="rows"/> is too short.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Jbig2EncodeMode Encode(
        ReadOnlySpan<byte> rows,
        int width,
        int height,
        Jbig2EncodeOptions options,
        ref PooledBuffer data,
        ref PooledBuffer globals,
        CancellationToken cancellationToken)
    {
        Validate(rows, width, height, options);
        data.Length = 0;
        globals.Length = 0;
        var ink = default(PooledBuffer);
        try
        {
            Jbig2Ink.FromSamples(rows, width, height, ref ink);
            var size = new Jbig2Size(width, height);
            return options.Mode == Jbig2EncodeMode.Smallest
                ? EncodeSmallest(ink.WrittenSpan, size, options, ref data, ref globals, cancellationToken)
                : EncodeMode(options.Mode, ink.WrittenSpan, size, options, ref data, ref globals, cancellationToken);
        }
        finally
        {
            ink.Dispose();
        }
    }

    /// <summary>Encodes several images with one shared symbol dictionary, for a /JBIG2Globals stream they all point to.</summary>
    /// <param name="pages">The images, each with packed rows, 0 for black.</param>
    /// <param name="options">The settings; the mode must be a symbol mode or <see cref="Jbig2EncodeMode.Smallest"/>, and <see cref="Jbig2EncodeOptions.SeparateGlobals"/> is implied.</param>
    /// <returns>The globals data and each image's data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pages"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The mode is not a symbol mode.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There are no images, a size or an option is out of range, or an image's rows are too short.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Jbig2SharedEncoding EncodeShared(IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options) =>
        EncodeShared(pages, options, CancellationToken.None);

    /// <summary>Encodes several images with one shared symbol dictionary while observing a token.</summary>
    /// <param name="pages">The images, each with packed rows, 0 for black.</param>
    /// <param name="options">The settings; the mode must be a symbol mode or <see cref="Jbig2EncodeMode.Smallest"/>, and <see cref="Jbig2EncodeOptions.SeparateGlobals"/> is implied.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>
    /// The globals data and each image's data. An image with no marks worth sharing is coded as a generic region and does
    /// not use the globals.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="pages"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The mode is not a symbol mode.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There are no images, a size or an option is out of range, or an image's rows are too short.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Jbig2SharedEncoding EncodeShared(IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var globals = default(PooledBuffer);
        var data = default(PooledBuffer);
        var lengths = ArrayPool<int>.Shared.Rent(pages.Count);
        try
        {
            var mode = EncodeShared(pages, options, ref globals, ref data, lengths, cancellationToken);
            var streams = new byte[pages.Count][];
            var start = 0;
            for (var i = 0; i < streams.Length; i++)
            {
                streams[i] = data.WrittenSpan.Slice(start, lengths[i]).ToArray();
                start += lengths[i];
            }

            return new(globals.ToArray(), streams, mode, mode != Jbig2EncodeMode.LossySymbolMerging);
        }
        finally
        {
            globals.Dispose();
            data.Dispose();
            ArrayPool<int>.Shared.Return(lengths);
        }
    }

    /// <summary>Encodes several images with one shared symbol dictionary into caller-owned pooled buffers.</summary>
    /// <param name="pages">The images, each with packed rows, 0 for black.</param>
    /// <param name="options">The settings; the mode must be a symbol mode or <see cref="Jbig2EncodeMode.Smallest"/>, and <see cref="Jbig2EncodeOptions.SeparateGlobals"/> is implied.</param>
    /// <param name="globals">Receives the /JBIG2Globals data, replacing any contents; empty when no image has marks worth sharing.</param>
    /// <param name="data">Receives every image's stream one after another, replacing any contents.</param>
    /// <param name="pageLengths">Receives the length of each image's stream in <paramref name="data"/>; at least one entry for each image.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode used: the symbol mode, the one <see cref="Jbig2EncodeMode.Smallest"/> picked, or a generic region when no image has marks worth sharing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pages"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The mode is not a symbol mode.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// There are no images, a size or an option is out of range, an image's rows are too short, or
    /// <paramref name="pageLengths"/> is shorter than the image count.
    /// </exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Jbig2EncodeMode EncodeShared(
        IReadOnlyList<Jbig2PageInput> pages,
        Jbig2EncodeOptions options,
        ref PooledBuffer globals,
        ref PooledBuffer data,
        scoped Span<int> pageLengths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentOutOfRangeException.ThrowIfZero(pages.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageLengths.Length, pages.Count, nameof(pageLengths));
        for (var i = 0; i < pages.Count; i++)
        {
            Validate(pages[i].Rows.Span, pages[i].Width, pages[i].Height, options);
        }

        return Jbig2SharedEncoder.Encode(pages, options, ref globals, ref data, pageLengths, cancellationToken);
    }

    /// <summary>Checks the arguments.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="options">The options.</param>
    private static void Validate(ReadOnlySpan<byte> rows, int width, int height, Jbig2EncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan((long)rows.Length, (long)Jbig2Bits.Stride(width) * height, nameof(rows));
        ArgumentOutOfRangeException.ThrowIfNegative(options.GenericTemplate);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(options.GenericTemplate, TemplateCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MergeSimilarity, MinimumSimilarity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MergeSimilarity, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumSymbolSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaximumSymbolSize, MaximumSymbolLimit);
    }

    /// <summary>Tries each lossless coding and keeps the smallest.</summary>
    /// <param name="ink">The packed rows, 1 for black.</param>
    /// <param name="size">The page size.</param>
    /// <param name="options">The options.</param>
    /// <param name="data">Receives the smallest coding's page data.</param>
    /// <param name="globals">Receives the smallest coding's globals.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode of the smallest coding.</returns>
    private static Jbig2EncodeMode EncodeSmallest(
        ReadOnlySpan<byte> ink,
        Jbig2Size size,
        Jbig2EncodeOptions options,
        ref PooledBuffer data,
        ref PooledBuffer globals,
        CancellationToken cancellationToken)
    {
        var best = EncodeMode(Jbig2EncodeMode.GenericRegion, ink, size, options, ref data, ref globals, cancellationToken);
        foreach (var mode in (ReadOnlySpan<Jbig2EncodeMode>)[Jbig2EncodeMode.GenericRegionMmr, Jbig2EncodeMode.SymbolText, Jbig2EncodeMode.SymbolTextRefined])
        {
            var page = new PooledBuffer(Math.Max(MinimumOutput, ink.Length / OutputDivisor));
            var extra = default(PooledBuffer);
            try
            {
                if (!Jbig2PageEncoder.TryEncode(mode, ink, size, options, ref page, ref extra, cancellationToken) || page.Length + extra.Length >= data.Length + globals.Length)
                {
                    continue;
                }

                data.Swap(ref page);
                globals.Swap(ref extra);
                best = mode;
            }
            finally
            {
                page.Dispose();
                extra.Dispose();
            }
        }

        return best;
    }

    /// <summary>Encodes with one mode, falling back to a generic region when the mode does not suit the page.</summary>
    /// <param name="mode">The mode, not <see cref="Jbig2EncodeMode.Smallest"/>.</param>
    /// <param name="ink">The packed rows, 1 for black.</param>
    /// <param name="size">The page size.</param>
    /// <param name="options">The options.</param>
    /// <param name="data">Receives the page data.</param>
    /// <param name="globals">Receives the globals.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode used.</returns>
    private static Jbig2EncodeMode EncodeMode(
        Jbig2EncodeMode mode,
        ReadOnlySpan<byte> ink,
        Jbig2Size size,
        Jbig2EncodeOptions options,
        ref PooledBuffer data,
        ref PooledBuffer globals,
        CancellationToken cancellationToken)
    {
        if (Jbig2PageEncoder.TryEncode(mode, ink, size, options, ref data, ref globals, cancellationToken))
        {
            return mode;
        }

        _ = Jbig2PageEncoder.TryEncode(Jbig2EncodeMode.GenericRegion, ink, size, options, ref data, ref globals, cancellationToken);
        return Jbig2EncodeMode.GenericRegion;
    }
}
