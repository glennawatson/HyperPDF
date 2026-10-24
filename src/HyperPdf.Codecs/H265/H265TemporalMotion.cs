// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Temporal luma motion vector prediction from the co-located picture (8.5.3.2.8 and 8.5.3.2.9).</summary>
internal static class H265TemporalMotion
{
    /// <summary>The base 2 logarithm of the grid on which the co-located motion is kept.</summary>
    private const int ColocatedLog2 = 4;

    /// <summary>Finds the temporal motion vector predictor of a prediction block for one reference picture.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="area">The prediction block.</param>
    /// <param name="list">The reference list, 0 or 1.</param>
    /// <param name="refIdx">The reference index in that list.</param>
    /// <param name="vector">Receives the predictor.</param>
    /// <returns><see langword="true"/> when a predictor is available.</returns>
    internal static bool TryGet(H265SliceContext context, BlockRect area, int list, int refIdx, out MotionVector vector)
    {
        vector = default;
        if (!context.Header.TemporalMvp)
        {
            return false;
        }

        var refs = context.Header.Refs;
        var colPic = (refs.CollocatedFromL0 ? context.RefList0 : context.RefList1)[refs.CollocatedRefIdx];
        var picture = context.Picture;
        var bottom = area.Y + area.Height;
        var right = area.X + area.Width;
        var ctbLog2 = context.Layout.CtbLog2;
        if (area.Y >> ctbLog2 == bottom >> ctbLog2 && bottom < picture.Height && right < picture.Width
            && TryColocated(context, colPic, Align(right), Align(bottom), list, refIdx, out vector))
        {
            return true;
        }

        return TryColocated(context, colPic, Align(area.X + (area.Width >> 1)), Align(area.Y + (area.Height >> 1)), list, refIdx, out vector);
    }

    /// <summary>Rounds a luma position down to the 16x16 grid on which co-located motion is kept.</summary>
    /// <param name="position">The position.</param>
    /// <returns>The aligned position.</returns>
    private static int Align(int position) => (position >> ColocatedLog2) << ColocatedLog2;

    /// <summary>Derives the predictor from the motion at one position of the co-located picture (8.5.3.2.9).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="colPic">The co-located picture.</param>
    /// <param name="x">The aligned column in the co-located picture.</param>
    /// <param name="y">The aligned row in the co-located picture.</param>
    /// <param name="list">The reference list of the current block.</param>
    /// <param name="refIdx">The reference index of the current block.</param>
    /// <param name="vector">Receives the predictor.</param>
    /// <returns><see langword="true"/> when the position yields a predictor.</returns>
    private static bool TryColocated(H265SliceContext context, H265Picture colPic, int x, int y, int list, int refIdx, out MotionVector vector)
    {
        vector = default;
        var index = colPic.GridIndex(x, y);
        if (colPic.Kind[index] != H265Picture.Inter)
        {
            return false;
        }

        var motion = colPic.Motion[index];
        var colList = ColocatedList(context, motion, list);
        var vectorCol = colList == 0 ? motion.Vector0 : motion.Vector1;
        var refIdxCol = colList == 0 ? motion.RefIdx0 : motion.RefIdx1;
        var ctb = ((y >> context.Layout.CtbLog2) * context.Layout.WidthInCtbs) + (x >> context.Layout.CtbLog2);
        var slice = colPic.SliceRefs[colPic.RefsIndexOfCtb[ctb]];
        var colRefPoc = colList == 0 ? slice.Poc0[refIdxCol] : slice.Poc1[refIdxCol];
        var colRefLong = colList == 0 ? slice.LongTerm0[refIdxCol] : slice.LongTerm1[refIdxCol];
        var target = (list == 0 ? context.RefList0 : context.RefList1)[refIdx];
        var currentLong = target.IsLongTerm;
        if (currentLong != colRefLong)
        {
            return false;
        }

        var colDiff = colPic.Poc - colRefPoc;
        var currentDiff = context.Picture.Poc - target.Poc;
        vector = currentLong || colDiff == currentDiff ? vectorCol : H265MotionScaling.Scale(vectorCol, colDiff, currentDiff);
        return true;
    }

    /// <summary>Picks which list of a bi-predicted co-located block supplies the vector.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="motion">The co-located motion.</param>
    /// <param name="list">The reference list of the current block.</param>
    /// <returns>0 or 1.</returns>
    private static int ColocatedList(H265SliceContext context, H265PuMotion motion, int list)
    {
        if (!motion.UsesList0)
        {
            return 1;
        }

        if (!motion.UsesList1)
        {
            return 0;
        }

        if (context.NoBackwardPred)
        {
            return list;
        }

        return context.Header.Refs.CollocatedFromL0 ? 1 : 0;
    }
}
