// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Tells whether the motion of two inter coded 4x4 blocks differs enough for the deblocking filter to give their edge strength 1 (clause 8.7.2.1 of ITU-T H.264).</summary>
internal static class H264MotionCompare
{
    /// <summary>The difference in a motion vector component, in quarter samples, that counts as different motion.</summary>
    private const int MotionLimit = 4;

    /// <summary>The 4x4 blocks along a macroblock side.</summary>
    private const int Side = 4;

    /// <summary>The 4x4 blocks along the side of an 8x8 group.</summary>
    private const int Half = 2;

    /// <summary>Tells whether the motion of two blocks differs.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="first">One block.</param>
    /// <param name="second">The other block.</param>
    /// <returns><see langword="true"/> when they use different reference frames, a different number of vectors, or vectors that are far apart.</returns>
    internal static bool Differs(H264Picture picture, H264BlockAt first, H264BlockAt second)
    {
        var a = Motion(picture, first);
        var b = Motion(picture, second);
        if (a.Count != b.Count)
        {
            return true;
        }

        return a.Count == 1 ? a.OnlyUid != b.OnlyUid || Far(a.OnlyVector, b.OnlyVector) : PairDiffers(a, b);
    }

    /// <summary>Compares two blocks that each use two vectors.</summary>
    /// <param name="a">One block's motion.</param>
    /// <param name="b">The other block's motion.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    private static bool PairDiffers(H264BlockMotion a, H264BlockMotion b)
    {
        var same = a.Uid0 == b.Uid0 && a.Uid1 == b.Uid1;
        if (!same && !(a.Uid0 == b.Uid1 && a.Uid1 == b.Uid0))
        {
            return true;
        }

        var straight = Far(a.Vector0, b.Vector0) || Far(a.Vector1, b.Vector1);
        var crossed = Far(a.Vector0, b.Vector1) || Far(a.Vector1, b.Vector0);
        if (a.Uid0 == a.Uid1)
        {
            return straight && crossed;
        }

        return same ? straight : crossed;
    }

    /// <summary>Tells whether two vectors are far enough apart to count as different motion.</summary>
    /// <param name="a">One vector.</param>
    /// <param name="b">The other vector.</param>
    /// <returns><see langword="true"/> when a component differs by a whole sample or more.</returns>
    private static bool Far(MotionVector a, MotionVector b) => Math.Abs(a.X - b.X) >= MotionLimit || Math.Abs(a.Y - b.Y) >= MotionLimit;

    /// <summary>Reads the motion of a 4x4 block for both lists.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="block">The block.</param>
    /// <returns>The reference frames and vectors.</returns>
    private static H264BlockMotion Motion(H264Picture picture, H264BlockAt block)
    {
        var group = (block.Address * H264Picture.GroupsPerMb) + ((block.Row >> 1) * Half) + (block.Column >> 1);
        var index = (block.Address * H264Picture.BlocksPerMb) + (block.Row * Side) + block.Column;
        return new(
            picture.Field0.RefUid[group],
            new(picture.Field0.MvX[index], picture.Field0.MvY[index]),
            picture.Field1.RefUid[group],
            new(picture.Field1.MvX[index], picture.Field1.MvY[index]));
    }
}
