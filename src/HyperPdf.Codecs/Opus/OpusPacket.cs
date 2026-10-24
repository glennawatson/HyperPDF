// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Splits an Opus packet into its frames, following the packet structure of RFC 6716 section 3.</summary>
internal static class OpusPacket
{
    /// <summary>The most frames a packet may hold.</summary>
    internal const int MaxFrames = 48;

    /// <summary>The longest frame in bytes.</summary>
    private const int MaxFrameBytes = 1_275;

    /// <summary>The first length byte value that says a second byte follows.</summary>
    private const int TwoByteLength = 252;

    /// <summary>The most samples at 48 kHz a packet may hold: 120 ms.</summary>
    private const int MaxPacketSamples = 5_760;

    /// <summary>The bit of the first byte after the table of contents that says frame lengths are coded.</summary>
    private const int VariableBit = 0x80;

    /// <summary>The bit that says padding follows the frames.</summary>
    private const int PaddingBit = 0x40;

    /// <summary>The mask of the frame count.</summary>
    private const int CountMask = 0x3F;

    /// <summary>The bits the configuration number is shifted up by in the table of contents.</summary>
    private const int ConfigShift = 3;

    /// <summary>The bit of the table of contents that says the frames are stereo.</summary>
    private const int StereoBit = 0x04;

    /// <summary>The mask of the frame packing code.</summary>
    private const int CodeMask = 0x03;

    /// <summary>The largest padding length a byte of 255 adds up to.</summary>
    private const int PaddingStep = 254;

    /// <summary>The padding byte value that says another padding byte follows.</summary>
    private const int PaddingContinues = 255;

    /// <summary>The frame packing code for a single frame.</summary>
    private const int OneFrame = 0;

    /// <summary>The frame packing code for two frames of equal size.</summary>
    private const int TwoEqualFrames = 1;

    /// <summary>The frame packing code for two frames of different sizes.</summary>
    private const int TwoUnequalFrames = 2;

    /// <summary>The bytes of the table of contents, which come before the frames.</summary>
    private const int HeaderBytes = 1;

    /// <summary>Two, the frames of packing codes 1 and 2 and the bytes of a long coded length.</summary>
    private const int TwoFrames = 2;

    /// <summary>The weight of the second byte of a long coded length.</summary>
    private const int LengthStep = 4;

    /// <summary>The samples at 48 kHz in a frame of 2.5 ms.</summary>
    private const int ShortestFrame = 120;

    /// <summary>The samples at 48 kHz in a frame of 10 ms.</summary>
    private const int TenMs = 480;

    /// <summary>The samples at 48 kHz in a frame of 20 ms.</summary>
    private const int TwentyMs = 960;

    /// <summary>The samples at 48 kHz in a frame of 40 ms.</summary>
    private const int FortyMs = 1_920;

    /// <summary>The samples at 48 kHz in a frame of 60 ms.</summary>
    private const int SixtyMs = 2_880;

    /// <summary>The first configuration number of the hybrid modes.</summary>
    private const int FirstHybrid = 12;

    /// <summary>The first hybrid configuration number that is full band.</summary>
    private const int FirstHybridFullBand = 14;

    /// <summary>The first configuration number of the CELT-only modes.</summary>
    private const int FirstCelt = 16;

    /// <summary>The shift that groups configuration numbers by bandwidth.</summary>
    private const int BandwidthShift = 2;

