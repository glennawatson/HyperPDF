// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Collects the drawing of one frame and runs it on the GPU when disposed.</summary>
/// <param name="owner">The backend that owns the GPU objects.</param>
internal sealed class VulkanFramePass(VulkanRenderBackend owner) : IFramePass
{
    /// <summary>The alpha of a colour that is fully opaque.</summary>
    private const uint OpaqueAlpha = 255;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The mask of the red, green and blue bits of a colour.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>How much what is drawn counts, out of 255.</summary>
    private uint _opacity = OpaqueAlpha;

    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint argb) => owner.Record(new(VulkanDrawKind.Clear, null, 0, 0, 0, 0, VulkanColor.FromArgb(argb), false));

    /// <inheritdoc/>
    public void FillRect(in LayoutRect area, uint argb)
    {
        var alpha = (uint)PremultipliedColor.DivideBy255((int)((argb >> AlphaShift) * _opacity));
        var faded = (argb & ColorMask) | (alpha << AlphaShift);
        owner.Record(new(VulkanDrawKind.Fill, null, (float)area.X, (float)area.Y, (float)area.Width, (float)area.Height, VulkanColor.FromArgb(faded), false));
    }

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not VulkanTileSurface tile)
        {
            throw new ArgumentException("The surface was not made by the Vulkan backend.", nameof(surface));
        }

        // The tile shader multiplies the tile by this colour: white at the layer's alpha, which is the premultiplied form of that alpha for every channel.
        var tint = VulkanColor.FromArgb((_opacity << AlphaShift) | ColorMask);
        owner.Record(new(VulkanDrawKind.Tile, tile, (float)destination.X, (float)destination.Y, (float)destination.Width, (float)destination.Height, tint, smooth));
    }

    /// <inheritdoc/>
    public void SetOpacity(double opacity) => _opacity = (uint)Math.Round(Math.Clamp(opacity, 0, 1) * OpaqueAlpha);

    /// <inheritdoc/>
    public void SetClip(in LayoutRect area)
    {
        var size = owner.Size;
        var pixels = PixelClip.Of(area, size.Width, size.Height);
        owner.Record(new(VulkanDrawKind.Clip, null, pixels.Left, pixels.Top, pixels.Width, pixels.Height, default, false));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearClip() => owner.Record(new(VulkanDrawKind.Unclip, null, 0, 0, 0, 0, default, false));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // The pass is handed out again for the next frame, which starts opaque.
        _opacity = OpaqueAlpha;
        owner.Present();
    }
}
