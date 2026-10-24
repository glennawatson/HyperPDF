// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Decodes the frames of Opus packets with the right layer or layers (RFC 6716 section 4): SILK for speech, CELT for music, or SILK for the low
/// band with CELT on top for the high band. It keeps both layers' state between frames and resets a layer that is not used for a while.
/// </summary>
internal sealed class OpusFrameDecoder
{
    /// <summary>The first band of the CELT layer when it is coded on top of a SILK layer.</summary>
    private const int HybridStartBand = 17;

    /// <summary>The number of channels of a stereo frame.</summary>
    private const int StereoChannels = 2;

    /// <summary>The last band a narrowband CELT frame codes, plus one.</summary>
    private const int NarrowEndBand = 13;

    /// <summary>The last band a wideband CELT frame codes, plus one.</summary>
    private const int WideEndBand = 17;

    /// <summary>The last band a super wideband CELT frame codes, plus one.</summary>
    private const int SuperWideEndBand = 19;

    /// <summary>The internal rate of the SILK layer in a narrowband stream.</summary>
    private const int NarrowRateHz = 8000;

    /// <summary>The internal rate of the SILK layer in a mediumband stream.</summary>
    private const int MediumRateHz = 12_000;

    /// <summary>The internal rate of the SILK layer in a wideband stream, and in a hybrid stream.</summary>
    private const int WideRateHz = 16_000;

    /// <summary>The samples at 48 kHz in a millisecond.</summary>
    private const int SamplesPerMs = 48;

    /// <summary>The fewest milliseconds of sound the SILK layer works in.</summary>
    private const int MinimumSilkMs = 10;

    /// <summary>The samples at 48 kHz of 2.5 ms, the shortest frame.</summary>
    private const int ShortestFrame = 120;

    /// <summary>The samples at 48 kHz of 5 ms, the length of a redundant frame.</summary>
    private const int RedundantFrame = 240;

    /// <summary>The bits the redundancy flag of a hybrid frame is coded with: its probability is one over two to this power.</summary>
    private const int RedundancyOdds = 12;

    /// <summary>The bits of the hybrid redundancy flag and its length that must be left in the frame for them to be read.</summary>
    private const int HybridRedundancyBits = 20;

    /// <summary>The bits of redundancy information that must be left in the frame for them to be read.</summary>
    private const int RedundancyBits = 17;

    /// <summary>The bytes the length of a redundant frame is coded above.</summary>
    private const int MinimumRedundantBytes = 2;

    /// <summary>The values the length of a redundant frame is coded among.</summary>
    private const int RedundantLengthRange = 256;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The scale from a 16 bit sample to the range of -1 to 1.</summary>
    private const float SilkScale = 1F / 32768F;

    /// <summary>The largest frame, 60 ms at 48 kHz.</summary>
    private const int MaxFrameSamples = 60 * SamplesPerMs;

    /// <summary>The bytes of the frame that makes the CELT layer fade out.</summary>
    private static readonly byte[] SilenceFrame = [0xFF, 0xFF];

    /// <summary>The CELT layer.</summary>
    private readonly CeltDecoder _celt;

    /// <summary>The SILK layer.</summary>
    private readonly SilkDecoder _silk = new();

    /// <summary>The channels of the output.</summary>
    private readonly int _channels;

    /// <summary>Room for the SILK layer's samples of a frame.</summary>
    private readonly short[] _silkPcm;

    /// <summary>Room for the samples of a redundant frame.</summary>
    private readonly float[] _redundantPcm;

    /// <summary>The mode of the frame before, or <see langword="null"/> before any.</summary>
    private OpusMode? _previousMode;

    /// <summary>Whether the frame before ended with a redundant frame, which left the CELT layer ready for this one.</summary>
    private bool _previousRedundancy;

    /// <summary>Initializes a new instance of the <see cref="OpusFrameDecoder"/> class.</summary>
    /// <param name="channels">The channels of the output, 1 or 2.</param>
    internal OpusFrameDecoder(int channels)
    {
        _channels = channels;
        _celt = new(channels);
        _silkPcm = new short[MaxFrameSamples * StereoChannels];
        _redundantPcm = new float[RedundantFrame * StereoChannels];
    }

