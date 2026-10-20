// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.MacOS;

/// <summary>Native <c>AudioStreamBasicDescription</c>.</summary>
/// <param name="SampleRate">The sample rate.</param>
/// <param name="FormatId">The format, <c>'lpcm'</c>.</param>
/// <param name="FormatFlags">The format flags.</param>
/// <param name="BytesPerPacket">The bytes per packet.</param>
/// <param name="FramesPerPacket">The frames per packet.</param>
/// <param name="BytesPerFrame">The bytes per frame.</param>
/// <param name="ChannelsPerFrame">The channels.</param>
/// <param name="BitsPerChannel">The bits per sample.</param>
/// <param name="Reserved">Zero.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct StreamDescription(
    double SampleRate,
    uint FormatId,
    uint FormatFlags,
    uint BytesPerPacket,
    uint FramesPerPacket,
    uint BytesPerFrame,
    uint ChannelsPerFrame,
    uint BitsPerChannel,
    uint Reserved)
{
    /// <summary>Linear PCM, <c>'lpcm'</c>.</summary>
    private const uint LinearPcm = 0x6C70636D;

    /// <summary>Float samples, packed: kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked.</summary>
    private const uint PackedFloat = 0x1 | 0x8;

    /// <summary>The bits in a float sample.</summary>
    private const uint FloatBits = 32;

    /// <summary>Describes interleaved 32 bit float samples.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <param name="channels">The number of channels.</param>
    /// <returns>The description.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static StreamDescription Float(int rate, int channels) =>
        new(rate, LinearPcm, PackedFloat, (uint)(sizeof(float) * channels), 1, (uint)(sizeof(float) * channels), (uint)channels, FloatBits, 0);

    /// <summary>Describes mono 32 bit float samples.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <returns>The description.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static StreamDescription MonoFloat(int rate) => new(rate, LinearPcm, PackedFloat, sizeof(float), 1, sizeof(float), 1, FloatBits, 0);
}
