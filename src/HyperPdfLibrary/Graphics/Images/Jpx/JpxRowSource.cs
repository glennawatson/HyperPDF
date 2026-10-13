// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Hands out the rows of a sub-band or a resolution one after another, top to bottom, each row exactly once. A source
/// does the work for a block of rows when its first row is asked for, so only that block is in memory at a time. A source
/// is bound to a tile-component, used, released, and bound again for the next tile.
/// </summary>
internal abstract class JpxRowSource
{
    /// <summary>Gets the samples in a row.</summary>
    internal abstract int Width { get; }

    /// <summary>Gets the number of rows the source gives.</summary>
    internal abstract int Height { get; }

    /// <summary>Gets a value indicating whether the next row is ready without any decoding.</summary>
    internal abstract bool IsReady { get; }

    /// <summary>Does the work for the next block of rows, if the next row starts one.</summary>
    internal abstract void Prepare();

    /// <summary>Gets the next row. The samples are integers, or the bits of floats for a 9/7 resolution, and the row may be changed by the caller.</summary>
    /// <returns>The row, valid until the next call.</returns>
    internal abstract Span<int> NextRow();

    /// <summary>Forgets the tile and its memory; the source may be bound again afterwards.</summary>
    internal abstract void Release();
}
