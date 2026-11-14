// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>A premultiplied colour as four floats.</summary>
/// <param name="Red">The red, already multiplied by alpha.</param>
/// <param name="Green">The green, already multiplied by alpha.</param>
/// <param name="Blue">The blue, already multiplied by alpha.</param>
/// <param name="Alpha">The alpha.</param>
[DebuggerDisplay("VulkanColor ({Red}, {Green}, {Blue}, {Alpha})")]
internal readonly record struct VulkanColor(float Red, float Green, float Blue, float Alpha)
{
    /// <summary>The largest channel value.</summary>
    private const float Max = 255F;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>Converts a 0xAARRGGBB colour to premultiplied floats.</summary>
    /// <param name="argb">The colour.</param>
    /// <returns>The premultiplied colour.</returns>
    internal static VulkanColor FromArgb(uint argb)
    {
        var alpha = ((argb >> AlphaShift) & ChannelMask) / Max;
        return new(((argb >> RedShift) & ChannelMask) / Max * alpha, ((argb >> GreenShift) & ChannelMask) / Max * alpha, (argb & ChannelMask) / Max * alpha, alpha);
    }
}
