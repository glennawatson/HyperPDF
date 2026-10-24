// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The SILK layer of an Opus decoder (RFC 6716 section 4.2): linear predictive speech coding at 8, 12 or 16 kHz, which is resampled to 48 kHz.
/// A packet holds one to three frames of 20 ms, or one of 10 ms; stereo is coded as mid and side channels.
/// </summary>
internal sealed class SilkDecoder
{
    /// <summary>The bits of every table's total.</summary>
    private const int TotalBits = 8;

    /// <summary>The samples of history in front of a frame, which stereo and the resampler read.</summary>
    private const int FrameHistory = 2;

    /// <summary>The most samples of 48 kHz output in a frame of 20 ms.</summary>
    private const int MaxOutput = 20 * SilkResampler.OutputKhz;

    /// <summary>The rate the output is resampled to, in Hz.</summary>
    private const int OutputRateHz = SilkResampler.OutputKhz * SilkConstants.HertzPerKilohertz;

    /// <summary>The bits a rate in Hz is shifted down by to find its kHz less one.</summary>
    private const int RateShift = 10;

    /// <summary>The milliseconds of a 10 ms frame.</summary>
    private const int ShortFrameMs = 10;

    /// <summary>The milliseconds of a 40 ms packet, which holds two frames.</summary>
    private const int TwoFramesMs = 40;

    /// <summary>The milliseconds of a 60 ms packet, which holds three frames.</summary>
    private const int ThreeFramesMs = 60;

    /// <summary>The frames in a packet of 40 ms.</summary>
    private const int TwoFrames = 2;

    /// <summary>The frames in a packet of 60 ms.</summary>
    private const int ThreeFrames = 3;

    /// <summary>The channel states.</summary>
    private readonly SilkChannel[] _channels = [new(), new()];

    /// <summary>The stereo state.</summary>
    private readonly SilkStereo _stereo = new();

    /// <summary>The arrays the synthesis works in.</summary>
    private readonly SilkSynthesisScratch _scratch = new();

    /// <summary>The decoded frame of each channel, with two samples of history in front.</summary>
    private readonly short[][] _frames = [new short[SilkConstants.MaxFrameLength + FrameHistory], new short[SilkConstants.MaxFrameLength + FrameHistory]];

    /// <summary>Room for the resampled frame of a channel when the output is stereo.</summary>
    private readonly short[] _resampled = new short[MaxOutput];

    /// <summary>The channels of the output of the last packet.</summary>
    private int _outputChannels;

    /// <summary>The channels the stream coded in the last packet.</summary>
    private int _streamChannels;

    /// <summary>Whether the last frame had only the mid channel.</summary>
    private bool _previousMidOnly;

    /// <summary>Initializes a new instance of the <see cref="SilkDecoder"/> class.</summary>
    internal SilkDecoder() => Reset();

    /// <summary>Puts the decoder back to the state before any frame.</summary>
    internal void Reset()
    {
        foreach (var channel in _channels)
        {
            channel.Initialize();
        }

        _stereo.Reset();
        _outputChannels = 0;
        _streamChannels = 0;
        _previousMidOnly = false;
    }

    /// <summary>Decodes one frame of a packet.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="control">What the packet is like.</param>
    /// <param name="lost">Whether the packet was lost and is to be concealed.</param>
    /// <param name="firstFrame">Whether this is the first frame of the packet.</param>
    /// <param name="output">Receives the samples at 48 kHz, the channels interleaved.</param>
    /// <returns>The samples of each channel written.</returns>
    internal int Decode(ref OpusRangeDecoder decoder, SilkControl control, bool lost, bool firstFrame, Span<short> output)
    {
        var stereoToMono = BeginFrame(control, firstFrame);
        if (!lost && _channels[0].FramesDecoded == 0)
        {
            ReadFrameFlags(ref decoder, control.InternalChannels);
        }

        Span<int> predictors = stackalloc int[SilkConstants.Pair];
        var midOnly = ReadStereoSide(ref decoder, control.InternalChannels, lost, predictors);
        var samples = DecodeChannels(ref decoder, control.InternalChannels, lost, midOnly);
        FinishFrame(control.InternalChannels, lost, midOnly);
        return Emit(control, predictors, samples, stereoToMono, output);
    }

