// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Reads the side information of a CELT frame that comes before the band shapes (RFC 6716 sections 4.3.1 to 4.3.3).</summary>
internal static class CeltSideInfo
{
    /// <summary>The fractional bits of the bit counts.</summary>
    private const int FractionBits = CeltAllocation.FractionBits;

    /// <summary>The bits of the post-filter's pitch before the octave adds more.</summary>
    private const int PostFilterPitchBits = 4;

    /// <summary>The gain of the post-filter for each step of its quantised gain.</summary>
    private const float PostFilterGainStep = 0.09375F;

    /// <summary>The octaves the post-filter's pitch is split into.</summary>
    private const int PostFilterOctaves = 6;

    /// <summary>The pitch of the lowest octave.</summary>
    private const int PostFilterBasePitch = 16;

    /// <summary>The bits of the quantised gain.</summary>
    private const int PostFilterGainBits = 3;

    /// <summary>The bits the post-filter's tap set needs.</summary>
    private const int TapsetBits = 2;

    /// <summary>The bits of a tap set's symbol.</summary>
    private const int TapsetSymbolBits = 2;

    /// <summary>The bits the spreading symbol needs.</summary>
    private const int SpreadBits = 4;

    /// <summary>The bits of the spreading symbol's distribution.</summary>
    private const int SpreadSymbolBits = 5;

    /// <summary>The spreading decision when none is coded.</summary>
    private const int DefaultSpread = CeltPvq.SpreadNormal;

    /// <summary>The probability of a time-frequency change in a transient frame.</summary>
    private const int ChangeOddsTransient = 2;

    /// <summary>The probability of a time-frequency change in a frame of one long block, for the first band.</summary>
    private const int ChangeOddsLong = 4;

    /// <summary>The probability of a time-frequency change after the first band in a transient frame.</summary>
    private const int FollowOddsTransient = 4;

    /// <summary>The probability of a time-frequency change after the first band in a frame of one long block.</summary>
    private const int FollowOddsLong = 5;

    /// <summary>The entries of one block size in the time-frequency table.</summary>
    private const int SelectRow = 8;

    /// <summary>The distance in the table between the entries for no time-frequency selection and for the selection.</summary>
    private const int SelectStep = 2;

    /// <summary>The entries of the table for a transient frame.</summary>
    private const int SelectTransientRow = 4;

    /// <summary>The bits of a dynamic allocation flag, as a probability for the first flag.</summary>
    private const int DynamicOdds = 6;

    /// <summary>The probability of every flag after the first.</summary>
    private const int DynamicFollowOdds = 1;

    /// <summary>The fewest the odds of the first flag of a band fall to.</summary>
    private const int DynamicLowestOdds = 2;

    /// <summary>The smallest boost a dynamic allocation flag adds, in bits.</summary>
    private const int DynamicQuantaBits = 6;

    /// <summary>The bits of the allocation trim's distribution.</summary>
    private const int TrimSymbolBits = 7;

    /// <summary>The allocation trim when none is coded.</summary>
    private const int DefaultTrim = 5;

    /// <summary>Reads the post-filter flag and, if it is set, its parameters.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="totalBits">The bits in the frame.</param>
    /// <returns>The post-filter's parameters; a gain of zero means none.</returns>
    internal static CeltPostFilterSettings ReadPostFilter(ref OpusRangeDecoder decoder, int totalBits)
    {
        if (!decoder.ReadBit(1))
        {
            return new(0, 0F, 0);
        }

        var octave = (int)decoder.ReadUnsigned(PostFilterOctaves);
        var pitch = (PostFilterBasePitch << octave) + (int)decoder.ReadRaw(PostFilterPitchBits + octave) - 1;
        var quantisedGain = (int)decoder.ReadRaw(PostFilterGainBits);
        var tapset = decoder.Tell() + TapsetBits <= totalBits ? decoder.ReadSymbol(CeltTables.TapSetIcdf, TapsetSymbolBits) : 0;
        return new(pitch, PostFilterGainStep * (quantisedGain + 1), tapset);
    }

