// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Predicts and reconstructs the luma samples of macroblocks that use the 8x8 transform.</summary>
internal static class H264Luma8x8
{
    /// <summary>The 8x8 blocks in a macroblock.</summary>
    private const int Groups = 4;

    /// <summary>The samples along an 8x8 block.</summary>
    private const int Side = 8;

    /// <summary>The coefficients of an 8x8 block.</summary>
    private const int Coefficients = 64;

    /// <summary>The 8x8 blocks along a macroblock side.</summary>
    private const int PerRow = 2;

    /// <summary>The index of the second 8x8 block, the top right one.</summary>
    private const int TopRightGroup = 1;

    /// <summary>The index of the third 8x8 block, the bottom left one.</summary>
    private const int BottomLeftGroup = 2;

    /// <summary>Predicts each 8x8 block of an Intra 8x8 macroblock and adds its residual, one block at a time because each block predicts from the ones before it.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="mb">The macroblock, with its coefficients and prediction modes read.</param>
    /// <param name="qp">The luma quantiser.</param>
    internal static void ReconstructIntra(H264Picture picture, H264LevelScale scale, H264CurrentMb mb, int qp)
    {
        var stride = picture.LumaStride;
        var x = mb.X * H264Layout.MbLuma;
        var y = mb.Y * H264Layout.MbLuma;
        for (var group = 0; group < Groups; group++)
        {
            var offset = ((y + ((group / PerRow) * Side)) * stride) + x + ((group % PerRow) * Side);
            H264Intra8x8.Predict(picture.Luma, offset, stride, mb.Modes[group], NeighboursOf(mb.Neighbours, group));
            AddBlock(picture, scale, mb, new(group, offset, qp, 0));
        }
    }

    /// <summary>Adds the residual of an inter macroblock that uses the 8x8 transform to the prediction already in the picture.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="mb">The macroblock, with its coefficients read.</param>
    /// <param name="qp">The luma quantiser.</param>
    internal static void AddInterResidual(H264Picture picture, H264LevelScale scale, H264CurrentMb mb, int qp)
    {
        var stride = picture.LumaStride;
        var x = mb.X * H264Layout.MbLuma;
        var y = mb.Y * H264Layout.MbLuma;
        for (var group = 0; group < Groups; group++)
        {
            var offset = ((y + ((group / PerRow) * Side)) * stride) + x + ((group % PerRow) * Side);
            AddBlock(picture, scale, mb, new(group, offset, qp, 1));
        }
    }

    /// <summary>Finds which neighbours of an 8x8 block hold samples that may predict it.</summary>
    /// <param name="edge">The neighbours of the macroblock.</param>
    /// <param name="group">The 8x8 block, 0 to 3 in coding order.</param>
    /// <returns>The usable neighbours.</returns>
    private static H264Neighbours NeighboursOf(H264Neighbours edge, int group)
    {
        var column = group % PerRow;
        var row = group / PerRow;
        var left = column != 0 || edge.Left;
        var top = row != 0 || edge.Top;
        var topRight = group switch
        {
            0 => edge.Top,
            TopRightGroup => edge.TopRight,
            BottomLeftGroup => true,
            _ => false,
        };
        var topLeft = group switch
        {
            0 => edge.TopLeft,
            TopRightGroup => edge.Top,
            BottomLeftGroup => edge.Left,
            _ => true,
        };
        return new(left, top, topRight, topLeft);
    }

    /// <summary>Scales and inverse transforms one 8x8 block and adds it to the samples already in the picture.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="block">Which block, where its samples start, the quantiser and the scaling list.</param>
    private static void AddBlock(H264Picture picture, H264LevelScale scale, H264CurrentMb mb, H264BlockJob block)
    {
        if ((mb.CbpLuma & (1 << block.Group)) == 0)
        {
            return;
        }

        var coefficients = mb.Luma.AsSpan(block.Group * Coefficients, Coefficients);
        H264Transform.Scale8x8(coefficients, block.Qp, scale.Factors8x8(block.List, block.Qp % H264Layout.QpPerOctave));
        H264Transform.AddInverse8x8(coefficients, picture.Luma, block.Offset, picture.LumaStride);
    }
}
