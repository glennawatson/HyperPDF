// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Draws one frame into the CPU backend's buffer and presents it on dispose.</summary>
/// <param name="owner">The backend that owns the frame buffer.</param>
internal sealed class CpuFramePass(CpuRenderBackend owner) : IFramePass
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint argb) => CpuFill.Fill(owner.WritableFrame(), argb);

    /// <inheritdoc/>
    public void FillRect(in LayoutRect area, uint argb)
    {
        var size = owner.Size;
        var left = Math.Max((int)Math.Floor(area.X), 0);
        var top = Math.Max((int)Math.Floor(area.Y), 0);
        var right = Math.Min((int)Math.Ceiling(area.X + area.Width), size.Width);
        var bottom = Math.Min((int)Math.Ceiling(area.Y + area.Height), size.Height);
        var frame = owner.WritableFrame();
        for (var row = top; row < bottom && left < right; row++)
        {
            CpuFill.Fill(frame.Slice((row * owner.Stride) + (left * BytesPerPixel), (right - left) * BytesPerPixel), argb);
        }
    }

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not CpuPixelSurface cpu)
        {
            throw new ArgumentException("The surface was not made by the CPU backend.", nameof(surface));
        }

        var request = new BlitRequest(
            (int)Math.Round(destination.X),
            (int)Math.Round(destination.Y),
            (int)Math.Round(destination.Width),
            (int)Math.Round(destination.Height),
            smooth);
        CpuBlit.Draw(owner.WritableFrame(), owner.Grid, cpu.Pixels, cpu.Grid, request);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => owner.Present();
}
