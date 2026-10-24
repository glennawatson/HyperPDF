// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>Predicts the samples of an intra macroblock and adds the residual to them.</summary>
[DebuggerDisplay("H264IntraReconstructor")]
internal sealed class H264IntraReconstructor
{
    /// <summary>The number of luma coefficients in a macroblock's 16 blocks.</summary>
    private const int Rows = 4;

    /// <summary>The luma blocks per two block rows, which is how far the coding order index moves down two rows.</summary>
    private const int BlocksPerBand = 8;

    /// <summary>The first scaling list of inter prediction.</summary>
    private const int InterListBase = 3;

    /// <summary>The luma blocks in a 8x8 group.</summary>
    private const int BlocksPerGroup = 4;

    /// <summary>The blocks along one side of a group.</summary>
    private const int GroupSide = 2;

    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The picture parameter set.</summary>
    private readonly H264Pps _pps;

    /// <summary>The level scale factors.</summary>
    private readonly H264LevelScale _scale;

    /// <summary>Initializes a new instance of the <see cref="H264IntraReconstructor"/> class.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="scale">The level scale factors.</param>
    internal H264IntraReconstructor(H264Picture picture, H264Pps pps, H264LevelScale scale)
    {
        _picture = picture;
        _pps = pps;
        _scale = scale;
    }

    /// <summary>Makes the samples of a macroblock.</summary>
    /// <param name="mb">The macroblock, with its coefficients read.</param>
    /// <param name="qp">The macroblock's luma quantiser.</param>
    internal void Reconstruct(H264CurrentMb mb, int qp)
    {
        var lumaX = mb.X * H264Layout.MbLuma;
        var lumaY = mb.Y * H264Layout.MbLuma;
        if (mb.Kind == H264MbKind.Intra16x16)
        {
            ReconstructIntra16x16(mb, qp, lumaX, lumaY);
        }
        else if (mb.Transform8x8)
        {
            H264Luma8x8.ReconstructIntra(_picture, _scale, mb, qp);
        }
        else
        {
            ReconstructIntra4x4(mb, qp, lumaX, lumaY);
        }

        ReconstructChroma(mb, qp);
    }

    /// <summary>Adds the residual of an inter macroblock to the prediction already in the picture.</summary>
    /// <param name="mb">The macroblock, with its coefficients read.</param>
    /// <param name="qp">The macroblock's luma quantiser.</param>
    internal void AddInterResidual(H264CurrentMb mb, int qp)
    {
        var stride = _picture.LumaStride;
        var x = mb.X * H264Layout.MbLuma;
        var y = mb.Y * H264Layout.MbLuma;
        var factors = _scale.Factors(InterListBase, qp % H264Layout.QpPerOctave);
        for (var block = 0; block < H264Layout.LumaBlocks && !mb.Transform8x8; block++)
        {
            if ((mb.CbpLuma & (1 << (block / BlocksPerGroup))) == 0)
            {
                continue;
            }

            int column = H264Tables.BlockColumn[block];
            int row = H264Tables.BlockRow[block];
            var coefficients = mb.Luma.AsSpan(block * H264Layout.BlockSamples, H264Layout.BlockSamples);
            H264Transform.Scale4x4(coefficients, qp, factors, 0);
            H264Transform.AddInverse4x4(coefficients, _picture.Luma, ((y + (row * H264Layout.BlockSide)) * stride) + x + (column * H264Layout.BlockSide), stride);
        }

        if (mb.Transform8x8)
        {
            H264Luma8x8.AddInterResidual(_picture, _scale, mb, qp);
        }

        if (mb.CbpChroma == 0)
        {
            return;
        }

        var chromaOffset = (mb.Y * H264Layout.MbChroma * _picture.ChromaStride) + (mb.X * H264Layout.MbChroma);
        for (var component = 0; component < H264Layout.Components; component++)
        {
            var plane = component == 0 ? _picture.Cb : _picture.Cr;
            AddChromaResidual(mb, component, plane, chromaOffset, ChromaQpOf(qp, component), InterListBase);
        }
    }

