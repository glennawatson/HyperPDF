// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// Joint stereo: in mid and side coding the two channels carry the sum and difference of left and right, and in intensity coding the upper bands
/// of the right channel are dropped and the left channel's coefficients are shared out between the two by a ratio.
/// </summary>
internal static class Mp3Stereo
{
    /// <summary>The bit of the mode extension that turns on mid and side stereo.</summary>
    internal const int MidSideBit = 2;

    /// <summary>The window groups of short blocks.</summary>
    private const int Windows = 3;

    /// <summary>The intensity position that means the band is not intensity coded, in MPEG 1.</summary>
    private const int Mpeg1NoIntensity = 7;

    /// <summary>The intensity position that means the band is not intensity coded, in MPEG 2.</summary>
    private const int Mpeg2NoIntensity = 64;

    /// <summary>The intensity position the last band gets in MPEG 1 when the band below it is coded.</summary>
    private const int Mpeg1DefaultPosition = 3;

    /// <summary>The bit of the mode extension that turns on intensity stereo.</summary>
    private const int IntensityBit = 1;

    /// <summary>The square root of two, which restores the level the gain took off in a mid and side frame.</summary>
    private const float Root2 = 1.4142135F;

    /// <summary>Two, the base of the steps of MPEG 2 intensity stereo.</summary>
    private const float Two = 2F;

    /// <summary>The quarter steps in an octave.</summary>
    private const float QuartersPerOctave = 4F;

    /// <summary>Applies the joint stereo of a frame to one granule.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the right channel, which says how the bands are split.</param>
    /// <param name="widths">The widths of the scale factor entries.</param>
    /// <param name="positions">The scale factors of the right channel, which are its intensity positions.</param>
    /// <param name="left">The left (or mid) channel's coefficients.</param>
    /// <param name="right">The right (or side) channel's coefficients.</param>
    internal static void Process(in Mp3FrameHeader header, Mp3GranuleChannel info, ReadOnlySpan<byte> widths, Span<int> positions, Span<float> left, Span<float> right)
    {
        var midSide = (header.ModeExtension & MidSideBit) != 0;
        if ((header.ModeExtension & IntensityBit) != 0)
        {
            ProcessIntensity(header, info, widths, positions, left, right);
        }
        else if (midSide)
        {
            SumAndDifference(left, right);
        }
    }

    /// <summary>Finds, for each window group, the highest scale factor entry in which the right channel has a coefficient.</summary>
    /// <param name="widths">The widths of the entries.</param>
    /// <param name="right">The right channel's coefficients.</param>
    /// <param name="topBand">Receives the entry index for each group, or -1.</param>
    private static void FindTopBands(ReadOnlySpan<byte> widths, ReadOnlySpan<float> right, Span<int> topBand)
    {
        topBand.Fill(-1);
        var line = 0;
        for (var entry = 0; entry < widths.Length; entry++)
        {
            if (right.Slice(line, widths[entry]).ContainsAnyExcept(0F))
            {
                topBand[entry % Windows] = entry;
            }

            line += widths[entry];
        }
    }

    /// <summary>Works out two to the power of minus a number of quarters.</summary>
    /// <param name="quarters">The number of quarters.</param>
    /// <returns>The factor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Quarter(int quarters) => MathF.Pow(Two, -quarters / QuartersPerOctave);