    /// <summary>Reads which frames of a channel have redundancy.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="channel">The channel.</param>
    private static void ReadRedundancyFlags(ref OpusRangeDecoder decoder, SilkChannel channel)
    {
        Array.Clear(channel.LbrrFlags);
        if (!channel.LbrrFlag)
        {
            return;
        }

        if (channel.FramesPerPacket == 1)
        {
            channel.LbrrFlags[0] = true;
            return;
        }

        var table = channel.FramesPerPacket == TwoFrames ? SilkTables.Lbrr2Icdf : SilkTables.Lbrr3Icdf;
        var symbol = decoder.ReadSymbol(table, TotalBits) + 1;
        for (var i = 0; i < channel.FramesPerPacket; i++)
        {
            channel.LbrrFlags[i] = ((symbol >> i) & 1) != 0;
        }
    }

    /// <summary>Resets the side channel for its first frame after the mid channel was coded alone.</summary>
    /// <param name="channel">The side channel.</param>
    private static void ResetSideChannel(SilkChannel channel)
    {
        Array.Clear(channel.OutputBuffer);
        Array.Clear(channel.LpcState);
        channel.PreviousLag = SilkChannel.InitialLag;
        channel.LastGainIndex = SilkChannel.InitialGainIndex;
        channel.PreviousSignalType = SilkConstants.TypeNoVoiceActivity;
        channel.FirstFrameAfterReset = true;
    }

    /// <summary>Gets how many frames a packet of a duration holds.</summary>
    /// <param name="payloadMs">The milliseconds of sound in the packet.</param>
    /// <returns>1, 2 or 3.</returns>
    private static int GetFrameCount(int payloadMs) => payloadMs switch
    {
        TwoFramesMs => TwoFrames,
        ThreeFramesMs => ThreeFrames,
        _ => 1,
    };

    /// <summary>Remembers what the next frame needs to know of this one.</summary>
    /// <param name="internalChannels">The channels the stream codes.</param>
    /// <param name="lost">Whether the frame was lost.</param>
    /// <param name="midOnly">Whether only the mid channel was coded.</param>
    private void FinishFrame(int internalChannels, bool lost, bool midOnly)
    {
        if (lost)
        {
            ForgetGains(internalChannels);
            return;
        }

        _previousMidOnly = midOnly;
    }

    /// <summary>Prepares the channels for a frame: counts a new packet, sets up the sizes and notes a change in the number of channels.</summary>
    /// <param name="control">What the packet is like.</param>
    /// <param name="firstFrame">Whether this is the first frame of the packet.</param>
    /// <returns><see langword="true"/> when the stream has just changed from stereo to mono at the same rate.</returns>
    private bool BeginFrame(SilkControl control, bool firstFrame)
    {
        var internalChannels = control.InternalChannels;
        if (firstFrame)
        {
            for (var n = 0; n < internalChannels; n++)
            {
                _channels[n].FramesDecoded = 0;
            }
        }

        if (internalChannels > _streamChannels)
        {
            _channels[1].Initialize();
        }

        var stereoToMono = IsStereoToMono(control);
        if (_channels[0].FramesDecoded == 0)
        {
            Configure(control);
        }

        if (IsMonoToStereo(control))
        {
            _stereo.Reset();
            _channels[1].CopyResamplerFrom(_channels[0]);
        }

        _outputChannels = control.Channels;
        _streamChannels = internalChannels;
        return stereoToMono;
    }

    /// <summary>Tells whether the stream has just changed from stereo to mono at the same rate.</summary>
    /// <param name="control">What the packet is like.</param>
    /// <returns><see langword="true"/> for such a change.</returns>
    private bool IsStereoToMono(SilkControl control) =>
        control.InternalChannels == 1 && _streamChannels == SilkConstants.Pair
        && control.InternalRateHz == SilkConstants.HertzPerKilohertz * _channels[0].SampleRateKhz;

    /// <summary>Tells whether the output is stereo and the stream has just started to code both channels.</summary>
    /// <param name="control">What the packet is like.</param>
    /// <returns><see langword="true"/> for such a change.</returns>
    private bool IsMonoToStereo(SilkControl control) =>
        control.Channels == SilkConstants.Pair && control.InternalChannels == SilkConstants.Pair && (_outputChannels == 1 || _streamChannels == 1);