    /// <summary>Finds the coding order index of the luma block at a position.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The index.</returns>
    private static int BlockIndexAt(int row, int column) =>
        ((row >> 1) * BlocksPerBand) + ((column >> 1) * BlocksPerGroup) + ((row & 1) * GroupSide) + (column & 1);

    /// <summary>Finds which neighbours of a 4x4 block hold samples that may predict it.</summary>
    /// <param name="edge">The neighbours of the macroblock.</param>
    /// <param name="block">The block, in coding order.</param>
    /// <param name="column">The block's column in the macroblock.</param>
    /// <param name="row">The block's row in the macroblock.</param>
    /// <returns>The usable neighbours.</returns>
    private static H264Neighbours BlockNeighbours(H264Neighbours edge, int block, int column, int row) =>
        new(column != 0 || edge.Left, row != 0 || edge.Top, TopRightOf(edge, block, column, row), TopLeftOf(edge, column, row));

    /// <summary>Tells whether the samples above and to the right of a block can be used.</summary>
    /// <param name="edge">The neighbours of the macroblock.</param>
    /// <param name="block">The block, in coding order.</param>
    /// <param name="column">The block's column in the macroblock.</param>
    /// <param name="row">The block's row in the macroblock.</param>
    /// <returns><see langword="true"/> when they can.</returns>
    private static bool TopRightOf(H264Neighbours edge, int block, int column, int row)
    {
        if (row == 0)
        {
            return column < H264Layout.LastBlock ? edge.Top : edge.TopRight;
        }

        return column < H264Layout.LastBlock && BlockIndexAt(row - 1, column + 1) < block;
    }

    /// <summary>Tells whether the sample above and to the left of a block can be used.</summary>
    /// <param name="edge">The neighbours of the macroblock.</param>
    /// <param name="column">The block's column in the macroblock.</param>
    /// <param name="row">The block's row in the macroblock.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool TopLeftOf(H264Neighbours edge, int column, int row)
    {
        if (column != 0 && row != 0)
        {
            return true;
        }

        if (column != 0)
        {
            return edge.Top;
        }

        return row != 0 ? edge.Left : edge.TopLeft;
    }

    /// <summary>Makes the luma samples of an Intra 16x16 macroblock.</summary>
    /// <param name="mb">The macroblock.</param>
    /// <param name="qp">The quantiser.</param>
    /// <param name="x">The first column of the macroblock.</param>
    /// <param name="y">The first row of the macroblock.</param>
    private void ReconstructIntra16x16(H264CurrentMb mb, int qp, int x, int y)
    {
        var stride = _picture.LumaStride;
        var offset = (y * stride) + x;
        H264Intra16x16.Predict(_picture.Luma, offset, stride, mb.Intra16x16Mode, mb.Neighbours);
        var factors = _scale.Factors(0, qp % H264Layout.QpPerOctave);
        H264Transform.LumaDc(mb.LumaDc, qp, factors[0]);
        for (var block = 0; block < H264Layout.LumaBlocks; block++)
        {
            var column = H264Tables.BlockColumn[block];
            var row = H264Tables.BlockRow[block];
            var coefficients = mb.Luma.AsSpan(block * H264Layout.BlockSamples, H264Layout.BlockSamples);
            coefficients[0] = mb.LumaDc[(row * Rows) + column];
            H264Transform.Scale4x4(coefficients, qp, factors, 1);
            H264Transform.AddInverse4x4(coefficients, _picture.Luma, offset + (row * H264Layout.BlockSide * stride) + (column * H264Layout.BlockSide), stride);
        }
    }

