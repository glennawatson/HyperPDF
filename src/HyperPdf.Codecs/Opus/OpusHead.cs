// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Opus;

/// <summary>The identification header of an Opus stream in Ogg (RFC 7845 section 5.1).</summary>
/// <param name="Channels">The channels in the output.</param>
/// <param name="PreSkip">The samples at 48 kHz to drop from the start of the decoded sound.</param>
/// <param name="OutputGain">The gain to apply to the output, in 1/256 of a decibel.</param>
/// <param name="MappingFamily">0 for mono or stereo, 1 for up to eight channels in Vorbis order, or 255 for no defined order.</param>
/// <param name="StreamCount">The Opus streams the packet holds; one for mapping family 0.</param>
/// <param name="CoupledCount">How many of those streams are stereo.</param>
/// <param name="ChannelMapping">For each output channel, the decoded channel it takes, or 255 for silence.</param>
/// <param name="Packet">The header packet as it was read, which is the decoder's configuration.</param>
internal sealed record OpusHead(int Channels, int PreSkip, int OutputGain, int MappingFamily, int StreamCount, int CoupledCount, byte[] ChannelMapping, byte[] Packet)
{
    /// <summary>The sample rate Opus decodes to, whatever the rate of the sound that was encoded.</summary>
    internal const int OutputRate = 48_000;

    /// <summary>The length of the fixed part of the header.</summary>
    private const int FixedBytes = 19;

    /// <summary>The offset of the channel count.</summary>
    private const int ChannelsAt = 9;

    /// <summary>The offset of the pre-skip.</summary>
    private const int PreSkipAt = 10;

    /// <summary>The offset of the output gain.</summary>
    private const int GainAt = 16;

    /// <summary>The offset of the mapping family.</summary>
    private const int FamilyAt = 18;

    /// <summary>The offset of the stream count when a mapping table follows.</summary>
    private const int StreamCountAt = 19;

    /// <summary>The bytes before the channel mapping table.</summary>
    private const int TableAt = 21;

    /// <summary>The largest version number this reader handles; the high four bits are the major version.</summary>
    private const int MajorMask = 0xF0;

    /// <summary>The most channels mapping family 0 allows.</summary>
    private const int MaxPlainChannels = 2;

    /// <summary>The most channels a stream may have.</summary>
    private const int MaxChannels = 255;

    /// <summary>The value in a mapping table that means silence.</summary>
    private const byte Silent = 255;

    /// <summary>Reads the header of the first packet of a stream.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="head">Receives the header.</param>
    /// <returns><see langword="true"/> when the packet is an Opus header this reader understands.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> packet, [NotNullWhen(true)] out OpusHead? head)
    {
        head = null;
        if (packet.Length < FixedBytes || !packet.StartsWith("OpusHead"u8) || (packet[8] & MajorMask) != 0)
        {
            return false;
        }

        var channels = packet[ChannelsAt];
        var family = packet[FamilyAt];
        if (channels == 0)
        {
            return false;
        }

        return family == 0 ? TryFamilyZero(packet, channels, out head) : TryMapped(packet, channels, family, out head);
    }

    /// <summary>Reads a header of mapping family 0: mono or stereo with the channels in order.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="head">Receives the header.</param>
    /// <returns><see langword="true"/> for one or two channels.</returns>
    private static bool TryFamilyZero(ReadOnlySpan<byte> packet, int channels, out OpusHead? head)
    {
        head = null;
        if (channels > MaxPlainChannels)
        {
            return false;
        }

        byte[] mapping = channels == 1 ? [0] : [0, 1];
        head = new(channels, ReadPreSkip(packet), ReadGain(packet), 0, 1, channels - 1, mapping, packet.ToArray());
        return true;
    }

    /// <summary>Reads a header that carries a channel mapping table.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="family">The mapping family.</param>
    /// <param name="head">Receives the header.</param>
    /// <returns><see langword="true"/> when the table is whole and consistent.</returns>
    private static bool TryMapped(ReadOnlySpan<byte> packet, int channels, int family, out OpusHead? head)
    {
        head = null;
        if (channels > MaxChannels || packet.Length < TableAt + channels)
        {
            return false;
        }

        int streams = packet[StreamCountAt];
        int coupled = packet[StreamCountAt + 1];
        var table = packet.Slice(TableAt, channels);
        if (streams == 0 || coupled > streams || !TableFits(table, streams + coupled))
        {
            return false;
        }

        head = new(channels, ReadPreSkip(packet), ReadGain(packet), family, streams, coupled, table.ToArray(), packet.ToArray());
        return true;
    }

    /// <summary>Checks that a mapping table names only decoded channels that exist.</summary>
    /// <param name="table">The table.</param>
    /// <param name="decodedChannels">The channels the streams decode to.</param>
    /// <returns><see langword="true"/> when every entry is a decoded channel or silence.</returns>
    private static bool TableFits(ReadOnlySpan<byte> table, int decodedChannels)
    {
        foreach (var entry in table)
        {
            if (entry != Silent && entry >= decodedChannels)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the pre-skip.</summary>
    /// <param name="packet">The header packet.</param>
    /// <returns>The samples at 48 kHz.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadPreSkip(ReadOnlySpan<byte> packet) => BinaryPrimitives.ReadUInt16LittleEndian(packet[PreSkipAt..]);

    /// <summary>Reads the output gain.</summary>
    /// <param name="packet">The header packet.</param>
    /// <returns>The gain in 1/256 of a decibel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadGain(ReadOnlySpan<byte> packet) => BinaryPrimitives.ReadInt16LittleEndian(packet[GainAt..]);
}
