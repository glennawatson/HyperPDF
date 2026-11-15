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
    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint argb) => owner.Record(new(VulkanDrawKind.Clear, null, 0, 0, 0, 0, VulkanColor.FromArgb(argb), false));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRect(in LayoutRect area, uint argb) =>
        owner.Record(new(VulkanDrawKind.Fill, null, (float)area.X, (float)area.Y, (float)area.Width, (float)area.Height, VulkanColor.FromArgb(argb), false));

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not VulkanTileSurface tile)
        {
            throw new ArgumentException("The surface was not made by the Vulkan backend.", nameof(surface));
        }

        owner.Record(new(VulkanDrawKind.Tile, tile, (float)destination.X, (float)destination.Y, (float)destination.Width, (float)destination.Height, default, smooth));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => owner.Present();
}
