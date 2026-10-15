// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The tables, counts and components a JPEG encode builds. Finished encodes return the state to their scratch owner
/// for another image from that owner.
/// </summary>
[DebuggerDisplay("JpegEncoderState")]
internal sealed class JpegEncoderState
{
    /// <summary>The most components a frame can have.</summary>
    internal const int MaxComponents = 4;

    /// <summary>The quantization table slots: luminance and chrominance.</summary>
    internal const int QuantSlots = 2;

    /// <summary>Initializes a new instance of the <see cref="JpegEncoderState"/> class.</summary>
    private JpegEncoderState()
    {
        for (var i = 0; i < MaxComponents; i++)
        {
            Components[i] = new();
        }
    }

    /// <summary>Gets the components; a frame uses the first few.</summary>
    internal JpegEncoderComponent[] Components { get; } = new JpegEncoderComponent[MaxComponents];

    /// <summary>Gets the quantization steps of both slots in zigzag order, 64 bytes each.</summary>
    internal byte[] QuantSteps { get; } = new byte[QuantSlots * JpegBlock.Length];

    /// <summary>Gets the quantization reciprocals of both slots in storage order, 64 ints each.</summary>
    internal int[] QuantReciprocals { get; } = new int[QuantSlots * JpegBlock.Length];

    /// <summary>Gets the symbol counts of the four Huffman table slots, <see cref="JpegHuffmanBuilder.FrequencySlots"/> each.</summary>
    internal int[] Frequencies { get; } = new int[JpegHuffmanBuilder.TableSlots * JpegHuffmanBuilder.FrequencySlots];

    /// <summary>Gets the code counts of the four table slots, 16 bytes each.</summary>
    internal byte[] TableCounts { get; } = new byte[JpegHuffmanBuilder.TableSlots * JpegHuffmanBuilder.MaxCodeLength];

    /// <summary>Gets the symbols of the four table slots, 256 bytes each.</summary>
    internal byte[] TableValues { get; } = new byte[JpegHuffmanBuilder.TableSlots * JpegHuffmanBuilder.SymbolSlots];

    /// <summary>Gets the number of symbols in each table slot; zero for a slot with no table.</summary>
    internal int[] TableSizes { get; } = new int[JpegHuffmanBuilder.TableSlots];

    /// <summary>Gets the code lookup of the four table slots, 256 entries each.</summary>
    internal uint[] Codes { get; } = new uint[JpegHuffmanBuilder.TableSlots * JpegHuffmanBuilder.SymbolSlots];

    /// <summary>Gets the correction bits that wait for the end of a block run in a progressive scan.</summary>
    internal byte[] CorrectionBits { get; } = new byte[JpegProgressiveScan.CorrectionCapacity];

    /// <summary>Takes a state that is ready for a new image.</summary>
    /// <param name="scratchPools">The encoder's scratch owner.</param>
    /// <returns>The state, which the caller returns with <see cref="Return"/>.</returns>
    internal static JpegEncoderState Rent(ScratchPools scratchPools) => scratchPools.Slots<JpegEncoderState>().Take() ?? new();

    /// <summary>Gives a state back after releasing what its components borrowed.</summary>
    /// <param name="state">The state.</param>
    /// <param name="scratchPools">The encoder's scratch owner.</param>
    internal static void Return(JpegEncoderState state, ScratchPools scratchPools)
    {
        foreach (var component in state.Components)
        {
            component.Dispose();
        }

        _ = scratchPools.Slots<JpegEncoderState>().Offer(state);
    }

    /// <summary>Clears the symbol counts before a counting pass.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearFrequencies() => Frequencies.AsSpan().Clear();

    /// <summary>Builds the table of every slot that has counts, and forgets the others.</summary>
    internal void BuildOptimalTables()
    {
        for (var slot = 0; slot < JpegHuffmanBuilder.TableSlots; slot++)
        {
            var counts = TableCounts.AsSpan(slot * JpegHuffmanBuilder.MaxCodeLength, JpegHuffmanBuilder.MaxCodeLength);
            var values = TableValues.AsSpan(slot * JpegHuffmanBuilder.SymbolSlots, JpegHuffmanBuilder.SymbolSlots);
            var frequencies = Frequencies.AsSpan(slot * JpegHuffmanBuilder.FrequencySlots, JpegHuffmanBuilder.FrequencySlots);
            TableSizes[slot] = JpegHuffmanBuilder.BuildOptimal(frequencies, counts, values);
            if (TableSizes[slot] > 0)
            {
                JpegHuffmanBuilder.BuildCodes(counts, values, Codes.AsSpan(slot * JpegHuffmanBuilder.SymbolSlots, JpegHuffmanBuilder.SymbolSlots));
            }
        }
    }

    /// <summary>Sets a table slot to a table of Annex K.3.</summary>
    /// <param name="slot">The table slot.</param>
    /// <param name="counts">The code counts.</param>
    /// <param name="values">The symbols.</param>
    internal void SetTable(int slot, ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
    {
        counts.CopyTo(TableCounts.AsSpan(slot * JpegHuffmanBuilder.MaxCodeLength));
        values.CopyTo(TableValues.AsSpan(slot * JpegHuffmanBuilder.SymbolSlots));
        TableSizes[slot] = values.Length;
        JpegHuffmanBuilder.BuildCodes(counts, values, Codes.AsSpan(slot * JpegHuffmanBuilder.SymbolSlots, JpegHuffmanBuilder.SymbolSlots));
    }

    /// <summary>Forgets every table.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearTables() => TableSizes.AsSpan().Clear();
}
