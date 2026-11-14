// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Metal;

/// <summary>Collects the drawing of one frame and runs it on the GPU when disposed. The backend keeps one pass and hands it out for every frame.</summary>
/// <param name="owner">The backend that owns the GPU objects.</param>
internal sealed class MetalFramePass(MetalRenderBackend owner) : IFramePass
{
    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    public void Clear(uint argb)
    {
        // Metal clears a frame only as it starts drawing, so a clear after other drawing becomes a fill of the whole frame.
        if (owner.Quads.Batches.IsEmpty)
        {
            owner.Quads.AddClear(argb);
            return;
        }

        owner.Quads.AddQuad(null, smooth: false, new(0, 0, owner.Size.Width, owner.Size.Height), argb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRect(in LayoutRect area, uint argb) => owner.Quads.AddQuad(null, smooth: false, area, argb);

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not MetalTileSurface tile)
        {
            throw new ArgumentException("The surface was not made by the Metal backend.", nameof(surface));
        }

        owner.Quads.AddQuad(tile, smooth, destination, uint.MaxValue);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => owner.Present();
}
