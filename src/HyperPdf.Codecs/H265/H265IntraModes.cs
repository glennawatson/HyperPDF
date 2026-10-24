// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Derives the intra prediction modes of a prediction block from the syntax elements and its neighbours (8.4.2 and 8.4.3).</summary>
internal static class H265IntraModes
{
    /// <summary>The planar mode.</summary>
    internal const int Planar = 0;

    /// <summary>The DC mode.</summary>
    internal const int Dc = 1;

    /// <summary>The horizontal mode.</summary>
    internal const int Horizontal = 10;

    /// <summary>The vertical mode.</summary>
    internal const int Vertical = 26;

    /// <summary>The diagonal mode that stands in when a chroma mode would repeat the luma mode.</summary>
    internal const int Diagonal = 34;

    /// <summary>The intra_chroma_pred_mode value that copies the luma mode.</summary>
    internal const int DerivedFromLuma = 4;

    /// <summary>The number of angular modes the neighbour modes wrap around.</summary>
    private const int AngularModes = 32;

    /// <summary>The offset that wraps a mode downwards.</summary>
    private const int WrapDown = 29;

    /// <summary>The first angular mode.</summary>
    private const int FirstAngular = 2;

    /// <summary>The number of most probable modes.</summary>
    private const int CandidateCount = 3;

    /// <summary>The chroma modes that intra_chroma_pred_mode 0 to 3 select.</summary>
    private static readonly int[] ChromaChoices = [Planar, Vertical, Horizontal, Dc];

    /// <summary>The mapping of 4:2:2 chroma modes (Table 8-3).</summary>
    private static readonly int[] Chroma422 =
    [
        0, 1, 2, 2, 2, 2, 3, 5, 7, 8, 10, 12, 13, 15, 17, 18, 19, 20,
        21, 22, 23, 23, 24, 24, 25, 25, 26, 27, 27, 28, 28, 29, 29, 30, 31,
    ];

    /// <summary>Derives the luma mode of a prediction block.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x">The column of the block's top-left luma sample.</param>
    /// <param name="y">The row of the block's top-left luma sample.</param>
    /// <param name="mpmFlag">The prev_intra_luma_pred_flag.</param>
    /// <param name="value">The mpm_idx when the flag is set, otherwise the rem_intra_luma_pred_mode.</param>
    /// <returns>The mode, from 0 to 34.</returns>
    internal static int DeriveLuma(H265SliceContext context, int x, int y, bool mpmFlag, int value)
    {
        var candA = Candidate(context, x, y, x - 1, y, false);
        var candB = Candidate(context, x, y, x, y - 1, true);
        Span<int> list = stackalloc int[CandidateCount];
        BuildList(candA, candB, list);
        if (mpmFlag)
        {
            return list[value];
        }

        list.Sort();
        var mode = value;
        for (var i = 0; i < CandidateCount; i++)
        {
            if (mode >= list[i])
            {
                mode++;
            }
        }

        return mode;
    }

    /// <summary>Derives the chroma mode of a prediction block.</summary>
    /// <param name="chromaArrayType">The chroma format.</param>
    /// <param name="syntaxValue">The intra_chroma_pred_mode.</param>
    /// <param name="lumaMode">The luma mode of the same block.</param>
    /// <returns>The mode.</returns>
    internal static int DeriveChroma(int chromaArrayType, int syntaxValue, int lumaMode)
    {
        var mode = lumaMode;
        if (syntaxValue != DerivedFromLuma)
        {
            mode = ChromaChoices[syntaxValue];
            if (mode == lumaMode)
            {
                mode = Diagonal;
            }
        }

        return chromaArrayType == ChromaSampling.Yuv422 ? Chroma422[mode] : mode;
    }

    /// <summary>Finds the candidate mode a neighbouring block offers.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x">The column of the current block.</param>
    /// <param name="y">The row of the current block.</param>
    /// <param name="neighbourX">The neighbour's column.</param>
    /// <param name="neighbourY">The neighbour's row.</param>
    /// <param name="above">Whether the neighbour is above, which is not used across a coding tree block boundary.</param>
    /// <returns>The candidate mode.</returns>
    private static int Candidate(H265SliceContext context, int x, int y, int neighbourX, int neighbourY, bool above)
    {
        if (!H265Availability.IsAvailable(context, x, y, neighbourX, neighbourY))
        {
            return Dc;
        }

        var picture = context.Picture;
        var index = picture.GridIndex(neighbourX, neighbourY);
        if (picture.Kind[index] != H265Picture.Intra || (picture.Flags[index] & H265Picture.PcmFlag) != 0)
        {
            return Dc;
        }

        var ctbTop = (y >> context.Layout.CtbLog2) << context.Layout.CtbLog2;
        return above && neighbourY < ctbTop ? Dc : picture.IntraMode[index];
    }

    /// <summary>Builds the list of most probable modes (8-21 to 8-28).</summary>
    /// <param name="candA">The mode offered by the left neighbour.</param>
    /// <param name="candB">The mode offered by the upper neighbour.</param>
    /// <param name="list">Receives the three modes.</param>
    private static void BuildList(int candA, int candB, Span<int> list)
    {
        if (candA == candB)
        {
            if (candA < FirstAngular)
            {
                list[0] = Planar;
                list[1] = Dc;
                list[2] = Vertical;
                return;
            }

            list[0] = candA;
            list[1] = FirstAngular + ((candA + WrapDown) % AngularModes);
            list[2] = FirstAngular + ((candA - FirstAngular + 1) % AngularModes);
            return;
        }

        list[0] = candA;
        list[1] = candB;
        if (candA != Planar && candB != Planar)
        {
            list[2] = Planar;
        }
        else
        {
            list[2] = candA != Dc && candB != Dc ? Dc : Vertical;
        }
    }
}
