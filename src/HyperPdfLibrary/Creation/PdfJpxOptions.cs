// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>How <see cref="PdfJpxEncoder"/> compresses an image. The defaults give a lossless file with one tile, one layer and 64 by 64 code-blocks.</summary>
[DebuggerDisplay("PdfJpxOptions: {Mode}, quality {Quality}, {Layers} layers")]
public sealed record PdfJpxOptions
{
    /// <summary>The default quality of a lossy file.</summary>
    internal const int DefaultQuality = 80;

    /// <summary>The default code-block side.</summary>
    internal const int DefaultCodeBlockSize = 64;

    /// <summary>Gets the options that give a lossless file.</summary>
    public static PdfJpxOptions Lossless { get; } = new();

    /// <summary>Gets the options that give a lossy file at quality 80.</summary>
    public static PdfJpxOptions Lossy { get; } = new() { Mode = PdfJpxMode.Lossy };

    /// <summary>Gets whether the file keeps every sample or trades detail for size.</summary>
    public PdfJpxMode Mode { get; init; }

    /// <summary>
    /// Gets the quality of a lossy file, from 1 (smallest) to 100 (the finest step). It sets the quantization step of every
    /// sub-band: each 16 points halve the step. A lossless file ignores it.
    /// </summary>
    public int Quality { get; init; } = DefaultQuality;

    /// <summary>Gets the wavelet decomposition levels from 0 to 32, or <see langword="null"/> to choose from the tile size (up to 5).</summary>
    public int? DecompositionLevels { get; init; }

    /// <summary>Gets the code-block width: a power of two from 4 to 1024. The width times the height is at most 4096.</summary>
    public int CodeBlockWidth { get; init; } = DefaultCodeBlockSize;

    /// <summary>Gets the code-block height: a power of two from 4 to 1024. The width times the height is at most 4096.</summary>
    public int CodeBlockHeight { get; init; } = DefaultCodeBlockSize;

    /// <summary>Gets the quality layers from 1 to 65535. Each layer adds detail; a decoder may stop after any of them.</summary>
    public int Layers { get; init; } = 1;

    /// <summary>Gets the tile width, or zero to use one tile for a small image and 2048 sample tiles for a large one.</summary>
    public int TileWidth { get; init; }

    /// <summary>Gets the tile height, or zero to use one tile for a small image and 2048 sample tiles for a large one.</summary>
    public int TileHeight { get; init; }

    /// <summary>Gets the precinct side: a power of two from 2 up, or zero for one precinct per resolution.</summary>
    public int PrecinctSize { get; init; }

    /// <summary>Gets the packet order.</summary>
    public PdfJpxProgression Progression { get; init; } = PdfJpxProgression.ResolutionLayerComponentPosition;

    /// <summary>Gets a value indicating whether three colour channels go through the colour transform (reversible for a lossless file, irreversible for a lossy one).</summary>
    public bool ColorTransform { get; init; } = true;

    /// <summary>Gets the code-block mode switches.</summary>
    public PdfJpxBlockStyle BlockStyle { get; init; }

    /// <summary>Gets a value indicating whether each packet starts with an SOP marker segment.</summary>
    public bool StartOfPacketMarkers { get; init; }

    /// <summary>Gets a value indicating whether each packet header ends with an EPH marker.</summary>
    public bool EndOfPacketHeaderMarkers { get; init; }

    /// <summary>Gets a value indicating whether the codestream is wrapped in the JP2 file format. PDF needs the wrapper (ISO 32000-2 section 7.4.9).</summary>
    public bool WriteFileFormat { get; init; } = true;
}
