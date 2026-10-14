// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// One component of an image being encoded: its sampling, the strip of samples for the MCU row in hand and the
/// quantized coefficients of every block. A component is set up again for each image, so an encoder keeps its components between images.
/// </summary>
[DebuggerDisplay("JpegEncoderComponent: id {Id}, {HorizontalSampling}x{VerticalSampling}")]
internal sealed class JpegEncoderComponent : IDisposable
{
    /// <summary>The cache the arrays come from and return to.</summary>
    private ScratchPools _scratchPools = null!;

    /// <summary>The quantized coefficients of every block in zigzag order, blocks row by row across the MCU-padded width.</summary>
    private short[]? _coefficients;

    /// <summary>The samples of the MCU row at the image's finest sampling.</summary>
    private byte[]? _full;

    /// <summary>The samples of the MCU row at this component's own sampling, or null when that is the finest.</summary>
    private byte[]? _reduced;

    /// <summary>Gets the component id written in the headers.</summary>
    internal int Id { get; private set; }

    /// <summary>Gets the horizontal sampling factor.</summary>
    internal int HorizontalSampling { get; private set; }

    /// <summary>Gets the vertical sampling factor.</summary>
    internal int VerticalSampling { get; private set; }

    /// <summary>Gets the quantization table slot: 0 for luminance, 1 for chrominance.</summary>
    internal int QuantSlot { get; private set; }

    /// <summary>Gets the Huffman table slot: 0 for luminance, 1 for chrominance.</summary>
    internal int TableSlot { get; private set; }

    /// <summary>Gets the blocks across including the padding of the last MCU.</summary>
    internal int BlocksPerLine { get; private set; }

    /// <summary>Gets the blocks across that hold image data; a scan of this component alone covers these.</summary>
    internal int UsedBlocksPerLine { get; private set; }

    /// <summary>Gets the blocks down that hold image data.</summary>
    internal int UsedBlocksPerColumn { get; private set; }

    /// <summary>Gets the distance between rows of <see cref="FullStrip"/>.</summary>
    internal int FullStride { get; private set; }

    /// <summary>Gets the distance between rows of <see cref="Plane"/>.</summary>
    internal int PlaneStride { get; private set; }

    /// <summary>Gets the rows of <see cref="FullStrip"/>.</summary>
    internal int FullRows { get; private set; }

    /// <summary>Gets the rows of <see cref="Plane"/>.</summary>
    internal int PlaneRows { get; private set; }

    /// <summary>Gets the strip the colour conversion writes: the MCU row at the image's finest sampling.</summary>
    internal Span<byte> FullStrip => _full.AsSpan(0, FullStride * FullRows);

    /// <summary>Gets the samples to transform: the strip at this component's sampling.</summary>
    internal ReadOnlySpan<byte> Plane => (_reduced ?? _full).AsSpan(0, PlaneStride * PlaneRows);

    /// <summary>Gets the strip that receives the reduced samples, or an empty span when the component is not reduced.</summary>
    internal Span<byte> ReducedStrip => _reduced.AsSpan(0, _reduced is null ? 0 : PlaneStride * PlaneRows);

    /// <summary>Gets a value indicating whether the component is sampled more coarsely than the finest.</summary>
    internal bool IsReduced => _reduced is not null;

    /// <summary>Returns the pooled arrays.</summary>
    public void Dispose()
    {
        var coefficients = _coefficients;
        _coefficients = null;
        if (coefficients is not null)
        {
            _scratchPools.Get<short>().Return(coefficients);
        }

        var full = _full;
        _full = null;
        var reduced = _reduced;
        _reduced = null;
        if (full is not null)
        {
            _scratchPools.Get<byte>().Return(full);
        }

        if (reduced is not null)
        {
            _scratchPools.Get<byte>().Return(reduced);
        }
    }

    /// <summary>Sets the component up for an image, renting its arrays.</summary>
    /// <param name="id">The component id.</param>
    /// <param name="sampling">The sampling factors and table slots.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal void Initialize(int id, in JpegEncoderSampling sampling, in JpegFrameGeometry geometry, ScratchPools scratchPools)
    {
        Dispose();
        _scratchPools = scratchPools;
        Id = id;
        HorizontalSampling = sampling.Horizontal;
        VerticalSampling = sampling.Vertical;
        QuantSlot = sampling.QuantSlot;
        TableSlot = sampling.QuantSlot;
        BlocksPerLine = geometry.McusPerLine * sampling.Horizontal;
        var blocksPerColumn = geometry.McusPerColumn * sampling.Vertical;
        UsedBlocksPerLine = CeilingDivide(CeilingDivide(geometry.Width * sampling.Horizontal, geometry.MaxHorizontal), JpegBlock.Side);
        UsedBlocksPerColumn = CeilingDivide(CeilingDivide(geometry.Height * sampling.Vertical, geometry.MaxVertical), JpegBlock.Side);
        FullStride = geometry.McusPerLine * JpegBlock.Side * geometry.MaxHorizontal;
        FullRows = JpegBlock.Side * geometry.MaxVertical;
        PlaneStride = BlocksPerLine * JpegBlock.Side;
        PlaneRows = JpegBlock.Side * sampling.Vertical;

        var bytes = scratchPools.Get<byte>();
        _full = bytes.Rent(FullStride * FullRows);
        var reduced = sampling.Horizontal != geometry.MaxHorizontal || sampling.Vertical != geometry.MaxVertical;
        _reduced = reduced ? bytes.Rent(PlaneStride * PlaneRows) : null;
        if (!reduced)
        {
            PlaneStride = FullStride;
            PlaneRows = FullRows;
        }

        _coefficients = scratchPools.Get<short>().Rent(checked(BlocksPerLine * blocksPerColumn * JpegBlock.Length));
    }

    /// <summary>Gets the 64 coefficients of a block.</summary>
    /// <param name="row">The block row.</param>
    /// <param name="column">The block column.</param>
    /// <returns>The coefficients in zigzag order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<short> GetBlock(int row, int column) =>
        _coefficients.AsSpan(((row * BlocksPerLine) + column) * JpegBlock.Length, JpegBlock.Length);

    /// <summary>Divides and rounds up.</summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The rounded-up quotient.</returns>
    private static int CeilingDivide(int value, int divisor) => (value + divisor - 1) / divisor;
}
