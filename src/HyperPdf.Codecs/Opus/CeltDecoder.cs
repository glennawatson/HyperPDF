// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The CELT layer of an Opus decoder (RFC 6716 section 4.3). It turns the frames of a CELT or hybrid packet into floating point
/// samples at 48 kHz, and keeps what the next frame needs from this one: the band energies, the tail of the overlapping blocks, the
/// comb filter and the de-emphasis memory.
/// </summary>
internal sealed class CeltDecoder
{
    /// <summary>The samples of a block of the smallest frame size.</summary>
    internal const int ShortBlockSize = 120;

    /// <summary>The samples the neighbouring blocks overlap by.</summary>
    internal const int Overlap = 120;

    /// <summary>The samples of the longest frame, 20 ms at 48 kHz.</summary>
    internal const int MaxFrameSize = 960;

    /// <summary>The number of times a frame can be halved: from 20 ms to 2.5 ms.</summary>
    internal const int MaxShift = 3;

    /// <summary>The samples of output a channel keeps, which the comb filter reads back through.</summary>
    internal const int HistorySize = 2048;

    /// <summary>The most bytes a frame may have.</summary>
    internal const int MaxFrameBytes = 1275;

    /// <summary>The logarithm of the probability of the silence flag.</summary>
    private const int SilenceOdds = 15;

    /// <summary>The bits the post-filter flag and its parameters need at least.</summary>
    private const int PostFilterBits = 16;

    /// <summary>The bits a transient flag, the intra flag or the spreading decision needs at least.</summary>
    private const int FlagBits = 3;

    /// <summary>The logarithm of the probability of the transient flag.</summary>
    private const int TransientOdds = 3;

    /// <summary>The first bit of a frame, which the silence flag follows.</summary>
    private const int FirstBit = 1;

    /// <summary>The most channels.</summary>
    private const int MaxChannels = 2;

    /// <summary>The fractional bits of the bit counts.</summary>
    private const int FractionBits = CeltAllocation.FractionBits;

    /// <summary>The bits the anti-collapse flag takes, in eighths.</summary>
    private const int AntiCollapseBits = 1 << FractionBits;

    /// <summary>The frame size from which a transient frame has an anti-collapse flag.</summary>
    private const int AntiCollapseFrameSize = 2;

    /// <summary>The bytes' worth of bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Half, the weight of each channel when a stereo frame is played in mono.</summary>
    private const float Half = 0.5F;

    /// <summary>The growth of the background energy, per short block of a frame, in steps of 6 dB.</summary>
    private const float BackgroundRise = 0.001F;

    /// <summary>The channels of the output.</summary>
    private readonly int _channels;

    /// <summary>The output of each channel: the history, the new frame, then the overlap kept for the next frame.</summary>
    private readonly float[][] _history;

    /// <summary>The band energies kept from frame to frame.</summary>
    private readonly CeltEnergyState _energy = new();

    /// <summary>The frame being decoded.</summary>
    private readonly CeltFrame _frame = new();

    /// <summary>The scratch arrays of the inverse transform.</summary>
    private readonly CeltSynthesisWorkspace _workspace = new();

    /// <summary>The inverse transform.</summary>
    private readonly CeltMdct _mdct = new();

    /// <summary>The normalised shapes of the bands of both channels.</summary>
    private readonly float[] _shapes = new float[MaxChannels * MaxFrameSize];

    /// <summary>The spectrum of both channels.</summary>
    private readonly float[] _spectrum = new float[MaxChannels * MaxFrameSize];

    /// <summary>The amplitude of each band of both channels.</summary>
    private readonly float[] _amplitudes = new float[MaxChannels * CeltEnergy.Bands];

    /// <summary>The collapse mask of each band of both channels.</summary>
    private readonly byte[] _masks = new byte[MaxChannels * CeltEnergy.Bands];

    /// <summary>The memory of the de-emphasis filter of each channel.</summary>
    private readonly float[] _deemphasis = new float[MaxChannels];

    /// <summary>The comb filter that follows the frame decoded last.</summary>
    private CeltPostFilterSettings _postFilter;

    /// <summary>The comb filter that followed the frame before the last.</summary>
    private CeltPostFilterSettings _postFilterOld;

    /// <summary>The noise generator's seed, which is the range of the range decoder at the end of the last frame.</summary>
    private uint _seed;

