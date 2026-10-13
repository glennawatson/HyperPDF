// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The symbol dictionary decoding procedure (T.88 section 6.5): height classes of symbols decoded with the generic
/// region procedure, by refinement or aggregation of earlier symbols, or, with Huffman coding, as collective bitmaps;
/// then the export flags pick the symbols the dictionary offers. Sizes and counts are checked against <see cref="Jbig2Limits"/>.
/// </summary>
internal static partial class Jbig2SymbolDictionaryDecoder
{
    /// <summary>Decodes an arithmetic-coded symbol dictionary.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="inputs">The symbols of the dictionaries it refers to.</param>
    /// <param name="genericContexts">The generic contexts.</param>
    /// <param name="refinementContexts">The refinement contexts, or an empty span without refinement.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The exported symbols, or <see langword="null"/> when the data is damaged.</returns>
    internal static Jbig2SymbolStore? DecodeArithmetic(
        ref Jbig2ArithmeticDecoder decoder,
        Jbig2SymbolDictionarySettings settings,
        Jbig2SymbolSet inputs,
        Span<byte> genericContexts,
        Span<byte> refinementContexts,
        Jbig2Workspace workspace)
    {
        var inputCount = inputs.Count;
        var created = new Jbig2SymbolStore(workspace.ScratchPools);
        using var integers = new Jbig2IntegerContexts(Jbig2Bits.CeilLog2((long)inputCount + settings.NewCount), workspace.ScratchPools);
        var aggregate = Jbig2TextRegionSettings.ForAggregate(false, integers.IdCodeLength, settings.RefinementTemplate, settings.RefinementAt);
        var context = new ArithmeticContext(settings, inputs.With(created), created, workspace, aggregate);
        if (!DecodeArithmeticSymbols(ref decoder, context, integers, genericContexts, refinementContexts))
        {
            created.Dispose();
            return null;
        }

        var flags = workspace.ScratchPools.Bytes.Rent(inputCount + settings.NewCount);
        try
        {
            var span = flags.AsSpan(0, inputCount + settings.NewCount);
            return ReadArithmeticExportFlags(ref decoder, integers, span, settings.ExportCount)
                ? Export(inputs, created, span, settings.ExportCount)
                : Discard(created);
        }
        finally
        {
            workspace.ScratchPools.Bytes.Return(flags);
        }
    }