    /// <summary>Puts both layers back to the state before any frame.</summary>
    internal void Reset()
    {
        _celt.Reset();
        _silk.Reset();
        _previousMode = null;
        _previousRedundancy = false;
    }

    /// <summary>Decodes one frame.</summary>
    /// <param name="data">The frame's bytes; a frame of one byte or none is lost.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">Receives the samples, the channels interleaved.</param>
    /// <exception cref="InvalidDataException">The frame is not valid.</exception>
    internal void Decode(ReadOnlySpan<byte> data, OpusPacketInfo info, Span<float> pcm)
    {
        if (data.Length <= 1)
        {
            Conceal(info, pcm);
            return;
        }

        if (info.Mode != OpusMode.Celt && _previousMode == OpusMode.Celt)
        {
            _silk.Reset();
        }

        var redundancy = info.Mode == OpusMode.Celt ? default : DecodeLayers(data, info, pcm);
        if (info.Mode == OpusMode.Celt)
        {
            DecodeCelt(data, info, pcm);
        }

        _previousMode = info.Mode;
        _previousRedundancy = redundancy.Present && !redundancy.CeltToSilk;
    }

    /// <summary>Works out the last band, plus one, that a frame of a bandwidth codes.</summary>
    /// <param name="bandwidth">The bandwidth.</param>
    /// <returns>The end band.</returns>
    private static int GetEndBand(OpusBandwidth bandwidth) => bandwidth switch
    {
        OpusBandwidth.Narrow => NarrowEndBand,
        OpusBandwidth.Medium or OpusBandwidth.Wide => WideEndBand,
        OpusBandwidth.SuperWide => SuperWideEndBand,
        _ => CeltEnergy.Bands,
    };

    /// <summary>Works out the internal rate of the SILK layer in a stream of a bandwidth.</summary>
    /// <param name="bandwidth">The bandwidth.</param>
    /// <returns>The rate in Hz.</returns>
    private static int GetSilkRate(OpusBandwidth bandwidth) => bandwidth switch
    {
        OpusBandwidth.Narrow => NarrowRateHz,
        OpusBandwidth.Medium => MediumRateHz,
        _ => WideRateHz,
    };

    /// <summary>Adds the SILK layer's samples to the CELT layer's.</summary>
    /// <param name="pcm">The samples so far, which the SILK samples are added to.</param>
    /// <param name="silk">The SILK layer's samples.</param>
    private static void AddSilk(Span<float> pcm, ReadOnlySpan<short> silk)
    {
        for (var i = 0; i < pcm.Length; i++)
        {
            pcm[i] += silk[i] * SilkScale;
        }
    }

    /// <summary>Cross-fades from one signal to another over 2.5 ms, using the square of the window as the weight of the second.</summary>
    /// <param name="first">The signal faded from.</param>
    /// <param name="second">The signal faded to.</param>
    /// <param name="output">Receives the cross-fade; it may be either input.</param>
    /// <param name="channels">The channels.</param>
    private static void SmoothFade(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> output, int channels)
    {
        var window = CeltFloatTables.Window;
        for (var i = 0; i < ShortestFrame; i++)
        {
            var weight = window[i] * window[i];
            for (var c = 0; c < channels; c++)
            {
                var at = (i * channels) + c;
                output[at] = (weight * second[at]) + ((1F - weight) * first[at]);
            }
        }
    }