    /// <summary>Initializes a new instance of the <see cref="CeltDecoder"/> class.</summary>
    /// <param name="channels">The channels of the output, 1 or 2.</param>
    internal CeltDecoder(int channels)
    {
        _channels = channels;
        _history = new float[channels][];
        for (var channel = 0; channel < channels; channel++)
        {
            _history[channel] = new float[HistorySize + Overlap];
        }

        Start = 0;
        End = CeltEnergy.Bands;
    }

    /// <summary>Gets or sets the first band that is coded: 0 for CELT only, 17 for hybrid.</summary>
    internal int Start { get; set; }

    /// <summary>Gets or sets one past the last band that is coded, which depends on the bandwidth.</summary>
    internal int End { get; set; }

    /// <summary>Puts the decoder back to the state before any frame was decoded.</summary>
    internal void Reset()
    {
        foreach (var history in _history)
        {
            Array.Clear(history);
        }

        _energy.Reset();
        Array.Clear(_deemphasis);
        _postFilter = default;
        _postFilterOld = default;
        _seed = 0;
    }

    /// <summary>Decodes one frame.</summary>
    /// <param name="data">The frame's bytes; a frame of one byte or none is lost and is played as silence.</param>
    /// <param name="samples">The samples of each channel the frame holds: 120, 240, 480 or 960.</param>
    /// <param name="streamChannels">The channels the frame was coded with.</param>
    /// <param name="pcm">Receives the samples, the channels interleaved.</param>
    /// <returns>The samples of each channel decoded, or a negative number when the frame is not valid.</returns>
    internal int Decode(ReadOnlySpan<byte> data, int samples, int streamChannels, Span<float> pcm)
    {
        var decoder = new OpusRangeDecoder(data);
        return Decode(ref decoder, data.Length, samples, streamChannels, pcm);
    }

    /// <summary>Decodes one frame from a range decoder that may already have been used by another layer.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="length">The bytes in the frame.</param>
    /// <param name="samples">The samples of each channel the frame holds: 120, 240, 480 or 960.</param>
    /// <param name="streamChannels">The channels the frame was coded with.</param>
    /// <param name="pcm">Receives the samples, the channels interleaved.</param>
    /// <returns>The samples of each channel decoded, or a negative number when the frame is not valid.</returns>
    internal int Decode(ref OpusRangeDecoder decoder, int length, int samples, int streamChannels, Span<float> pcm)
    {
        var frameSize = GetFrameSize(samples);
        if (frameSize < 0 || length > MaxFrameBytes || pcm.Length < samples * _channels)
        {
            return -1;
        }

        if (length <= 1)
        {
            pcm[..(samples * _channels)].Clear();
            return samples;
        }

        PrepareFrame(frameSize, streamChannels, samples);
        var flags = ReadFlags(ref decoder, length * BitsPerByte);
        DecodeBands(ref decoder, length, samples, flags);
        Synthesize(flags, samples, pcm);
        _seed = decoder.Range;
        return samples;
    }

    /// <summary>Works out the log2 of a frame's size in units of 2.5 ms.</summary>
    /// <param name="samples">The samples of a channel in the frame.</param>
    /// <returns>0 to 3, or -1 if the size is not one a CELT frame can have.</returns>
    private static int GetFrameSize(int samples)
    {
        for (var frameSize = 0; frameSize <= MaxShift; frameSize++)
        {
            if (ShortBlockSize << frameSize == samples)
            {
                return frameSize;
            }
        }

        return -1;
    }

    /// <summary>Sets up the frame, clears the shapes outside the coded bands and brings the band energies in line with the channel count.</summary>
    /// <param name="frameSize">The log2 of the frame size in units of 2.5 ms.</param>
    /// <param name="streamChannels">The channels the frame was coded with.</param>
    /// <param name="samples">The samples of each channel the frame holds.</param>
    private void PrepareFrame(int frameSize, int streamChannels, int samples)
    {
        var frame = _frame;
        frame.Start = Start;
        frame.End = End;
        frame.FrameSize = frameSize;
        frame.Channels = streamChannels;
        var scale = 1 << frameSize;
        for (var channel = 0; channel < streamChannels; channel++)
        {
            var shapes = _shapes.AsSpan(channel * samples, samples);
            shapes[..(scale * CeltTables.BandEdges[Start])].Clear();
            shapes[(scale * CeltTables.BandEdges[End])..].Clear();
        }

        if (streamChannels != 1)
        {
            return;
        }

        var current = _energy.Current;
        for (var band = 0; band < CeltEnergy.Bands; band++)
        {
            current[band] = Math.Max(current[band], current[CeltEnergy.Bands + band]);
        }
    }

