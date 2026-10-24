// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Shares the bits of a CELT frame out between the bands (RFC 6716 section 4.3.3): a table of static allocations is
/// interpolated for the quality that fits, tilted by the frame's trim, and the bands that would get too little are dropped.
/// </summary>
internal static class CeltAllocation
{
    /// <summary>The fractional bits in the units the allocation works in: eighths of a bit.</summary>
    internal const int FractionBits = 3;

    /// <summary>The steps the interpolation between two table rows is divided into, as a power of two.</summary>
    private const int InterpolationSteps = 6;

    /// <summary>The rows in the static allocation table.</summary>
    private const int AllocationVectors = 11;

    /// <summary>The offset that centres the number of fine energy bits on the log of the band size.</summary>
    private const int FineOffset = 21;

    /// <summary>The shift that turns the allocation table's 1/32 bit per sample into eighths of a bit.</summary>
    private const int TableShift = 2;

    /// <summary>The shift that brings the trim tilt back to the units of the allocation.</summary>
    private const int TrimShift = 6;

    /// <summary>The default allocation trim.</summary>
    private const int NeutralTrim = 5;

    /// <summary>The shift that sizes the threshold below which no pulse bits are allocated.</summary>
    private const int ThresholdShift = 4;

    /// <summary>The factor of three in the threshold.</summary>
    private const int ThresholdFactor = 3;

    /// <summary>The steps of the binary search for the number of pulses.</summary>
    private const int PulseSearchSteps = 6;

    /// <summary>The pulse steps that count one for one.</summary>
    private const int LinearPulses = 8;

    /// <summary>The mask of the part of a pulse step that is below the shift.</summary>
    private const int PulseMask = 7;

    /// <summary>The bits the exponent of a pulse step moves up by.</summary>
    private const int PulseExponentShift = 3;

    /// <summary>The cache offset a block takes over a band's own size.</summary>
    private const int CacheBlockOffset = 1;

    /// <summary>The offset added to a band's cap so it is never negative.</summary>
    private const int CapOffset = 64;

    /// <summary>The channels of a stereo frame.</summary>
    private const int StereoChannels = 2;

    /// <summary>The bits per band entry above which a band of two entries is not treated as a single one.</summary>
    private const int TwoEntries = 2;

    /// <summary>The factor of three in the thresholds of the bits that make a fine energy bit worthwhile.</summary>
    private const int ThirdFine = 3;

    /// <summary>Gets the cost in eighths of a bit of coding which of a number of bands is the first intensity stereo band.</summary>
    private static ReadOnlySpan<byte> Log2Fraction =>
    [
        0x00, 0x08, 0x0D, 0x10, 0x13, 0x15, 0x17, 0x18, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x20, 0x21, 0x22, 0x22, 0x23, 0x24, 0x24, 0x25, 0x25,
    ];

