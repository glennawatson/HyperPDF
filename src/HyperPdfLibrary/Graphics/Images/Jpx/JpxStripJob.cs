// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes a run of code-blocks into a strip buffer, on several threads when the run is long enough to pay for it. One
/// job belongs to one tile-component, which runs one strip at a time, so its fields need no lock.
/// </summary>
[DebuggerDisplay("JpxStripJob: {_start}")]
internal sealed class JpxStripJob : JpxParallelJob
{
    /// <summary>The fewest code-blocks of a strip that are decoded in parallel.</summary>
    private const int ParallelBlocks = 4;

    /// <summary>The decoders and token of the decode.</summary>
    private readonly JpxBlockWorkers _workers;

    /// <summary>The tile the code-blocks belong to.</summary>
    private JpxTile _tile = null!;

    /// <summary>The tile's packet data.</summary>
    private byte[] _data = [];

    /// <summary>The code-block indices in strip order.</summary>
    private int[] _order = [];

    /// <summary>Where the coefficients go.</summary>
    private JpxBlockTarget _target;

    /// <summary>The sub-band, placed so that its first sample is where the strip buffer starts.</summary>
    private JpxBandLayout _band;

    /// <summary>The position in <see cref="_order"/> of the first code-block of the run.</summary>
    private int _start;

    /// <summary>Initializes a new instance of the <see cref="JpxStripJob"/> class.</summary>
    /// <param name="workers">The decoders and token of the decode.</param>
    internal JpxStripJob(JpxBlockWorkers workers) => _workers = workers;

    /// <summary>Decodes a run of code-blocks.</summary>
    /// <param name="tile">The tile.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    /// <param name="band">The sub-band, with its buffer position set for <paramref name="target"/>.</param>
    /// <param name="order">The code-block indices in strip order.</param>
    /// <param name="start">The position in <paramref name="order"/> of the first code-block.</param>
    /// <param name="count">The code-blocks to decode.</param>
    internal void Run(JpxTile tile, byte[] data, in JpxBlockTarget target, in JpxBandLayout band, int[] order, int start, int count)
    {
        _tile = tile;
        _data = data;
        _target = target;
        _band = band;
        _order = order;
        _start = start;
        Dispatch(count, count >= ParallelBlocks && Environment.ProcessorCount > 1);
    }

    /// <summary>Forgets the tile, its data and the strip, so an idle job keeps nothing alive.</summary>
    internal void Clear()
    {
        _tile = null!;
        _data = [];
        _order = [];
        _target = default;
        _band = default;
        _start = 0;
    }

    /// <summary>Decodes code-blocks with one decoder until none are left.</summary>
    protected override void RunWorker()
    {
        var decoder = _workers.Rent();
        try
        {
            while (TryClaim(out var index))
            {
                JpxBlockDecoder.Decode(decoder, _tile, _order[_start + index], _data, _target, _band, _workers.Token);
            }
        }
        finally
        {
            _workers.Return(decoder);
        }
    }
}