    /// <summary>Reads whether the frame ends with a redundant frame, which the layers after the SILK layer must stay clear of.</summary>
    /// <param name="decoder">The range decoder, whose storage is cut short of the redundant frame.</param>
    /// <param name="length">The bytes in the frame.</param>
    /// <param name="hybrid">Whether the frame is a hybrid frame.</param>
    /// <returns>The redundancy.</returns>
    private static OpusRedundancy ReadRedundancy(ref OpusRangeDecoder decoder, int length, bool hybrid)
    {
        var none = new OpusRedundancy(length, 0, false);
        if (decoder.Tell() + RedundancyBits + (hybrid ? HybridRedundancyBits : 0) > length * BitsPerByte)
        {
            return none;
        }

        if (hybrid && !decoder.ReadBit(RedundancyOdds))
        {
            return none;
        }

        var celtToSilk = decoder.ReadBit(1);
        var bytes = hybrid
            ? (int)decoder.ReadUnsigned(RedundantLengthRange) + MinimumRedundantBytes
            : length - ((decoder.Tell() + BitsPerByte - 1) / BitsPerByte);
        var kept = length - bytes;
        if (kept * BitsPerByte < decoder.Tell())
        {
            return none;
        }

        decoder.ShrinkStorage(bytes);
        return new(kept, bytes, celtToSilk);
    }

    /// <summary>Makes up a frame that was lost.</summary>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">Receives the samples.</param>
    private void Conceal(OpusPacketInfo info, Span<float> pcm)
    {
        pcm.Clear();
        if (_previousMode is not (OpusMode.Silk or OpusMode.Hybrid))
        {
            return;
        }

        var decoder = new OpusRangeDecoder(default);
        var count = DecodeSilkLayer(ref decoder, info, _previousMode == OpusMode.Hybrid, true);
        AddSilk(pcm, _silkPcm.AsSpan(0, count));
    }

    /// <summary>Decodes a frame coded by the CELT layer alone.</summary>
    /// <param name="data">The frame's bytes.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">Receives the samples.</param>
    /// <exception cref="InvalidDataException">The frame is not valid.</exception>
    private void DecodeCelt(ReadOnlySpan<byte> data, OpusPacketInfo info, Span<float> pcm)
    {
        ResetCeltOnModeChange(info.Mode);
        _celt.Start = 0;
        _celt.End = GetEndBand(info.Bandwidth);
        if (_celt.Decode(data, info.FrameSamples, info.Stereo ? StereoChannels : 1, pcm) < 0)
        {
            throw new InvalidDataException("The Opus frame is not valid.");
        }
    }

    /// <summary>Forgets the CELT layer's state when the stream changes to a mode that uses it, unless a redundant frame has just left it ready.</summary>
    /// <param name="mode">The mode of the frame about to be decoded.</param>
    private void ResetCeltOnModeChange(OpusMode mode)
    {
        if (_previousMode is not null && mode != _previousMode && !_previousRedundancy)
        {
            _celt.Reset();
        }
    }

    /// <summary>Decodes a frame coded by the SILK layer, with the CELT layer on top of it when the frame is a hybrid one.</summary>
    /// <param name="data">The frame's bytes.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">Receives the samples.</param>
    /// <returns>The redundant frame the frame ends with.</returns>
    /// <exception cref="InvalidDataException">The frame is not valid.</exception>
    private OpusRedundancy DecodeLayers(ReadOnlySpan<byte> data, OpusPacketInfo info, Span<float> pcm)
    {
        var hybrid = info.Mode == OpusMode.Hybrid;
        var decoder = new OpusRangeDecoder(data);
        var count = DecodeSilkLayer(ref decoder, info, hybrid, false);
        var redundancy = ReadRedundancy(ref decoder, data.Length, hybrid);
        _celt.End = GetEndBand(info.Bandwidth);
        if (redundancy.Present && redundancy.CeltToSilk)
        {
            DecodeRedundantFrame(data, redundancy, info);
        }

        if (hybrid)
        {
            DecodeHybridCelt(ref decoder, redundancy.KeptLength, info, pcm);
        }
        else
        {
            pcm.Clear();
            FadeOutAfterHybrid(info, pcm, redundancy);
        }

        AddSilk(pcm, _silkPcm.AsSpan(0, count));
        BlendRedundancy(data, redundancy, info, pcm);
        return redundancy;
    }