    /// <summary>Reads the time-frequency change of every band.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame, whose time-frequency changes are filled in.</param>
    /// <param name="transient">Whether the frame is split into short blocks.</param>
    internal static void ReadTimeFrequency(ref OpusRangeDecoder decoder, CeltFrame frame, bool transient)
    {
        var budget = decoder.TotalBits;
        var tell = decoder.Tell();
        var odds = transient ? ChangeOddsTransient : ChangeOddsLong;
        var selectable = frame.FrameSize > 0 && tell + odds + 1 <= budget;
        if (selectable)
        {
            budget--;
        }

        var changed = ReadChanges(ref decoder, frame, transient, budget);
        var row = (frame.FrameSize * SelectRow) + (transient ? SelectTransientRow : 0);
        var select = selectable && CeltTables.TimeFrequencySelect[row + changed] != CeltTables.TimeFrequencySelect[row + SelectStep + changed] && decoder.ReadBit(1) ? 1 : 0;
        for (var band = frame.Start; band < frame.End; band++)
        {
            frame.TimeFrequency[band] = CeltTables.TimeFrequencySelect[row + (SelectStep * select) + frame.TimeFrequency[band]];
        }
    }

    /// <summary>Reads the spreading decision.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="totalBits">The bits in the frame.</param>
    /// <returns>The spreading decision.</returns>
    internal static int ReadSpread(ref OpusRangeDecoder decoder, int totalBits) =>
        decoder.Tell() + SpreadBits <= totalBits ? decoder.ReadSymbol(CeltTables.SpreadIcdf, SpreadSymbolBits) : DefaultSpread;

    /// <summary>Reads the extra bits bands ask for.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame, whose offsets are filled in.</param>
    /// <param name="totalBits">The bits in the frame, in eighths.</param>
    /// <returns>The trim of the allocation.</returns>
    internal static int ReadAllocationRequests(ref OpusRangeDecoder decoder, CeltFrame frame, int totalBits)
    {
        var odds = DynamicOdds;
        var tell = decoder.TellFractional();
        for (var band = frame.Start; band < frame.End; band++)
        {
            var boost = ReadBoost(ref decoder, frame, band, odds, ref tell, ref totalBits);
            frame.Offsets[band] = boost;
            if (boost > 0)
            {
                odds = Math.Max(DynamicLowestOdds, odds - 1);
            }
        }

        return tell + (DynamicOdds << FractionBits) <= totalBits ? decoder.ReadSymbol(CeltTables.TrimIcdf, TrimSymbolBits) : DefaultTrim;
    }

    /// <summary>Reads the time-frequency flags and collects whether any changed.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame, whose changes are filled in as 0 or 1.</param>
    /// <param name="transient">Whether the frame is split into short blocks.</param>
    /// <param name="budget">The bits available for the flags.</param>
    /// <returns>One if any band changed, otherwise zero.</returns>
    private static int ReadChanges(ref OpusRangeDecoder decoder, CeltFrame frame, bool transient, int budget)
    {
        var odds = transient ? ChangeOddsTransient : ChangeOddsLong;
        var current = 0;
        var changed = 0;
        var tell = decoder.Tell();
        for (var band = frame.Start; band < frame.End; band++)
        {
            if (tell + odds <= budget)
            {
                current ^= decoder.ReadBit(odds) ? 1 : 0;
                tell = decoder.Tell();
                changed |= current;
            }

            frame.TimeFrequency[band] = current;
            odds = transient ? FollowOddsTransient : FollowOddsLong;
        }

        return changed;
    }

    /// <summary>Reads how much extra a band is boosted by.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="band">The band.</param>
    /// <param name="odds">The odds of the first flag.</param>
    /// <param name="tell">The bits used so far in eighths, updated.</param>
    /// <param name="totalBits">The bits available in eighths, reduced by the boost.</param>
    /// <returns>The boost in eighths of a bit.</returns>
    private static int ReadBoost(ref OpusRangeDecoder decoder, CeltFrame frame, int band, int odds, ref int tell, ref int totalBits)
    {
        var width = (frame.Channels * (CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band])) << frame.FrameSize;
        var quanta = Math.Min(width << FractionBits, Math.Max(DynamicQuantaBits << FractionBits, width));
        var boost = 0;
        var loopOdds = odds;
        while (tell + (loopOdds << FractionBits) < totalBits && boost < frame.Caps[band])
        {
            var flag = decoder.ReadBit(loopOdds);
            tell = decoder.TellFractional();
            if (!flag)
            {
                break;
            }

            boost += quanta;
            totalBits -= quanta;
            loopOdds = DynamicFollowOdds;
        }

        return boost;
    }
}
