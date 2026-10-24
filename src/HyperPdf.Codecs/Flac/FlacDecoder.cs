// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Flac;

/// <summary>Decodes FLAC frames to samples. Each packet is one frame; the decoder keeps no sound from frame to frame, so any frame can be decoded on its own.</summary>
[DebuggerDisplay("FlacDecoder: {SampleRate} Hz, {Channels} channels, {_info.BitsPerSample} bits")]
public sealed class FlacDecoder : AudioDecoder
{
    /// <summary>The most bits a sample can have for the 32 bit arithmetic here to hold the side channel, which has one more.</summary>
    private const int MostBits = 24;

    /// <summary>The bits of the subframe type field.</summary>
    private const int TypeBits = 6;

    /// <summary>The subframe type of a constant block.</summary>
    private const uint ConstantType = 0;

    /// <summary>The subframe type of an unencoded block.</summary>
    private const uint VerbatimType = 1;

    /// <summary>The first subframe type of the fixed predictors.</summary>
    private const uint FixedFirst = 8;

    /// <summary>The last subframe type of the fixed predictors.</summary>
    private const uint FixedLast = 12;

    /// <summary>The first subframe type of the linear predictors.</summary>
    private const uint LinearFirst = 32;

    /// <summary>The mask of the order in the type of a linear predictor.</summary>
    private const uint LinearOrderMask = 0x1F;

    /// <summary>The assignment code for left and side.</summary>
    private const int LeftSide = 8;

    /// <summary>The assignment code for side and right.</summary>
    private const int SideRight = 9;

    /// <summary>The assignment code for mid and side.</summary>
    private const int MidSide = 10;

    /// <summary>The most coefficients a linear predictor has.</summary>
    private const int MostCoefficients = 32;

    /// <summary>The most channels a stream has.</summary>
    private const int MostChannels = 8;

    /// <summary>The bytes of the check at the end of a frame.</summary>
    private const int FooterBytes = 2;

    /// <summary>The stream information.</summary>
    private readonly FlacStreamInfo _info;

    /// <summary>The decoded samples of each channel, with <see cref="FlacPredictors.Padding"/> zero values in front.</summary>
    private readonly int[][] _planes = new int[MostChannels][];

    /// <summary>The weights of a linear predictor.</summary>
    private readonly int[] _coefficients = new int[MostCoefficients];

    /// <summary>The samples each plane holds.</summary>
    private int _capacity;

    /// <summary>Initializes a new instance of the <see cref="FlacDecoder"/> class.</summary>
    /// <param name="info">The stream information.</param>
    private FlacDecoder(FlacStreamInfo info)
        : base(info.SampleRate, info.Channels) => _info = info;

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The FLAC track, whose configuration is the 34 bytes of the stream information block.</param>
    /// <returns>The decoder, or <see langword="null"/> when the track's information is missing or the sound is too wide for this decoder.</returns>
    public static FlacDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var info = FlacStreamInfo.Parse(track.Configuration.Span);
        return info is { BitsPerSample: <= MostBits, Channels: <= MostChannels } ? new(info) : null;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        // A frame is decoded on its own; nothing carries over.
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var data = packet.Data;
        if (!FlacFrameHeader.TryParse(data, _info, out var header) || data.Length < header.Length + FooterBytes || header.Channels != Channels)
        {
            throw new InvalidDataException("The FLAC frame header is not valid.");
        }

        Ensure(header.BlockSize);
        var reader = new MsbBitReader(data[header.Length..^FooterBytes]);
        for (var channel = 0; channel < header.Channels; channel++)
        {
            var bits = header.BitsPerSample + (IsSide(header.Assignment, channel) ? 1 : 0);
            if (!ReadSubframe(ref reader, _planes[channel], header.BlockSize, bits))
            {
                throw new InvalidDataException("A FLAC subframe is not valid.");
            }
        }

        Decorrelate(header);
        Interleave(header, output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing is held that needs to be freed.
    }

