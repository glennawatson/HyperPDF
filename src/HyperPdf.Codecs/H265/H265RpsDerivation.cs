// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Works out which pictures of the buffer the current picture may refer to and marks the rest as unused (8.3.2).</summary>
internal static class H265RpsDerivation
{
    /// <summary>Derives the reference picture set of a picture.</summary>
    /// <param name="dpb">The decoded picture buffer.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="header">The slice segment header.</param>
    /// <param name="poc">The picture order count of the current picture.</param>
    /// <param name="idr">Whether the current picture is an IDR picture, which refers to nothing.</param>
    /// <param name="makeMissing">Makes a stand-in picture with a given order count, for a reference that is not in the buffer.</param>
    /// <returns>The pictures the current picture may use.</returns>
    internal static H265RefPicSet Derive(H265Dpb dpb, H265Sps sps, H265SliceHeader header, int poc, bool idr, Func<int, H265Picture> makeMissing)
    {
        if (idr)
        {
            dpb.UnmarkAll();
            return H265RefPicSet.Empty;
        }

        var keep = new HashSet<H265Picture>();
        var longTerm = FindLongTerm(dpb, header, poc, 1 << sps.Log2MaxPocLsb, keep, makeMissing);
        var rps = header.ShortTerm;
        var before = FindShortTerm(dpb, rps.DeltaPocS0, rps.UsedS0, poc, keep, makeMissing);
        var after = FindShortTerm(dpb, rps.DeltaPocS1, rps.UsedS1, poc, keep, makeMissing);
        foreach (var picture in dpb.Pictures)
        {
            if (!keep.Contains(picture))
            {
                picture.ReferenceState = 0;
            }
        }

        return new(before, after, longTerm);
    }

    /// <summary>Finds the long-term pictures the slice header names, marking them long-term.</summary>
    /// <param name="dpb">The decoded picture buffer.</param>
    /// <param name="header">The slice segment header.</param>
    /// <param name="poc">The current picture order count.</param>
    /// <param name="maxLsb">The range of the low bits of the picture order count.</param>
    /// <param name="keep">Collects every picture the set keeps.</param>
    /// <param name="makeMissing">Makes a stand-in for a missing picture.</param>
    /// <returns>The pictures the current picture uses.</returns>
    private static H265Picture[] FindLongTerm(H265Dpb dpb, H265SliceHeader header, int poc, int maxLsb, HashSet<H265Picture> keep, Func<int, H265Picture> makeMissing)
    {
        var used = new List<H265Picture>();
        foreach (var entry in header.LongTerm)
        {
            var target = entry.PocLsb;
            if (entry.MsbPresent)
            {
                target += poc - (entry.DeltaPocMsbCycle * maxLsb) - (poc & (maxLsb - 1));
            }

            var picture = FindReference(dpb, target, entry.MsbPresent ? -1 : maxLsb - 1) ?? CreateMissing(dpb, makeMissing, target);
            picture.ReferenceState = H265Picture.LongTerm;
            _ = keep.Add(picture);
            if (entry.Used)
            {
                used.Add(picture);
            }
        }

        return [.. used];
    }

    /// <summary>Finds the short-term pictures of one side of the set.</summary>
    /// <param name="dpb">The decoded picture buffer.</param>
    /// <param name="deltas">The picture order count distances.</param>
    /// <param name="usedFlags">Whether the current picture refers to each.</param>
    /// <param name="poc">The current picture order count.</param>
    /// <param name="keep">Collects every picture the set keeps.</param>
    /// <param name="makeMissing">Makes a stand-in for a missing picture.</param>
    /// <returns>The pictures the current picture uses.</returns>
    private static H265Picture[] FindShortTerm(H265Dpb dpb, int[] deltas, bool[] usedFlags, int poc, HashSet<H265Picture> keep, Func<int, H265Picture> makeMissing)
    {
        var used = new List<H265Picture>();
        for (var i = 0; i < deltas.Length; i++)
        {
            var target = poc + deltas[i];
            var picture = dpb.Pictures.Find(candidate => candidate.ReferenceState == 1 && candidate.Poc == target) ?? CreateMissing(dpb, makeMissing, target);
            picture.ReferenceState = picture.ReferenceState == H265Picture.Unused ? H265Picture.ShortTerm : picture.ReferenceState;
            _ = keep.Add(picture);
            if (usedFlags[i])
            {
                used.Add(picture);
            }
        }

        return [.. used];
    }

    /// <summary>Finds a picture used for reference by its picture order count.</summary>
    /// <param name="dpb">The decoded picture buffer.</param>
    /// <param name="poc">The count, or only its low bits when a mask is given.</param>
    /// <param name="mask">A mask for the low bits to compare, or -1 to compare the whole count.</param>
    /// <returns>The picture, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static H265Picture? FindReference(H265Dpb dpb, int poc, int mask) =>
        dpb.Pictures.Find(candidate => candidate.ReferenceState != 0 && (mask < 0 ? candidate.Poc == poc : (candidate.Poc & mask) == poc));

    /// <summary>Makes a stand-in for a reference picture that is missing and adds it to the buffer.</summary>
    /// <param name="dpb">The decoded picture buffer.</param>
    /// <param name="makeMissing">Makes the picture.</param>
    /// <param name="poc">Its picture order count.</param>
    /// <returns>The picture.</returns>
    private static H265Picture CreateMissing(H265Dpb dpb, Func<int, H265Picture> makeMissing, int poc)
    {
        var picture = makeMissing(poc);
        dpb.Pictures.Add(picture);
        return picture;
    }
}