    /// <summary>Splits a packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="frames">Receives the frames; at least <see cref="MaxFrames"/> long.</param>
    /// <param name="info">Receives what the table of contents says.</param>
    /// <returns><see langword="true"/> when the packet is well formed.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> packet, Span<OpusFrame> frames, out OpusPacketInfo info)
    {
        info = default;
        if (packet.IsEmpty)
        {
            return false;
        }

        var table = packet[0];
        var config = table >> ConfigShift;
        var frameSamples = GetFrameSamples(config);
        var body = packet[HeaderBytes..];
        int count;
        var padding = 0;
        switch (table & CodeMask)
        {
            case OneFrame:
            {
                count = 1;
                frames[0] = new(HeaderBytes, body.Length);
                break;
            }

            case TwoEqualFrames:
            {
                count = SplitEqual(body, HeaderBytes, TwoFrames, frames);
                break;
            }

            case TwoUnequalFrames:
            {
                count = SplitUnequal(body, HeaderBytes, frames);
                break;
            }

            default:
            {
                count = SplitArbitrary(body, HeaderBytes, frames, out padding);
                break;
            }
        }

        if (count <= 0 || count * frameSamples > MaxPacketSamples || !FramesFit(frames[..count], packet.Length - padding))
        {
            return false;
        }

        info = new(GetMode(config), GetBandwidth(config), frameSamples, (table & StereoBit) != 0, count);
        return true;
    }

    /// <summary>Gets the length of a frame in samples at 48 kHz.</summary>
    /// <param name="config">The configuration number from the table of contents.</param>
    /// <returns>The samples.</returns>
    private static int GetFrameSamples(int config)
    {
        // SILK frames are 10, 20, 40 or 60 ms, hybrid frames 10 or 20 ms, and CELT frames 2.5, 5, 10 or 20 ms.
        if (config < FirstHybrid)
        {
            ReadOnlySpan<int> silk = [TenMs, TwentyMs, FortyMs, SixtyMs];
            return silk[config & CodeMask];
        }

        return config < FirstCelt ? TenMs << (config & 1) : ShortestFrame << (config & CodeMask);
    }

    /// <summary>Gets the coding mode a configuration number names.</summary>
    /// <param name="config">The configuration number.</param>
    /// <returns>The mode.</returns>
    private static OpusMode GetMode(int config)
    {
        if (config < FirstHybrid)
        {
            return OpusMode.Silk;
        }

        return config < FirstCelt ? OpusMode.Hybrid : OpusMode.Celt;
    }

    /// <summary>Gets the audio bandwidth a configuration number names.</summary>
    /// <param name="config">The configuration number.</param>
    /// <returns>The bandwidth.</returns>
    private static OpusBandwidth GetBandwidth(int config)
    {
        if (config < FirstHybrid)
        {
            return (OpusBandwidth)(config >> BandwidthShift);
        }

        if (config < FirstCelt)
        {
            return config < FirstHybridFullBand ? OpusBandwidth.SuperWide : OpusBandwidth.Full;
        }

        // CELT has no medium band, so its four groups are narrow, wide, super wide and full.
        var celt = (config - FirstCelt) >> BandwidthShift;
        return celt == 0 ? OpusBandwidth.Narrow : (OpusBandwidth)(celt + 1);
    }

    /// <summary>Splits the body into frames of equal size.</summary>
    /// <param name="body">The bytes after the table of contents.</param>
    /// <param name="offset">The offset of the body in the packet.</param>
    /// <param name="count">The frames.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <returns>The frame count, or -1 when the body does not divide.</returns>
    private static int SplitEqual(ReadOnlySpan<byte> body, int offset, int count, Span<OpusFrame> frames)
    {
        if (body.Length % count != 0)
        {
            return -1;
        }

        var size = body.Length / count;
        for (var i = 0; i < count; i++)
        {
            frames[i] = new(offset + (i * size), size);
        }

        return count;
    }

    /// <summary>Splits the body into two frames, the first with a coded length.</summary>
    /// <param name="body">The bytes after the table of contents.</param>
    /// <param name="offset">The offset of the body in the packet.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <returns>Two, or -1 when the lengths do not fit.</returns>
    private static int SplitUnequal(ReadOnlySpan<byte> body, int offset, Span<OpusFrame> frames)
    {
        var used = ReadLength(body, out var first);
        if (used < 0 || first > body.Length - used)
        {
            return -1;
        }

        frames[0] = new(offset + used, first);
        frames[1] = new(offset + used + first, body.Length - used - first);
        return TwoFrames;
    }

