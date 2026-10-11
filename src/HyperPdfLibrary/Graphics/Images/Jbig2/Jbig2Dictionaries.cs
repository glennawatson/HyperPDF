// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Decodes symbol and pattern dictionaries with their retained arithmetic contexts.</summary>
internal static class Jbig2Dictionaries
{
    /// <summary>The symbol dictionary flag for refinement and aggregation.</summary>
    internal const int RefinementAggregateFlag = 0x0002;

    /// <summary>The shift of the generic template in symbol dictionary flags.</summary>
    internal const int DictionaryTemplateShift = 10;

    /// <summary>The shift of the refinement template in symbol dictionary flags.</summary>
    internal const int DictionaryRefinementShift = 12;

    /// <summary>The symbol dictionary flag that reuses the last referred dictionary's contexts.</summary>
    internal const int ReuseContextsFlag = 0x0100;

    /// <summary>The symbol dictionary flag that keeps the contexts for a later dictionary.</summary>
    internal const int RetainContextsFlag = 0x0200;

    /// <summary>Determines whether a dictionary retained contexts of the sizes a reusing dictionary needs, as PDFium checks.</summary>
    /// <param name="last">The last referred dictionary.</param>
    /// <param name="genericLength">The generic contexts needed, or zero.</param>
    /// <param name="refinementLength">The refinement contexts needed, or zero.</param>
    /// <returns><see langword="true"/> when the sizes match.</returns>
    internal static bool ContextsMatch(Jbig2SymbolDictionary last, int genericLength, int refinementLength) =>
        (genericLength == 0 || last.GenericContexts.Length == genericLength)
        && (refinementLength == 0 || last.RefinementContexts.Length == refinementLength);

    /// <summary>Gets the generic contexts a reused dictionary passes on.</summary>
    /// <param name="reused">The dictionary whose contexts are reused, or <see langword="null"/>.</param>
    /// <returns>The contexts; empty when none are reused.</returns>
    internal static ReadOnlySpan<byte> ReusedGeneric(Jbig2SymbolDictionary? reused) => reused is null ? default : reused.GenericContexts;

    /// <summary>Gets the refinement contexts a reused dictionary passes on.</summary>
    /// <param name="reused">The dictionary whose contexts are reused, or <see langword="null"/>.</param>
    /// <returns>The contexts; empty when none are reused.</returns>
    internal static ReadOnlySpan<byte> ReusedRefinement(Jbig2SymbolDictionary? reused) => reused is null ? default : reused.RefinementContexts;

    /// <summary>Copies contexts for a later dictionary when the dictionary asks for it.</summary>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="contexts">The final contexts.</param>
    /// <returns>The copy, or <see langword="null"/>.</returns>
    internal static byte[]? Retain(Jbig2SymbolDictionarySettings settings, ReadOnlySpan<byte> contexts) =>
        settings.RetainContexts && !contexts.IsEmpty ? contexts.ToArray() : null;

    /// <summary>Reads a symbol dictionary's header fields.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="flags">The flags.</param>
    /// <returns><see langword="false"/> when the data ends or a count is too large.</returns>
    internal static bool TryReadSymbolDictionary(ref Jbig2Reader reader, out Jbig2SymbolDictionarySettings settings, out ushort flags)
    {
        settings = new();
        if (!reader.TryReadUInt16(out flags))
        {
            return false;
        }

        settings.Huffman = (flags & Jbig2TextSegments.HuffmanFlag) != 0;
        settings.RefinementAggregate = (flags & RefinementAggregateFlag) != 0;
        settings.Template = (flags >> DictionaryTemplateShift) & Jbig2TextSegments.TwoBits;
        settings.RefinementTemplate = ((flags >> DictionaryRefinementShift) & Jbig2TextSegments.TwoBits) != 0 ? 1 : 0;
        settings.ReuseContexts = (flags & ReuseContextsFlag) != 0;
        settings.RetainContexts = (flags & RetainContextsFlag) != 0;
        if (!TryReadDictionaryAt(ref reader, settings) || !reader.TryReadUInt32(out var exported) || !reader.TryReadUInt32(out var created))
        {
            return false;
        }

        settings.ExportCount = exported;
        settings.NewCount = (int)Math.Min(created, int.MaxValue);
        return exported <= Jbig2Limits.MaxExportSymbols && created <= Jbig2Limits.MaxNewSymbols;
    }

    /// <summary>Reads the generic and refinement adaptive pixels of a symbol dictionary.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadDictionaryAt(ref Jbig2Reader reader, Jbig2SymbolDictionarySettings settings)
    {
        var at = default(Jbig2AtPixels);
        if (!settings.Huffman && !Jbig2Regions.TryReadAt(ref reader, settings.Template, out at))
        {
            return false;
        }

        settings.At = at;
        if (!settings.RefinementAggregate || settings.RefinementTemplate != 0)
        {
            return true;
        }

        var read = Jbig2Regions.TryReadRefinementAt(ref reader, out var refinementAt);
        settings.RefinementAt = refinementAt;
        return read;
    }

