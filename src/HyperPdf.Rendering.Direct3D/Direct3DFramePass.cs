// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>Collects the drawing of one frame and runs it on the GPU when disposed. The backend keeps one pass and hands it out for every frame.</summary>
/// <param name="owner">The backend that owns the GPU objects.</param>
internal sealed class Direct3DFramePass(Direct3DRenderBackend owner) : IFramePass
{
    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint argb) => owner.Quads.AddClear(argb);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRect(in LayoutRect area, uint argb) => owner.Quads.AddQuad(null, smooth: false, area, argb);

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not Direct3DTileSurface tile)
        {
            throw new ArgumentException("The surface was not made by the Direct3D backend.", nameof(surface));
        }

        owner.Quads.AddQuad(tile, smooth, destination, uint.MaxValue);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => owner.Present();
}