    /// <summary>Sets up the channels for a packet: how many frames and subframes, and the sampling rate.</summary>
    /// <param name="control">What the packet is like.</param>
    private void Configure(SilkControl control)
    {
        var rateKhz = (control.InternalRateHz >> RateShift) + 1;
        for (var n = 0; n < control.InternalChannels; n++)
        {
            var channel = _channels[n];
            channel.FramesPerPacket = GetFrameCount(control.PayloadMs);
            channel.SubframeCount = control.PayloadMs <= ShortFrameMs ? SilkConstants.MaxSubframes >> 1 : SilkConstants.MaxSubframes;
            channel.SetSampleRate(rateKhz, OutputRateHz);
        }
    }

    /// <summary>Reads the voice activity and redundancy flags that open a packet, and skips the redundancy frames.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="internalChannels">The channels the stream codes.</param>
    private void ReadFrameFlags(ref OpusRangeDecoder decoder, int internalChannels)
    {
        for (var n = 0; n < internalChannels; n++)
        {
            var channel = _channels[n];
            for (var i = 0; i < channel.FramesPerPacket; i++)
            {
                channel.VadFlags[i] = decoder.ReadBit(1);
            }

            channel.LbrrFlag = decoder.ReadBit(1);
        }

        for (var n = 0; n < internalChannels; n++)
        {
            ReadRedundancyFlags(ref decoder, _channels[n]);
        }

        for (var i = 0; i < _channels[0].FramesPerPacket; i++)
        {
            for (var n = 0; n < internalChannels; n++)
            {
                SkipRedundancyFrame(ref decoder, i, n, internalChannels);
            }
        }
    }

    /// <summary>Reads past one redundancy frame, which a decoder that is not recovering from a loss does not use.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frame">The index of the frame in the packet.</param>
    /// <param name="n">The channel.</param>
    /// <param name="internalChannels">The channels the stream codes.</param>
    private void SkipRedundancyFrame(ref OpusRangeDecoder decoder, int frame, int n, int internalChannels)
    {
        var channel = _channels[n];
        if (!channel.LbrrFlags[frame])
        {
            return;
        }

        if (internalChannels == SilkConstants.Pair && n == 0)
        {
            Span<int> ignored = stackalloc int[SilkConstants.Pair];
            SilkStereo.ReadPredictors(ref decoder, ignored);
            if (!_channels[1].LbrrFlags[frame])
            {
                _ = SilkStereo.ReadMidOnly(ref decoder);
            }
        }

        var coding = frame > 0 && channel.LbrrFlags[frame - 1] ? SilkConstants.CodeConditionally : SilkConstants.CodeIndependently;
        SilkIndexReader.Read(channel, ref decoder, frame, redundant: true, coding);
        SilkPulses.Decode(ref decoder, _scratch.Pulses, channel.Indices.SignalType, channel.Indices.QuantOffsetType, channel.FrameLength);
    }

    /// <summary>Reads the weights that predict the side channel from the mid channel, and whether the side is left out.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="internalChannels">The channels the stream codes.</param>
    /// <param name="lost">Whether the packet was lost.</param>
    /// <param name="predictors">Receives the weights.</param>
    /// <returns><see langword="true"/> when only the mid channel is coded.</returns>
    private bool ReadStereoSide(ref OpusRangeDecoder decoder, int internalChannels, bool lost, scoped Span<int> predictors)
    {
        if (internalChannels != SilkConstants.Pair)
        {
            return false;
        }

        if (lost)
        {
            _stereo.PreviousPredictors.AsSpan().CopyTo(predictors);
            return false;
        }

        SilkStereo.ReadPredictors(ref decoder, predictors);
        return !_channels[1].VadFlags[_channels[0].FramesDecoded] && SilkStereo.ReadMidOnly(ref decoder);
    }

    /// <summary>Decodes the frame of each channel.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="internalChannels">The channels the stream codes.</param>
    /// <param name="lost">Whether the packet was lost.</param>
    /// <param name="midOnly">Whether only the mid channel is coded.</param>
    /// <returns>The samples of a frame.</returns>
    private int DecodeChannels(ref OpusRangeDecoder decoder, int internalChannels, bool lost, bool midOnly)
    {
        if (internalChannels == SilkConstants.Pair && !midOnly && _previousMidOnly)
        {
            ResetSideChannel(_channels[1]);
        }

        var hasSide = lost ? !_previousMidOnly : !midOnly;
        var samples = 0;
        for (var n = 0; n < internalChannels; n++)
        {
            var frame = _frames[n].AsSpan(FrameHistory);
            if (n == 0 || hasSide)
            {
                samples = SilkFrame.Decode(_channels[n], ref decoder, frame, lost, GetCodingMode(n), _scratch);
            }
            else
            {
                frame[..samples].Clear();
            }

            _channels[n].FramesDecoded++;
        }

        return samples;
    }