    /// <summary>Splits a body that has the arbitrary count header.</summary>
    /// <param name="body">The bytes after the table of contents.</param>
    /// <param name="offset">The offset of the body in the packet.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <param name="padding">Receives the padding bytes at the end of the packet.</param>
    /// <returns>The frame count, or -1 when the header is damaged.</returns>
    private static int SplitArbitrary(ReadOnlySpan<byte> body, int offset, Span<OpusFrame> frames, out int padding)
    {
        padding = 0;
        if (body.IsEmpty)
        {
            return -1;
        }

        var header = body[0];
        var count = header & CountMask;
        var position = 1;
        if (count == 0 || count > MaxFrames)
        {
            return -1;
        }

        if ((header & PaddingBit) != 0)
        {
            padding = ReadPadding(body, ref position);
            if (padding < 0)
            {
                return -1;
            }
        }

        var end = body.Length - padding;
        return (header & VariableBit) != 0 ? SplitVariable(body[..end], offset, position, count, frames) : SplitEqual(body[position..end], offset + position, count, frames);
    }

    /// <summary>Splits frames whose lengths are coded, all but the last.</summary>
    /// <param name="body">The body up to the padding.</param>
    /// <param name="offset">The offset of the body in the packet.</param>
    /// <param name="position">The position in the body after the header.</param>
    /// <param name="count">The frames.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <returns>The frame count, or -1.</returns>
    private static int SplitVariable(ReadOnlySpan<byte> body, int offset, int position, int count, Span<OpusFrame> frames)
    {
        Span<int> sizes = stackalloc int[MaxFrames];
        var total = 0;
        for (var i = 0; i < count - 1; i++)
        {
            var used = ReadLength(body[position..], out sizes[i]);
            if (used < 0)
            {
                return -1;
            }

            position += used;
            total += sizes[i];
        }

        var last = body.Length - position - total;
        if (last < 0)
        {
            return -1;
        }

        sizes[count - 1] = last;
        var start = offset + position;
        for (var i = 0; i < count; i++)
        {
            frames[i] = new(start, sizes[i]);
            start += sizes[i];
        }

        return count;
    }

    /// <summary>Reads the length of the padding.</summary>
    /// <param name="body">The body.</param>
    /// <param name="position">The position of the padding length; moved past it.</param>
    /// <returns>The padding bytes, or -1 when the length runs off the end.</returns>
    private static int ReadPadding(ReadOnlySpan<byte> body, ref int position)
    {
        var padding = 0;
        while (true)
        {
            if (position >= body.Length)
            {
                return -1;
            }

            var step = body[position];
            position++;
            if (step != PaddingContinues)
            {
                return padding + step;
            }

            padding += PaddingStep;
        }
    }

    /// <summary>Reads a frame length of one or two bytes.</summary>
    /// <param name="data">The bytes holding the length.</param>
    /// <param name="length">Receives the length.</param>
    /// <returns>The bytes the length took, or -1 when there are too few.</returns>
    private static int ReadLength(ReadOnlySpan<byte> data, out int length)
    {
        length = 0;
        if (data.IsEmpty)
        {
            return -1;
        }

        if (data[0] < TwoByteLength)
        {
            length = data[0];
            return 1;
        }

        if (data.Length < TwoFrames)
        {
            return -1;
        }

        length = (data[1] * LengthStep) + data[0];
        return TwoFrames;
    }

    /// <summary>Checks that every frame is within the size limit and inside the packet.</summary>
    /// <param name="frames">The frames.</param>
    /// <param name="end">The end of the data frames may use.</param>
    /// <returns><see langword="true"/> when they all fit.</returns>
    private static bool FramesFit(ReadOnlySpan<OpusFrame> frames, int end)
    {
        foreach (var frame in frames)
        {
            if (frame.Length > MaxFrameBytes || frame.Offset + frame.Length > end)
            {
                return false;
            }
        }

        return true;
    }
}
