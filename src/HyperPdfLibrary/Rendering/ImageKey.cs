// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies one decoded resolution, ICC rendering and transfer function of an image stream.</summary>
/// <param name="Stream">The source image.</param>
/// <param name="ReductionLevels">The finest wavelet levels omitted, or zero for full size.</param>
/// <param name="RenderingSlot">The packed ICC intent and black point choice.</param>
/// <param name="Transfer">The transfer instance applied to the decoded pixels, or null for none.</param>
internal readonly record struct ImageKey(PdfStream Stream, int ReductionLevels, byte RenderingSlot, PdfTransfer? Transfer)
{
    /// <summary>The bit selecting black point compensation.</summary>
    private const int CompensationBit = 1;

    /// <summary>Initializes a new instance of the <see cref="ImageKey"/> struct for the initial graphics state's rendering.</summary>
    /// <param name="stream">The source image.</param>
    /// <param name="reductionLevels">The finest wavelet levels omitted, or zero for full size.</param>
    internal ImageKey(PdfStream stream, int reductionLevels)
        : this(stream, reductionLevels, IccRendering.Default, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageKey"/> struct without a transfer function.</summary>
    /// <param name="stream">The source image.</param>
    /// <param name="reductionLevels">The finest wavelet levels omitted, or zero for full size.</param>
    /// <param name="rendering">The rendering intent and black point choice.</param>
    internal ImageKey(PdfStream stream, int reductionLevels, IccRendering rendering)
        : this(stream, reductionLevels, rendering, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageKey"/> struct for one transfer instance.</summary>
    /// <param name="stream">The source image.</param>
    /// <param name="reductionLevels">The omitted wavelet levels.</param>
    /// <param name="rendering">The ICC conversion choice.</param>
    /// <param name="transfer">The transfer instance, or null for none.</param>
    internal ImageKey(PdfStream stream, int reductionLevels, IccRendering rendering, PdfTransfer? transfer)
        : this(stream, reductionLevels, (byte)rendering.Slot, transfer)
    {
    }

    /// <summary>Gets the ICC conversion choice packed in this key.</summary>
    internal IccRendering Rendering => new((IccIntent)(RenderingSlot >> CompensationBit), (RenderingSlot & CompensationBit) != 0);
}
