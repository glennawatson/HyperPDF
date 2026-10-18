// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Codes one page: its page information segment and the regions that draw it.</summary>
internal static class Jbig2PageEncoder
{
    /// <summary>The most columns of a page that symbol coding handles.</summary>
    internal const int MaximumSymbolPageWidth = 1 << 20;

    /// <summary>The most rows of a page that symbol coding handles.</summary>
    internal const int MaximumSymbolPageHeight = 1 << 24;

    /// <summary>The segment number of the first segment written.</summary>
    private const int FirstSegment = 0;

    /// <summary>The segments a page with a refined dictionary has in front of its regions: the two dictionaries.</summary>
    private const int RefinedDictionaries = 2;

    /// <summary>The shift of a strip number in a sort key.</summary>
    private const int StripShift = 20;

    /// <summary>The divisor that turns a pixel count into the largest black count symbol coding accepts.</summary>
    private const int HalfDivisor = 2;

    /// <summary>Codes a page with one mode.</summary>
    /// <param name="mode">The mode, not <see cref="Jbig2EncodeMode.Smallest"/>.</param>
    /// <param name="ink">The packed rows, 1 for black, with zero padding bits.</param>
    /// <param name="input">The page size and settings.</param>
    /// <param name="page">Receives the page's segments.</param>
    /// <param name="globals">Receives the global segments, when the options ask for them and the mode has any.</param>
    /// <param name="pools">The caller's MMR line-buffer owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns><see langword="false"/> when the mode does not suit the page.</returns>
    /// <exception cref="InvalidOperationException">MMR coding has no caller-owned scratch.</exception>
    internal static bool TryEncode(
        Jbig2EncodeMode mode,
        ReadOnlySpan<byte> ink,
        in Jbig2EncodingInput input,
        ref PooledBuffer page,
        ref PooledBuffer globals,
        ScratchPools? pools,
        CancellationToken cancellationToken) =>
        mode switch
        {
            Jbig2EncodeMode.GenericRegion => EncodeArithmetic(ink, input.Size, input.Options, ref page, cancellationToken),
            Jbig2EncodeMode.GenericRegionMmr when pools is { } owner => EncodeMmr(ink, input.Size, ref page, owner, cancellationToken),
            Jbig2EncodeMode.GenericRegionMmr => throw new InvalidOperationException("MMR encoding needs a caller-owned scratch pool."),
            Jbig2EncodeMode.SymbolText => EncodeSymbols(ink, new(input.Size, input.Options, true, false), ref page, ref globals, cancellationToken),
            Jbig2EncodeMode.SymbolTextRefined => EncodeSymbols(ink, new(input.Size, input.Options, true, true), ref page, ref globals, cancellationToken),
            Jbig2EncodeMode.LossySymbolMerging => EncodeSymbols(ink, new(input.Size, input.Options, false, false), ref page, ref globals, cancellationToken),
            _ => false,
        };

    /// <summary>Determines whether symbol coding suits a page: a size it handles, and mostly white.</summary>
    /// <param name="ink">The packed rows.</param>
    /// <param name="size">The page size.</param>
    /// <returns><see langword="true"/> when symbol coding may be tried.</returns>
    internal static bool SuitsSymbols(ReadOnlySpan<byte> ink, Jbig2Size size) =>
        size.Width <= MaximumSymbolPageWidth && size.Height <= MaximumSymbolPageHeight && Jbig2Ink.CountBlack(ink) * HalfDivisor <= (long)size.Width * size.Height;

    /// <summary>Writes the symbol dictionaries, to a stream, numbering them from a segment number.</summary>
    /// <param name="symbols">The symbols in dictionary order.</param>
    /// <param name="template">The generic template of the symbol bitmaps.</param>
    /// <param name="target">The stream.</param>
    /// <param name="numbering">The first segment number and the page association.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The base and refined dictionary numbers (-1 when there is no refined dictionary) and the next free number.</returns>
    internal static Jbig2HeadSegments WriteDictionaries(in Jbig2EncoderSymbols symbols, int template, ref PooledBuffer target, Jbig2Point numbering, CancellationToken cancellationToken)
    {
        var (first, association) = numbering;
        Jbig2SymbolDictionaryWriter.Write(ref target, first, association, symbols, template, cancellationToken);
        if (symbols.Count == symbols.BaseCount)
        {
            return new(first, -1, first + 1);
        }

        Jbig2SymbolDictionaryWriter.WriteRefined(ref target, first + 1, association, symbols, first, template, cancellationToken);
        return new(first, first + 1, first + RefinedDictionaries);
    }