    /// <summary>Tells whether a channel carries the difference of the two sides, which needs one more bit.</summary>
    /// <param name="assignment">The channel assignment of the frame.</param>
    /// <param name="channel">The channel's index.</param>
    /// <returns>Whether the channel is the side channel.</returns>
    private static bool IsSide(int assignment, int channel) =>
        assignment switch
        {
            LeftSide or MidSide => channel == 1,
            SideRight => channel == 0,
            _ => false,
        };

    /// <summary>Reads a subframe's header and picks the way it is coded.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of the samples in this channel.</param>
    /// <returns><see langword="false"/> when the subframe is not valid.</returns>
    private bool ReadSubframe(ref MsbBitReader reader, int[] plane, int blockSize, int bits)
    {
        if (reader.ReadBit())
        {
            return false;
        }

        var type = reader.Read(TypeBits);
        var wasted = reader.ReadBit() ? reader.ReadUnary() + 1 : 0;
        if (wasted >= bits)
        {
            return false;
        }

        var coded = bits - wasted;
        var ok = type switch
        {
            ConstantType => FlacSubframes.ReadConstant(ref reader, plane, blockSize, coded),
            VerbatimType => FlacSubframes.ReadVerbatim(ref reader, plane, blockSize, coded),
            >= FixedFirst and <= FixedLast => FlacSubframes.ReadFixed(ref reader, plane, blockSize, coded, (int)(type - FixedFirst)),
            >= LinearFirst => FlacSubframes.ReadLinear(ref reader, plane, blockSize, coded, _coefficients, (int)(type & LinearOrderMask) + 1),
            _ => false,
        };
        if (ok && wasted > 0)
        {
            FlacPredictors.RestoreWastedBits(plane, blockSize, wasted);
        }

        return ok && !reader.IsOverrun;
    }

    /// <summary>Undoes the stereo coding, so each plane holds one channel's samples.</summary>
    /// <param name="header">The frame header.</param>
    private void Decorrelate(in FlacFrameHeader header)
    {
        if (header.Assignment is not (LeftSide or SideRight or MidSide))
        {
            return;
        }

        var first = _planes[0].AsSpan(FlacPredictors.Padding, header.BlockSize);
        var second = _planes[1].AsSpan(FlacPredictors.Padding, header.BlockSize);
        switch (header.Assignment)
        {
            case LeftSide:
            {
                for (var index = 0; index < first.Length; index++)
                {
                    second[index] = first[index] - second[index];
                }

                break;
            }

            case SideRight:
            {
                for (var index = 0; index < first.Length; index++)
                {
                    first[index] += second[index];
                }

                break;
            }

            default:
            {
                for (var index = 0; index < first.Length; index++)
                {
                    // The mid channel dropped the lowest bit of the sum; it is the lowest bit of the side channel.
                    var mid = (first[index] << 1) | (second[index] & 1);
                    var side = second[index];
                    first[index] = (mid + side) >> 1;
                    second[index] = (mid - side) >> 1;
                }

                break;
            }
        }
    }

    /// <summary>Converts the planes to floats between -1 and 1 and appends them, interleaved by channel.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="output">Receives the samples.</param>
    private void Interleave(in FlacFrameHeader header, PcmBuffer output)
    {
        var channels = header.Channels;
        var scale = 1F / (1L << (header.BitsPerSample - 1));
        var samples = output.Append(header.BlockSize * channels);
        for (var channel = 0; channel < channels; channel++)
        {
            var plane = _planes[channel].AsSpan(FlacPredictors.Padding, header.BlockSize);
            for (var index = 0; index < plane.Length; index++)
            {
                samples[(index * channels) + channel] = plane[index] * scale;
            }
        }
    }

    /// <summary>Makes sure every plane can hold a block.</summary>
    /// <param name="blockSize">The samples in the block.</param>
    private void Ensure(int blockSize)
    {
        if (_capacity >= blockSize)
        {
            return;
        }

        var size = BitOperations.RoundUpToPowerOf2((uint)blockSize);
        for (var channel = 0; channel < _planes.Length; channel++)
        {
            _planes[channel] = new int[FlacPredictors.Padding + (int)size];
        }

        _capacity = (int)size;
    }
}