    /// <summary>Reads the flags at the start of the frame.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="totalBits">The bits in the frame.</param>
    /// <returns>The flags.</returns>
    private CeltFrameFlags ReadFlags(ref OpusRangeDecoder decoder, int totalBits)
    {
        var tell = decoder.Tell();
        var silence = tell >= totalBits || (tell == FirstBit && decoder.ReadBit(SilenceOdds));
        if (silence)
        {
            decoder.SkipToEnd();
            tell = totalBits;
        }

        var postFilter = default(CeltPostFilterSettings);
        if (Start == 0 && tell + PostFilterBits <= totalBits)
        {
            postFilter = CeltSideInfo.ReadPostFilter(ref decoder, totalBits);
            tell = decoder.Tell();
        }

        var transient = false;
        if (_frame.FrameSize > 0 && tell + FlagBits <= totalBits)
        {
            transient = decoder.ReadBit(TransientOdds);
            tell = decoder.Tell();
        }

        var intra = tell + FlagBits <= totalBits && decoder.ReadBit(FlagBits);
        return new(silence, postFilter, transient, intra);
    }

    /// <summary>Reads the energy and shape of every band.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="length">The bytes in the frame.</param>
    /// <param name="samples">The samples of each channel the frame holds.</param>
    /// <param name="flags">The flags at the start of the frame.</param>
    private void DecodeBands(ref OpusRangeDecoder decoder, int length, int samples, CeltFrameFlags flags)
    {
        var frame = _frame;
        CeltEnergy.ReadCoarse(ref decoder, _energy.Current, frame, flags.Intra);
        CeltSideInfo.ReadTimeFrequency(ref decoder, frame, flags.Transient);
        var spread = CeltSideInfo.ReadSpread(ref decoder, length * BitsPerByte);
        var reserve = ReadAllocation(ref decoder, length, flags.Transient);
        CeltEnergy.ReadFine(ref decoder, _energy.Current, frame);
        var second = frame.Channels == MaxChannels ? _shapes.AsSpan(samples, samples) : default;
        var reader = new CeltBandReader(decoder, frame, spread, _seed);
        reader.ReadAll(_shapes.AsSpan(0, samples), second, _masks, ((length * BitsPerByte) << FractionBits) - reserve, flags.Transient);
        decoder = reader.Decoder;
        var collapse = reserve > 0 && decoder.ReadRaw(1) != 0;
        CeltEnergy.ReadFinalBits(ref decoder, _energy.Current, frame, (length * BitsPerByte) - decoder.Tell());
        if (collapse)
        {
            CeltAntiCollapse.Apply(frame, _shapes, _masks, _energy, reader.Seed, samples);
        }
    }

    /// <summary>Reads how the bits are shared between the bands and works out the allocation.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="length">The bytes in the frame.</param>
    /// <param name="transient">Whether the frame is split into short blocks.</param>
    /// <returns>The bits reserved for the anti-collapse flag, in eighths.</returns>
    private int ReadAllocation(ref OpusRangeDecoder decoder, int length, bool transient)
    {
        var frame = _frame;
        CeltAllocation.InitializeCaps(frame);
        var frameBits = (length * BitsPerByte) << FractionBits;
        frame.Trim = CeltSideInfo.ReadAllocationRequests(ref decoder, frame, frameBits);
        var bits = frameBits - decoder.TellFractional() - 1;
        var reserve = transient && frame.FrameSize >= AntiCollapseFrameSize && bits >= (frame.FrameSize + AntiCollapseFrameSize) << FractionBits ? AntiCollapseBits : 0;
        frame.TotalBits = bits - reserve;
        CeltAllocation.Allocate(ref decoder, frame);
        return reserve;
    }

