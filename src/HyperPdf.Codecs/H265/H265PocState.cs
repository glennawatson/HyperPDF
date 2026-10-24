// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Works out the picture order count of each picture from the low bits in its slice header (8.3.1).</summary>
internal sealed class H265PocState
{
    /// <summary>The picture order count of the previous picture of the lowest temporal sub-layer that other pictures may refer to.</summary>
    private int _previousTid0;

    /// <summary>Forgets the previous picture, as at the start of a stream.</summary>
    internal void Reset() => _previousTid0 = 0;

    /// <summary>Works out the picture order count of a picture.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="header">The slice segment header.</param>
    /// <param name="nalType">The NAL unit type.</param>
    /// <param name="temporalId">The temporal sub-layer of the picture.</param>
    /// <param name="noRaslOutput">Whether the picture is an IRAP picture that starts a new coded video sequence.</param>
    /// <returns>The picture order count.</returns>
    internal int Compute(H265Sps sps, H265SliceHeader header, int nalType, int temporalId, bool noRaslOutput)
    {
        var maxLsb = 1 << sps.Log2MaxPocLsb;
        var msb = 0;
        if (!(H265Nal.IsIrap(nalType) && noRaslOutput))
        {
            var previousLsb = _previousTid0 & (maxLsb - 1);
            msb = MsbOf(header.PocLsb, previousLsb, _previousTid0 - previousLsb, maxLsb);
        }

        var poc = msb + header.PocLsb;
        if (temporalId == 0 && !IsLeading(nalType) && !H265Nal.IsSubLayerNonReference(nalType))
        {
            _previousTid0 = poc;
        }

        return poc;
    }

    /// <summary>Works out the high part of the picture order count (8-1).</summary>
    /// <param name="lsb">The low bits of the current picture.</param>
    /// <param name="previousLsb">The low bits of the previous picture.</param>
    /// <param name="previousMsb">The high part of the previous picture.</param>
    /// <param name="maxLsb">The range of the low bits.</param>
    /// <returns>The high part.</returns>
    private static int MsbOf(int lsb, int previousLsb, int previousMsb, int maxLsb)
    {
        var half = maxLsb >> 1;
        if (lsb < previousLsb && previousLsb - lsb >= half)
        {
            return previousMsb + maxLsb;
        }

        return lsb > previousLsb && lsb - previousLsb > half ? previousMsb - maxLsb : previousMsb;
    }

    /// <summary>Tells whether a NAL unit type is that of a RASL or RADL picture.</summary>
    /// <param name="nalType">The type.</param>
    /// <returns><see langword="true"/> for the leading picture types.</returns>
    private static bool IsLeading(int nalType) => nalType is >= H265Nal.RadlN and <= H265Nal.RaslR;
}
