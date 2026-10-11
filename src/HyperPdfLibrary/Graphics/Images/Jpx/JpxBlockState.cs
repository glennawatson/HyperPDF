// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Owns one worker's reusable code-block buffers and geometry.</summary>
/// <remarks>Buffers store four stripe rows in adjacent bytes so one 32-bit read examines a column.</remarks>
[DebuggerDisplay("JpxBlockDecoder: {_width}x{_height}")]
internal sealed class JpxBlockState : IDisposable
{
    /// <summary>The cache this worker returns its buffers to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The coefficients, twice scale, in stripe order.</summary>
    private int[] _values = [];

    /// <summary>The coefficient flags, in stripe order.</summary>
    private byte[] _flags = [];

    /// <summary>The neighbourhood codes, in stripe order.</summary>
    private byte[] _neighbours = [];

    /// <summary>The joined code-block data when it arrived in pieces.</summary>
    private byte[] _joined = [];

    /// <summary>The code-block width.</summary>
    private int _width;

    /// <summary>The code-block height.</summary>
    private int _height;

    /// <summary>The number of stripes.</summary>
    private int _stripes;

    /// <summary>The distance between stripes in the state buffers.</summary>
    private int _stripeStride;

    /// <summary>The used length of the state buffers.</summary>
    private int _length;

    /// <summary>The offset of the current orientation's zero-coding table.</summary>
    private int _zeroTable;

    /// <summary>Whether the vertically causal mode is on.</summary>
    private bool _causal;

    /// <summary>Initializes a new instance of the <see cref="JpxBlockState"/> class.</summary>
    internal JpxBlockState()
        : this(new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JpxBlockState"/> class with an explicit cache.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxBlockState(ScratchPools scratchPools) => _scratchPools = scratchPools;

    /// <summary>Gets the arithmetic coding context states.</summary>
    internal byte[] Contexts { get; } = new byte[JpxContexts.Count];

    /// <summary>Gets the worker's scratch cache.</summary>
    internal ScratchPools ScratchPools => _scratchPools;

    /// <summary>Gets a reference to the pooled coefficients in stripe order.</summary>
    internal ref int[] Values => ref _values;

    /// <summary>Gets a reference to the pooled coefficient flags in stripe order.</summary>
    internal ref byte[] Flags => ref _flags;

    /// <summary>Gets a reference to the pooled neighbourhood codes in stripe order.</summary>
    internal ref byte[] Neighbours => ref _neighbours;

    /// <summary>Gets a reference to the pooled joined code-block bytes.</summary>
    internal ref byte[] Joined => ref _joined;

    /// <summary>Gets a reference to the code-block width.</summary>
    internal ref int Width => ref _width;

    /// <summary>Gets a reference to the code-block height.</summary>
    internal ref int Height => ref _height;

    /// <summary>Gets a reference to the stripe count.</summary>
    internal ref int Stripes => ref _stripes;

    /// <summary>Gets a reference to the distance between stripes.</summary>
    internal ref int StripeStride => ref _stripeStride;

    /// <summary>Gets a reference to the used buffer length.</summary>
    internal ref int Length => ref _length;

    /// <summary>Gets a reference to the orientation's zero-coding table offset.</summary>
    internal ref int ZeroTable => ref _zeroTable;

    /// <summary>Gets a reference to the vertically causal mode flag.</summary>
    internal ref bool Causal => ref _causal;

    /// <inheritdoc/>
    public void Dispose()
    {
        JpxBlockLayout.Return(ref _values, _scratchPools);
        JpxBlockLayout.Return(ref _flags, _scratchPools);
        JpxBlockLayout.Return(ref _neighbours, _scratchPools);
        JpxBlockLayout.Return(ref _joined, _scratchPools);
    }
}
