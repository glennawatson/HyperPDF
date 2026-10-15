// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <content>The Huffman-coded symbol dictionary decoding procedure.</content>
internal static partial class Jbig2SymbolDictionaryDecoder
{
    /// <summary>The bytes after a refinement's arithmetic data that its size includes.</summary>
    private const int RefinementTrailer = 2;

    /// <summary>Decodes a Huffman-coded symbol dictionary.</summary>
    /// <param name="reader">The reader, at the coded symbols.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="inputs">The symbols of the dictionaries it refers to.</param>
    /// <param name="refinementContexts">The refinement contexts, or an empty span without refinement.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The exported symbols, or <see langword="null"/> when the data is damaged.</returns>
    internal static Jbig2SymbolStore? DecodeHuffman(ref Jbig2Reader reader, Jbig2SymbolDictionarySettings settings, Jbig2SymbolSet inputs, Span<byte> refinementContexts, Jbig2Workspace workspace)
    {
        var total = inputs.Count + settings.NewCount;
        var created = Jbig2SymbolStore.Rent(workspace);
        var widths = workspace.ScratchPools.Ints.Rent(Math.Max(settings.NewCount, 1));
        var flags = workspace.ScratchPools.Bytes.Rent(Math.Max(total, 1));
        using var all = inputs.With(created);
        try
        {
            var context = new HuffmanContext(settings, all, created, workspace, widths, Math.Max(Jbig2Bits.CeilLog2(total), 1));
            return !DecodeHuffmanSymbols(ref reader, ref context, refinementContexts) || !ReadHuffmanExportFlags(ref reader, flags.AsSpan(0, total), settings.ExportCount)
                ? Discard(created)
                : Export(inputs, created, flags.AsSpan(0, total), settings.ExportCount, workspace);
        }
        finally
        {
            workspace.ScratchPools.Ints.Return(widths);
            workspace.ScratchPools.Bytes.Return(flags);
        }
    }

