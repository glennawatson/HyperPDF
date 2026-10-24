// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The filter coefficients of the resampler that brings the SILK layer's 8, 12 or 16 kHz output up to 48 kHz.</summary>
internal static class SilkResamplerTables
{
    /// <summary>Gets the coefficients of the all-pass sections that make the even samples of the 2x upsampler, in Q16.</summary>
    internal static ReadOnlySpan<short> UpsampleEven =>
    [
        0x06D2, 0x3A8A, -0x6755,
    ];

    /// <summary>Gets the coefficients of the all-pass sections that make the odd samples of the 2x upsampler, in Q16.</summary>
    internal static ReadOnlySpan<short> UpsampleOdd =>
    [
        0x1AC6, 0x64A9, -0x270A,
    ];

    /// <summary>Gets the coefficients of the polyphase interpolation filter: 12 phases of 4 taps, mirrored for the other 4.</summary>
    internal static ReadOnlySpan<short> InterpolationTaps =>
    [
        0x00BD, -0x0258, 0x0269, 0x7767,
        0x0075, -0x009F, -0x042E, 0x7408,
        0x0034, 0x00DD, -0x0958, 0x6E74,
        -0x0004, 0x0211, -0x0D16, 0x66E5,
        -0x0030, 0x02F6, -0x0F74, 0x5DA5,
        -0x0050, 0x0389, -0x108B, 0x5306,
        -0x0063, 0x03CC, -0x107E, 0x4766,
        -0x006B, 0x03C7, -0x0F75, 0x3B27,
        -0x0067, 0x0380, -0x0D9F, 0x2EAE,
        -0x005B, 0x0305, -0x0B31, 0x225E,
        -0x0047, 0x0263, -0x085F, 0x1698,
        -0x002E, 0x01A9, -0x055F, 0x0BB4,
    ];
}
