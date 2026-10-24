// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Flac;

/// <summary>The STREAMINFO block at the start of every FLAC stream: how the sound is laid out and, when the encoder knew them, its length and the MD5 of the unencoded sound.</summary>
/// <param name="MinBlockSize">The smallest block, in samples for each channel.</param>
/// <param name="MaxBlockSize">The largest block, in samples for each channel.</param>
/// <param name="MinFrameSize">The smallest frame in bytes, or 0 when not known.</param>
/// <param name="MaxFrameSize">The largest frame in bytes, or 0 when not known.</param>
/// <param name="SampleRate">The samples each second for each channel.</param>
/// <param name="Channels">The number of channels, from 1 to 8.</param>
/// <param name="BitsPerSample">The bits in each sample, from 4 to 32.</param>
/// <param name="TotalSamples">The samples for each channel in the stream, or 0 when not known.</param>
[DebuggerDisplay("FlacStreamInfo: {SampleRate} Hz, {Channels} channels, {BitsPerSample} bits, {TotalSamples} samples")]
internal sealed record FlacStreamInfo(
    int MinBlockSize,
    int MaxBlockSize,
    int MinFrameSize,
    int MaxFrameSize,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long TotalSamples)
{
    /// <summary>The length of the block in bytes.</summary>
    internal const int Length = 34;

    /// <summary>The bits of a block size.</summary>
    private const int BlockSizeBits = 16;

    /// <summary>The bits of a frame size.</summary>
    private const int FrameSizeBits = 24;

    /// <summary>The bits of the sample rate.</summary>
    private const int RateBits = 20;

    /// <summary>The bits of the channel count.</summary>
    private const int ChannelBits = 3;

    /// <summary>The bits of the sample size.</summary>
    private const int SampleSizeBits = 5;

    /// <summary>The bits of the total sample count.</summary>
    private const int TotalBits = 36;

    /// <summary>The smallest block size the format allows.</summary>
    private const int SmallestBlock = 16;

    /// <summary>Reads the block.</summary>
    /// <param name="data">The 34 bytes of the block's content.</param>
    /// <returns>The stream information, or <see langword="null"/> when the block is too short or says something impossible.</returns>
    internal static FlacStreamInfo? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < Length)
        {
            return null;
        }

        var reader = new MsbBitReader(data);
        var minBlock = (int)reader.Read(BlockSizeBits);
        var maxBlock = (int)reader.Read(BlockSizeBits);
        var minFrame = (int)reader.Read(FrameSizeBits);
        var maxFrame = (int)reader.Read(FrameSizeBits);
        var rate = (int)reader.Read(RateBits);
        var channels = (int)reader.Read(ChannelBits) + 1;
        var bits = (int)reader.Read(SampleSizeBits) + 1;
        var total = (long)reader.ReadLong(TotalBits);
        var valid = rate > 0 && minBlock >= SmallestBlock && maxBlock >= minBlock;
        return valid ? new(minBlock, maxBlock, minFrame, maxFrame, rate, channels, bits, total) : null;
    }
}
