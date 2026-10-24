// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// Decodes MPEG audio layer 3. A frame's coded data may start in the frames before it (the bit reservoir), so the decoder keeps the
/// last 511 bytes of data; a frame whose data reaches back further than the decoder has seen, as after a seek, comes out as silence.
/// </summary>
[DebuggerDisplay("Mp3Decoder: {SampleRate} Hz, {Channels} channels")]
public sealed class Mp3Decoder : AudioDecoder
{
    /// <summary>The most bytes of earlier data a frame can use.</summary>
    private const int ReservoirBytes = 511;

    /// <summary>The bytes the reservoir has room for: the earlier data and the largest frame.</summary>
    private const int ReservoirCapacity = 4_096;

    /// <summary>The most channels in a frame.</summary>
    private const int MostChannels = 2;

    /// <summary>The most granules in a frame.</summary>
    private const int MostGranules = 2;

    /// <summary>The track's gapless information.</summary>
    private readonly Mp3StreamTag _tag;

    /// <summary>The side information of the frame being decoded.</summary>
    private readonly Mp3SideInfo _sideInfo = new();

    /// <summary>The earlier main data followed by this frame's.</summary>
    private readonly byte[] _reservoir = new byte[ReservoirCapacity];

    /// <summary>The whole numbers of a channel being decoded.</summary>
    private readonly int[] _values = new int[Mp3Tables.GranuleSamples];

    /// <summary>The scale factors of each channel, which the second granule of MPEG 1 may repeat.</summary>
    private readonly int[][] _scalefactors = [new int[Mp3Scalefactors.Capacity], new int[Mp3Scalefactors.Capacity]];

    /// <summary>The coefficients of each channel of the granule being decoded.</summary>
    private readonly float[][] _coefficients = [new float[Mp3Tables.GranuleSamples], new float[Mp3Tables.GranuleSamples]];

    /// <summary>The number of coefficients up to the last coded one, for each channel.</summary>
    private readonly int[] _used = new int[MostChannels];

    /// <summary>The second half of the last granule's transforms, for each channel.</summary>
    private readonly float[][] _overlap = [new float[Mp3Tables.GranuleSamples], new float[Mp3Tables.GranuleSamples]];

    /// <summary>The subband samples of the granule, for each channel.</summary>
    private readonly float[] _slots = new float[Mp3Tables.GranuleSamples];

    /// <summary>The polyphase filter of each channel.</summary>
    private readonly Mp3Synthesis[] _synthesis = [new(), new()];

    /// <summary>The sound of the frame being decoded, interleaved by the frame's channels.</summary>
    private readonly float[] _frame =new float[Mp3Tables.GranuleSamples * MostGranules * MostChannels];

    /// <summary>The bytes of earlier main data in the reservoir.</summary>
    private int _reservoirLength;

    /// <summary>Initializes a new instance of the <see cref="Mp3Decoder"/> class.</summary>
    /// <param name="track">The track.</param>
    private Mp3Decoder(MediaTrack track)
        : base(track.SampleRate, track.Channels) => _tag = Mp3StreamTag.FromConfiguration(track.Configuration.Span);

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The MP3 track.</param>
    /// <returns>The decoder.</returns>
    public static Mp3Decoder Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new(track);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _reservoirLength = 0;
        foreach (var overlap in _overlap)
        {
            Array.Clear(overlap);
        }

        foreach (var filter in _synthesis)
        {
            filter.Reset();
        }
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var data = packet.Data;
        if (!Mp3FrameHeader.TryParse(data, out var header) || header.Layer != Mp3FrameHeader.Layer3 || data.Length < header.FrameLength)
        {
            throw new InvalidDataException("The MP3 frame header is not valid.");
        }

        var frame = _frame.AsSpan(0, header.SamplesPerFrame * header.Channels);
        frame.Clear();
        DecodeFrame(header, data[..header.FrameLength], frame);
        Emit(header, packet.Time, frame, output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing is held that needs to be freed.
    }

    /// <summary>Decodes a frame to sound, leaving it silent when its data cannot be used.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="data">The whole frame.</param>
    /// <param name="frame">Receives the interleaved sound.</param>
    private void DecodeFrame(in Mp3FrameHeader header, ReadOnlySpan<byte> data, Span<float> frame)
    {
        var sideStart = Mp3FrameHeader.Length + (header.HasCrc ? Mp3FrameHeader.CrcLength : 0);
        var mainStart = sideStart + header.SideInfoLength;
        if (mainStart > data.Length || !_sideInfo.Read(header, data.Slice(sideStart, header.SideInfoLength)))
        {
            _reservoirLength = 0;
            return;
        }

        var earlier = _reservoirLength;
        var main = data[mainStart..];
        var available = earlier + main.Length <= ReservoirCapacity;
        var usable = available && _sideInfo.MainDataBegin <= earlier;
        if (available)
        {
            main.CopyTo(_reservoir.AsSpan(earlier));
        }

        if (usable)
        {
            var start = earlier - _sideInfo.MainDataBegin;
            DecodeGranules(header, _reservoir.AsSpan(start, earlier + main.Length - start), frame);
        }

        KeepReservoir(available ? earlier + main.Length : 0);
    }

