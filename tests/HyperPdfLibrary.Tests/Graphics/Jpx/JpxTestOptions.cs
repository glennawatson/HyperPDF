// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>What the test encoder writes: a lossless 5/3 codestream of 8-bit samples.</summary>
[DebuggerDisplay("JpxTestOptions: {Width}x{Height}, {Components} components, {Order}")]
internal sealed record JpxTestOptions
{
    /// <summary>The default image width.</summary>
    private const int DefaultWidth = 61;

    /// <summary>The default image height.</summary>
    private const int DefaultHeight = 45;

    /// <summary>The default decomposition levels.</summary>
    private const int DefaultLevels = 3;

    /// <summary>The default code-block side as a power of two.</summary>
    private const int DefaultBlockExponent = 4;

    /// <summary>Gets the image width.</summary>
    internal int Width { get; init; } = DefaultWidth;

    /// <summary>Gets the image height.</summary>
    internal int Height { get; init; } = DefaultHeight;

    /// <summary>Gets the left edge of the image on the reference grid.</summary>
    internal int X0 { get; init; }

    /// <summary>Gets the top edge of the image on the reference grid.</summary>
    internal int Y0 { get; init; }

    /// <summary>Gets the number of components.</summary>
    internal int Components { get; init; } = 1;

    /// <summary>Gets the subsampling of the second and later components.</summary>
    internal int ChromaSubsampling { get; init; } = 1;

    /// <summary>Gets the decomposition levels.</summary>
    internal int Levels { get; init; } = DefaultLevels;

    /// <summary>Gets the code-block side as a power of two.</summary>
    internal int BlockExponent { get; init; } = DefaultBlockExponent;

    /// <summary>Gets the code-block mode switches.</summary>
    internal JpxBlockStyle Style { get; init; }

    /// <summary>Gets the progression order.</summary>
    internal JpxProgressionOrder Order { get; init; }

    /// <summary>Gets the quality layers.</summary>
    internal int Layers { get; init; } = 1;

    /// <summary>Gets the precinct side as a power of two, or zero for the default single precinct.</summary>
    internal int PrecinctExponent { get; init; }

    /// <summary>Gets the tile width, or zero for one tile.</summary>
    internal int TileWidth { get; init; }

    /// <summary>Gets the tile height, or zero for one tile.</summary>
    internal int TileHeight { get; init; }

    /// <summary>Gets a value indicating whether the reversible component transform is used.</summary>
    internal bool Transform { get; init; }

    /// <summary>Gets a value indicating whether SOP and EPH markers are written.</summary>
    internal bool Markers { get; init; }

    /// <summary>Gets a value indicating whether packet headers go in PPT markers.</summary>
    internal bool PackedHeaders { get; init; }

    /// <summary>Gets a value indicating whether each tile is split into two tile-parts.</summary>
    internal bool SplitTileParts { get; init; }

    /// <summary>Gets a value indicating whether the codestream is wrapped in JP2 boxes.</summary>
    internal bool Wrap { get; init; }

    /// <summary>Gets a value indicating whether HT code-blocks also send SigProp and MagRef passes where they stay lossless.</summary>
    internal bool HtRefinement { get; init; }

    /// <summary>Gets the HT placeholder sets (P0) written before each HT code-block's first cleanup pass: three placeholder passes each, as many as the block's zero bit-planes allow.</summary>
    internal int HtPlaceholderSets { get; init; }

    /// <summary>
    /// Gets the maxshift region-of-interest shift written in RGN markers, or zero for none. Every third coefficient of
    /// each sub-band is treated as inside the region and shifted up.
    /// </summary>
    internal int RoiShift { get; init; }

    /// <summary>Gets the progression volumes written in a POC marker, or <see langword="null"/> for none.</summary>
    internal JpxProgressionChange[]? Changes { get; init; }
}
