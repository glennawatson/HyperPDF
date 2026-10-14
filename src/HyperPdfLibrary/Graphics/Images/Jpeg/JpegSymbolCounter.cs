// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Counts how often each Huffman symbol occurs in a scan, so optimal tables can be built before the scan is written.</summary>
/// <param name="Frequencies">The cleared counts of every table slot, <see cref="JpegHuffmanBuilder.FrequencySlots"/> entries each.</param>
internal readonly record struct JpegSymbolCounter(int[] Frequencies) : IJpegEntropySink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Symbol(int table, int symbol, uint bits, int bitCount) => Symbol(table, symbol);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Symbol(int table, int symbol) => Frequencies[(table * JpegHuffmanBuilder.FrequencySlots) + symbol]++;

    /// <inheritdoc/>
    public void Bits(uint bits, int bitCount)
    {
    }

    /// <inheritdoc/>
    public void Restart(int index)
    {
    }

    /// <inheritdoc/>
    public void EndScan()
    {
    }
}