    /// <summary>Turns the decoded bands into samples and keeps what the next frame needs.</summary>
    /// <param name="flags">The flags at the start of the frame.</param>
    /// <param name="samples">The samples of each channel the frame holds.</param>
    /// <param name="pcm">Receives the samples, the channels interleaved.</param>
    private void Synthesize(CeltFrameFlags flags, int samples, Span<float> pcm)
    {
        var frame = _frame;
        CeltEnergy.ToAmplitudes(_energy.Current, _amplitudes, frame);
        if (flags.Silence)
        {
            Array.Clear(_amplitudes);
            _energy.Current.AsSpan(0, frame.Channels * CeltEnergy.Bands).Fill(CeltEnergyState.SilentEnergy);
        }

        CeltSynthesis.Denormalise(frame, _shapes, _amplitudes, _spectrum, samples);
        MixChannels(frame.Channels, samples);
        for (var channel = 0; channel < _channels; channel++)
        {
            var history = _history[channel];
            history.AsSpan(samples, HistorySize - samples).CopyTo(history);
            CeltSynthesis.Inverse(_mdct, _workspace, _spectrum.AsSpan(channel * samples, samples), flags.Transient, history);
        }

        ApplyPostFilter(flags.PostFilter, samples);
        UpdateEnergyHistory(flags.Transient);
        for (var channel = 0; channel < _channels; channel++)
        {
            CeltPostFilter.Deemphasis(_history[channel].AsSpan(HistorySize - samples, samples), pcm, channel, _channels, ref _deemphasis[channel]);
        }
    }

    /// <summary>Copies a mono spectrum to both output channels, or averages a stereo spectrum into one.</summary>
    /// <param name="streamChannels">The channels the frame was coded with.</param>
    /// <param name="samples">The samples of each channel the frame holds.</param>
    private void MixChannels(int streamChannels, int samples)
    {
        if (_channels == MaxChannels && streamChannels == 1)
        {
            _spectrum.AsSpan(0, samples).CopyTo(_spectrum.AsSpan(samples, samples));
        }
        else if (_channels == 1 && streamChannels == MaxChannels)
        {
            for (var i = 0; i < samples; i++)
            {
                _spectrum[i] = Half * (_spectrum[i] + _spectrum[samples + i]);
            }
        }
    }

    /// <summary>Runs the comb filter over the new samples of each channel and moves on its parameters.</summary>
    /// <param name="next">The filter that follows this frame.</param>
    /// <param name="samples">The samples of each channel the frame holds.</param>
    private void ApplyPostFilter(CeltPostFilterSettings next, int samples)
    {
        _postFilter = _postFilter with { Period = Math.Max(_postFilter.Period, CeltPostFilter.MinimumPeriod) };
        _postFilterOld = _postFilterOld with { Period = Math.Max(_postFilterOld.Period, CeltPostFilter.MinimumPeriod) };
        var start = HistorySize - samples;
        for (var channel = 0; channel < _channels; channel++)
        {
            CeltPostFilter.Apply(_history[channel], start, ShortBlockSize, _postFilterOld, _postFilter);
            if (samples > ShortBlockSize)
            {
                CeltPostFilter.Apply(_history[channel], start + ShortBlockSize, samples - ShortBlockSize, _postFilter, next);
            }
        }

        _postFilterOld = _postFilter;
        _postFilter = next;
        if (samples > ShortBlockSize)
        {
            _postFilterOld = _postFilter;
        }
    }

    /// <summary>Moves the band energies of this frame into the history the next frame is checked against.</summary>
    /// <param name="transient">Whether the frame was split into short blocks.</param>
    private void UpdateEnergyHistory(bool transient)
    {
        var frame = _frame;
        var energy = _energy;
        var current = energy.Current;
        if (frame.Channels == 1)
        {
            current.AsSpan(0, CeltEnergy.Bands).CopyTo(current.AsSpan(CeltEnergy.Bands));
        }

        if (transient)
        {
            for (var i = 0; i < current.Length; i++)
            {
                energy.Previous[i] = Math.Min(energy.Previous[i], current[i]);
            }
        }
        else
        {
            energy.Previous.CopyTo(energy.BeforePrevious, 0);
            current.CopyTo(energy.Previous, 0);
            var rise = BackgroundRise * (1 << frame.FrameSize);
            for (var i = 0; i < current.Length; i++)
            {
                energy.Background[i] = Math.Min(energy.Background[i] + rise, current[i]);
            }
        }

        ClearUnusedBands();
    }

    /// <summary>Clears the energies of the bands outside the range that is coded.</summary>
    private void ClearUnusedBands()
    {
        var energy = _energy;
        for (var channel = 0; channel < MaxChannels; channel++)
        {
            for (var band = 0; band < CeltEnergy.Bands; band++)
            {
                if (band >= Start && band < End)
                {
                    continue;
                }

                var at = (channel * CeltEnergy.Bands) + band;
                energy.Current[at] = 0F;
                energy.Previous[at] = CeltEnergyState.SilentEnergy;
                energy.BeforePrevious[at] = CeltEnergyState.SilentEnergy;
            }
        }
    }
}
