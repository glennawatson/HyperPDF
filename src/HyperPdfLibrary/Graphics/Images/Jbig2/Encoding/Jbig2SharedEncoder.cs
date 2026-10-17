// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Codes several images with one symbol dictionary: the marks of all the images are matched together, the dictionary goes in
/// a globals stream, and each image's stream holds only its page information, text region and any large components. An
/// image with no marks to share is coded as a generic region and ignores the globals.
/// </summary>
internal static class Jbig2SharedEncoder
{
    /// <summary>The segment number of the first dictionary in the globals.</summary>
    private const int FirstSegment = 0;

    /// <summary>Encodes images with a shared dictionary into caller-owned buffers.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options; the mode must be a symbol mode or <see cref="Jbig2EncodeMode.Smallest"/>.</param>
    /// <param name="globals">Receives the shared dictionary's data, replacing any contents; empty when the images share nothing.</param>
    /// <param name="data">Receives every image's stream one after another, replacing any contents.</param>
    /// <param name="lengths">Receives the length of each image's stream in <paramref name="data"/>.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode used: a symbol mode, or a generic region when no image has marks to share.</returns>
    /// <exception cref="ArgumentException">The mode is not a symbol mode.</exception>
    internal static Jbig2EncodeMode Encode(
        IReadOnlyList<Jbig2PageInput> pages,
        Jbig2EncodeOptions options,
        ref PooledBuffer globals,
        ref PooledBuffer data,
        scoped Span<int> lengths,
        CancellationToken cancellationToken)
    {
        globals.Length = 0;
        data.Length = 0;
        if (options.Mode == Jbig2EncodeMode.Smallest)
        {
            return EncodeSmallest(pages, options, ref globals, ref data, lengths, cancellationToken);
        }

        if (options.Mode is not (Jbig2EncodeMode.SymbolText or Jbig2EncodeMode.SymbolTextRefined or Jbig2EncodeMode.LossySymbolMerging))
        {
            throw new ArgumentException("Shared globals hold a symbol dictionary, so the mode must be a symbol mode.", nameof(options));
        }

        return EncodeMode(options.Mode, pages, options, ref globals, ref data, lengths, cancellationToken);
    }

    /// <summary>Encodes with plain and with refined symbols and keeps the smaller.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options.</param>
    /// <param name="globals">Receives the shared dictionary's data.</param>
    /// <param name="data">Receives every image's stream.</param>
    /// <param name="lengths">Receives the length of each image's stream.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode of the smaller encoding.</returns>
    private static Jbig2EncodeMode EncodeSmallest(
        IReadOnlyList<Jbig2PageInput> pages,
        Jbig2EncodeOptions options,
        ref PooledBuffer globals,
        ref PooledBuffer data,
        scoped Span<int> lengths,
        CancellationToken cancellationToken)
    {
        var plain = EncodeMode(Jbig2EncodeMode.SymbolText, pages, options, ref globals, ref data, lengths, cancellationToken);
        var refinedGlobals = default(PooledBuffer);
        var refinedData = default(PooledBuffer);
        var refinedLengths = ArrayPool<int>.Shared.Rent(pages.Count);
        try
        {
            var refined = EncodeMode(Jbig2EncodeMode.SymbolTextRefined, pages, options, ref refinedGlobals, ref refinedData, refinedLengths, cancellationToken);
            if ((long)refinedGlobals.Length + refinedData.Length >= (long)globals.Length + data.Length)
            {
                return plain;
            }

            globals.Swap(ref refinedGlobals);
            data.Swap(ref refinedData);
            refinedLengths.AsSpan(0, pages.Count).CopyTo(lengths);
            return refined;
        }
        finally
        {
            refinedGlobals.Dispose();
            refinedData.Dispose();
            ArrayPool<int>.Shared.Return(refinedLengths);
        }
    }

    /// <summary>Encodes with one symbol mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options.</param>
    /// <param name="globals">Receives the shared dictionary's data.</param>
    /// <param name="data">Receives every image's stream.</param>
    /// <param name="lengths">Receives the length of each image's stream.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The mode used.</returns>
    private static Jbig2EncodeMode EncodeMode(
        Jbig2EncodeMode mode,
        IReadOnlyList<Jbig2PageInput> pages,
        Jbig2EncodeOptions options,
        ref PooledBuffer globals,
        ref PooledBuffer data,
        scoped Span<int> lengths,
        CancellationToken cancellationToken)
    {
        globals.Length = 0;
        data.Length = 0;
        var count = pages.Count;
        var inks = ArrayPool<byte[]>.Shared.Rent(count);
        var parts = ArrayPool<Jbig2Components>.Shared.Rent(count);
        inks.AsSpan(0, count).Clear();
        parts.AsSpan(0, count).Clear();
        var combined = default(Jbig2Components);
        try
        {
            Analyse(pages, options, inks, parts, cancellationToken);
            if (!Jbig2Components.TryCombine(parts.AsSpan(0, count), out combined))
            {
                EncodeAll(pages, inks, options, ref data, lengths, cancellationToken);
                return Jbig2EncodeMode.GenericRegion;
            }

            Write(new(mode, mode != Jbig2EncodeMode.LossySymbolMerging), new(pages, inks, parts, combined), options, ref globals, ref data, lengths, cancellationToken);
            return mode;
        }
        finally
        {
            combined.Dispose();
            for (var i = 0; i < count; i++)
            {
                parts[i].Dispose();
                if (inks[i] is { } ink)
                {
                    ArrayPool<byte>.Shared.Return(ink);
                }
            }

            ArrayPool<byte[]>.Shared.Return(inks, true);
            ArrayPool<Jbig2Components>.Shared.Return(parts, true);
        }
    }

