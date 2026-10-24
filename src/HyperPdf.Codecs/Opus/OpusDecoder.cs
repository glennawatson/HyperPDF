// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Decodes Opus audio packets (RFC 6716). A packet holds one or more frames; each is coded by the SILK layer, the CELT layer, or both,
/// and decodes to 48 kHz sound. The first samples of a stream are not sound, so the decoder drops as many as the stream header says.
/// </summary>
[DebuggerDisplay("OpusDecoder: {SampleRate} Hz, {Channels} channels")]
public sealed class OpusDecoder : AudioDecoder
{
    /// <summary>The number of channels of a stereo frame.</summary>
    private const int StereoChannels = 2;

    /// <summary>The largest frame, 60 ms at 48 kHz.</summary>
    private const int MaxFrameSamples = 60 * 48;

    /// <summary>The steps of the output gain in a decibel.</summary>
    private const float GainStepsPerDecibel = 256F;

    /// <summary>The decibels in a tenth of a bel, the step a gain is divided by to find its power ratio.</summary>
    private const float DecibelsPerFactor = 20F;

    /// <summary>The base of the decibel scale.</summary>
    private const float DecibelBase = 10F;

    /// <summary>The layers.</summary>
    private readonly OpusFrameDecoder _frames;

    /// <summary>Room for the samples of one frame.</summary>
    private readonly float[] _frame;

    /// <summary>The factor the output is scaled by, from the stream's output gain.</summary>
    private readonly float _gain;

    /// <summary>Whether the stream's output gain is anything but zero.</summary>
    private readonly bool _hasGain;

    /// <summary>The samples at the start of the stream that are not yet dropped.</summary>
    private int _skip;

    /// <summary>Initializes a new instance of the <see cref="OpusDecoder"/> class.</summary>
    /// <param name="head">The stream's header.</param>
    private OpusDecoder(OpusHead head)
        : base(OpusHead.OutputRate, head.Channels)
    {
        _frames = new(head.Channels);
        _frame = new float[MaxFrameSamples * StereoChannels];
        _hasGain = head.OutputGain != 0;
        _gain = MathF.Pow(DecibelBase, head.OutputGain / GainStepsPerDecibel / DecibelsPerFactor);
        _skip = head.PreSkip;
    }

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The Opus track, whose configuration holds the stream's identification header.</param>
    /// <returns>The decoder, or <see langword="null"/> when the header is damaged or the stream has more than two channels.</returns>
    public static OpusDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return OpusHead.TryRead(track.Configuration.Span, out var head) ? new OpusDecoder(head) : null;
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        Span<OpusFrame> frames = stackalloc OpusFrame[OpusPacket.MaxFrames];
        if (!OpusPacket.TryParse(packet.Data, frames, out var info))
        {
            throw new InvalidDataException("The Opus packet is not well formed.");
        }

        for (var index = 0; index < info.FrameCount; index++)
        {
            var (offset, length) = frames[index];
            DecodeFrame(packet.Data.Slice(offset, length), info, output);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _frames.Reset();
        _skip = 0;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }

    /// <summary>Multiplies samples by a factor.</summary>
    /// <param name="samples">The samples, changed in place.</param>
    /// <param name="factor">The factor.</param>
    private static void Scale(Span<float> samples, float factor)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= factor;
        }
    }

    /// <summary>Decodes one frame and appends its sound.</summary>
    /// <param name="data">The frame's bytes.</param>
    /// <param name="info">What the packet's table of contents says.</param>
    /// <param name="output">Receives the samples.</param>
    /// <exception cref="InvalidDataException">The frame is not valid.</exception>
    private void DecodeFrame(ReadOnlySpan<byte> data, OpusPacketInfo info, PcmBuffer output)
    {
        var channels = Channels;
        var pcm = _frame.AsSpan(0, info.FrameSamples * channels);
        _frames.Decode(data, info, pcm);
        var dropped = Math.Min(_skip, info.FrameSamples);
        _skip -= dropped;
        var kept = pcm[(dropped * channels)..];
        var destination = output.Append(kept.Length);
        kept.CopyTo(destination);
        if (_hasGain)
        {
            Scale(destination, _gain);
        }
    }
}