    /// <summary>Decodes every height class.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="integers">The integer contexts.</param>
    /// <param name="genericContexts">The generic contexts.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeArithmeticSymbols(ref Jbig2ArithmeticDecoder decoder, ArithmeticContext context, Jbig2IntegerContexts integers, Span<byte> genericContexts, Span<byte> refinementContexts)
    {
        long height = 0;
        while (context.Created.Count < context.Settings.NewCount)
        {
            _ = Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.HeightDelta), out var delta);
            height += delta;
            if (height is < 0 or > Jbig2Limits.MaxImageSide
                || !DecodeArithmeticHeightClass(ref decoder, context, integers, genericContexts, refinementContexts, (int)height))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes the symbols of one height class, until the out-of-band width delta.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="integers">The integer contexts.</param>
    /// <param name="genericContexts">The generic contexts.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="height">The class height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeArithmeticHeightClass(
        ref Jbig2ArithmeticDecoder decoder,
        ArithmeticContext context,
        Jbig2IntegerContexts integers,
        Span<byte> genericContexts,
        Span<byte> refinementContexts,
        int height)
    {
        long width = 0;
        while (Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.WidthDelta), out var delta))
        {
            width += delta;
            if (context.Created.Count >= context.Settings.NewCount || width is < 0 or > Jbig2Limits.MaxImageSide)
            {
                return false;
            }

            if (height == 0 || width == 0)
            {
                context.Created.AddAbsent();
                continue;
            }

            if (!DecodeArithmeticSymbol(ref decoder, context, integers, genericContexts, refinementContexts, (int)width, height))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes one symbol and adds it to the new symbols.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="integers">The integer contexts.</param>
    /// <param name="genericContexts">The generic contexts.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="width">The symbol width.</param>
    /// <param name="height">The symbol height.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeArithmeticSymbol(
        ref Jbig2ArithmeticDecoder decoder,
        ArithmeticContext context,
        Jbig2IntegerContexts integers,
        Span<byte> genericContexts,
        Span<byte> refinementContexts,
        int width,
        int height)
    {
        var bitmap = context.Workspace.Symbol(width, height);
        if (bitmap is null || !context.Workspace.TryCharge((long)width * height))
        {
            return false;
        }

        var settings = context.Settings;
        if (!settings.RefinementAggregate)
        {
            return Jbig2GenericRegion.Decode(ref decoder, genericContexts, new(settings.Template, false, settings.At), bitmap, null)
                && context.Created.TryAdd(bitmap.View);
        }

        _ = Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.AggregateCount), out var count);
        var instances = (long)(uint)count;
        if (instances == 0)
        {
            context.Created.AddAbsent();
            return true;
        }

        var decoded = instances == 1
            ? RefineArithmetic(ref decoder, context, integers, refinementContexts, bitmap)
            : AggregateArithmetic(ref decoder, context, integers, refinementContexts, bitmap, instances);
        return decoded && context.Created.TryAdd(bitmap.View);
    }

    /// <summary>Decodes a symbol made of several refined instances, as a text region.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="integers">The integer contexts, shared with the dictionary.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="bitmap">The bitmap receiving the symbol.</param>
    /// <param name="instances">The number of instances.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool AggregateArithmetic(
        ref Jbig2ArithmeticDecoder decoder,
        ArithmeticContext context,
        Jbig2IntegerContexts integers,
        Span<byte> refinementContexts,
        Jbig2Bitmap bitmap,
        long instances)
    {
        var region = context.Aggregate;
        region.Width = bitmap.Width;
        region.Height = bitmap.Height;
        region.InstanceCount = instances;
        return Jbig2TextRegion.DecodeArithmetic(ref decoder, region, context.All, integers, refinementContexts, bitmap, context.Workspace);
    }

    /// <summary>Decodes a symbol that refines one earlier symbol.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="context">The decoding state.</param>
    /// <param name="integers">The integer contexts.</param>
    /// <param name="refinementContexts">The refinement contexts.</param>
    /// <param name="bitmap">The bitmap receiving the symbol.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool RefineArithmetic(ref Jbig2ArithmeticDecoder decoder, ArithmeticContext context, Jbig2IntegerContexts integers, Span<byte> refinementContexts, Jbig2Bitmap bitmap)
    {
        var id = Jbig2IntegerDecoder.DecodeId(ref decoder, integers.Id, integers.IdCodeLength);
        if (!context.All.TryFind(id, out var store, out var index) || !store.IsPresent(index))
        {
            return false;
        }

        _ = Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.RefineX), out var dx);
        _ = Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.RefineY), out var dy);
        var settings = context.Settings;
        var parameters = new Jbig2RefinementParameters(settings.RefinementTemplate, false, dx, dy, settings.RefinementAt);
        return Jbig2RefinementRegion.Decode(ref decoder, refinementContexts, parameters, store.Get(index), bitmap);
    }

    /// <summary>Reads the arithmetic-coded export flags.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="integers">The integer contexts.</param>
    /// <param name="flags">Receives 1 for each exported symbol.</param>
    /// <param name="exportCount">The number of symbols the dictionary says it exports.</param>
    /// <returns><see langword="false"/> when the runs overflow or export too many symbols.</returns>
    private static bool ReadArithmeticExportFlags(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers, Span<byte> flags, long exportCount)
    {
        var runs = new ExportRuns(flags);
        while (!runs.IsComplete)
        {
            _ = Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(Jbig2IntegerKind.ExportRun), out var length);
            if (!runs.TryAdd((uint)length))
            {
                return false;
            }
        }

        return runs.Exported <= exportCount;
    }

    /// <summary>Builds the exported symbols from the input and new symbols.</summary>
    /// <param name="inputs">The input symbols.</param>
    /// <param name="created">The new symbols; owned by the result or disposed.</param>
    /// <param name="flags">1 for each exported symbol, inputs first.</param>
    /// <param name="exportCount">The most symbols exported.</param>
    /// <returns>The exported symbols, or <see langword="null"/> when they pass the store limit.</returns>
    private static Jbig2SymbolStore? Export(Jbig2SymbolSet inputs, Jbig2SymbolStore created, ReadOnlySpan<byte> flags, long exportCount)
    {
        var inputCount = flags.Length - created.Count;

        // Encoders usually export exactly the new symbols; the new store is then the dictionary.
        if (created.Count <= exportCount && !flags[..inputCount].Contains((byte)1) && !flags[inputCount..].ContainsAnyExcept((byte)1))
        {
            return created;
        }

        var exported = new Jbig2SymbolStore(created.ScratchPools);
        var copied = 0;
        for (var i = 0; i < flags.Length && copied < exportCount; i++)
        {
            if (flags[i] == 0)
            {
                continue;
            }

            if (!CopySymbol(inputs, created, inputCount, i, exported))
            {
                created.Dispose();
                return Discard(exported);
            }

            copied++;
        }

        created.Dispose();
        return exported;
    }

    /// <summary>Copies one input or new symbol into the exported symbols.</summary>
    /// <param name="inputs">The input symbols.</param>
    /// <param name="created">The new symbols.</param>
    /// <param name="inputCount">The number of input symbols.</param>
    /// <param name="index">The symbol, inputs first.</param>
    /// <param name="exported">The exported symbols.</param>
    /// <returns><see langword="false"/> when the store limit is passed.</returns>
    private static bool CopySymbol(Jbig2SymbolSet inputs, Jbig2SymbolStore created, int inputCount, int index, Jbig2SymbolStore exported)
    {
        if (index >= inputCount)
        {
            return exported.TryAddCopy(created, index - inputCount);
        }

        if (!inputs.TryFind(index, out var store, out var local))
        {
            exported.AddAbsent();
            return true;
        }

        return exported.TryAddCopy(store, local);
    }

    /// <summary>Disposes a store and gives no result.</summary>
    /// <param name="store">The store.</param>
    /// <returns><see langword="null"/>.</returns>
    private static Jbig2SymbolStore? Discard(Jbig2SymbolStore store)
    {
        store.Dispose();
        return null;
    }

    /// <summary>The state shared while decoding an arithmetic symbol dictionary.</summary>
    /// <param name="Settings">The dictionary parameters.</param>
    /// <param name="All">The input symbols followed by the new symbols.</param>
    /// <param name="Created">The new symbols.</param>
    /// <param name="Workspace">The work budget and scratch bitmaps.</param>
    /// <param name="Aggregate">The text region settings reused for every aggregate symbol.</param>
    [System.Diagnostics.DebuggerDisplay("ArithmeticContext: {Created.Count} created")]
    private sealed record ArithmeticContext(
        Jbig2SymbolDictionarySettings Settings,
        Jbig2SymbolSet All,
        Jbig2SymbolStore Created,
        Jbig2Workspace Workspace,
        Jbig2TextRegionSettings Aggregate);
}