    /// <summary>Decodes the CELT layer of a hybrid frame, after the SILK layer on the same range decoder.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="length">The bytes of the frame that are not redundancy.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">Receives the samples.</param>
    /// <exception cref="InvalidDataException">The frame is not valid.</exception>
    private void DecodeHybridCelt(ref OpusRangeDecoder decoder, int length, OpusPacketInfo info, Span<float> pcm)
    {
        _celt.Start = HybridStartBand;
        ResetCeltOnModeChange(OpusMode.Hybrid);
        if (_celt.Decode(ref decoder, length, info.FrameSamples, info.Stereo ? StereoChannels : 1, pcm) < 0)
        {
            throw new InvalidDataException("The Opus frame is not valid.");
        }
    }

    /// <summary>Lets the CELT layer's tail fade out over the first of a SILK frame that follows a hybrid frame.</summary>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">The samples of the frame, to which the tail is added.</param>
    /// <param name="redundancy">The redundant frame the frame ends with.</param>
    private void FadeOutAfterHybrid(OpusPacketInfo info, Span<float> pcm, OpusRedundancy redundancy)
    {
        if (_previousMode != OpusMode.Hybrid || (redundancy.Present && redundancy.CeltToSilk && _previousRedundancy))
        {
            return;
        }

        _celt.Start = 0;
        var tail = pcm[..(ShortestFrame * _channels)];
        Span<float> faded = stackalloc float[ShortestFrame * StereoChannels];
        if (_celt.Decode(SilenceFrame, ShortestFrame, info.Stereo ? StereoChannels : 1, faded[..tail.Length]) < 0)
        {
            return;
        }

        for (var i = 0; i < tail.Length; i++)
        {
            tail[i] += faded[i];
        }
    }

    /// <summary>Decodes the redundant frame that covers the start of a frame that follows a CELT frame.</summary>
    /// <param name="data">The frame's bytes.</param>
    /// <param name="redundancy">The redundant frame.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    private void DecodeRedundantFrame(ReadOnlySpan<byte> data, OpusRedundancy redundancy, OpusPacketInfo info)
    {
        _celt.Start = 0;
        _ = _celt.Decode(data.Slice(redundancy.KeptLength, redundancy.Bytes), RedundantFrame, info.Stereo ? StereoChannels : 1, _redundantPcm.AsSpan(0, RedundantFrame * _channels));
    }

    /// <summary>Cross-fades a frame with the redundant frame the stream changed through.</summary>
    /// <param name="data">The frame's bytes.</param>
    /// <param name="redundancy">The redundant frame.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="pcm">The samples of the frame, changed in place.</param>
    private void BlendRedundancy(ReadOnlySpan<byte> data, OpusRedundancy redundancy, OpusPacketInfo info, Span<float> pcm)
    {
        if (!redundancy.Present)
        {
            return;
        }

        var redundant = _redundantPcm.AsSpan(0, RedundantFrame * _channels);
        var half = ShortestFrame * _channels;
        if (redundancy.CeltToSilk)
        {
            redundant[..half].CopyTo(pcm);
            SmoothFade(redundant[half..], pcm[half..], pcm[half..], _channels);
            return;
        }

        _celt.Reset();
        DecodeRedundantFrame(data, redundancy, info);
        var end = pcm.Length - half;
        SmoothFade(pcm[end..], redundant[half..], pcm[end..], _channels);
    }

    /// <summary>Decodes the SILK layer of a frame from a range decoder.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="hybrid">Whether the frame is a hybrid frame.</param>
    /// <param name="lost">Whether the frame is lost.</param>
    /// <returns>The samples written to the SILK buffer, counting every channel.</returns>
    private int DecodeSilkLayer(ref OpusRangeDecoder decoder, OpusPacketInfo info, bool hybrid, bool lost)
    {
        var rate = hybrid ? WideRateHz : GetSilkRate(info.Bandwidth);
        var control = new SilkControl(_channels, info.Stereo ? StereoChannels : 1, rate, Math.Max(MinimumSilkMs, info.FrameSamples / SamplesPerMs));
        var decoded = 0;
        while (decoded < info.FrameSamples)
        {
            decoded += _silk.Decode(ref decoder, control, lost, decoded == 0, _silkPcm.AsSpan(decoded * _channels));
        }

        return info.FrameSamples * _channels;
    }
}
