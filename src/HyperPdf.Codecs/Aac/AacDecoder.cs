// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// Decodes AAC Low Complexity sound. Each packet is one raw data block of 1024 samples a channel. The track's configuration is the
/// number of samples to drop from the start of the sound, as four bytes, then the audio specific config.
/// </summary>
[DebuggerDisplay("AacDecoder: {SampleRate} Hz, {Channels} channels")]
public sealed class AacDecoder : AudioDecoder
{
    /// <summary>The bytes of the skipped sample count that starts the configuration.</summary>
    internal const int SkipBytes = sizeof(int);

    /// <summary>The seed of the noise generator.</summary>
    private const uint NoiseSeed = 0x1F2E3D4C;

    /// <summary>The bits of the element type.</summary>
    private const int ElementBits = 3;

    /// <summary>The bits of an element's instance tag.</summary>
    private const int TagBits = 4;

    /// <summary>The bits of the global gain.</summary>
    private const int GainBits = 8;

    /// <summary>The bits of the mid/side mask mode.</summary>
    private const int MaskModeBits = 2;

    /// <summary>The bits of a byte count in a data stream element.</summary>
    private const int ByteCountBits = 8;

    /// <summary>The byte count that continues in a second count.</summary>
    private const int ByteCountEscape = 255;

    /// <summary>The bits of the length of a fill element.</summary>
    private const int FillBits = 4;

    /// <summary>The fill length that continues in a second count.</summary>
    private const int FillEscape = 15;

    /// <summary>The element type of a single channel.</summary>
    private const int SingleChannel = 0;

    /// <summary>The element type of a channel pair.</summary>
    private const int ChannelPair = 1;

    /// <summary>The channels of a channel pair.</summary>
    private const int PairChannels = 2;

    /// <summary>The element type of a low frequency channel.</summary>
    private const int LowFrequency = 3;

    /// <summary>The element type of a data stream element.</summary>
    private const int DataStream = 4;

    /// <summary>The element type of a program config element.</summary>
    private const int ProgramConfig = 5;

    /// <summary>The element type of a fill element.</summary>
    private const int Fill = 6;

    /// <summary>The element type that ends a frame.</summary>
    private const int End = 7;

    /// <summary>The coded channel that is each output channel, for each channel configuration.</summary>
    private static readonly byte[][] ChannelOrders =
    [
        [],
        [0x0],
        [0x0, 0x1],
        [0x1, 0x2, 0x0],
        [0x1, 0x2, 0x0, 0x3],
        [0x1, 0x2, 0x0, 0x3, 0x4],
        [0x1, 0x2, 0x0, 0x5, 0x3, 0x4],
        [0x3, 0x4, 0x0, 0x7, 0x5, 0x6, 0x1, 0x2],
    ];

    /// <summary>The coded channels.</summary>
    private readonly AacChannel[] _coded;

    /// <summary>The samples of each coded channel in the frame.</summary>
    private readonly float[][] _samples;

    /// <summary>The filterbank.</summary>
    private readonly AacFilterbank _filterbank = new();

    /// <summary>The mid/side mask of the pair being decoded.</summary>
    private readonly bool[] _mask = new bool[AacChannel.ShortCount * AacChannel.BandStride];

    /// <summary>The coded channel that is each output channel.</summary>
    private readonly byte[] _order;

    /// <summary>The sample rate index.</summary>
    private readonly int _rateIndex;

    /// <summary>The samples still to drop.</summary>
    private int _skip;

    /// <summary>The state of the noise generator.</summary>
    private uint _noise = NoiseSeed;

    /// <summary>Initializes a new instance of the <see cref="AacDecoder"/> class.</summary>
    /// <param name="config">The config.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="skip">The samples to drop from the start.</param>
    private AacDecoder(AacConfig config, int channels, int skip)
        : base(config.SampleRate, channels)
    {
        _rateIndex = config.RateIndex;
        _skip = skip;
        _coded = new AacChannel[channels];
        _samples = new float[channels][];
        for (var index = 0; index < channels; index++)
        {
            _coded[index] = new();
            _samples[index] = new float[AacChannel.LongSize];
        }

        _order = OrderOf(config.ChannelConfiguration, channels);
    }

