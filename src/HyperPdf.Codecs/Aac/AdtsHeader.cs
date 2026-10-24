// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Aac;

/// <summary>What an ADTS frame header says.</summary>
/// <param name="ObjectType">The audio object type.</param>
/// <param name="RateIndex">The sample rate index.</param>
/// <param name="Channels">The channel configuration.</param>
/// <param name="FrameLength">The bytes of the frame including its header.</param>
/// <param name="HeaderLength">The bytes of the header, with its checksum if it has one.</param>
internal readonly record struct AdtsHeader(int ObjectType, int RateIndex, int Channels, int FrameLength, int HeaderLength)
{
    /// <summary>The bytes of a header without a checksum.</summary>
    internal const int BaseSize = 7;

    /// <summary>The bytes of the checksum.</summary>
    private const int ChecksumSize = 2;

    /// <summary>The first byte of every header.</summary>
    private const int SyncByte = 0xFF;

    /// <summary>The mask of the second byte that keeps the sync bits and the layer.</summary>
    private const int SecondMask = 0xF6;

    /// <summary>The value of the second byte under its mask.</summary>
    private const int SecondValue = 0xF0;

    /// <summary>The bit of the second byte that says there is no checksum.</summary>
    private const int NoChecksumBit = 1;

    /// <summary>The shift of the object type in the third byte.</summary>
    private const int ObjectShift = 6;

    /// <summary>The shift of the sample rate index in the third byte.</summary>
    private const int RateShift = 2;

    /// <summary>The mask of the sample rate index once shifted.</summary>
    private const int RateMask = 0xF;

    /// <summary>The shift of the high bit of the channels, in the third byte, into place.</summary>
    private const int ChannelHighShift = 2;

    /// <summary>The shift of the low bits of the channels, in the fourth byte.</summary>
    private const int ChannelLowShift = 6;

    /// <summary>The mask of the high bits of the frame length in the fourth byte.</summary>
    private const int LengthHighMask = 3;

    /// <summary>The shift of the high bits of the frame length.</summary>
    private const int LengthHighShift = 11;

    /// <summary>The shift of the middle bits of the frame length.</summary>
    private const int LengthMiddleShift = 3;

    /// <summary>The shift of the low bits of the frame length, in the sixth byte.</summary>
    private const int LengthLowShift = 5;

    /// <summary>The mask of the count of raw blocks in the seventh byte.</summary>
    private const int BlockMask = 3;

    /// <summary>The index of the third byte.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth byte.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the fifth byte.</summary>
    private const int Fifth = 4;

    /// <summary>The index of the sixth byte.</summary>
    private const int Sixth = 5;

    /// <summary>The index of the seventh byte.</summary>
    private const int Seventh = 6;

    /// <summary>Reads a header.</summary>
    /// <param name="bytes">The bytes at the start of a frame.</param>
    /// <param name="header">Receives the header.</param>
    /// <returns><see langword="true"/> when the bytes are a header with one raw block, which is all this reader plays.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> bytes, out AdtsHeader header)
    {
        header = default;
        if (bytes.Length < BaseSize || bytes[0] != SyncByte || (bytes[1] & SecondMask) != SecondValue || (bytes[Seventh] & BlockMask) != 0)
        {
            return false;
        }

        var length = ((bytes[Fourth] & LengthHighMask) << LengthHighShift) | (bytes[Fifth] << LengthMiddleShift) | (bytes[Sixth] >> LengthLowShift);
        var size = (bytes[1] & NoChecksumBit) != 0 ? BaseSize : BaseSize + ChecksumSize;
        if (length < size)
        {
            return false;
        }

        header = new(
            (bytes[Third] >> ObjectShift) + 1,
            (bytes[Third] >> RateShift) & RateMask,
            ((bytes[Third] & 1) << ChannelHighShift) | (bytes[Fourth] >> ChannelLowShift),
            length,
            size);
        return true;
    }
}