    /// <summary>Decodes every height class.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeHuffmanSymbols(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts)
    {
        long height = 0;
        while (context.Decoded < context.Settings.NewCount)
        {
            if (context.Settings.HeightDelta.Decode(ref reader, out var delta) != Jbig2HuffmanResult.Value)
            {
                return false;
            }

            height += delta;
            if (height is < 0 or > Jbig2Limits.MaxImageSide || !DecodeHuffmanHeightClass(ref reader, ref context, refinementContexts, (int)height))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes the symbols of one height class, then its collective bitmap when symbols are not refined.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="height">The class height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeHuffmanHeightClass(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts, int height)
    {
        var first = context.Decoded;
        long width = 0;
        long totalWidth = 0;
        while (true)
        {
            var result = context.Settings.WidthDelta.Decode(ref reader, out var delta);
            if (result == Jbig2HuffmanResult.OutOfBand)
            {
                break;
            }

            width += delta;
            if (result != Jbig2HuffmanResult.Value || context.Decoded >= context.Settings.NewCount || width is < 0 or > Jbig2Limits.MaxImageSide)
            {
                return false;
            }

            totalWidth += width;
            if (!DecodeHuffmanSymbol(ref reader, ref context, refinementContexts, (int)width, height))
            {
                return false;
            }

            context.Decoded++;
        }

        return context.Settings.RefinementAggregate || ReadCollectiveBitmap(ref reader, ref context, first, totalWidth, height);
    }

    /// <summary>Decodes one symbol of a height class, or records its width for the collective bitmap.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="width">The symbol width.</param>
    /// <param name="height">The symbol height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeHuffmanSymbol(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts, int width, int height)
    {
        var empty = width == 0 || height == 0;
        if (!context.Settings.RefinementAggregate)
        {
            context.Widths[context.Decoded] = empty ? 0 : width;
            return true;
        }

        if (empty)
        {
            context.Created.AddAbsent();
            return true;
        }

        return DecodeHuffmanAggregate(ref reader, ref context, refinementContexts, width, height);
    }

    /// <summary>Decodes a refinement/aggregate symbol: its instance count, then the refined or aggregated bitmap.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="width">The symbol width.</param>
    /// <param name="height">The symbol height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeHuffmanAggregate(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts, int width, int height)
    {
        if (context.Settings.AggregateCount.Decode(ref reader, out var count) != Jbig2HuffmanResult.Value)
        {
            return false;
        }

        var instances = (long)(uint)count;
        if (instances == 0)
        {
            context.Created.AddAbsent();
            return true;
        }

        var bitmap = context.Workspace.Symbol(width, height);
        if (bitmap is null || !context.Workspace.TryCharge((long)width * height))
        {
            return false;
        }

        var decoded = instances == 1
            ? RefineHuffman(ref reader, ref context, refinementContexts, bitmap)
            : AggregateHuffman(ref reader, ref context, refinementContexts, bitmap, instances);
        return decoded && context.Created.TryAdd(bitmap.View);
    }

    /// <summary>Decodes a symbol made of several refined instances, as a Huffman-coded text region.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="bitmap">The bitmap receiving the symbol.</param>
    /// <param name="instances">The number of instances.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool AggregateHuffman(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts, Jbig2Bitmap bitmap, long instances)
    {
        var region = context.Aggregate;
        region.Width = bitmap.Width;
        region.Height = bitmap.Height;
        region.InstanceCount = instances;
        return Jbig2TextRegion.DecodeHuffman(ref reader, region, context.All, refinementContexts, bitmap, context.Workspace);
    }

    /// <summary>Decodes a symbol that refines one earlier symbol, checking the size of its arithmetic data.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="bitmap">The bitmap receiving the symbol.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool RefineHuffman(ref Jbig2Reader reader, ref HuffmanContext context, Span<byte> refinementContexts, Jbig2Bitmap bitmap)
    {
        if (!reader.TryReadBits(context.CodeLength, out var id) || !context.All.TryFind(id, out var store, out var index) || !store.IsPresent(index))
        {
            return false;
        }

        var deltas = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B15);
        if (deltas.Decode(ref reader, out var dx) != Jbig2HuffmanResult.Value
            || deltas.Decode(ref reader, out var dy) != Jbig2HuffmanResult.Value
            || Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1).Decode(ref reader, out var size) != Jbig2HuffmanResult.Value)
        {
            return false;
        }

        reader.AlignByte();
        var start = reader.Offset;
        var settings = context.Settings;
        var parameters = new Jbig2RefinementParameters(settings.RefinementTemplate, false, dx, dy, settings.RefinementAt);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, start, reader.CancellationToken);
        var decoded = Jbig2RefinementRegion.Decode(ref decoder, refinementContexts, parameters, store.Get(index), bitmap);
        reader.Offset = decoder.Position;
        reader.AlignByte();
        reader.Skip(RefinementTrailer);
        return decoded && reader.Offset - start == (uint)size;
    }

    /// <summary>Reads a height class's collective bitmap, uncompressed or MMR coded, and splits it into symbols.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="first">The first symbol of the class.</param>
    /// <param name="totalWidth">The sum of the class's symbol widths.</param>
    /// <param name="height">The class height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool ReadCollectiveBitmap(ref Jbig2Reader reader, ref HuffmanContext context, int first, long totalWidth, int height)
    {
        if (context.Settings.BitmapSize.Decode(ref reader, out var size) != Jbig2HuffmanResult.Value)
        {
            return false;
        }

        reader.AlignByte();
        if (totalWidth == 0 || height == 0)
        {
            reader.Skip((uint)size);
            return AddEmptySymbols(ref context, first, height);
        }

        using var collective = Jbig2Limits.IsValidSize(totalWidth, height) ? context.Workspace.RentBitmap((int)totalWidth, height) : null;
        if (collective is null || !context.Workspace.TryCharge(totalWidth * height) || !ReadCollectiveRows(ref reader, collective, (uint)size))
        {
            return false;
        }

        var x = 0;
        for (var i = first; i < context.Decoded; i++)
        {
            var width = context.Widths[i];
            if (!context.Created.TryAddColumns(collective.View, x, width))
            {
                return false;
            }

            x += width;
        }

        return true;
    }

    /// <summary>Reads a collective bitmap's rows.</summary>
    /// <param name="reader">The reader, after the bitmap size and alignment.</param>
    /// <param name="collective">The bitmap.</param>
    /// <param name="size">The coded size: 0 for uncompressed rows, else the bytes of MMR data.</param>
    /// <returns><see langword="false"/> when the data is too short.</returns>
    private static bool ReadCollectiveRows(ref Jbig2Reader reader, Jbig2Bitmap collective, long size)
    {
        if (size == 0)
        {
            var length = collective.Data.Length;
            if (reader.BytesLeft < length)
            {
                return false;
            }

            reader.Data.Slice(reader.Offset, length).CopyTo(collective.Data);
            reader.Skip(length);
            ClearRowPadding(collective);
            return true;
        }

        _ = Jbig2GenericRegion.DecodeMmr(reader.Data[reader.Offset..], collective);
        reader.Skip(size);
        return true;
    }

    /// <summary>Clears the padding bits after the last pixel of every row of uncompressed data.</summary>
    /// <param name="bitmap">The bitmap.</param>
    private static void ClearRowPadding(Jbig2Bitmap bitmap)
    {
        var padding = (bitmap.Stride << Jbig2Bits.ByteShift) - bitmap.Width;
        if (padding == 0)
        {
            return;
        }

        var mask = (byte)(byte.MaxValue << padding);
        for (var y = 0; y < bitmap.Height; y++)
        {
            bitmap.Row(y)[^1] &= mask;
        }
    }

    /// <summary>Adds the symbols of a class whose collective bitmap has no pixels: each keeps its size but has no data.</summary>
    /// <param name="context">The decoding state.</param>
    /// <param name="first">The first symbol of the class.</param>
    /// <param name="height">The class height.</param>
    /// <returns><see langword="true"/>.</returns>
    private static bool AddEmptySymbols(ref HuffmanContext context, int first, int height)
    {
        for (var i = first; i < context.Decoded; i++)
        {
            // One side is zero, so the symbol has no bytes to copy.
            _ = context.Created.TryAdd(new(default, context.Widths[i], height));
        }

        return true;
    }

    /// <summary>Reads the Huffman-coded export flags with table B.1.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="flags">Receives 1 for each exported symbol.</param>
    /// <param name="exportCount">The number of symbols the dictionary says it exports.</param>
    /// <returns><see langword="false"/> when the data is damaged or exports too many symbols.</returns>
    private static bool ReadHuffmanExportFlags(ref Jbig2Reader reader, Span<byte> flags, long exportCount)
    {
        var table = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);
        var runs = new ExportRuns(flags);
        while (!runs.IsComplete)
        {
            if (table.Decode(ref reader, out var length) != Jbig2HuffmanResult.Value || !runs.TryAdd((uint)length))
            {
                return false;
            }
        }

        return runs.Exported <= exportCount;
    }

    /// <summary>Marks the exported symbols from alternating runs of not-exported and exported symbols.</summary>
    private ref struct ExportRuns
    {
        /// <summary>The most runs per symbol, plus one, that valid data can hold.</summary>
        private const int RunsPerSymbol = 2;

        /// <summary>The flags.</summary>
        private readonly Span<byte> _flags;

        /// <summary>The next symbol.</summary>
        private int _index;

        /// <summary>Whether the next run is exported.</summary>
        private bool _exporting;

        /// <summary>The runs read so far.</summary>
        private long _runs;

        /// <summary>Initializes a new instance of the <see cref="ExportRuns"/> struct, clearing the flags.</summary>
        /// <param name="flags">The flags, one per symbol.</param>
        internal ExportRuns(Span<byte> flags)
        {
            _flags = flags;
            flags.Clear();
        }

        /// <summary>Gets a value indicating whether every symbol has a flag.</summary>
        internal readonly bool IsComplete => _index >= _flags.Length;

        /// <summary>Gets the number of symbols exported so far.</summary>
        internal long Exported { get; private set; }

        /// <summary>Adds a run.</summary>
        /// <param name="length">The run length.</param>
        /// <returns><see langword="false"/> when the run passes the last symbol or there are more runs than symbols allow.</returns>
        internal bool TryAdd(long length)
        {
            _runs++;

            // Valid runs alternate and each pair covers at least one symbol, so more runs than this mean a loop.
            if (length > _flags.Length - _index || _runs > ((long)_flags.Length + 1) * RunsPerSymbol)
            {
                return false;
            }

            if (_exporting)
            {
                _flags.Slice(_index, (int)length).Fill(1);
                Exported += length;
            }

            _index += (int)length;
            _exporting = !_exporting;
            return true;
        }
    }

    /// <summary>The state shared while decoding a Huffman symbol dictionary.</summary>
    [DebuggerDisplay("HuffmanContext: {Decoded} decoded")]
    private struct HuffmanContext
    {
        /// <summary>Initializes a new instance of the <see cref="HuffmanContext"/> struct.</summary>
        /// <param name="settings">The dictionary parameters.</param>
        /// <param name="all">The input symbols followed by the new symbols.</param>
        /// <param name="created">The new symbols.</param>
        /// <param name="workspace">The work budget and scratch bitmaps.</param>
        /// <param name="widths">The widths of the symbols awaiting their collective bitmap.</param>
        /// <param name="codeLength">The bits of a symbol ID in a refinement.</param>
        internal HuffmanContext(Jbig2SymbolDictionarySettings settings, Jbig2SymbolSet all, Jbig2SymbolStore created, Jbig2Workspace workspace, int[] widths, int codeLength)
        {
            Settings = settings;
            All = all;
            Created = created;
            Workspace = workspace;
            Widths = widths;
            CodeLength = codeLength;
            Aggregate = workspace.AggregateSettings;
            Aggregate.ConfigureAggregate(true, codeLength, settings.RefinementTemplate, settings.RefinementAt);
        }

        /// <summary>Gets the dictionary parameters.</summary>
        internal readonly Jbig2SymbolDictionarySettings Settings { get; }

        /// <summary>Gets the input symbols followed by the new symbols.</summary>
        internal readonly Jbig2SymbolSet All { get; }

        /// <summary>Gets the new symbols.</summary>
        internal readonly Jbig2SymbolStore Created { get; }

        /// <summary>Gets the work budget and scratch bitmaps.</summary>
        internal readonly Jbig2Workspace Workspace { get; }

        /// <summary>Gets the widths of the symbols awaiting their collective bitmap.</summary>
        internal readonly int[] Widths { get; }

        /// <summary>Gets the bits of a symbol ID in a refinement.</summary>
        internal readonly int CodeLength { get; }

        /// <summary>Gets the text region settings reused for every aggregate symbol.</summary>
        internal readonly Jbig2TextRegionSettings Aggregate { get; }

        /// <summary>Gets or sets the number of symbols decoded, NSYMSDECODED.</summary>
        internal int Decoded { readonly get; set; }
    }
}
