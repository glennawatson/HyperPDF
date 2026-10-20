// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows.Audio;

/// <summary>Native <c>WAVEFORMATEX</c>.</summary>
/// <param name="FormatTag">The format, <c>WAVE_FORMAT_IEEE_FLOAT</c>.</param>
/// <param name="Channels">The channel count.</param>
/// <param name="SamplesPerSecond">The sample rate.</param>
/// <param name="AverageBytesPerSecond">The bytes per second.</param>
/// <param name="BlockAlign">The bytes per frame.</param>
/// <param name="BitsPerSample">The bits per sample.</param>
/// <param name="ExtraSize">Extra bytes after the structure, zero.</param>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal readonly record struct WaveFormat(ushort FormatTag, ushort Channels, uint SamplesPerSecond, uint AverageBytesPerSecond, ushort BlockAlign, ushort BitsPerSample, ushort ExtraSize)
{
    /// <summary>The IEEE float format tag.</summary>
    private const ushort IeeeFloat = 3;

    /// <summary>The bits in a float sample.</summary>
    private const ushort FloatBits = 32;

    /// <summary>Describes interleaved 32 bit float samples.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <param name="channels">The number of channels.</param>
    /// <returns>The format.</returns>
    internal static WaveFormat Float(int rate, int channels) =>
        new(IeeeFloat, (ushort)channels, (uint)rate, (uint)(rate * channels * sizeof(float)), (ushort)(channels * sizeof(float)), FloatBits, 0);

    /// <summary>Describes mono 32 bit float samples.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <returns>The format.</returns>
    internal static WaveFormat MonoFloat(int rate) => new(IeeeFloat, 1, (uint)rate, (uint)(rate * sizeof(float)), sizeof(float), FloatBits, 0);
}
