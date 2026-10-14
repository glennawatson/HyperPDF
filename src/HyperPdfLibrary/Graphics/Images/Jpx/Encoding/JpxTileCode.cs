// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The coded code-blocks of one tile: for each block its bit-planes and passes, the bytes of its segments joined, and for
/// each pass the number of bytes of its segment that a decoder needs to decode the passes so far. The stores are pooled and
/// reused from tile to tile.
/// </summary>
[DebuggerDisplay("JpxTileCode: {Blocks.Count} code-blocks, {Data.Count} bytes")]
internal sealed class JpxTileCode : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="JpxTileCode"/> class.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxTileCode(ScratchPools scratchPools)
    {
        Blocks = new(scratchPools);
        Data = new(scratchPools);
        PassEnds = new(scratchPools);
    }

    /// <summary>Gets the coded blocks, by the tile's code-block index.</summary>
    internal JpxList<JpxCodedBlock> Blocks { get; }

    /// <summary>Gets the bytes of every block, block after block, segment after segment.</summary>
    internal JpxList<byte> Data { get; }

    /// <summary>Gets the pass lengths: for every pass, the bytes of its segment that cover the passes of the segment up to it.</summary>
    internal JpxList<int> PassEnds { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Blocks.Dispose();
        Data.Dispose();
        PassEnds.Dispose();
    }

    /// <summary>Empties the stores for a tile with a number of code-blocks.</summary>
    /// <param name="blocks">The code-blocks.</param>
    internal void Reset(int blocks)
    {
        Blocks.Clear();
        Data.Clear();
        PassEnds.Clear();
        _ = Blocks.AddDefault(blocks);
    }

    /// <summary>Appends a segment's bytes to the data store.</summary>
    /// <param name="bytes">The bytes.</param>
    internal void AppendData(ReadOnlySpan<byte> bytes)
    {
        var start = Data.AddDefault(bytes.Length);
        bytes.CopyTo(Data.AsSpan()[start..]);
    }
}
