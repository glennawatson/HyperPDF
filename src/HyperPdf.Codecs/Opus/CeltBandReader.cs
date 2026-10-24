// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Reads the shape of every band of a CELT frame (RFC 6716 section 4.3.4). A band is either read as one pulse vector, or
/// split in two halves (or into mid and side) whose share of the bits is coded as an angle and which are read in turn; bands
/// without pulses are filled by folding the band below or with noise.
/// </summary>
internal ref struct CeltBandReader
{
    /// <summary>The fractional bits.</summary>
    private const int FractionBits = CeltAllocation.FractionBits;

    /// <summary>The most bands the leftover bits are shared between.</summary>
    private const int MaxBalanceShare = 3;

    /// <summary>The most bits a band is given, in eighths.</summary>
    private const int MaxBandBits = 16_383;

    /// <summary>Two: the channels of a stereo frame, the entries of the smallest split band and the halves of a split.</summary>
    private const int Pair = 2;

    /// <summary>The bits above the cost of the most pulses at which a band is split, in eighths.</summary>
    private const int SplitMargin = 12;

    /// <summary>The offset of the angle's precision from its fair share.</summary>
    private const int ThetaOffset = 4;

    /// <summary>The offset of the angle's precision for a stereo band of two entries.</summary>
    private const int ThetaOffsetTwoEntries = 16;

    /// <summary>The bits a rebalancing must exceed before it is passed on, in eighths.</summary>
    private const int ReboundBits = 3 << FractionBits;

    /// <summary>The bits an inversion flag costs, in eighths.</summary>
    private const int InversionBits = 2 << FractionBits;

    /// <summary>The probability of the inversion flag, as a power of two.</summary>
    private const int InversionOdds = 2;

    /// <summary>The share of full scale a folded entry is offset by, 48 dB below the normal folding level.</summary>
    private const float FoldNoise = 1F / 256F;

    /// <summary>The bit of the noise generator's seed that makes a positive or negative offset.</summary>
    private const uint FoldSignBit = 0x8000;

    /// <summary>The shift that brings the noise generator's seed to a small number.</summary>
    private const int NoiseShift = 20;

    /// <summary>The shift that halves a block count.</summary>
    private const int HalfShift = 1;

    /// <summary>The shift of the delta's tilt for a band of short blocks that is louder in its second half.</summary>
    private const int DeltaShiftUp = 4;

    /// <summary>The shift of the forward masking slope of short blocks.</summary>
    private const int DeltaShiftDown = 5;

    /// <summary>The frame being read.</summary>
    private readonly CeltFrame _frame;

    /// <summary>The spreading decision.</summary>
    private readonly int _spread;

    /// <summary>The range decoder, moved in by value because a ref field cannot refer to a ref struct; <see cref="Decoder"/> hands the advanced state back.</summary>
    private OpusRangeDecoder _decoder;

    /// <summary>The noise generator's seed.</summary>
    private uint _seed;

    /// <summary>The bits left to share, in eighths.</summary>
    private int _remaining;

    /// <summary>Initializes a new instance of the <see cref="CeltBandReader"/> struct.</summary>
    /// <param name="decoder">The range decoder, at the position of the first band.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="spread">The spreading decision.</param>
    /// <param name="seed">The seed of the noise generator.</param>
    internal CeltBandReader(OpusRangeDecoder decoder, CeltFrame frame, int spread, uint seed)
    {
        _decoder = decoder;
        _frame = frame;
        _spread = spread;
        _seed = seed;
        _remaining = 0;
    }

    /// <summary>Gets the range decoder, positioned after the bands that were read.</summary>
    internal readonly OpusRangeDecoder Decoder => _decoder;

    /// <summary>Gets the seed of the noise generator after the bands that were read.</summary>
    internal readonly uint Seed => _seed;

    /// <summary>Reads every band.</summary>
    /// <param name="x">The first channel's entries, band after band.</param>
    /// <param name="y">The second channel's entries, or empty for mono.</param>
    /// <param name="masks">Receives, for each band and channel, which short blocks had a pulse.</param>
    /// <param name="totalBits">The bits of the frame, in eighths, less any reserved for the anti-collapse bit.</param>
    /// <param name="shortBlocks">Whether the frame is made of short blocks.</param>
    internal void ReadAll(Span<float> x, Span<float> y, Span<byte> masks, int totalBits, bool shortBlocks)
    {
        var frame = _frame;
        var scale = 1 << frame.FrameSize;
        var layout = new BandLayout(scale, shortBlocks ? scale : 1, y.IsEmpty ? 1 : Pair);
        var normSize = scale * CeltTables.BandEdges[CeltEnergy.Bands];
        var norm = frame.Norm.AsSpan(0, layout.Channels * normSize);
        var norm2 = layout.Channels == Pair ? norm[normSize..] : default;
        var buffers = new BandBuffers(x, y, masks, norm, norm2, frame.Scratch);
        var progress = new BandProgress(frame.Balance, new(0, true), frame.DualStereo);
        for (var band = frame.Start; band < frame.End; band++)
        {
            progress = ReadOneBand(band, progress, layout, buffers, totalBits);
        }
    }

    /// <summary>Decides whether a mono band needs more bits than a single pulse vector can use and so is split in two.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state.</param>
    /// <returns><see langword="true"/> to split.</returns>
    private static bool CanSplit(in CeltBandArgs args, BandState state)
    {
        if (state.Stereo || state.FrameSize == -1 || state.Size <= Pair)
        {
            return false;
        }

        var cache = CeltAllocation.GetCache(args.Band, state.FrameSize);
        return state.Bits > cache[cache[0]] + SplitMargin;
    }

    /// <summary>Halves a mono band.</summary>
    /// <param name="state">The band's state.</param>
    /// <returns>The state of each half.</returns>
    private static BandState SplitMono(BandState state)
    {
        var fill = state.Blocks == 1 ? (state.Fill & 1) | (state.Fill << 1) : state.Fill;
        return state with { Size = state.Size >> 1, FrameSize = state.FrameSize - 1, Fill = fill, Blocks = (state.Blocks + 1) >> 1, Split = true };
    }

    /// <summary>Moves the bit split of a band of short blocks, giving more bits to the quieter half.</summary>
    /// <param name="delta">The split.</param>
    /// <param name="angle">The angle.</param>
    /// <param name="state">The band's state.</param>
    /// <returns>The new split.</returns>
    private static int TiltDelta(int delta, int angle, BandState state) =>
        angle > CeltTheta.EighthTurn
            ? delta - (delta >> (DeltaShiftUp - state.FrameSize))
            : Math.Min(0, delta + ((state.Size << FractionBits) >> (DeltaShiftDown - state.FrameSize)));

    /// <summary>Gets the memory of the first half of a split band, which folds from and saves for the same bands as the whole.</summary>
    /// <param name="spans">The memory of the band.</param>
    /// <param name="stereo">Whether the band is stereo, which passes on the saved entries.</param>
    /// <returns>The memory.</returns>
    private static CeltBandSpans FirstHalf(CeltBandSpans spans, bool stereo) =>
        new(spans.Shape, default, spans.Lowband, stereo ? spans.LowbandOut : default, spans.Scratch);

    /// <summary>Reads one band of the frame.</summary>
    /// <param name="band">The band.</param>
    /// <param name="progress">What the bands before it left.</param>
    /// <param name="layout">The shape of the frame.</param>
    /// <param name="buffers">The memory of the frame.</param>
    /// <param name="totalBits">The bits of the frame.</param>
    /// <returns>What this band leaves for the next.</returns>
    private BandProgress ReadOneBand(int band, BandProgress progress, BandLayout layout, in BandBuffers buffers, int totalBits)
    {
        var frame = _frame;
        var edges = CeltTables.BandEdges;
        var start = layout.Scale * edges[band];
        var width = (layout.Scale * edges[band + 1]) - start;
        var tell = _decoder.TellFractional();
        var balance = band == frame.Start ? progress.Balance : progress.Balance - tell;
        _remaining = totalBits - tell - 1;
        var bits = band < frame.CodedBands ? GetBandBits(band, balance) : 0;
        var offset = CeltFolding.NextOffset(frame, progress.Fold, band, layout.Scale);
        if (band == frame.Start + 1)
        {
            ExtendFirstBand(layout, progress.DualStereo, buffers);
        }

        var change = frame.TimeFrequency[band];
        var fold = CeltFolding.Resolve(frame, buffers.Masks, new(band, layout.Scale, layout.Blocks, layout.Channels, width, change, offset, _spread));
        var dual = progress.DualStereo;
        if (dual && band == frame.Intensity)
        {
            buffers.AverageChannels(layout.Scale * edges[frame.Start], start);
            dual = false;
        }

        var args = new CeltBandArgs(band, width, bits, layout.Blocks, change, 0, 1F, frame.FrameSize, 0);
        var masks = ReadChannels(args, fold, dual, buffers, start);
        buffers.Masks[band * layout.Channels] = (byte)masks.First;
        buffers.Masks[(band * layout.Channels) + layout.Channels - 1] = (byte)masks.Second;
        return new(balance + frame.Pulses[band] + tell, new(offset, bits > (width << FractionBits)), dual);
    }

    /// <summary>Copies enough of the first band for the second to fold from, in both channels when they are coded apart.</summary>
    /// <param name="layout">The shape of the frame.</param>
    /// <param name="dual">Whether the channels are coded apart.</param>
    /// <param name="buffers">The memory of the frame.</param>
    private readonly void ExtendFirstBand(BandLayout layout, bool dual, in BandBuffers buffers)
    {
        CeltFolding.ExtendFirstBand(_frame, layout.Scale, buffers.Norm);
        if (dual)
        {
            CeltFolding.ExtendFirstBand(_frame, layout.Scale, buffers.Norm2);
        }
    }

    /// <summary>Works out how many bits a band is given from the share of the balance.</summary>
    /// <param name="band">The band.</param>
    /// <param name="balance">The bits the bands before it did not use.</param>
    /// <returns>The bits.</returns>
    private readonly int GetBandBits(int band, int balance)
    {
        var current = balance / Math.Min(MaxBalanceShare, _frame.CodedBands - band);
        return Math.Max(0, Math.Min(MaxBandBits, Math.Min(_remaining + 1, _frame.Pulses[band] + current)));
    }

    /// <summary>Reads the band for each channel, or for both together.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="fold">Where the band folds from.</param>
    /// <param name="dual">Whether the channels are coded apart.</param>
    /// <param name="buffers">The memory of the frame.</param>
    /// <param name="start">The index of the band's first entry.</param>
    /// <returns>The collapse masks of the first and second channel.</returns>
    private MaskPair ReadChannels(in CeltBandArgs args, CeltFold fold, bool dual, in BandBuffers buffers, int start)
    {
        var x = buffers.X.Slice(start, args.Size);
        var y = buffers.Y.IsEmpty ? default : buffers.Y.Slice(start, args.Size);
        var lowband = fold.Lowband == -1 ? default : buffers.Norm[fold.Lowband..];
        if (!dual)
        {
            var both = ReadBand(args with { Fill = (int)(fold.FirstMask | fold.SecondMask) }, new(x, y, lowband, buffers.Norm[start..], buffers.Scratch));
            return new(both, both);
        }

        var lowband2 = fold.Lowband == -1 ? default : buffers.Norm2[fold.Lowband..];
        var first = ReadBand(args with { Bits = args.Bits / Pair, Fill = (int)fold.FirstMask }, new(x, default, lowband, buffers.Norm[start..], buffers.Scratch));
        var second = ReadBand(args with { Bits = args.Bits / Pair, Fill = (int)fold.SecondMask }, new(y, default, lowband2, buffers.Norm2[start..], buffers.Scratch));
        return new(first, second);
    }

    /// <summary>Reads one band, recursively when it is split.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>The collapse mask: a bit for each short block that has a pulse.</returns>
    private uint ReadBand(in CeltBandArgs args, in CeltBandSpans spans)
    {
        if (args.Size == 1)
        {
            return ReadSingle(spans);
        }

        var lowband = spans.Lowband;
        var fill = args.Fill;
        var reshape = new CeltReshape(args.Blocks, args.Size / args.Blocks, 0, 0, args.Blocks == 1);
        var mono = !spans.IsStereo && args.Level == 0;
        if (mono)
        {
            reshape = CeltTimeFrequency.Prepare(args.Size, args.Blocks, args.TimeFrequency, ref fill, ref lowband, spans.Scratch);
        }

        var state = new BandState(args.Size, args.Bits, reshape.Blocks, args.FrameSize, fill, spans.IsStereo, spans.IsStereo);
        if (CanSplit(args, state))
        {
            state = SplitMono(state);
        }

        var parts = new CeltBandSpans(spans.Shape, state.Stereo ? spans.Second : spans.Shape[state.Size..], lowband, spans.LowbandOut, spans.Scratch);
        var collapse = state.Split ? ReadSplit(args, state, parts, reshape.Blocks) : ReadPulses(args, state, spans.Shape, lowband);
        return mono ? CeltTimeFrequency.Finish(spans.Shape, args.Size, collapse, reshape, spans.LowbandOut) : collapse;
    }

    /// <summary>Reads a band of a single entry: a sign for each channel if the bits allow it.</summary>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>One.</returns>
    private uint ReadSingle(in CeltBandSpans spans)
    {
        ReadSign(spans.Shape);
        if (spans.IsStereo)
        {
            ReadSign(spans.Second);
        }

        if (!spans.LowbandOut.IsEmpty)
        {
            spans.LowbandOut[0] = spans.Shape[0];
        }

        return 1;
    }

    /// <summary>Reads the sign of a single entry, if a bit can be spent on it.</summary>
    /// <param name="entry">The entry.</param>
    private void ReadSign(Span<float> entry)
    {
        var negative = false;
        if (_remaining >= 1 << FractionBits)
        {
            negative = _decoder.ReadRaw(1) != 0;
            _remaining -= 1 << FractionBits;
        }

        entry[0] = negative ? -1F : 1F;
    }

    /// <summary>Reads a band that is not split: one pulse vector, folded spectrum or noise.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state.</param>
    /// <param name="shape">The band.</param>
    /// <param name="lowband">The folded band, or empty.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadPulses(in CeltBandArgs args, BandState state, Span<float> shape, Span<float> lowband)
    {
        var steps = ReserveSteps(args.Band, state);
        var band = shape[..state.Size];
        if (steps != 0)
        {
            return CeltPvq.Read(ref _decoder, band, CeltAllocation.GetPulses(steps), _spread, state.Blocks, args.Gain);
        }

        var mask = (1U << state.Blocks) - 1;
        var filled = (uint)state.Fill & mask;
        if (filled == 0)
        {
            band.Clear();
            return 0;
        }

        var collapse = lowband.IsEmpty ? FillWithNoise(band, mask) : FillWithFold(band, lowband, filled);
        CeltPvq.Renormalise(band, args.Gain);
        return collapse;
    }

    /// <summary>Works out how many pulse steps the band can afford, and takes their cost from what is left.</summary>
    /// <param name="band">The band.</param>
    /// <param name="state">The band's state.</param>
    /// <returns>The steps.</returns>
    private int ReserveSteps(int band, BandState state)
    {
        var steps = CeltAllocation.BitsToPulses(band, state.FrameSize, state.Bits);
        var cost = CeltAllocation.PulsesToBits(band, state.FrameSize, steps);
        _remaining -= cost;
        while (_remaining < 0 && steps > 0)
        {
            _remaining += cost;
            steps--;
            cost = CeltAllocation.PulsesToBits(band, state.FrameSize, steps);
            _remaining -= cost;
        }

        return steps;
    }

    /// <summary>Fills a band with noise.</summary>
    /// <param name="band">The band.</param>
    /// <param name="mask">The mask of all blocks.</param>
    /// <returns>The collapse mask, which is all blocks.</returns>
    private uint FillWithNoise(Span<float> band, uint mask)
    {
        for (var j = 0; j < band.Length; j++)
        {
            _seed = CeltBandMath.NextRandom(_seed);
            band[j] = (int)_seed >> NoiseShift;
        }

        return mask;
    }

    /// <summary>Fills a band by folding the band below with a little noise added.</summary>
    /// <param name="band">The band.</param>
    /// <param name="lowband">The folded band.</param>
    /// <param name="filled">The mask of blocks to fill.</param>
    /// <returns>The collapse mask, which is the blocks that were filled.</returns>
    private uint FillWithFold(Span<float> band, Span<float> lowband, uint filled)
    {
        for (var j = 0; j < band.Length; j++)
        {
            _seed = CeltBandMath.NextRandom(_seed);
            band[j] = lowband[j] + ((_seed & FoldSignBit) != 0 ? FoldNoise : -FoldNoise);
        }

        return filled;
    }

    /// <summary>Reads a band that is split into two halves, or into mid and side, coding the angle between them.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state after splitting.</param>
    /// <param name="spans">The memory of the band, whose second part is the second half.</param>
    /// <param name="originalBlocks">The short blocks the band was read with before splitting.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadSplit(in CeltBandArgs args, BandState state, in CeltBandSpans spans, int originalBlocks)
    {
        var theta = ReadTheta(args, state, originalBlocks);
        var angle = CeltTheta.Resolve(theta.Angle, state.Size, state.Fill, state.Blocks);
        var after = state with { Bits = state.Bits - theta.Allocated, Fill = angle.Fill };
        var collapse = state.Size == Pair && state.Stereo
            ? ReadTwoEntryStereo(args, after with { Fill = state.Fill }, angle, theta.Allocated, spans)
            : ReadHalves(args, after, new(angle, theta.Allocated), spans, originalBlocks);
        if (state.Stereo)
        {
            CeltStereo.Finish(spans.Shape[..state.Size], spans.Second[..state.Size], angle.MidGain, theta.Inverted);
        }

        return collapse;
    }

    /// <summary>Reads the angle that splits a band between its two halves.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state.</param>
    /// <param name="originalBlocks">The short blocks the band was read with before splitting.</param>
    /// <returns>The angle, the bits it cost and whether the side is inverted.</returns>
    private ThetaResult ReadTheta(in CeltBandArgs args, BandState state, int originalBlocks)
    {
        var pulseCap = CeltTables.BandLogSizes[args.Band] + (state.FrameSize * (1 << FractionBits));
        var offset = (pulseCap >> 1) - (state.Stereo && state.Size == Pair ? ThetaOffsetTwoEntries : ThetaOffset);
        var steps = state.Stereo && args.Band >= _frame.Intensity ? 1 : CeltTheta.ComputeSteps(state.Size, state.Bits, offset, pulseCap, state.Stereo);
        var tell = _decoder.TellFractional();
        var angle = 0;
        var inverted = false;
        if (steps != 1)
        {
            angle = CeltTheta.ReadIndex(ref _decoder, steps, state.Size, originalBlocks, state.Stereo) * CeltTheta.QuarterTurn / steps;
        }
        else if (state.Stereo)
        {
            inverted = state.Bits > InversionBits && _remaining > InversionBits && _decoder.ReadBit(InversionOdds);
        }

        return new(angle, _decoder.TellFractional() - tell, inverted);
    }

    /// <summary>Reads a stereo band of two entries, whose side is coded with a single bit.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state, with the fill mask from before the angle cleared bits of it.</param>
    /// <param name="angle">The angle and gains.</param>
    /// <param name="allocated">The bits the angle took.</param>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadTwoEntryStereo(in CeltBandArgs args, BandState state, CeltSplitAngle angle, int allocated, in CeltBandSpans spans)
    {
        var sideBits = angle.Angle != 0 && angle.Angle != CeltTheta.QuarterTurn ? 1 << FractionBits : 0;
        var flip = angle.Angle > CeltTheta.EighthTurn;
        _remaining -= allocated + sideBits;
        var main = flip ? spans.Second : spans.Shape;
        var side = flip ? spans.Shape : spans.Second;
        var sign = sideBits != 0 ? 1 - (Pair * (int)_decoder.ReadRaw(1)) : 1;
        var mainArgs = args with { Size = Pair, Bits = state.Bits - sideBits, Blocks = state.Blocks, FrameSize = state.FrameSize, Fill = state.Fill };
        var collapse = ReadBand(mainArgs, new(main, default, spans.Lowband, spans.LowbandOut, spans.Scratch));
        side[0] = -sign * main[1];
        side[1] = sign * main[0];
        CeltStereo.SplitTwoEntries(spans.Shape, spans.Second, angle.MidGain, angle.SideGain);
        return collapse;
    }

    /// <summary>Reads the two halves of a split band: the first then the second, or the other way round when the second gets more bits.</summary>
    /// <param name="args">What is known about the band.</param>
    /// <param name="state">The band's state, with the bits and fill mask after the angle.</param>
    /// <param name="split">The angle and the bits it took.</param>
    /// <param name="spans">The memory of the band, whose second part is the second half.</param>
    /// <param name="originalBlocks">The short blocks the band was read with before splitting.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadHalves(in CeltBandArgs args, BandState state, SplitBits split, in CeltBandSpans spans, int originalBlocks)
    {
        var angle = split.Angle;
        var delta = angle.Delta;
        if (originalBlocks > 1 && !state.Stereo && (angle.Angle & (CeltTheta.QuarterTurn - 1)) != 0)
        {
            delta = TiltDelta(delta, angle.Angle, state);
        }

        var firstBits = Math.Max(0, Math.Min(state.Bits, (state.Bits - delta) / Pair));
        _remaining -= split.Allocated;
        var next = args with { Size = state.Size, Blocks = state.Blocks, FrameSize = state.FrameSize, Level = state.Stereo ? 0 : args.Level + 1 };
        var shift = state.Stereo ? 0 : originalBlocks >> HalfShift;
        var plan = new HalfPlan(next, state.Fill, state.Stereo ? 1F : args.Gain * angle.MidGain, args.Gain * angle.SideGain, shift, state.Stereo);
        var secondBits = state.Bits - firstBits;
        return firstBits >= secondBits
            ? ReadFirstThenSecond(plan, angle, firstBits, secondBits, spans)
            : ReadSecondThenFirst(plan, angle, firstBits, secondBits, spans);
    }

    /// <summary>Reads the first half then the second.</summary>
    /// <param name="plan">How to read each half.</param>
    /// <param name="angle">The angle.</param>
    /// <param name="firstBits">The bits of the first half.</param>
    /// <param name="secondBits">The bits of the second half.</param>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadFirstThenSecond(in HalfPlan plan, CeltSplitAngle angle, int firstBits, int secondBits, in CeltBandSpans spans)
    {
        var before = _remaining;
        var collapse = ReadBand(plan.Args with { Bits = firstBits, Gain = plan.FirstGain, Fill = plan.Fill }, FirstHalf(spans, plan.Stereo));
        var rebalance = firstBits - (before - _remaining);
        if (rebalance > ReboundBits && angle.Angle != 0)
        {
            secondBits += rebalance - ReboundBits;
        }

        return collapse | (ReadSecondHalf(plan, secondBits, spans) << plan.Shift);
    }

    /// <summary>Reads the second half then the first.</summary>
    /// <param name="plan">How to read each half.</param>
    /// <param name="angle">The angle.</param>
    /// <param name="firstBits">The bits of the first half.</param>
    /// <param name="secondBits">The bits of the second half.</param>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>The collapse mask.</returns>
    private uint ReadSecondThenFirst(in HalfPlan plan, CeltSplitAngle angle, int firstBits, int secondBits, in CeltBandSpans spans)
    {
        var before = _remaining;
        var collapse = ReadSecondHalf(plan, secondBits, spans) << plan.Shift;
        var rebalance = secondBits - (before - _remaining);
        if (rebalance > ReboundBits && angle.Angle != CeltTheta.QuarterTurn)
        {
            firstBits += rebalance - ReboundBits;
        }

        return collapse | ReadBand(plan.Args with { Bits = firstBits, Gain = plan.FirstGain, Fill = plan.Fill }, FirstHalf(spans, plan.Stereo));
    }

    /// <summary>Reads the second half of a split band, which folds from the entries after the first half's and has no scratch.</summary>
    /// <param name="plan">How to read each half.</param>
    /// <param name="bits">The bits of the second half.</param>
    /// <param name="spans">The memory of the band.</param>
    /// <returns>The collapse mask, before it is shifted into place.</returns>
    private uint ReadSecondHalf(in HalfPlan plan, int bits, in CeltBandSpans spans)
    {
        var lowband = !spans.Lowband.IsEmpty && !plan.Stereo ? spans.Lowband[plan.Args.Size..] : default;
        return ReadBand(plan.Args with { Bits = bits, Gain = plan.SecondGain, Fill = plan.Fill >> plan.Args.Blocks }, new(spans.Second, default, lowband, default, default));
    }

    /// <summary>The shape of the frame.</summary>
    /// <param name="Scale">The short blocks that fit the frame, as a power of two.</param>
    /// <param name="Blocks">The short blocks a band is made of.</param>
    /// <param name="Channels">The channels.</param>
    private readonly record struct BandLayout(int Scale, int Blocks, int Channels);

    /// <summary>What a band leaves for the next.</summary>
    /// <param name="Balance">The bits the bands so far did not use.</param>
    /// <param name="Fold">What is left for the folding.</param>
    /// <param name="DualStereo">Whether the channels are still coded apart.</param>
    private readonly record struct BandProgress(int Balance, CeltFoldProgress Fold, bool DualStereo);

    /// <summary>The collapse masks of the first and second channel.</summary>
    /// <param name="First">The mask of the first channel.</param>
    /// <param name="Second">The mask of the second channel.</param>
    private readonly record struct MaskPair(uint First, uint Second);

    /// <summary>The state of a band while it is read.</summary>
    /// <param name="Size">The entries in each part of the band.</param>
    /// <param name="Bits">The bits left for the band.</param>
    /// <param name="Blocks">The short blocks of each part.</param>
    /// <param name="FrameSize">The log2 of the block size.</param>
    /// <param name="Fill">The fill mask.</param>
    /// <param name="Stereo">Whether the band has two channels.</param>
    /// <param name="Split">Whether the band is split in two.</param>
    private readonly record struct BandState(int Size, int Bits, int Blocks, int FrameSize, int Fill, bool Stereo, bool Split);

    /// <summary>The angle of a split and what it cost.</summary>
    /// <param name="Angle">The angle, 0 to 16384.</param>
    /// <param name="Allocated">The bits the angle took.</param>
    /// <param name="Inverted">Whether the side is inverted.</param>
    private readonly record struct ThetaResult(int Angle, int Allocated, bool Inverted);

    /// <summary>The angle of a split and the bits it took.</summary>
    /// <param name="Angle">The resolved angle.</param>
    /// <param name="Allocated">The bits the angle took.</param>
    private readonly record struct SplitBits(CeltSplitAngle Angle, int Allocated);

    /// <summary>How to read the two halves of a split band.</summary>
    /// <param name="Args">The arguments shared by both halves.</param>
    /// <param name="Fill">The fill mask.</param>
    /// <param name="FirstGain">The gain of the first half.</param>
    /// <param name="SecondGain">The gain of the second half.</param>
    /// <param name="Shift">How far the second half's collapse mask is shifted; zero for stereo.</param>
    /// <param name="Stereo">Whether the band is stereo.</param>
    private readonly record struct HalfPlan(CeltBandArgs Args, int Fill, float FirstGain, float SecondGain, int Shift, bool Stereo);

    /// <summary>The memory of the frame.</summary>
    private readonly ref struct BandBuffers
    {
        /// <summary>Half, the weight each channel has in the average that replaces dual stereo.</summary>
        private const float Half = 0.5F;

        /// <summary>Initializes a new instance of the <see cref="BandBuffers"/> struct.</summary>
        /// <param name="x">The first channel.</param>
        /// <param name="y">The second channel, or empty.</param>
        /// <param name="masks">The collapse masks.</param>
        /// <param name="norm">The normalised entries of the first channel.</param>
        /// <param name="norm2">The normalised entries of the second channel, or empty.</param>
        /// <param name="scratch">The scratch space.</param>
        internal BandBuffers(Span<float> x, Span<float> y, Span<byte> masks, Span<float> norm, Span<float> norm2, Span<float> scratch)
        {
            X = x;
            Y = y;
            Masks = masks;
            Norm = norm;
            Norm2 = norm2;
            Scratch = scratch;
        }

        /// <summary>Gets the first channel.</summary>
        internal Span<float> X { get; }

        /// <summary>Gets the second channel, or empty.</summary>
        internal Span<float> Y { get; }

        /// <summary>Gets the collapse masks.</summary>
        internal Span<byte> Masks { get; }

        /// <summary>Gets the normalised entries of the first channel.</summary>
        internal Span<float> Norm { get; }

        /// <summary>Gets the normalised entries of the second channel, or empty.</summary>
        internal Span<float> Norm2 { get; }

        /// <summary>Gets the scratch space.</summary>
        internal Span<float> Scratch { get; }

        /// <summary>Replaces the normalised entries of both channels by their average, when intensity stereo takes over from dual stereo.</summary>
        /// <param name="from">The first entry.</param>
        /// <param name="to">One past the last entry.</param>
        internal void AverageChannels(int from, int to)
        {
            for (var j = from; j < to; j++)
            {
                Norm[j] = Half * (Norm[j] + Norm2[j]);
            }
        }
    }
}