    /// <summary>Processes a table segment (T.88 section 7.4.13).</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeTable(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        segment.Table = Jbig2HuffmanTable.Parse(ref reader);
        reader.AlignByte();
        return segment.Table is null ? Jbig2Status.Failure : Jbig2Status.Success;
    }

    /// <summary>Processes a symbol dictionary segment (T.88 section 7.4.2).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeSymbolDictionary(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (!TryReadSymbolDictionary(ref reader, out var settings, out var flags)
            || !Jbig2Segments.AllReferredFound(state, segment)
            || !Jbig2SegmentTables.TrySelectDictionaryTables(state, segment, settings, flags))
        {
            return Jbig2Status.Failure;
        }

        var genericLength = settings.Huffman ? 0 : Jbig2GenericParameters.ContextCountFor(settings.Template);
        var refinementLength = settings.RefinementAggregate ? Jbig2RefinementParameters.ContextCountFor(settings.RefinementTemplate) : 0;
        var reused = settings.ReuseContexts ? LastReferredDictionary(state, segment) : null;
        if (reused is not null && !ContextsMatch(reused, genericLength, refinementLength))
        {
            return Jbig2Status.Failure;
        }

        using var generic = new Jbig2ContextBuffer(genericLength, ReusedGeneric(reused));
        using var refinement = new Jbig2ContextBuffer(refinementLength, ReusedRefinement(reused));
        if (DecodeSymbols(state, ref reader, settings, Jbig2TextSegments.CollectSymbols(state, segment), generic.Span, refinement.Span) is not { } symbols)
        {
            return Jbig2Status.Failure;
        }

        segment.Symbols = new(symbols, Retain(settings, generic.Span), Retain(settings, refinement.Span));
        return Jbig2Status.Success;
    }

    /// <summary>Decodes a symbol dictionary's symbols with the right coding.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="inputs">The input symbols.</param>
    /// <param name="generic">The generic contexts.</param>
    /// <param name="refinement">The refinement contexts.</param>
    /// <returns>The exported symbols, or <see langword="null"/>.</returns>
    internal static Jbig2SymbolStore? DecodeSymbols(
        Jbig2DecodeState state,
        ref Jbig2Reader reader,
        Jbig2SymbolDictionarySettings settings,
        Jbig2SymbolSet inputs,
        Span<byte> generic,
        Span<byte> refinement)
    {
        if (settings.Huffman)
        {
            var huffman = Jbig2SymbolDictionaryDecoder.DecodeHuffman(ref reader, settings, inputs, refinement, state.Workspace);
            reader.AlignByte();
            return huffman;
        }

        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        var symbols = Jbig2SymbolDictionaryDecoder.DecodeArithmetic(ref decoder, settings, inputs, generic, refinement, state.Workspace);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        return symbols;
    }

    /// <summary>Gets the last symbol dictionary a segment refers to.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    internal static Jbig2SymbolDictionary? LastReferredDictionary(Jbig2DecodeState state, Jbig2Segment segment)
    {
        Jbig2SymbolDictionary? last = null;
        foreach (var number in segment.Referred)
        {
            if (Jbig2Segments.Find(state, number) is { Symbols: { } dictionary })
            {
                last = dictionary;
            }
        }

        return last;
    }

    /// <summary>Processes a pattern dictionary segment (T.88 section 7.4.4).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodePatternDictionary(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (!reader.TryReadByte(out var flags) || !reader.TryReadByte(out var width) || !reader.TryReadByte(out var height)
            || !reader.TryReadUInt32(out var grayMax) || grayMax > Jbig2Limits.MaxPatternIndex)
        {
            return Jbig2Status.Failure;
        }

        var template = (flags >> Jbig2Regions.TemplateShift) & Jbig2Regions.TemplateMask;
        if ((flags & Jbig2Regions.MmrFlag) != 0)
        {
            segment.Patterns = Jbig2PatternDictionary.DecodeMmr(reader.Data[reader.Offset..], width, height, (int)grayMax, state.Workspace);
        }
        else
        {
            using var contexts = new Jbig2ContextBuffer(Jbig2GenericParameters.ContextCountFor(template));
            var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
            segment.Patterns = Jbig2PatternDictionary.DecodeArithmetic(ref decoder, contexts.Span, template, width, height, (int)grayMax, state.Workspace);
            Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        }

        return segment.Patterns is null ? Jbig2Status.Failure : Jbig2Status.Success;
    }
}
