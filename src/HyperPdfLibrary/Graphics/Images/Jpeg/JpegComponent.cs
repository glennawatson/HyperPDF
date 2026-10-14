// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// One colour component of a JPEG: its sampling, entropy-decoded coefficients and reconstructed sample plane. A component
/// is set up again for each image, so a decoder keeps its components between images.
/// </summary>
[DebuggerDisplay("JpegComponent: id {Id}, {HorizontalSampling}x{VerticalSampling}")]
internal sealed class JpegComponent : IDisposable
{
    /// <summary>The quantizer steps latched when the component first appeared in a scan, in storage order.</summary>
    private readonly int[] _quantSteps = new int[JpegBlock.Length];

    /// <summary>The cache this component returns its arrays to.</summary>
    private ScratchPools _scratchPools = null!;

    /// <summary>The coefficient array; blocks are stored row by row across the MCU-padded width.</summary>
    private short[]? _coefficients;

    /// <summary>The reconstructed samples.</summary>
    private byte[]? _plane;

    /// <summary>Whether <see cref="_quantSteps"/> holds a latched table.</summary>
    private bool _hasQuant;

    /// <summary>Gets the component id.</summary>
    internal int Id { get; private set; }

    /// <summary>Gets the horizontal sampling factor.</summary>
    internal int HorizontalSampling { get; private set; }

    /// <summary>Gets the vertical sampling factor.</summary>
    internal int VerticalSampling { get; private set; }

    /// <summary>Gets the quantization table id.</summary>
    internal int QuantId { get; private set; }

    /// <summary>Gets the blocks across that hold image data.</summary>
    internal int BlocksPerLine { get; private set; }

    /// <summary>Gets the blocks down that hold image data.</summary>
    internal int BlocksPerColumn { get; private set; }

    /// <summary>Gets the blocks across including the padding of the last MCU.</summary>
    internal int PaddedBlocksPerLine { get; private set; }

    /// <summary>Gets the blocks down including the padding of the last MCU.</summary>
    internal int PaddedBlocksPerColumn { get; private set; }

    /// <summary>Gets the width of the sample plane in samples.</summary>
    internal int Stride => PaddedBlocksPerLine * JpegBlock.Side;

    /// <summary>Gets or sets the DC table of the current scan.</summary>
    internal JpegHuffmanTable DcTable { get; set; } = JpegHuffmanTable.Empty;

    /// <summary>Gets or sets the AC table of the current scan.</summary>
    internal JpegHuffmanTable AcTable { get; set; } = JpegHuffmanTable.Empty;

    /// <summary>Gets or sets the previous DC value of the current scan.</summary>
    internal int DcPredictor { get; set; }

    /// <summary>Gets the sample plane, or an empty span before <see cref="Reconstruct"/>.</summary>
    internal ReadOnlySpan<byte> Plane => _plane;

    /// <summary>Returns the pooled arrays.</summary>
    public void Dispose()
    {
        var coefficients = _coefficients;
        _coefficients = null;
        if (coefficients is not null)
        {
            _scratchPools.Get<short>().Return(coefficients);
        }

        var plane = _plane;
        _plane = null;
        if (plane is not null)
        {
            _scratchPools.Get<byte>().Return(plane);
        }
    }

    /// <summary>Sets the component up for a frame, renting a zeroed coefficient array.</summary>
    /// <param name="spec">The frame component.</param>
    /// <param name="grid">The block layout.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal void Initialize(in JpegComponentSpec spec, in JpegGrid grid, ScratchPools scratchPools)
    {
        Dispose();
        _scratchPools = scratchPools;
        Id = spec.Id;
        HorizontalSampling = spec.HorizontalSampling;
        VerticalSampling = spec.VerticalSampling;
        QuantId = spec.QuantId;
        BlocksPerLine = grid.UsedBlocksPerLine;
        BlocksPerColumn = grid.UsedBlocksPerColumn;
        PaddedBlocksPerLine = grid.McusPerLine * HorizontalSampling;
        PaddedBlocksPerColumn = grid.McusPerColumn * VerticalSampling;
        DcTable = JpegHuffmanTable.Empty;
        AcTable = JpegHuffmanTable.Empty;
        DcPredictor = 0;
        _hasQuant = false;
        var length = checked(PaddedBlocksPerLine * PaddedBlocksPerColumn * JpegBlock.Length);
        _coefficients = scratchPools.Get<short>().Rent(length);
        _coefficients.AsSpan(0, length).Clear();
    }

    /// <summary>Keeps a copy of a quantization table for this component unless one is already latched.</summary>
    /// <param name="table">The table in storage order, or <see langword="null"/>.</param>
    internal void LatchQuant(int[]? table)
    {
        if (_hasQuant || table is null)
        {
            return;
        }

        table.CopyTo(_quantSteps, 0);
        _hasQuant = true;
    }

    /// <summary>Gets the 64 coefficients of a block.</summary>
    /// <param name="row">The block row.</param>
    /// <param name="column">The block column.</param>
    /// <returns>The coefficients in storage order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<short> GetBlock(int row, int column) =>
        _coefficients.AsSpan(((row * PaddedBlocksPerLine) + column) * JpegBlock.Length, JpegBlock.Length);

    /// <summary>Runs the inverse DCT over every block that holds image data.</summary>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="sampleDrop">The bits to drop from each sample: 4 reduces a 12-bit frame to 8 bits, 0 keeps an 8-bit frame.</param>
    /// <returns><see langword="false"/> when the component has no quantization table.</returns>
    internal bool Reconstruct(Span<int> work, int sampleDrop)
    {
        if (!_hasQuant)
        {
            return false;
        }

        var stride = Stride;
        _plane ??= _scratchPools.Get<byte>().Rent(stride * PaddedBlocksPerColumn * JpegBlock.Side);
        for (var row = 0; row < BlocksPerColumn; row++)
        {
            for (var column = 0; column < BlocksPerLine; column++)
            {
                var origin = (row * JpegBlock.Side * stride) + (column * JpegBlock.Side);
                JpegIdct.Transform(GetBlock(row, column), _quantSteps, work, _plane.AsSpan(origin), stride, sampleDrop);
            }
        }

        return true;
    }
}
