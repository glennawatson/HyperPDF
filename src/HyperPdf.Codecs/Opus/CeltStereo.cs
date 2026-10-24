// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Turns the mid and side halves of a stereo CELT band into left and right (RFC 6716 section 4.3.4.5).</summary>
internal static class CeltStereo
{
    /// <summary>The energy below which a channel is too quiet to scale, so both channels become the mid.</summary>
    private const float QuietEnergy = 6e-4F;

    /// <summary>The entries of a band that is coded with a single bit for the side.</summary>
    private const int TwoEntries = 2;

    /// <summary>Finishes a stereo band that was read as mid and side.</summary>
    /// <param name="mid">The mid, replaced by the left.</param>
    /// <param name="side">The side, replaced by the right.</param>
    /// <param name="midGain">The gain of the mid.</param>
    /// <param name="inverted">Whether the side is inverted.</param>
    internal static void Finish(Span<float> mid, Span<float> side, float midGain, bool inverted)
    {
        if (mid.Length != TwoEntries)
        {
            Merge(mid, side, midGain);
        }

        if (!inverted)
        {
            return;
        }

        for (var j = 0; j < side.Length; j++)
        {
            side[j] = -side[j];
        }
    }

    /// <summary>Scales the two entries of each channel of a band of two entries and turns them from mid and side into left and right.</summary>
    /// <param name="mid">The two entries of the mid, replaced by the left.</param>
    /// <param name="side">The two entries of the side, replaced by the right.</param>
    /// <param name="midGain">The gain of the mid.</param>
    /// <param name="sideGain">The gain of the side.</param>
    internal static void SplitTwoEntries(Span<float> mid, Span<float> side, float midGain, float sideGain)
    {
        for (var j = 0; j < TwoEntries; j++)
        {
            var scaledMid = midGain * mid[j];
            var scaledSide = sideGain * side[j];
            mid[j] = scaledMid - scaledSide;
            side[j] = scaledMid + scaledSide;
        }
    }

    /// <summary>Turns the mid and side of a band into left and right, scaling each to unit energy.</summary>
    /// <param name="x">The mid, replaced by the left.</param>
    /// <param name="y">The side, replaced by the right.</param>
    /// <param name="mid">The gain of the mid.</param>
    private static void Merge(Span<float> x, Span<float> y, float mid)
    {
        float cross = 0;
        float sideEnergy = 0;
        for (var j = 0; j < x.Length; j++)
        {
            cross += x[j] * y[j];
            sideEnergy += y[j] * y[j];
        }

        cross *= mid;
        var total = (mid * mid) + sideEnergy;
        var left = total - (TwoEntries * cross);
        var right = total + (TwoEntries * cross);
        if (right < QuietEnergy || left < QuietEnergy)
        {
            x.CopyTo(y);
            return;
        }

        var leftGain = 1F / MathF.Sqrt(left);
        var rightGain = 1F / MathF.Sqrt(right);
        for (var j = 0; j < x.Length; j++)
        {
            var l = mid * x[j];
            var r = y[j];
            x[j] = leftGain * (l - r);
            y[j] = rightGain * (l + r);
        }
    }
}
