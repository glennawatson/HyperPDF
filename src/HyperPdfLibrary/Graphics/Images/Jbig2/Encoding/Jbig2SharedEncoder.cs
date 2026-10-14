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

    /// <summary>Encodes images with a shared dictionary.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options; the mode must be a symbol mode or <see cref="Jbig2EncodeMode.Smallest"/>.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The globals and each image's data.</returns>
    /// <exception cref="ArgumentException">The mode is not a symbol mode.</exception>
    internal static Jbig2SharedEncoding Encode(IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        if (options.Mode == Jbig2EncodeMode.Smallest)
        {
            var plain = EncodeMode(Jbig2EncodeMode.SymbolText, pages, options, cancellationToken);
            var refined = EncodeMode(Jbig2EncodeMode.SymbolTextRefined, pages, options, cancellationToken);
            return Total(refined) < Total(plain) ? refined : plain;
        }

        if (options.Mode is not (Jbig2EncodeMode.SymbolText or Jbig2EncodeMode.SymbolTextRefined or Jbig2EncodeMode.LossySymbolMerging))
        {
            throw new ArgumentException("Shared globals hold a symbol dictionary, so the mode must be a symbol mode.", nameof(options));
        }

        return EncodeMode(options.Mode, pages, options, cancellationToken);
    }

    /// <summary>Gets the bytes an encoding takes.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns>The globals and all pages.</returns>
    private static long Total(Jbig2SharedEncoding encoding)
    {
        long total = encoding.Globals.Length;
        foreach (var page in encoding.Pages)
        {
            total += page.Length;
        }

        return total;
    }

    /// <summary>Encodes with one symbol mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The encoding.</returns>
    private static Jbig2SharedEncoding EncodeMode(Jbig2EncodeMode mode, IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        var inks = new byte[pages.Count][];
        var parts = new Jbig2Components?[pages.Count];
        Jbig2Components? combined = null;
        try
        {
            Analyse(pages, options, inks, parts, cancellationToken);
            combined = Combine(parts);
            return combined is null
                ? new([], EncodeAll(pages, inks, options, cancellationToken), Jbig2EncodeMode.GenericRegion, true)
                : Write(new(mode, mode != Jbig2EncodeMode.LossySymbolMerging), pages, new(inks, parts, combined), options, cancellationToken);
        }
        finally
        {
            combined?.Dispose();
            foreach (var part in parts)
            {
                part?.Dispose();
            }

            foreach (var ink in inks)
            {
                if (ink is not null)
                {
                    ArrayPool<byte>.Shared.Return(ink);
                }
            }
        }
    }

    /// <summary>Inverts each image to ink and finds its components when symbol coding suits it.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="options">The options.</param>
    /// <param name="inks">Receives each image's rented ink rows.</param>
    /// <param name="parts">Receives each suitable image's components, null for the others.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    private static void Analyse(IReadOnlyList<Jbig2PageInput> pages, Jbig2EncodeOptions options, byte[][] inks, Jbig2Components?[] parts, CancellationToken cancellationToken)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var (rows, width, height) = pages[i];
            var length = Jbig2Bits.Stride(width) * height;
            inks[i] = ArrayPool<byte>.Shared.Rent(length);
            Jbig2Ink.FromSamples(rows.Span, width, height, inks[i].AsSpan(0, length));
            var size = new Jbig2Size(width, height);
            if (!Jbig2PageEncoder.SuitsSymbols(inks[i].AsSpan(0, length), size))
            {
                continue;
            }

            var found = Jbig2Components.Find(inks[i].AsSpan(0, length), size, options.MaximumSymbolSize, cancellationToken);
            if (found is { SymbolCount: > 0 })
            {
                parts[i] = found;
                continue;
            }

            found?.Dispose();
        }
    }

    /// <summary>Joins the components of the images that have symbols.</summary>
    /// <param name="parts">Each image's components, or null.</param>
    /// <returns>The joined components, or <see langword="null"/> when no image has symbols or they are too large to join.</returns>
    private static Jbig2Components? Combine(Jbig2Components?[] parts)
    {
        var present = new List<Jbig2Components>(parts.Length);
        foreach (var part in parts)
        {
            if (part is not null)
            {
                present.Add(part);
            }
        }

        return present.Count == 0 ? null : Jbig2Components.Combine(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(present));
    }

    /// <summary>Codes every image as a generic region, for when nothing can be shared.</summary>
    /// <param name="pages">The images.</param>
    /// <param name="inks">Each image's ink rows.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>Each image's data.</returns>
    private static byte[][] EncodeAll(IReadOnlyList<Jbig2PageInput> pages, byte[][] inks, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        var result = new byte[pages.Count][];
        for (var i = 0; i < pages.Count; i++)
        {
            result[i] = EncodeGeneric(pages[i], inks[i], options, cancellationToken);
        }

        return result;
    }

    /// <summary>Codes one image as a generic region.</summary>
    /// <param name="page">The image.</param>
    /// <param name="ink">Its ink rows.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The image's data.</returns>
    private static byte[] EncodeGeneric(Jbig2PageInput page, byte[] ink, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            var length = Jbig2Bits.Stride(page.Width) * page.Height;
            _ = Jbig2PageEncoder.TryEncode(Jbig2EncodeMode.GenericRegion, ink.AsSpan(0, length), new(page.Width, page.Height), options, ref data, ref globals, cancellationToken);
            return data.ToArray();
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Matches the marks of all the images, writes the shared dictionaries and each image's stream.</summary>
    /// <param name="coding">The mode and whether it is lossless.</param>
    /// <param name="pages">The images.</param>
    /// <param name="work">The ink rows and components of the images.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The encoding.</returns>
    private static Jbig2SharedEncoding Write(Jbig2SharedCoding coding, IReadOnlyList<Jbig2PageInput> pages, Jbig2SharedWork work, Jbig2EncodeOptions options, CancellationToken cancellationToken)
    {
        var (mode, lossless) = coding;
        var refine = mode == Jbig2EncodeMode.SymbolTextRefined;
        var similarity = lossless && !refine ? 1.0 : options.MergeSimilarity;
        using var matcher = Jbig2SymbolMatcher.Match(work.Combined, similarity, refine, cancellationToken);
        using var symbols = Jbig2EncoderSymbols.Build(work.Combined, matcher);
        var globals = default(PooledBuffer);
        try
        {
            var head = Jbig2PageEncoder.WriteDictionaries(symbols, options.GenericTemplate, ref globals, new(FirstSegment, 0), cancellationToken);
            var streams = new byte[pages.Count][];
            var first = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                var slice = work.Parts[i] is { } own ? new Jbig2PageSlice(work.Combined, first, first + own.Count, own) : default;
                first += work.Parts[i]?.Count ?? 0;
                streams[i] = work.Parts[i] is null
                    ? EncodeGeneric(pages[i], work.Inks[i], options, cancellationToken)
                    : WritePage(new(new(pages[i].Width, pages[i].Height), options, lossless, refine), slice, symbols, head, cancellationToken);
            }

            return new(globals.ToArray(), streams, mode, lossless);
        }
        finally
        {
            globals.Dispose();
        }
    }

    /// <summary>Writes one image's stream: its page information, text region and residual region.</summary>
    /// <param name="settings">The page settings.</param>
    /// <param name="slice">The image's components.</param>
    /// <param name="symbols">The shared symbols.</param>
    /// <param name="dictionaries">The shared dictionaries' segment numbers.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The image's data.</returns>
    private static byte[] WritePage(in Jbig2PageSettings settings, in Jbig2PageSlice slice, Jbig2EncoderSymbols symbols, Jbig2HeadSegments dictionaries, CancellationToken cancellationToken)
    {
        var page = default(PooledBuffer);
        try
        {
            var (size, _, lossless, _) = settings;
            Jbig2SegmentWriter.WritePageInformation(ref page, dictionaries.NextNumber, size.Width, size.Height, lossless);
            var head = dictionaries with { NextNumber = dictionaries.NextNumber + 1 };
            Jbig2PageEncoder.WriteBody(slice, symbols, settings, head, ref page, cancellationToken);
            return page.ToArray();
        }
        finally
        {
            page.Dispose();
        }
    }
}
