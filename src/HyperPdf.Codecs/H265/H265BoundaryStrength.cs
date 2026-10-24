// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Works out the boundary filtering strength between two 4x4 blocks (8.7.2.4).</summary>
internal static class H265BoundaryStrength
{
    /// <summary>The strength between an intra block and any neighbour.</summary>
    internal const int Intra = 2;

    /// <summary>The motion vector difference, in quarter samples, from which two vectors count as different.</summary>
    private const int VectorLimit = 4;

    /// <summary>Works out the strength of an edge.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="indexP">The grid index of the block on the left or above.</param>
    /// <param name="indexQ">The grid index of the block on the right or below.</param>
    /// <param name="refsP">The reference lists of the slice that holds the P block.</param>
    /// <param name="refsQ">The reference lists of the slice that holds the Q block.</param>
    /// <param name="transformEdge">Whether the edge is also a transform block edge.</param>
    /// <returns>0, 1 or 2.</returns>
    internal static int Of(H265Picture picture, int indexP, int indexQ, H265SliceRefs? refsP, H265SliceRefs? refsQ, bool transformEdge)
    {
        if (picture.Kind[indexP] == H265Picture.Intra || picture.Kind[indexQ] == H265Picture.Intra)
        {
            return Intra;
        }

        if (transformEdge && (((picture.Flags[indexP] | picture.Flags[indexQ]) & H265Picture.CodedFlag) != 0))
        {
            return 1;
        }

        if (refsP is null || refsQ is null)
        {
            return 0;
        }

        return MotionDiffers(picture.Motion[indexP], refsP, picture.Motion[indexQ], refsQ) ? 1 : 0;
    }

    /// <summary>Tells whether the motion of two blocks differs enough to filter the edge between them.</summary>
    /// <param name="p">The motion of the P block.</param>
    /// <param name="refsP">The reference lists of the P block's slice.</param>
    /// <param name="q">The motion of the Q block.</param>
    /// <param name="refsQ">The reference lists of the Q block's slice.</param>
    /// <returns><see langword="true"/> when the edge has strength 1.</returns>
    private static bool MotionDiffers(H265PuMotion p, H265SliceRefs refsP, H265PuMotion q, H265SliceRefs refsQ)
    {
        var countP = ListCount(p);
        if (countP != ListCount(q))
        {
            return true;
        }

        return countP == 1
            ? SingleReference(p, refsP) != SingleReference(q, refsQ) || Far(SingleVector(p), SingleVector(q))
            : BiMotionDiffers(p, refsP, q, refsQ);
    }

    /// <summary>Counts the reference lists a block predicts from.</summary>
    /// <param name="motion">The motion.</param>
    /// <returns>1 or 2 for an inter block.</returns>
    private static int ListCount(H265PuMotion motion) => (motion.UsesList0 ? 1 : 0) + (motion.UsesList1 ? 1 : 0);

    /// <summary>Gets the picture order count of the one reference a block predicts from.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="refs">The reference lists of the block's slice.</param>
    /// <returns>The picture order count.</returns>
    private static int SingleReference(H265PuMotion motion, H265SliceRefs refs) =>
        motion.UsesList0 ? refs.Poc0[motion.RefIdx0] : refs.Poc1[motion.RefIdx1];

    /// <summary>Gets the vector of the one list a block predicts from.</summary>
    /// <param name="motion">The motion.</param>
    /// <returns>The vector.</returns>
    private static MotionVector SingleVector(H265PuMotion motion) => motion.UsesList0 ? motion.Vector0 : motion.Vector1;

    /// <summary>Compares the motion of two bi-predicted blocks.</summary>
    /// <param name="p">The motion of the P block.</param>
    /// <param name="refsP">The reference lists of the P block's slice.</param>
    /// <param name="q">The motion of the Q block.</param>
    /// <param name="refsQ">The reference lists of the Q block's slice.</param>
    /// <returns><see langword="true"/> when the edge has strength 1.</returns>
    private static bool BiMotionDiffers(H265PuMotion p, H265SliceRefs refsP, H265PuMotion q, H265SliceRefs refsQ)
    {
        var p0 = refsP.Poc0[p.RefIdx0];
        var p1 = refsP.Poc1[p.RefIdx1];
        var q0 = refsQ.Poc0[q.RefIdx0];
        var q1 = refsQ.Poc1[q.RefIdx1];
        var straight = Far(p.Vector0, q.Vector0) || Far(p.Vector1, q.Vector1);
        var crossed = Far(p.Vector0, q.Vector1) || Far(p.Vector1, q.Vector0);
        if (p0 == q0 && p1 == q1)
        {
            return p0 == p1 ? straight && crossed : straight;
        }

        if (p0 == q1 && p1 == q0)
        {
            return crossed;
        }

        return true;
    }

    /// <summary>Tells whether two vectors differ by a whole luma sample or more in either direction.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns><see langword="true"/> when they are far apart.</returns>
    private static bool Far(MotionVector a, MotionVector b) =>
        Math.Abs(a.X - b.X) >= VectorLimit || Math.Abs(a.Y - b.Y) >= VectorLimit;
}