    /// <summary>Works out the most bits each band can use, from its width, the frame size and the channel count.</summary>
    /// <param name="frame">The frame, whose caps are filled in.</param>
    internal static void InitializeCaps(CeltFrame frame)
    {
        for (var band = 0; band < CeltEnergy.Bands; band++)
        {
            var width = (CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band]) << frame.FrameSize;
            var cap = CeltTables.PulseCacheCaps[(CeltEnergy.Bands * ((TwoEntries * frame.FrameSize) + frame.Channels - 1)) + band];
            frame.Caps[band] = ((cap + CapOffset) * frame.Channels * width) >> TableShift;
        }
    }

    /// <summary>Gets the number of pulses a coded step stands for; the steps grow roughly geometrically.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The pulses.</returns>
    internal static int GetPulses(int step) => step < LinearPulses ? step : (LinearPulses + (step & PulseMask)) << ((step >> PulseExponentShift) - 1);

    /// <summary>Finds the number of pulse steps whose cost is nearest a number of bits.</summary>
    /// <param name="band">The band.</param>
    /// <param name="frameSize">The log2 of the block size the band is coded in.</param>
    /// <param name="bits">The bits to spend, in eighths.</param>
    /// <returns>The step.</returns>
    internal static int BitsToPulses(int band, int frameSize, int bits)
    {
        var cache = GetCache(band, frameSize);
        var low = 0;
        var high = (int)cache[0];
        bits--;
        for (var i = 0; i < PulseSearchSteps; i++)
        {
            var middle = (low + high + 1) >> 1;
            if (cache[middle] >= bits)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return bits - (low == 0 ? -1 : cache[low]) <= cache[high] - bits ? low : high;
    }

    /// <summary>Gets the cost in eighths of a bit of a number of pulse steps.</summary>
    /// <param name="band">The band.</param>
    /// <param name="frameSize">The log2 of the block size the band is coded in.</param>
    /// <param name="pulses">The steps.</param>
    /// <returns>The cost.</returns>
    internal static int PulsesToBits(int band, int frameSize, int pulses) => pulses == 0 ? 0 : GetCache(band, frameSize)[pulses] + 1;

    /// <summary>Gets the row of the pulse cache for a band at a block size.</summary>
    /// <param name="band">The band.</param>
    /// <param name="frameSize">The log2 of the block size the band is coded in; a band that is being split passes one less.</param>
    /// <returns>The cost of each pulse count, the first byte being the largest count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetCache(int band, int frameSize) =>
        CeltTables.PulseCacheBits[CeltTables.PulseCacheIndex[((frameSize + CacheBlockOffset) * CeltEnergy.Bands) + band]..];

    /// <summary>Allocates the bits of a frame.</summary>
    /// <param name="decoder">The range decoder, which signals the skipped bands and the stereo parameters.</param>
    /// <param name="frame">The frame, whose request is read and whose allocation is filled in.</param>
    internal static void Allocate(ref OpusRangeDecoder decoder, CeltFrame frame)
    {
        var channels = frame.Channels;
        var total = Math.Max(frame.TotalBits, 0);
        var skipReservation = total >= 1 << FractionBits ? 1 << FractionBits : 0;
        total -= skipReservation;
        var intensityReservation = 0;
        var dualStereoReservation = 0;
        if (channels == StereoChannels)
        {
            intensityReservation = Log2Fraction[frame.End - frame.Start];
            if (intensityReservation > total)
            {
                intensityReservation = 0;
            }
            else
            {
                total -= intensityReservation;
                dualStereoReservation = total >= 1 << FractionBits ? 1 << FractionBits : 0;
                total -= dualStereoReservation;
            }
        }

        PrepareThresholds(frame);
        var upper = FindUpperRow(frame, total);
        var skipStart = FillRows(frame, upper - 1, upper);
        var reserved = new Reservations(total, skipStart, skipReservation, intensityReservation, dualStereoReservation);
        Interpolate(ref decoder, frame, reserved);
    }

    /// <summary>Works out, for each band, the bits below which it gets no pulses and how far the trim tilts it.</summary>
    /// <param name="frame">The frame.</param>
    private static void PrepareThresholds(CeltFrame frame)
    {
        for (var band = frame.Start; band < frame.End; band++)
        {
            var width = CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band];
            frame.Thresholds[band] = Math.Max(frame.Channels << FractionBits, ((ThresholdFactor * width) << frame.FrameSize << FractionBits) >> ThresholdShift);
            frame.TrimOffsets[band] = (frame.Channels * width * (frame.Trim - NeutralTrim - frame.FrameSize) * (frame.End - band - 1) * (1 << (frame.FrameSize + FractionBits))) >> TrimShift;
            if (width << frame.FrameSize == 1)
            {
                frame.TrimOffsets[band] -= frame.Channels << FractionBits;
            }
        }
    }

    /// <summary>Gets what a band is given at a row of the allocation table before the trim and offsets.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="row">The row.</param>
    /// <param name="band">The band.</param>
    /// <returns>The bits in eighths.</returns>
    private static int RowBits(CeltFrame frame, int row, int band)
    {
        var width = CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band];
        return (frame.Channels * width * CeltTables.BandAllocation[(row * CeltEnergy.Bands) + band]) << frame.FrameSize >> TableShift;
    }

    /// <summary>Finds the first row of the allocation table that gives more bits than are available.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="total">The bits available.</param>
    /// <returns>The row.</returns>
    private static int FindUpperRow(CeltFrame frame, int total)
    {
        var low = 1;
        var high = AllocationVectors - 1;
        while (low <= high)
        {
            var done = false;
            var sum = 0;
            var middle = (low + high) >> 1;
            for (var band = frame.End - 1; band >= frame.Start; band--)
            {
                var bits = RowBits(frame, middle, band);
                if (bits > 0)
                {
                    bits = Math.Max(0, bits + frame.TrimOffsets[band]);
                }

                bits += frame.Offsets[band];
                if (bits >= frame.Thresholds[band] || done)
                {
                    done = true;
                    sum += Math.Min(bits, frame.Caps[band]);
                }
                else if (bits >= frame.Channels << FractionBits)
                {
                    sum += frame.Channels << FractionBits;
                }
            }

            if (sum > total)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    /// <summary>Fills in each band's bits at the two rows the frame falls between.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="lower">The row below.</param>
    /// <param name="upper">The row above.</param>
    /// <returns>The last band that was boosted, which may not be skipped.</returns>
    private static int FillRows(CeltFrame frame, int lower, int upper)
    {
        var skipStart = frame.Start;
        for (var band = frame.Start; band < frame.End; band++)
        {
            var lowBits = RowBits(frame, lower, band);
            var highBits = upper >= AllocationVectors ? frame.Caps[band] : RowBits(frame, upper, band);
            if (lowBits > 0)
            {
                lowBits = Math.Max(0, lowBits + frame.TrimOffsets[band]);
            }

            if (highBits > 0)
            {
                highBits = Math.Max(0, highBits + frame.TrimOffsets[band]);
            }

            if (lower > 0)
            {
                lowBits += frame.Offsets[band];
            }

            highBits += frame.Offsets[band];
            if (frame.Offsets[band] > 0)
            {
                skipStart = band;
            }

            frame.LowerBits[band] = lowBits;
            frame.UpperBits[band] = Math.Max(0, highBits - lowBits);
        }

        return skipStart;
    }

    /// <summary>Interpolates between the two table rows, drops the bands that cannot be afforded, and splits the bits between fine energy and shape.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="reserved">The bits set aside for signalling.</param>
    private static void Interpolate(ref OpusRangeDecoder decoder, CeltFrame frame, Reservations reserved)
    {
        var floor = frame.Channels << FractionBits;
        var total = reserved.Total;
        var lo = 0;
        var hi = 1 << InterpolationSteps;
        for (var step = 0; step < InterpolationSteps; step++)
        {
            var middle = (lo + hi) >> 1;
            if (SumAt(frame, middle, floor) > total)
            {
                hi = middle;
            }
            else
            {
                lo = middle;
            }
        }

        var sum = 0;
        var done = false;
        for (var band = frame.End - 1; band >= frame.Start; band--)
        {
            var amount = frame.LowerBits[band] + ((lo * frame.UpperBits[band]) >> InterpolationSteps);
            if (amount < frame.Thresholds[band] && !done)
            {
                amount = amount >= floor ? floor : 0;
            }
            else
            {
                done = true;
            }

            amount = Math.Min(amount, frame.Caps[band]);
            frame.Pulses[band] = amount;
            sum += amount;
        }

        var skip = new SkipState(sum, total, reserved.Intensity);
        var coded = SkipBands(ref decoder, frame, reserved, ref skip);
        frame.CodedBands = coded;
        frame.Intensity = skip.IntensityReservation > 0 ? frame.Start + (int)decoder.ReadUnsigned((uint)(coded + 1 - frame.Start)) : 0;
        var dualStereoReservation = reserved.DualStereo;
        if (frame.Intensity <= frame.Start)
        {
            skip.Total += dualStereoReservation;
            dualStereoReservation = 0;
        }

        frame.DualStereo = dualStereoReservation > 0 && decoder.ReadBit(1);
        Distribute(frame, skip.Total - skip.Sum);
    }

    /// <summary>Adds up the bits the bands would get at one position between the two rows.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="position">The position, out of 64.</param>
    /// <param name="floor">The least a band can be given.</param>
    /// <returns>The sum.</returns>
    private static int SumAt(CeltFrame frame, int position, int floor)
    {
        var sum = 0;
        var done = false;
        for (var band = frame.End - 1; band >= frame.Start; band--)
        {
            var amount = frame.LowerBits[band] + ((position * frame.UpperBits[band]) >> InterpolationSteps);
            if (amount >= frame.Thresholds[band] || done)
            {
                done = true;
                sum += Math.Min(amount, frame.Caps[band]);
            }
            else if (amount >= floor)
            {
                sum += floor;
            }
        }

        return sum;
    }

    /// <summary>Works back from the top band deciding which bands to skip, reading a bit for each band that could be.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The frame, whose pulses are the bits of each band and are updated for the bands skipped.</param>
    /// <param name="reserved">The reservations.</param>
    /// <param name="skip">The bits handed out and available, updated.</param>
    /// <returns>The coded bands.</returns>
    private static int SkipBands(ref OpusRangeDecoder decoder, CeltFrame frame, Reservations reserved, ref SkipState skip)
    {
        var floor = frame.Channels << FractionBits;
        var bits = frame.Pulses;
        var coded = frame.End;
        for (; coded > reserved.SkipStart + 1; coded--)
        {
            var band = coded - 1;
            var span = CeltTables.BandEdges[coded] - CeltTables.BandEdges[frame.Start];
            var left = skip.Total - skip.Sum;
            var perCoefficient = left / span;
            left -= span * perCoefficient;
            var remainder = Math.Max(left - (CeltTables.BandEdges[band] - CeltTables.BandEdges[frame.Start]), 0);
            var width = CeltTables.BandEdges[coded] - CeltTables.BandEdges[band];
            var bandBits = bits[band] + (perCoefficient * width) + remainder;
            if (bandBits >= Math.Max(frame.Thresholds[band], floor + (1 << FractionBits)))
            {
                if (decoder.ReadBit(1))
                {
                    return coded;
                }

                skip.Sum += 1 << FractionBits;
                bandBits -= 1 << FractionBits;
            }

            skip.Sum -= bits[band] + skip.IntensityReservation;
            if (skip.IntensityReservation > 0)
            {
                skip.IntensityReservation = Log2Fraction[band - frame.Start];
            }

            skip.Sum += skip.IntensityReservation;
            if (bandBits >= floor)
            {
                skip.Sum += floor;
                bits[band] = floor;
            }
            else
            {
                bits[band] = 0;
            }
        }

        skip.Total += reserved.Skip;
        return coded;
    }

    /// <summary>Shares the leftover bits and splits each band's bits into fine energy bits and shape bits.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="left">The bits not yet given to a band.</param>
    private static void Distribute(CeltFrame frame, int left)
    {
        var coded = frame.CodedBands;
        var bits = frame.Pulses;
        var span = CeltTables.BandEdges[coded] - CeltTables.BandEdges[frame.Start];
        var perCoefficient = left / span;
        left -= span * perCoefficient;
        for (var band = frame.Start; band < coded; band++)
        {
            bits[band] += perCoefficient * (CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band]);
        }

        for (var band = frame.Start; band < coded; band++)
        {
            var take = Math.Min(left, CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band]);
            bits[band] += take;
            left -= take;
        }

        var balance = 0;
        var band2 = frame.Start;
        for (; band2 < coded; band2++)
        {
            balance = SplitBand(frame, band2, balance);
        }

        frame.Balance = balance;
        var stereo = frame.Channels > 1 ? 1 : 0;
        for (; band2 < frame.End; band2++)
        {
            frame.FineBits[band2] = bits[band2] >> stereo >> FractionBits;
            bits[band2] = 0;
            frame.FinePriority[band2] = frame.FineBits[band2] < 1 ? 1 : 0;
        }
    }

    /// <summary>Splits one band's bits into fine energy bits and shape bits.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="band">The band.</param>
    /// <param name="balance">The bits the bands before it could not use.</param>
    /// <returns>The bits this band could not use.</returns>
    private static int SplitBand(CeltFrame frame, int band, int balance)
    {
        var size = (CeltTables.BandEdges[band + 1] - CeltTables.BandEdges[band]) << frame.FrameSize;
        frame.Pulses[band] += balance;
        var excess = size > 1 ? SplitWideBand(frame, band, size) : SplitSingleEntryBand(frame, band);
        return excess > 0 ? excess - GiveExtraFineBits(frame, band, excess, balance) : excess;
    }

    /// <summary>Gives the bits a band cannot use to its fine energy, since that cannot use the rebalancing the shapes get.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="band">The band.</param>
    /// <param name="excess">The bits the band cannot use.</param>
    /// <param name="balance">The bits the bands before it could not use.</param>
    /// <returns>The bits that were spent.</returns>
    private static int GiveExtraFineBits(CeltFrame frame, int band, int excess, int balance)
    {
        var channels = frame.Channels;
        var extraFine = Math.Min(excess >> ((channels > 1 ? 1 : 0) + FractionBits), CeltEnergy.MaxFineBits - frame.FineBits[band]);
        frame.FineBits[band] += extraFine;
        var extraBits = (extraFine * channels) << FractionBits;
        frame.FinePriority[band] = extraBits >= excess - balance ? 1 : 0;
        return extraBits;
    }

    /// <summary>Splits the bits of a band of one entry: they all go to the shape, and any above two go back to the balance.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="band">The band.</param>
    /// <returns>The bits the band cannot use.</returns>
    private static int SplitSingleEntryBand(CeltFrame frame, int band)
    {
        var excess = Math.Max(0, frame.Pulses[band] - (frame.Channels << FractionBits));
        frame.Pulses[band] -= excess;
        frame.FineBits[band] = 0;
        frame.FinePriority[band] = 1;
        return excess;
    }

    /// <summary>Splits the bits of a band of several entries between fine energy and shape.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="band">The band.</param>
    /// <param name="size">The entries in the band for the frame's block size.</param>
    /// <returns>The bits the band cannot use.</returns>
    private static int SplitWideBand(CeltFrame frame, int band, int size)
    {
        var channels = frame.Channels;
        var bits = frame.Pulses;
        var excess = Math.Max(bits[band] - frame.Caps[band], 0);
        bits[band] -= excess;
        var intensityBand = channels == StereoChannels && size > TwoEntries && !frame.DualStereo && band < frame.Intensity;
        var degrees = (channels * size) + (intensityBand ? 1 : 0);
        var scaled = degrees * (CeltTables.BandLogSizes[band] + (frame.FrameSize << FractionBits));
        var offset = FineBitsOffset(bits[band], scaled, degrees, size);
        var fine = Math.Max(0, (bits[band] + offset + (degrees << (FractionBits - 1))) / (degrees << FractionBits));
        if (channels * fine > bits[band] >> FractionBits)
        {
            fine = bits[band] >> (channels > 1 ? 1 : 0) >> FractionBits;
        }

        fine = Math.Min(fine, CeltEnergy.MaxFineBits);
        frame.FineBits[band] = fine;
        frame.FinePriority[band] = fine * (degrees << FractionBits) >= bits[band] + offset ? 1 : 0;
        bits[band] -= (channels * fine) << FractionBits;
        return excess;
    }

    /// <summary>Works out how far a band's bits are moved before they are divided into fine energy bits.</summary>
    /// <param name="bits">The bits of the band.</param>
    /// <param name="scaled">The band's size times the degrees of freedom.</param>
    /// <param name="degrees">The degrees of freedom of the band.</param>
    /// <param name="size">The entries in the band.</param>
    /// <returns>The offset.</returns>
    private static int FineBitsOffset(int bits, int scaled, int degrees, int size)
    {
        var offset = (scaled >> 1) - (degrees * FineOffset);
        if (size == TwoEntries)
        {
            offset += (degrees << FractionBits) >> 2;
        }

        if (bits + offset < (degrees * TwoEntries) << FractionBits)
        {
            return offset + (scaled >> 2);
        }

        return bits + offset < (degrees * ThirdFine) << FractionBits ? offset + (scaled >> 3) : offset;
    }

    /// <summary>The bits set aside for signalling before the rest are shared out.</summary>
    /// <param name="Total">The bits left to share.</param>
    /// <param name="SkipStart">The last boosted band, below which no band may be skipped.</param>
    /// <param name="Skip">The bits kept to signal the end of the skipped bands.</param>
    /// <param name="Intensity">The bits kept to signal the first intensity band.</param>
    /// <param name="DualStereo">The bits kept to signal dual stereo.</param>
    private readonly record struct Reservations(int Total, int SkipStart, int Skip, int Intensity, int DualStereo);

    /// <summary>The running totals while bands are being skipped.</summary>
    /// <param name="Sum">The bits handed out to bands.</param>
    /// <param name="Total">The bits available.</param>
    /// <param name="IntensityReservation">The bits still kept for the intensity band.</param>
    private record struct SkipState(int Sum, int Total, int IntensityReservation);
}