    /// <summary>Shares the left channel's coefficients between the channels by the band's intensity position.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="position">The band's intensity position.</param>
    /// <param name="scale">For MPEG 2, the low bit of the scale factor code, which picks the step of the ratios.</param>
    /// <param name="midSide">Whether the frame is also mid and side coded, so the level is restored.</param>
    /// <param name="left">The left channel's band, holding the data on the way in.</param>
    /// <param name="right">The right channel's band.</param>
    private static void ShareOut(in Mp3FrameHeader header, int position, int scale, bool midSide, Span<float> left, Span<float> right)
    {
        float toLeft;
        float toRight;
        if (header.IsMpeg1)
        {
            toLeft = Mp3Tables.IntensityLeft.Span[position];
            toRight = Mp3Tables.IntensityRight.Span[position];
        }
        else
        {
            var drop = Quarter(((position + 1) >> 1) << scale);
            toLeft = (position & 1) != 0 ? drop : 1F;
            toRight = (position & 1) != 0 ? 1F : drop;
        }

        var boost = midSide ? Root2 : 1F;
        for (var index = 0; index < left.Length; index++)
        {
            var value = left[index];
            right[index] = value * toRight * boost;
            left[index] = value * toLeft * boost;
        }
    }

    /// <summary>Turns mid and side into left and right: left is their sum and right their difference, the factor of one over the square root of two already being in the gain.</summary>
    /// <param name="left">The mid channel, which becomes left.</param>
    /// <param name="right">The side channel, which becomes right.</param>
    private static void SumAndDifference(Span<float> left, Span<float> right)
    {
        var index = 0;
        var step = Vector<float>.Count;
        for (; index + step <= left.Length; index += step)
        {
            var mid = new Vector<float>(left[index..]);
            var side = new Vector<float>(right[index..]);
            (mid + side).CopyTo(left[index..]);
            (mid - side).CopyTo(right[index..]);
        }

        for (; index < left.Length; index++)
        {
            var mid = left[index];
            var side = right[index];
            left[index] = mid + side;
            right[index] = mid - side;
        }
    }

    /// <summary>Gives the last entries an intensity position: the default when the entry below them holds data, the one below them otherwise.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="groups">The window groups: 3 for short windows, 1 otherwise.</param>
    /// <param name="topBand">The highest entry with data for each group.</param>
    /// <param name="positions">The intensity positions; the last entries are set.</param>
    /// <param name="entries">The number of entries.</param>
    private static void SetLastPositions(in Mp3FrameHeader header, int groups, ReadOnlySpan<int> topBand, Span<int> positions, int entries)
    {
        var fallback = header.IsMpeg1 ? Mpeg1DefaultPosition : 0;
        for (var window = 0; window < groups; window++)
        {
            var top = entries - groups + window;
            var previous = top - groups;
            positions[top] = topBand[window] >= previous ? fallback : positions[previous];
        }
    }

    /// <summary>Applies intensity stereo, with mid and side coding for the bands that are not intensity coded when the frame has both.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the right channel.</param>
    /// <param name="widths">The widths of the scale factor entries.</param>
    /// <param name="positions">The intensity positions.</param>
    /// <param name="left">The left channel's coefficients.</param>
    /// <param name="right">The right channel's coefficients.</param>
    private static void ProcessIntensity(in Mp3FrameHeader header, Mp3GranuleChannel info, ReadOnlySpan<byte> widths, Span<int> positions, Span<float> left, Span<float> right)
    {
        var midSide = (header.ModeExtension & MidSideBit) != 0;
        Span<int> topBand = stackalloc int[Windows];
        FindTopBands(widths, right, topBand);
        if (!info.IsShort || info.MixedBlock)
        {
            topBand.Fill(Math.Max(Math.Max(topBand[0], topBand[1]), topBand[2]));
        }

        SetLastPositions(header, info.IsShort ? Windows : 1, topBand, positions, widths.Length);
        var limit = header.IsMpeg1 ? Mpeg1NoIntensity : Mpeg2NoIntensity;
        var line = 0;
        for (var entry = 0; entry < widths.Length; entry++)
        {
            var leftBand = left.Slice(line, widths[entry]);
            var rightBand = right.Slice(line, widths[entry]);
            if (entry > topBand[entry % Windows] && positions[entry] < limit)
            {
                ShareOut(header, positions[entry], info.ScalefacCompress & 1, midSide, leftBand, rightBand);
            }
            else if (midSide)
            {
                SumAndDifference(leftBand, rightBand);
            }

            line += widths[entry];
        }
    }
}
