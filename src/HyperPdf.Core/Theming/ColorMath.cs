// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Theming;

/// <summary>Colour arithmetic and WCAG contrast for 0xRRGGBB values.</summary>
public static class ColorMath
{
    /// <summary>The mask of the colour bits.</summary>
    internal const uint RgbMask = 0xFFFFFFU;

    /// <summary>The maximum channel value.</summary>
    private const double ChannelMax = 255.0;

    /// <summary>The bit offset of red.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green.</summary>
    private const int GreenShift = 8;

    /// <summary>The channel mask.</summary>
    private const uint ChannelMask = 0xFFU;

    /// <summary>The sRGB linear segment threshold.</summary>
    private const double LinearThreshold = 0.04045;

    /// <summary>The sRGB linear segment divisor.</summary>
    private const double LinearDivisor = 12.92;

    /// <summary>The sRGB gamma offset.</summary>
    private const double GammaOffset = 0.055;

    /// <summary>The sRGB gamma divisor.</summary>
    private const double GammaDivisor = 1.055;

    /// <summary>The sRGB gamma exponent.</summary>
    private const double GammaExponent = 2.4;

    /// <summary>The WCAG luminance weight of red.</summary>
    private const double RedWeight = 0.2126;

    /// <summary>The WCAG luminance weight of green.</summary>
    private const double GreenWeight = 0.7152;

    /// <summary>The WCAG luminance weight of blue.</summary>
    private const double BlueWeight = 0.0722;

    /// <summary>The WCAG flare term.</summary>
    private const double Flare = 0.05;

    /// <summary>Computes the WCAG relative luminance.</summary>
    /// <param name="rgb">The colour.</param>
    /// <returns>A value from 0 to 1.</returns>
    public static double Luminance(uint rgb) =>
        (RedWeight * Linear((rgb >> RedShift) & ChannelMask)) + (GreenWeight * Linear((rgb >> GreenShift) & ChannelMask)) + (BlueWeight * Linear(rgb & ChannelMask));

    /// <summary>Computes the WCAG contrast ratio between two colours.</summary>
    /// <param name="first">The first colour.</param>
    /// <param name="second">The second colour.</param>
    /// <returns>A ratio from 1 to 21.</returns>
    public static double Contrast(uint first, uint second)
    {
        var a = Luminance(first) + Flare;
        var b = Luminance(second) + Flare;
        return a > b ? a / b : b / a;
    }

    /// <summary>Darkens a colour.</summary>
    /// <param name="rgb">The colour.</param>
    /// <param name="amount">The fraction to darken by.</param>
    /// <returns>The darker colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Shade(uint rgb, double amount) => Mix(0, rgb, amount);

    /// <summary>Mixes two colours.</summary>
    /// <param name="first">The colour weighted by <paramref name="amount"/>.</param>
    /// <param name="second">The other colour.</param>
    /// <param name="amount">The weight of <paramref name="first"/>, from 0 to 1.</param>
    /// <returns>The mix.</returns>
    public static uint Mix(uint first, uint second, double amount) =>
        (MixChannel(first, second, RedShift, amount) << RedShift) | (MixChannel(first, second, GreenShift, amount) << GreenShift) | MixChannel(first, second, 0, amount);

    /// <summary>Converts an sRGB channel to linear light.</summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The linear value.</returns>
    private static double Linear(uint channel)
    {
        var value = channel / ChannelMax;
        return value <= LinearThreshold ? value / LinearDivisor : Math.Pow((value + GammaOffset) / GammaDivisor, GammaExponent);
    }

    /// <summary>Mixes one channel.</summary>
    /// <param name="first">The first colour.</param>
    /// <param name="second">The second colour.</param>
    /// <param name="shift">The channel offset.</param>
    /// <param name="amount">The weight of the first colour.</param>
    /// <returns>The channel value.</returns>
    private static uint MixChannel(uint first, uint second, int shift, double amount) =>
        (uint)Math.Round((((first >> shift) & ChannelMask) * amount) + (((second >> shift) & ChannelMask) * (1 - amount)));
}
