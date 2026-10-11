// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Decodes text-region settings, symbol codes and instances.</summary>
internal static class Jbig2TextSegments
{
    /// <summary>The text region flag for Huffman coding.</summary>
    internal const int HuffmanFlag = 0x0001;

    /// <summary>The shift of the refinement flag in text region flags.</summary>
    internal const int RefineShift = 1;

    /// <summary>The shift of the log2 strip size in text region flags.</summary>
    internal const int StripShift = 2;

    /// <summary>The shift of the reference corner in text region flags.</summary>
    internal const int CornerShift = 4;

    /// <summary>The shift of the transposed flag in text region flags.</summary>
    internal const int TransposedShift = 6;

    /// <summary>The shift of the combination operator in text region flags.</summary>
    internal const int TextOperatorShift = 7;

    /// <summary>The shift of the default pixel in text region flags.</summary>
    internal const int TextDefaultShift = 9;

    /// <summary>The shift of the S offset in text region flags.</summary>
    internal const int SOffsetShift = 10;

    /// <summary>The mask of the 5-bit S offset.</summary>
    internal const int SOffsetMask = 0x1F;

    /// <summary>The smallest S offset bits that are negative.</summary>
    internal const int SOffsetSign = 0x10;

    /// <summary>The value subtracted to sign-extend the S offset.</summary>
    internal const int SOffsetRange = 0x20;

    /// <summary>The shift of the refinement template in text region flags.</summary>
    internal const int RefinementTemplateShift = 15;

    /// <summary>The mask of a two-bit field.</summary>
    internal const int TwoBits = 0x03;

    /// <summary>The run codes of a symbol ID Huffman table.</summary>
    internal const int RunCodeCount = 35;

    /// <summary>The bits of each run code length.</summary>
    internal const int RunCodeLengthBits = 4;

    /// <summary>The first run code that is not a code length.</summary>
    internal const int FirstRunCode = 32;

    /// <summary>The run code that repeats the previous length.</summary>
    internal const int RepeatCode = 32;

    /// <summary>The run code for a short run of zero lengths.</summary>
    internal const int ShortZerosCode = 33;

    /// <summary>The bits of a repeat run.</summary>
    internal const int RepeatBits = 2;

    /// <summary>The bits of a short zero run.</summary>
    internal const int ShortZeroBits = 3;

    /// <summary>The bits of a long zero run.</summary>
    internal const int LongZeroBits = 7;

    /// <summary>The shortest repeat or short zero run.</summary>
    internal const int ShortRunBase = 3;

    /// <summary>The shortest long zero run.</summary>
    internal const int LongRunBase = 11;

    /// <summary>Reads a text region's header fields.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="info">The region information.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="huffmanFlags">The Huffman table selection flags.</param>
    /// <returns><see langword="false"/> when the data ends or a value is out of range.</returns>
    internal static bool TryReadTextRegion(ref Jbig2Reader reader, out Jbig2RegionInfo info, out Jbig2TextRegionSettings settings, out ushort huffmanFlags)
    {
        settings = new();
        huffmanFlags = 0;
        if (!Jbig2RegionInfo.TryRead(ref reader, out info) || !reader.TryReadUInt16(out var flags) || !Jbig2Limits.IsValidSize(info.Width, info.Height))
        {
            return false;
        }

        ApplyTextFlags(settings, info, flags);
        if ((settings.Huffman && !reader.TryReadUInt16(out huffmanFlags)) || !TryReadTextRefinementAt(ref reader, settings))
        {
            return false;
        }

        if (!reader.TryReadUInt32(out var instances) || instances > (long)reader.Data.Length * Jbig2Limits.InstancesPerByte)
        {
            return false;
        }

        settings.InstanceCount = instances;
        return true;
    }

    /// <summary>Reads the refinement adaptive pixels when instances may be refined with template 0.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadTextRefinementAt(ref Jbig2Reader reader, Jbig2TextRegionSettings settings)
    {
        if (!settings.Refine || settings.RefinementTemplate != 0)
        {
            return true;
        }

        var read = Jbig2Regions.TryReadRefinementAt(ref reader, out var at);
        settings.RefinementAt = at;
        return read;
    }

    /// <summary>Applies the text region flags.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="info">The region information.</param>
    /// <param name="flags">The flags.</param>
    internal static void ApplyTextFlags(Jbig2TextRegionSettings settings, in Jbig2RegionInfo info, int flags)
    {
        var offset = (flags >> SOffsetShift) & SOffsetMask;
        var logStrips = (flags >> StripShift) & TwoBits;
        settings.Width = info.Width;
        settings.Height = info.Height;
        settings.Huffman = (flags & HuffmanFlag) != 0;
        settings.Refine = ((flags >> RefineShift) & 1) != 0;
        settings.Strips = 1 << logStrips;
        settings.StripBits = Math.Max(1, logStrips);
        settings.Corner = (Jbig2Corner)((flags >> CornerShift) & TwoBits);
        settings.Transposed = ((flags >> TransposedShift) & 1) != 0;
        settings.Operator = (Jbig2ComposeOperator)((flags >> TextOperatorShift) & TwoBits);
        settings.DefaultPixel = ((flags >> TextDefaultShift) & 1) != 0;
        settings.SOffset = offset >= SOffsetSign ? offset - SOffsetRange : offset;
        settings.RefinementTemplate = (flags >> RefinementTemplateShift) & 1;
    }