    /// <summary>Works out how a frame is coded: on its own when nothing came before it, otherwise depending on the frame before.</summary>
    /// <param name="n">The channel.</param>
    /// <returns>The coding mode.</returns>
    private int GetCodingMode(int n)
    {
        if (_channels[0].FramesDecoded <= n)
        {
            return SilkConstants.CodeIndependently;
        }

        return n > 0 && _previousMidOnly ? SilkConstants.CodeIndependentlyNoLtpScaling : SilkConstants.CodeConditionally;
    }

    /// <summary>After a lost packet, lets the gain of the next frame rise freely instead of being held to the one before.</summary>
    /// <param name="internalChannels">The channels the stream codes.</param>
    private void ForgetGains(int internalChannels)
    {
        for (var n = 0; n < internalChannels; n++)
        {
            _channels[n].LastGainIndex = SilkChannel.InitialGainIndex;
        }
    }

    /// <summary>Turns the decoded channels into the output: joins mid and side, resamples to 48 kHz and interleaves.</summary>
    /// <param name="control">What the packet is like.</param>
    /// <param name="predictors">The weights of the side predictors.</param>
    /// <param name="samples">The samples of a decoded frame.</param>
    /// <param name="stereoToMono">Whether the stream has just changed from stereo to mono.</param>
    /// <param name="output">Receives the samples.</param>
    /// <returns>The samples of each channel written.</returns>
    private int Emit(SilkControl control, ReadOnlySpan<int> predictors, int samples, bool stereoToMono, Span<short> output)
    {
        var rateKhz = _channels[0].SampleRateKhz;
        if (control.Channels == SilkConstants.Pair && control.InternalChannels == SilkConstants.Pair)
        {
            _stereo.ToLeftRight(_frames[0], _frames[1], predictors, rateKhz, samples);
        }
        else
        {
            _stereo.MidHistory.CopyTo(_frames[0], 0);
            _frames[0].AsSpan(samples, FrameHistory).CopyTo(_stereo.MidHistory);
        }

        var written = samples * OutputRateHz / (rateKhz * SilkConstants.HertzPerKilohertz);
        var channels = Math.Min(control.Channels, control.InternalChannels);
        for (var n = 0; n < channels; n++)
        {
            ResampleChannel(n, _frames[n].AsSpan(1, samples), control.Channels, written, output);
        }

        if (control.Channels == SilkConstants.Pair && control.InternalChannels == 1)
        {
            ExpandToStereo(stereoToMono, samples, written, output);
        }

        return written;
    }

    /// <summary>Resamples one channel and, when the output is stereo, interleaves it.</summary>
    /// <param name="n">The channel.</param>
    /// <param name="input">The channel's decoded samples.</param>
    /// <param name="outputChannels">The channels of the output.</param>
    /// <param name="written">The samples of output for each channel.</param>
    /// <param name="output">Receives the samples.</param>
    private void ResampleChannel(int n, ReadOnlySpan<short> input, int outputChannels, int written, Span<short> output)
    {
        var resampler = _channels[n].Resampler!;
        if (outputChannels != SilkConstants.Pair)
        {
            resampler.Resample(output, input);
            return;
        }

        resampler.Resample(_resampled, input);
        for (var i = 0; i < written; i++)
        {
            output[n + (SilkConstants.Pair * i)] = _resampled[i];
        }
    }

    /// <summary>Fills the second output channel when the stream is mono: with the same samples, or resampled separately right after a change from stereo.</summary>
    /// <param name="stereoToMono">Whether the stream has just changed from stereo to mono.</param>
    /// <param name="samples">The samples of a decoded frame.</param>
    /// <param name="written">The samples of output for each channel.</param>
    /// <param name="output">The interleaved output; the right channel is filled in.</param>
    private void ExpandToStereo(bool stereoToMono, int samples, int written, Span<short> output)
    {
        if (stereoToMono)
        {
            _channels[1].Resampler!.Resample(_resampled, _frames[0].AsSpan(1, samples));
            for (var i = 0; i < written; i++)
            {
                output[1 + (SilkConstants.Pair * i)] = _resampled[i];
            }

            return;
        }

        for (var i = 0; i < written; i++)
        {
            output[1 + (SilkConstants.Pair * i)] = output[SilkConstants.Pair * i];
        }
    }
}