    /// <summary>Makes the luma samples of an Intra 4x4 macroblock, one block at a time because each block predicts from the ones before it.</summary>
    /// <param name="mb">The macroblock.</param>
    /// <param name="qp">The quantiser.</param>
    /// <param name="x">The first column of the macroblock.</param>
    /// <param name="y">The first row of the macroblock.</param>
    private void ReconstructIntra4x4(H264CurrentMb mb, int qp, int x, int y)
    {
        var stride = _picture.LumaStride;
        var factors = _scale.Factors(0, qp % H264Layout.QpPerOctave);
        for (var block = 0; block < H264Layout.LumaBlocks; block++)
        {
            int column = H264Tables.BlockColumn[block];
            int row = H264Tables.BlockRow[block];
            var offset = ((y + (row * H264Layout.BlockSide)) * stride) + x + (column * H264Layout.BlockSide);
            H264Intra4x4.Predict(_picture.Luma, offset, stride, mb.Modes[block], BlockNeighbours(mb.Neighbours, block, column, row));
            if ((mb.CbpLuma & (1 << (block / BlocksPerGroup))) == 0)
            {
                continue;
            }

            var coefficients = mb.Luma.AsSpan(block * H264Layout.BlockSamples, H264Layout.BlockSamples);
            H264Transform.Scale4x4(coefficients, qp, factors, 0);
            H264Transform.AddInverse4x4(coefficients, _picture.Luma, offset, stride);
        }
    }

    /// <summary>Makes the chroma samples of the macroblock.</summary>
    /// <param name="mb">The macroblock.</param>
    /// <param name="qp">The luma quantiser.</param>
    private void ReconstructChroma(H264CurrentMb mb, int qp)
    {
        var stride = _picture.ChromaStride;
        var offset = (mb.Y * H264Layout.MbChroma * stride) + (mb.X * H264Layout.MbChroma);
        for (var component = 0; component < H264Layout.Components; component++)
        {
            var plane = component == 0 ? _picture.Cb : _picture.Cr;
            H264IntraChroma.Predict(plane, offset, stride, mb.ChromaMode, mb.Neighbours);
            if (mb.CbpChroma != 0)
            {
                AddChromaResidual(mb, component, plane, offset, ChromaQpOf(qp, component), 0);
            }
        }
    }

    /// <summary>Works out the quantiser of a chroma component.</summary>
    /// <param name="qp">The luma quantiser.</param>
    /// <param name="component">0 for Cb, 1 for Cr.</param>
    /// <returns>The chroma quantiser.</returns>
    private int ChromaQpOf(int qp, int component)
    {
        var shift = component == 0 ? _pps.ChromaQpOffset : _pps.SecondChromaQpOffset;
        return H264Tables.ChromaQp(Math.Clamp(qp + shift, 0, H264Layout.QpRange - 1));
    }

    /// <summary>Adds the residual of one chroma component to its prediction.</summary>
    /// <param name="mb">The macroblock.</param>
    /// <param name="component">0 for Cb, 1 for Cr.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="offset">The index of the macroblock's first sample.</param>
    /// <param name="chromaQp">The chroma quantiser.</param>
    /// <param name="listBase">The first scaling list of the kind of prediction: 0 for intra, 3 for inter.</param>
    private void AddChromaResidual(H264CurrentMb mb, int component, byte[] plane, int offset, int chromaQp, int listBase)
    {
        var stride = _picture.ChromaStride;
        var factors = _scale.Factors(listBase + 1 + component, chromaQp % H264Layout.QpPerOctave);
        var dc = mb.ChromaDc.AsSpan(component * H264Layout.ChromaBlocks, H264Layout.ChromaBlocks);
        H264Transform.ChromaDc2x2(dc, chromaQp, factors[0]);
        for (var block = 0; block < H264Layout.ChromaBlocks; block++)
        {
            var coefficients = mb.ChromaAc.AsSpan(((component * H264Layout.ChromaBlocks) + block) * H264Layout.BlockSamples, H264Layout.BlockSamples);
            coefficients[0] = dc[block];
            H264Transform.Scale4x4(coefficients, chromaQp, factors, 1);
            var blockOffset = offset + ((block / H264Layout.ChromaBlocksPerRow) * H264Layout.BlockSide * stride) + ((block % H264Layout.ChromaBlocksPerRow) * H264Layout.BlockSide);
            H264Transform.AddInverse4x4(coefficients, plane, blockOffset, stride);
        }
    }
}