    /// <summary>Keeps the last bytes of main data for the next frame.</summary>
    /// <param name="length">The bytes in the reservoir now.</param>
    private void KeepReservoir(int length)
    {
        var keep = Math.Min(length, ReservoirBytes);
        _reservoir.AsSpan(length - keep, keep).CopyTo(_reservoir);
        _reservoirLength = keep;
    }

    /// <summary>Decodes the granules of a frame.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="main">The main data, starting at the first granule's data.</param>
    /// <param name="frame">Receives the interleaved sound.</param>
    private void DecodeGranules(in Mp3FrameHeader header, ReadOnlySpan<byte> main, Span<float> frame)
    {
        var reader = new MsbBitReader(main);
        var channels = header.Channels;
        var granules = header.IsMpeg1 ? MostGranules : 1;
        long bit = 0;
        for (var granule = 0; granule < granules; granule++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var info = _sideInfo.Granules[granule][channel];
                reader.Seek(bit);
                DecodeChannel(ref reader, header, info, granule, channel, bit + info.Part23Length);
                bit += info.Part23Length;
            }

            ProcessStereo(header, granule);
            for (var channel = 0; channel < channels; channel++)
            {
                var info = _sideInfo.Granules[granule][channel];
                var widths = Mp3Bands.Widths(header, info);
                Mp3Hybrid.Run(header, info, widths, _used[channel], _coefficients[channel], _overlap[channel], _slots);
                _synthesis[channel].Run(_slots, frame, (granule * Mp3Tables.GranuleSamples * channels) + channel, channels);
            }
        }
    }

    /// <summary>Reads and scales the coefficients of one channel of a granule.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="granule">The granule.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="endBit">The bit where this granule's data ends.</param>
    private void DecodeChannel(ref MsbBitReader reader, in Mp3FrameHeader header, Mp3GranuleChannel info, int granule, int channel, long endBit)
    {
        var widths = Mp3Bands.Widths(header, info);
        var scalefactors = _scalefactors[channel];
        Mp3Scalefactors.Read(ref reader, header, info, granule, channel, scalefactors);
        var ok = Mp3Huffman.Decode(ref reader, info, widths, endBit, _values, out var used);
        _used[channel] = ok ? used : 0;
        if (ok)
        {
            Mp3Dequantizer.Apply(header, info, widths, scalefactors, _values, used, _coefficients[channel]);
        }
        else
        {
            _coefficients[channel].AsSpan().Clear();
        }
    }

    /// <summary>Applies the stereo coding of a granule.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="granule">The granule.</param>
    private void ProcessStereo(in Mp3FrameHeader header, int granule)
    {
        if (header.ChannelMode != Mp3FrameHeader.JointStereoMode || header.ModeExtension == 0)
        {
            return;
        }

        var right = _sideInfo.Granules[granule][1];
        Mp3Stereo.Process(header, right, Mp3Bands.Widths(header, right), _scalefactors[1], _coefficients[0], _coefficients[1]);
        _used[0] = Math.Max(_used[0], _used[1]);
        _used[1] = _used[0];
    }

    /// <summary>Appends a frame's sound to the output, leaving out what the encoder added at the start and end.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="time">The time of the frame's first sample, in seconds.</param>
    /// <param name="frame">The frame's sound, interleaved by the frame's channels.</param>
    /// <param name="output">Receives the sound.</param>
    private void Emit(in Mp3FrameHeader header, double time, ReadOnlySpan<float> frame, PcmBuffer output)
    {
        var firstSample = (long)Math.Round(time * SampleRate);
        var skip = _tag.HasTrim ? Math.Max(0L, _tag.EncoderDelay + Mp3StreamTag.DecoderDelay - firstSample) : 0;
        var end = _tag.HasTrim && _tag.Frames > 0 ? ((long)_tag.Frames * header.SamplesPerFrame) - _tag.EncoderPadding + Mp3StreamTag.DecoderDelay : long.MaxValue;
        var from = (int)Math.Min(skip, header.SamplesPerFrame);
        var to = (int)Math.Clamp(end - firstSample, 0, header.SamplesPerFrame);
        if (to <= from)
        {
            return;
        }

        var target = output.Append((to - from) * Channels);
        var source = header.Channels;
        for (var sample = from; sample < to; sample++)
        {
            Mp3ChannelMixer.WriteSample(frame.Slice(sample * source, source), target.Slice((sample - from) * Channels, Channels));
        }
    }
}