    /// <summary>Writes the text region and the generic region of what is too large to be a symbol.</summary>
    /// <param name="slice">The page's components.</param>
    /// <param name="symbols">The symbols in dictionary order.</param>
    /// <param name="settings">The page settings.</param>
    /// <param name="head">The dictionary segment numbers and the first free number.</param>
    /// <param name="page">Receives the segments.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void WriteBody(
        in Jbig2PageSlice slice,
        in Jbig2EncoderSymbols symbols,
        in Jbig2PageSettings settings,
        Jbig2HeadSegments head,
        ref PooledBuffer page,
        CancellationToken cancellationToken)
    {
        var (size, options, lossless, _) = settings;
        var instances = ArrayPool<Jbig2TextInstance>.Shared.Rent(Math.Max(slice.End - slice.First, 1));
        var next = head.NextNumber;
        try
        {
            var count = SortInstances(slice, symbols, instances, out var refined);
            var placement = new Jbig2TextPlacement(new(0, 0, size.Width, size.Height), symbols.Count, lossless, refined ? slice.Combined : default, refined ? symbols : default);
            Span<int> referred = stackalloc int[RefinedDictionaries];
            referred[0] = head.DictionaryNumber;
            referred[1] = head.RefinedNumber;
            Jbig2TextRegionWriter.Write(ref page, next, referred[..(head.RefinedNumber >= 0 ? RefinedDictionaries : 1)], placement, instances.AsSpan(0, count), cancellationToken);
            next++;
        }
        finally
        {
            ArrayPool<Jbig2TextInstance>.Shared.Return(instances);
        }

        var box = slice.Own.ResidualBox;
        if (box.Width == 0)
        {
            return;
        }

        var parameters = Jbig2GenericRegionWriter.Nominal(options.GenericTemplate, options.TypicalPrediction);
        var bitmap = new Jbig2BitmapView(slice.Own.ResidualRows, box.Width, box.Height);
        Jbig2GenericRegionWriter.WriteArithmetic(ref page, next, bitmap, new(box.X, box.Y), parameters, cancellationToken);
    }

    /// <summary>Codes a page as one generic region.</summary>
    /// <param name="ink">The packed rows.</param>
    /// <param name="size">The page size.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">Receives the segments.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns><see langword="true"/>.</returns>
    internal static bool EncodeArithmetic(ReadOnlySpan<byte> ink, Jbig2Size size, Jbig2EncodeOptions options, ref PooledBuffer page, CancellationToken cancellationToken)
    {
        Jbig2SegmentWriter.WritePageInformation(ref page, FirstSegment, size.Width, size.Height, true);
        var parameters = Jbig2GenericRegionWriter.Nominal(options.GenericTemplate, options.TypicalPrediction);
        var bitmap = new Jbig2BitmapView(ink, size.Width, size.Height);
        Jbig2GenericRegionWriter.WriteArithmetic(ref page, FirstSegment + 1, bitmap, default, parameters, cancellationToken);
        return true;
    }

    /// <summary>Codes a page as one MMR generic region.</summary>
    /// <param name="ink">The packed rows, 1 for black.</param>
    /// <param name="size">The page size.</param>
    /// <param name="page">Receives the segments.</param>
    /// <param name="pools">The caller's line-buffer owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>True after writing the region.</returns>
    private static bool EncodeMmr(ReadOnlySpan<byte> ink, Jbig2Size size, ref PooledBuffer page, ScratchPools pools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Jbig2SegmentWriter.WritePageInformation(ref page, FirstSegment, size.Width, size.Height, true);
        Jbig2GenericRegionWriter.WriteMmr(ref page, FirstSegment + 1, new(ink, size.Width, size.Height), default, pools, cancellationToken);
        return true;
    }

    /// <summary>Codes a page as a symbol dictionary, a text region and a generic region for what is too large to be a symbol.</summary>
    /// <param name="ink">The packed rows.</param>
    /// <param name="settings">The page size, options, and whether sharing is exact only.</param>
    /// <param name="page">Receives the page's segments.</param>
    /// <param name="globals">Receives the dictionaries when the options ask for separate globals.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns><see langword="false"/> when the page has no text-like components, or too many.</returns>
    private static bool EncodeSymbols(ReadOnlySpan<byte> ink, in Jbig2PageSettings settings, ref PooledBuffer page, ref PooledBuffer globals, CancellationToken cancellationToken)
    {
        if (!SuitsSymbols(ink, settings.Size) || !Jbig2Components.TryFind(ink, settings.Size, settings.Options.MaximumSymbolSize, cancellationToken, out var components))
        {
            return false;
        }

        using (components)
        {
            return components.SymbolCount != 0 && EncodeComponents(components, settings, ref page, ref globals, cancellationToken);
        }
    }

    /// <summary>Codes a page from its components.</summary>
    /// <param name="components">The page's components, which have at least one symbol.</param>
    /// <param name="settings">The page size, options, and whether sharing is exact only.</param>
    /// <param name="page">Receives the page's segments.</param>
    /// <param name="globals">Receives the dictionaries when the options ask for separate globals.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns><see langword="true"/>.</returns>
    private static bool EncodeComponents(in Jbig2Components components, in Jbig2PageSettings settings, ref PooledBuffer page, ref PooledBuffer globals, CancellationToken cancellationToken)
    {
        var similarity = settings.IsLossless && !settings.Refine ? 1.0 : settings.Options.MergeSimilarity;
        using var matcher = Jbig2SymbolMatcher.Match(components, similarity, settings.Refine, cancellationToken);
        using var symbols = Jbig2EncoderSymbols.Build(components, matcher);
        var head = WriteHead(symbols, settings, ref page, ref globals, cancellationToken);
        WriteBody(new(components, 0, components.Count, components), symbols, settings, head, ref page, cancellationToken);
        return true;
    }

    /// <summary>Writes the page information and the symbol dictionaries, to the page or to the globals.</summary>
    /// <param name="symbols">The symbols in dictionary order.</param>
    /// <param name="settings">The page settings.</param>
    /// <param name="page">Receives the page's segments.</param>
    /// <param name="globals">Receives the dictionaries when the options ask for separate globals.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The segment numbers used.</returns>
    private static Jbig2HeadSegments WriteHead(in Jbig2EncoderSymbols symbols, in Jbig2PageSettings settings, ref PooledBuffer page, ref PooledBuffer globals, CancellationToken cancellationToken)
    {
        var (size, options, lossless, _) = settings;
        if (options.SeparateGlobals)
        {
            var dictionaries = WriteDictionaries(symbols, options.GenericTemplate, ref globals, new(FirstSegment, 0), cancellationToken);
            Jbig2SegmentWriter.WritePageInformation(ref page, dictionaries.NextNumber, size.Width, size.Height, lossless);
            return dictionaries with { NextNumber = dictionaries.NextNumber + 1 };
        }

        Jbig2SegmentWriter.WritePageInformation(ref page, FirstSegment, size.Width, size.Height, lossless);
        return WriteDictionaries(symbols, options.GenericTemplate, ref page, new(FirstSegment + 1, 1), cancellationToken);
    }

    /// <summary>Lists the symbol instances of a page in strip order, and left to right within a strip.</summary>
    /// <param name="slice">The page's components.</param>
    /// <param name="symbols">The symbols in dictionary order.</param>
    /// <param name="instances">Receives the instances.</param>
    /// <param name="refined">Receives whether any instance is coded as a refinement.</param>
    /// <returns>The number of instances.</returns>
    private static int SortInstances(in Jbig2PageSlice slice, in Jbig2EncoderSymbols symbols, Jbig2TextInstance[] instances, out bool refined)
    {
        var capacity = Math.Max(slice.End - slice.First, 1);
        var keys = ArrayPool<long>.Shared.Rent(capacity);
        var unsorted = ArrayPool<Jbig2TextInstance>.Shared.Rent(capacity);
        var order = ArrayPool<int>.Shared.Rent(capacity);
        var components = slice.Combined;
        refined = false;
        try
        {
            var count = 0;
            for (var component = slice.First; component < slice.End; component++)
            {
                if (!components.IsSymbol(component))
                {
                    continue;
                }

                var rank = symbols.RankOfComponent(component, out var refine);
                var bottom = components.Top(component) + components.Height(component) - 1;
                var width = refine ? components.Width(component) : symbols.Width(rank);
                unsorted[count] = new(rank, components.Left(component), bottom, width, refine ? component : -1);
                keys[count] = ((long)Jbig2TextRegionWriter.StripOf(bottom) << StripShift) | (uint)components.Left(component);
                order[count] = count;
                refined |= refine;
                count++;
            }

            keys.AsSpan(0, count).Sort(order.AsSpan(0, count));
            for (var i = 0; i < count; i++)
            {
                instances[i] = unsorted[order[i]];
            }

            return count;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(keys);
            ArrayPool<Jbig2TextInstance>.Shared.Return(unsorted);
            ArrayPool<int>.Shared.Return(order);
        }
    }
}