    /// <summary>Reads the symbol ID Huffman table that precedes Huffman-coded instances (T.88 section 7.4.3.1.7).</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters, which receive the table.</param>
    /// <param name="symbolCount">The number of symbols.</param>
    /// <returns><see langword="false"/> when the table is damaged.</returns>
    internal static bool TryReadSymbolCodes(ref Jbig2Reader reader, Jbig2TextRegionSettings settings, int symbolCount)
    {
        Span<byte> runLengths = stackalloc byte[RunCodeCount];
        for (var i = 0; i < RunCodeCount; i++)
        {
            if (!reader.TryReadBits(RunCodeLengthBits, out var length))
            {
                return false;
            }

            runLengths[i] = (byte)length;
        }

        if (Jbig2HuffmanTable.FromCodeLengths(runLengths) is not { } runCodes)
        {
            return false;
        }

        var lengths = ScratchPool<byte>.Shared.Rent(Math.Max(symbolCount, 1));
        try
        {
            var span = lengths.AsSpan(0, symbolCount);
            settings.SymbolCodes = ReadCodeLengths(ref reader, runCodes, span) ? Jbig2HuffmanTable.FromCodeLengths(span) : null;
            reader.AlignByte();
            return settings.SymbolCodes is not null;
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(lengths);
        }
    }

    /// <summary>Reads the run-coded symbol ID code lengths.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="runCodes">The run code table.</param>
    /// <param name="lengths">Receives one code length per symbol.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    internal static bool ReadCodeLengths(ref Jbig2Reader reader, Jbig2HuffmanTable runCodes, Span<byte> lengths)
    {
        var i = 0;
        while (i < lengths.Length)
        {
            if (runCodes.Decode(ref reader, out var code) != Jbig2HuffmanResult.Value)
            {
                return false;
            }

            if (code < FirstRunCode)
            {
                lengths[i] = (byte)code;
                i++;
                continue;
            }

            if (!TryReadRun(ref reader, code, out var run) || run > lengths.Length - i)
            {
                return false;
            }

            lengths.Slice(i, run).Fill(code == RepeatCode && i > 0 ? lengths[i - 1] : (byte)0);
            i += run;
        }

        return true;
    }

    /// <summary>Reads the length of a repeat or zero run.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="code">The run code: 32, 33 or 34.</param>
    /// <param name="run">The run length.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadRun(ref Jbig2Reader reader, int code, out int run)
    {
        var bits = code switch
        {
            RepeatCode => RepeatBits,
            ShortZerosCode => ShortZeroBits,
            _ => LongZeroBits,
        };
        var read = reader.TryReadBits(bits, out var extra);
        run = (int)extra + (bits == LongZeroBits ? LongRunBase : ShortRunBase);
        return read;
    }

    /// <summary>Processes a text region segment.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeTextRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, Jbig2Bitmap page)
    {
        if (!TryReadTextRegion(ref reader, out var info, out var settings, out var huffmanFlags) || !Jbig2Segments.AllReferredFound(state, segment))
        {
            return Jbig2Status.Failure;
        }

        var symbols = CollectSymbols(state, segment);
        var symbolCount = symbols.Count;
        if (!settings.Huffman)
        {
            settings.SymbolCodeLength = Jbig2Bits.CeilLog2(symbolCount);
        }
        else if (!TryReadSymbolCodes(ref reader, settings, symbolCount) || !Jbig2SegmentTables.TrySelectTextTables(state, segment, settings, huffmanFlags))
        {
            return Jbig2Status.Failure;
        }

        var region = Jbig2Bitmap.Create(settings.Width, settings.Height);
        if (region is null || !DecodeText(state, ref reader, settings, symbols, region))
        {
            region?.Dispose();
            return Jbig2Status.Failure;
        }

        return Jbig2Segments.Finish(state, segment, info, region, segment.Type == Jbig2SegmentType.IntermediateTextRegion, page);
    }

    /// <summary>Decodes a text region's instances.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="symbols">The symbols.</param>
    /// <param name="region">The region bitmap.</param>
    /// <returns><see langword="false"/> when the region cannot be decoded.</returns>
    internal static bool DecodeText(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2TextRegionSettings settings, Jbig2SymbolSet symbols, Jbig2Bitmap region)
    {
        using var refinement = new Jbig2ContextBuffer(settings.Refine ? Jbig2RefinementParameters.ContextCountFor(settings.RefinementTemplate) : 0);
        if (settings.Huffman)
        {
            var decoded = Jbig2TextRegion.DecodeHuffman(ref reader, settings, symbols, refinement.Span, region, state.Workspace);
            reader.AlignByte();
            return decoded;
        }

        using var integers = new Jbig2IntegerContexts(settings.SymbolCodeLength);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        var result = Jbig2TextRegion.DecodeArithmetic(ref decoder, settings, symbols, integers, refinement.Span, region, state.Workspace);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        return result;
    }

    /// <summary>Collects the symbols of the symbol dictionaries a segment refers to, in order.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The symbols.</returns>
    internal static Jbig2SymbolSet CollectSymbols(Jbig2DecodeState state, Jbig2Segment segment)
    {
        var symbols = new Jbig2SymbolSet();
        foreach (var number in segment.Referred)
        {
            if (Jbig2Segments.Find(state, number) is { Symbols: { } dictionary })
            {
                symbols.Add(dictionary.Symbols);
            }
        }

        return symbols;
    }
}