    /// <summary>Inverts each image to ink and finds its components when symbol coding suits it.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options.</param>
    /// <param name="inks">Receives each image's rented ink rows.</param>
    /// <param name="parts">Receives each suitable image's components, the default value for the others.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private static void Analyse(IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options, byte[][] inks, Jbig2Components[] parts, CancellationToken cancellationToken)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var (rows, width, height) = pages[i];
            var length = Jbig2Bits.Stride(width) * height;
            inks[i] = ArrayPool<byte>.Shared.Rent(length);
            Jbig2Ink.FromSamples(rows.Span, width, height, inks[i].AsSpan(0, length));
            var size = new Jbig2Size(width, height);
            if (!Jbig2PageEncoder.SuitsSymbols(inks[i].AsSpan(0, length), size)
                || !Jbig2Components.TryFind(inks[i].AsSpan(0, length), size, options.MaximumSymbolSize, cancellationToken, out var found))
            {
                continue;
            }

            if (found.SymbolCount > 0)
            {
                parts[i] = found;
                continue;
            }

            found.Dispose();
        }
    }

    /// <summary>Codes every image as a generic region, for when nothing can be shared.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="inks">Each image's ink rows.</param>
    /// <param name="options">The options.</param>
    /// <param name="data">Receives every image's stream.</param>
    /// <param name="lengths">Receives the length of each image's stream.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeAll(IReadOnlyList<Jbig2PageInput> pages, byte[][] inks, Jbig2EncodeOptions options, ref PooledBuffer data, scoped Span<int> lengths, CancellationToken cancellationToken)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var start = data.Length;
            EncodeGeneric(pages[i], inks[i], options, ref data, cancellationToken);
            lengths[i] = data.Length - start;
        }
    }

    /// <summary>Codes one image as a generic region.</summary>
    /// <param name="page">The image.</param>
    /// <param name="ink">Its ink rows.</param>
    /// <param name="options">The options.</param>
    /// <param name="data">Receives the image's stream after what it holds.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeGeneric(Jbig2PageInput page, byte[] ink, Jbig2EncodeOptions options, ref PooledBuffer data, CancellationToken cancellationToken)
    {
        var length = Jbig2Bits.Stride(page.Width) * page.Height;
        _ = Jbig2PageEncoder.EncodeArithmetic(ink.AsSpan(0, length), new(page.Width, page.Height), options, ref data, cancellationToken);
    }

    /// <summary>Matches the marks of all the images, writes the shared dictionaries and each image's stream.</summary>
    /// <param name="coding">The mode and whether it is lossless.</param>
    /// <param name="work">The images with their ink rows and components.</param>
    /// <param name="options">The options.</param>
    /// <param name="globals">Receives the shared dictionaries.</param>
    /// <param name="data">Receives every image's stream.</param>
    /// <param name="lengths">Receives the length of each image's stream.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void Write(
        Jbig2SharedCoding coding,
        in Jbig2SharedWork work,
        Jbig2EncodeOptions options,
        ref PooledBuffer globals,
        ref PooledBuffer data,
        scoped Span<int> lengths,
        CancellationToken cancellationToken)
    {
        var (mode, lossless) = coding;
        var refine = mode == Jbig2EncodeMode.SymbolTextRefined;
        var similarity = lossless && !refine ? 1.0 : options.MergeSimilarity;
        using var matcher = Jbig2SymbolMatcher.Match(work.Combined, similarity, refine, cancellationToken);
        using var symbols = Jbig2EncoderSymbols.Build(work.Combined, matcher);
        var head = Jbig2PageEncoder.WriteDictionaries(symbols, options.GenericTemplate, ref globals, new(FirstSegment, 0), cancellationToken);
        var first = 0;
        for (var i = 0; i < work.Pages.Count; i++)
        {
            var own = work.Parts[i];
            var start = data.Length;
            if (own.IsDefault)
            {
                EncodeGeneric(work.Pages[i], work.Inks[i], options, ref data, cancellationToken);
            }
            else
            {
                var settings = new Jbig2PageSettings(new(work.Pages[i].Width, work.Pages[i].Height), options, lossless, refine);
                WritePage(settings, new(work.Combined, first, first + own.Count, own), symbols, head, ref data, cancellationToken);
            }

            first += own.Count;
            lengths[i] = data.Length - start;
        }
    }

    /// <summary>Writes one image's stream: its page information, text region and residual region.</summary>
    /// <param name="settings">The page settings.</param>
    /// <param name="slice">The image's components.</param>
    /// <param name="symbols">The shared symbols.</param>
    /// <param name="dictionaries">The shared dictionaries' segment numbers.</param>
    /// <param name="page">Receives the image's data after what it holds.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void WritePage(
        in Jbig2PageSettings settings,
        in Jbig2PageSlice slice,
        in Jbig2EncoderSymbols symbols,
        Jbig2HeadSegments dictionaries,
        ref PooledBuffer page,
        CancellationToken cancellationToken)
    {
        var (size, _, lossless, _) = settings;
        Jbig2SegmentWriter.WritePageInformation(ref page, dictionaries.NextNumber, size.Width, size.Height, lossless);
        var head = dictionaries with { NextNumber = dictionaries.NextNumber + 1 };
        Jbig2PageEncoder.WriteBody(slice, symbols, settings, head, ref page, cancellationToken);
    }
}
