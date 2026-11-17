// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Draws one frame into the CPU backend's buffer and presents it on dispose. The backend keeps one pass and hands it out for every frame, so a frame allocates nothing.</summary>
/// <param name="owner">The backend that owns the frame buffer.</param>
internal sealed class CpuFramePass(CpuRenderBackend owner) : IFramePass
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of steps a layer's weight is out of.</summary>
    private const int WeightSteps = 256;

    /// <summary>The largest weight a layer can have when it is mixed in.</summary>
    private const int MaxWeight = 255;

    /// <summary>The part of the frame drawing may reach, or <see langword="null"/> for the whole frame.</summary>
    private PixelClip? _clip;

    /// <summary>The frame as it was before the layer being drawn, or <see langword="null"/> when no layer is being drawn.</summary>
    private byte[]? _behind;

    /// <summary>How much of the layer shows, out of 256.</summary>
    private uint _layerWeight;

    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint argb) => CpuFill.Fill(owner.WritableFrame(), argb);

    /// <inheritdoc/>
    public void FillRect(in LayoutRect area, uint argb)
    {
        var size = owner.Size;
        var reach = _clip ?? new PixelClip(0, 0, size.Width, size.Height);
        var left = Math.Max((int)Math.Floor(area.X), reach.Left);
        var top = Math.Max((int)Math.Floor(area.Y), reach.Top);
        var right = Math.Min((int)Math.Ceiling(area.X + area.Width), reach.Right);
        var bottom = Math.Min((int)Math.Ceiling(area.Y + area.Height), reach.Bottom);
        var frame = owner.WritableFrame();
        for (var row = top; row < bottom && left < right; row++)
        {
            CpuFill.Over(frame.Slice((row * owner.Stride) + (left * BytesPerPixel), (right - left) * BytesPerPixel), argb);
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
            smooth,
            cpu.IsOpaque) { Clip = _clip };
        CpuBlit.Draw(owner.WritableFrame(), owner.Grid, cpu.Pixels, cpu.Grid, request);
    }

    /// <inheritdoc/>
    public void SetClip(in LayoutRect area)
    {
        var size = owner.Size;
        _clip = PixelClip.Of(area, size.Width, size.Height);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearClip() => _clip = null;

    /// <inheritdoc/>
    public void SetOpacity(double opacity)
    {
        // What was drawn at the opacity before is mixed in first, then the frame is kept so what comes next can be mixed with it.
        EndLayer();
        if (opacity >= 1)
        {
            return;
        }

        var frame = owner.WritableFrame();
        _behind = ArrayPool<byte>.Shared.Rent(frame.Length);
        frame.CopyTo(_behind);
        _layerWeight = (uint)Math.Clamp(Math.Round(Math.Max(opacity, 0) * WeightSteps), 0, MaxWeight);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The pass is handed out again for the next frame, which starts without a clip or a layer.
        EndLayer();
        _clip = null;
        owner.Present();
    }

    /// <summary>
    /// Mixes the layer drawn since <see cref="SetOpacity"/> with the frame as it was before the layer: the layer was drawn over the frame, so
    /// the mix leaves the frame as it was where the layer is transparent and fades it elsewhere. Outside the clip nothing changed, so it mixes to the same.
    /// </summary>
    private void EndLayer()
    {
        if (_behind is not { } behind)
        {
            return;
        }

        _behind = null;
        var frame = owner.WritableFrame();
        var stride = owner.Stride;
        for (var offset = 0; offset < frame.Length; offset += stride)
        {
            var row = frame.Slice(offset, stride);
            CpuBlend.MixRows(row, behind.AsSpan(offset, stride), row, _layerWeight);
        }

        ArrayPool<byte>.Shared.Return(behind);
    }
}