    /// <summary>Makes a decoder for an AAC track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">The track is a kind of AAC this decoder does not read.</exception>
    public static AacDecoder Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length <= SkipBytes)
        {
            throw new NotSupportedException("The AAC track has no config.");
        }

        var config = AacConfig.Parse(data[SkipBytes..]);
        var channels = config.Channels > 0 ? config.Channels : track.Channels;
        if (config.RateIndex < 0 || channels < 1)
        {
            throw new NotSupportedException("The AAC track names no usable rate or channels.");
        }

        return new(config, channels, Math.Max(0, BinaryPrimitives.ReadInt32LittleEndian(data)));
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var reader = new MsbBitReader(packet.Data);
        var count = ReadBlock(ref reader);
        if (reader.IsOverrun || count != Channels)
        {
            throw new InvalidDataException("An AAC frame is cut short or has the wrong channels.");
        }

        var dropped = Math.Min(_skip, AacChannel.LongSize);
        _skip -= dropped;
        var frames = AacChannel.LongSize - dropped;
        var target = output.Append(frames * Channels);
        for (var channel = 0; channel < Channels; channel++)
        {
            var source = _samples[_order[channel]].AsSpan(dropped, frames);
            for (var frame = 0; frame < frames; frame++)
            {
                target[(frame * Channels) + channel] = source[frame];
            }
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        foreach (var channel in _coded)
        {
            channel.Reset();
        }

        _skip = 0;
        _noise = NoiseSeed;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing here needs to be let go.
    }

    /// <summary>Finds which coded channel is each output channel.</summary>
    /// <param name="configuration">The channel configuration.</param>
    /// <param name="channels">The number of channels.</param>
    /// <returns>The coded channel of each output channel.</returns>
    private static byte[] OrderOf(int configuration, int channels)
    {
        if (configuration > 0 && configuration < ChannelOrders.Length && ChannelOrders[configuration].Length == channels)
        {
            return ChannelOrders[configuration];
        }

        var order = new byte[channels];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = (byte)index;
        }

        return order;
    }

    /// <summary>Skips a data stream element.</summary>
    /// <param name="reader">The bits.</param>
    private static void SkipDataStream(ref MsbBitReader reader)
    {
        reader.Skip(TagBits);
        var align = reader.ReadBit();
        var bytes = (int)reader.Read(ByteCountBits);
        bytes += bytes == ByteCountEscape ? (int)reader.Read(ByteCountBits) : 0;
        if (align)
        {
            reader.Align();
        }

        AacBits.Skip(ref reader, (long)bytes * AacBits.ByteBits);
    }

    /// <summary>Skips a fill element.</summary>
    /// <param name="reader">The bits.</param>
    private static void SkipFill(ref MsbBitReader reader)
    {
        var bytes = (int)reader.Read(FillBits);
        bytes += bytes == FillEscape ? (int)reader.Read(ByteCountBits) - 1 : 0;
        AacBits.Skip(ref reader, (long)bytes * AacBits.ByteBits);
    }

    /// <summary>Reads the elements of a frame.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The number of channels decoded.</returns>
    /// <exception cref="InvalidDataException">The frame is not valid or uses a tool this decoder does not read.</exception>
    private int ReadBlock(ref MsbBitReader reader)
    {
        var channels = 0;
        while (!reader.IsOverrun)
        {
            var element = (int)reader.Read(ElementBits);
            switch (element)
            {
                case SingleChannel or LowFrequency:
                {
                    reader.Skip(TagBits);
                    channels = ReadSingle(ref reader, channels);
                    break;
                }

                case ChannelPair:
                {
                    reader.Skip(TagBits);
                    channels = ReadPair(ref reader, channels);
                    break;
                }

                case DataStream:
                {
                    SkipDataStream(ref reader);
                    break;
                }

                case ProgramConfig:
                {
                    AacProgramConfig.Skip(ref reader);
                    break;
                }

                case Fill:
                {
                    SkipFill(ref reader);
                    break;
                }

                case End:
                {
                    return channels;
                }

                default:
                {
                    throw new InvalidDataException("An AAC coupling channel is not supported.");
                }
            }
        }

        return channels;
    }

    /// <summary>Reads and decodes a single channel element.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channels">The channels decoded before.</param>
    /// <returns>The channels decoded so far.</returns>
    private int ReadSingle(ref MsbBitReader reader, int channels)
    {
        var channel = Next(channels, 1);
        ReadChannelStream(ref reader, channel, false);
        Finish(channel, channels);
        return channels + 1;
    }

    /// <summary>Reads and decodes a channel pair element.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channels">The channels decoded before.</param>
    /// <returns>The channels decoded so far.</returns>
    private int ReadPair(ref MsbBitReader reader, int channels)
    {
        var first = Next(channels, PairChannels);
        var second = _coded[channels + 1];
        var common = reader.ReadBit();
        var mode = 0;
        if (common)
        {
            AacSideInfo.ReadWindowInfo(ref reader, first, _rateIndex);
            mode = (int)reader.Read(MaskModeBits);
            if (mode == AacStereo.MaskedBands)
            {
                ReadMask(ref reader, first);
            }

            second.CopyWindowInfo(first);
        }

        ReadChannelStream(ref reader, first, common);
        ReadChannelStream(ref reader, second, common);
        var offsets = second.IsShort ? AacBands.Short(_rateIndex) : AacBands.Long(_rateIndex);
        AacStereo.Apply(first, second, offsets, _mask, mode);
        Finish(first, channels);
        Finish(second, channels + 1);
        return channels + PairChannels;
    }

    /// <summary>Gets the coded channel for the next element, checking that there is room.</summary>
    /// <param name="channels">The channels decoded before.</param>
    /// <param name="needed">The channels the element has.</param>
    /// <returns>The first channel of the element.</returns>
    /// <exception cref="InvalidDataException">There is no room.</exception>
    private AacChannel Next(int channels, int needed)
    {
        if (channels + needed > _coded.Length)
        {
            throw new InvalidDataException("An AAC frame has more channels than its config says.");
        }

        return _coded[channels];
    }

    /// <summary>Reads the mid/side mask.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel that has the groups and bands.</param>
    private void ReadMask(ref MsbBitReader reader, AacChannel channel)
    {
        for (var group = 0; group < channel.GroupCount; group++)
        {
            for (var band = 0; band < channel.MaxSfb; band++)
            {
                _mask[(group * AacChannel.BandStride) + band] = reader.ReadBit();
            }
        }
    }

    /// <summary>Reads one channel's stream and makes its spectrum.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="common">Whether the window info was read for a pair.</param>
    /// <exception cref="InvalidDataException">The stream is not valid or uses gain control.</exception>
    private void ReadChannelStream(ref MsbBitReader reader, AacChannel channel, bool common)
    {
        channel.GlobalGain = (int)reader.Read(GainBits);
        if (!common)
        {
            AacSideInfo.ReadWindowInfo(ref reader, channel, _rateIndex);
        }

        AacSideInfo.ReadSections(ref reader, channel);
        AacSideInfo.ReadScaleFactors(ref reader, channel);
        channel.PulseCount = 0;
        if (reader.ReadBit())
        {
            AacSideInfo.ReadPulse(ref reader, channel);
        }

        channel.HasTns = reader.ReadBit();
        if (channel.HasTns)
        {
            AacSideInfo.ReadTns(ref reader, channel);
        }

        if (reader.ReadBit())
        {
            throw new InvalidDataException("AAC gain control is not supported.");
        }

        var offsets = channel.IsShort ? AacBands.Short(_rateIndex) : AacBands.Long(_rateIndex);
        AacSpectral.Read(ref reader, channel, offsets);
        _noise = AacDequantizer.Run(channel, offsets, _noise);
    }

    /// <summary>Applies TNS to a channel and turns its spectrum into samples.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="index">The coded channel number.</param>
    private void Finish(AacChannel channel, int index)
    {
        if (channel.HasTns)
        {
            var offsets = channel.IsShort ? AacBands.Short(_rateIndex) : AacBands.Long(_rateIndex);
            AacTns.Apply(channel, offsets, _rateIndex);
        }

        _filterbank.Run(channel, _samples[index]);
    }
}
