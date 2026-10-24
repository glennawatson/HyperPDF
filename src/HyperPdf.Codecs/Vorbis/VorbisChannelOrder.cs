// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Vorbis;

/// <summary>Maps the channel order Vorbis uses to the usual file order: left, right, centre, low frequency, then the surrounds.</summary>
internal static class VorbisChannelOrder
{
    /// <summary>The layout of three channels.</summary>
    private const int ThreeChannels = 3;

    /// <summary>The layout of five channels.</summary>
    private const int FiveChannels = 5;

    /// <summary>The layout of six channels.</summary>
    private const int SixChannels = 6;

    /// <summary>The layout of seven channels.</summary>
    private const int SevenChannels = 7;

    /// <summary>The layout of eight channels.</summary>
    private const int EightChannels = 8;

    /// <summary>Gets the output position of each Vorbis channel in a layout of three channels.</summary>
    private static ReadOnlySpan<byte> Three => [0x00, 0x02, 0x01];

    /// <summary>Gets the output position of each Vorbis channel in a layout of five channels.</summary>
    private static ReadOnlySpan<byte> Five => [0x00, 0x02, 0x01, 0x03, 0x04];

    /// <summary>Gets the output position of each Vorbis channel in a layout of six channels.</summary>
    private static ReadOnlySpan<byte> Six => [0x00, 0x02, 0x01, 0x04, 0x05, 0x03];

    /// <summary>Gets the output position of each Vorbis channel in a layout of seven channels.</summary>
    private static ReadOnlySpan<byte> Seven => [0x00, 0x02, 0x01, 0x05, 0x06, 0x04, 0x03];

    /// <summary>Gets the output position of each Vorbis channel in a layout of eight channels.</summary>
    private static ReadOnlySpan<byte> Eight => [0x00, 0x02, 0x01, 0x06, 0x07, 0x04, 0x05, 0x03];

    /// <summary>Finds where a channel goes in the output.</summary>
    /// <param name="channels">The number of channels.</param>
    /// <param name="channel">The channel in Vorbis order.</param>
    /// <returns>The channel's position in the output.</returns>
    internal static int OutputSlot(int channels, int channel) => channels switch
    {
        ThreeChannels => Three[channel],
        FiveChannels => Five[channel],
        SixChannels => Six[channel],
        SevenChannels => Seven[channel],
        EightChannels => Eight[channel],
        _ => channel,
    };
}
