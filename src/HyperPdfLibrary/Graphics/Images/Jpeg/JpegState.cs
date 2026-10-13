// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The tables and components a JPEG decode builds from the file's headers. The objects are the same for every image, so
/// finished decodes hand their state back and the next decode on any thread takes it, instead of building it again.
/// </summary>
[DebuggerDisplay("JpegState")]
internal sealed class JpegState
{
    /// <summary>The Huffman and quantization table ids a JPEG can use.</summary>
    internal const int TableSlots = 4;

    /// <summary>The most components a frame can have.</summary>
    internal const int MaxComponents = 4;

    /// <summary>The states waiting for the next decode.</summary>
    private static readonly ReusableSlots<JpegState> Idle = new();

    /// <summary>Initializes a new instance of the <see cref="JpegState"/> class.</summary>
    private JpegState()
    {
        for (var i = 0; i < TableSlots; i++)
        {
            DcTables[i] = new();
            AcTables[i] = new();
            Quantization[i] = new int[JpegBlock.Length];
        }

        for (var i = 0; i < MaxComponents; i++)
        {
            Components[i] = new();
        }
    }

    /// <summary>Gets the DC Huffman tables by id.</summary>
    internal JpegHuffmanTable[] DcTables { get; } = new JpegHuffmanTable[TableSlots];

    /// <summary>Gets the AC Huffman tables by id.</summary>
    internal JpegHuffmanTable[] AcTables { get; } = new JpegHuffmanTable[TableSlots];

    /// <summary>Gets the quantization tables by id, in storage order; <see cref="QuantizationDefined"/> says which the file gave.</summary>
    internal int[][] Quantization { get; } = new int[TableSlots][];

    /// <summary>Gets which quantization tables the file has defined.</summary>
    internal bool[] QuantizationDefined { get; } = new bool[TableSlots];

    /// <summary>Gets the components; a frame uses the first few.</summary>
    internal JpegComponent[] Components { get; } = new JpegComponent[MaxComponents];

    /// <summary>Takes a state that is ready for a new image.</summary>
    /// <returns>The state, which the caller returns with <see cref="Return"/>.</returns>
    internal static JpegState Rent()
    {
        var state = Idle.Take() ?? new();
        for (var i = 0; i < TableSlots; i++)
        {
            state.DcTables[i].Undefine();
            state.AcTables[i].Undefine();
            state.QuantizationDefined[i] = false;
        }

        return state;
    }

    /// <summary>Gives a state back after releasing what its components borrowed.</summary>
    /// <param name="state">The state.</param>
    internal static void Return(JpegState state)
    {
        foreach (var component in state.Components)
        {
            component.Dispose();
        }

        _ = Idle.Offer(state);
    }
}
