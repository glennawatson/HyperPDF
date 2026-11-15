// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>A tile whose pixels the renderer writes on any thread and the render thread uploads to a texture when they changed.</summary>
[DebuggerDisplay("Direct3DTileSurface: {Width} x {Height}")]
internal sealed class Direct3DTileSurface : IRenderSurface
{
    /// <summary>The pixels, written by the renderer.</summary>
    private readonly CpuPixelSurface _pixels;

    /// <summary>The queue the render thread empties to release the GPU objects of disposed tiles, because a frame may still use them.</summary>
    private readonly ConcurrentQueue<Direct3DRetired> _retired;

    /// <summary>One when the pixels changed since the last upload.</summary>
    private int _dirty = 1;

    /// <summary>Initializes a new instance of the <see cref="Direct3DTileSurface"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="retired">The queue of GPU objects to release.</param>
    internal Direct3DTileSurface(int width, int height, ConcurrentQueue<Direct3DRetired> retired)
    {
        _pixels = new(width, height);
        _retired = retired;
    }

    /// <inheritdoc/>
    public int Width => _pixels.Width;

    /// <inheritdoc/>
    public int Height => _pixels.Height;

    /// <inheritdoc/>
    public long ByteSize => _pixels.ByteSize;

    /// <summary>Gets or sets the texture, or 0 before the first upload. Only the render thread touches it.</summary>
    internal ulong Texture { get; set; }

    /// <summary>Gets or sets the view shaders sample the texture through, or 0.</summary>
    internal ulong View { get; set; }

    /// <summary>Gets the pixels to upload.</summary>
    internal ReadOnlySpan<byte> Pixels => _pixels.Pixels;

    /// <summary>Gets the bytes per line of the pixels.</summary>
    internal int Stride => _pixels.Stride;

    /// <inheritdoc/>
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer)
    {
        var written = _pixels.Write(in state, writer);
        Volatile.Write(ref _dirty, 1);
        return written;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Texture != 0)
        {
            _retired.Enqueue(new(Texture, View));
            Texture = 0;
            View = 0;
        }

        _pixels.Dispose();
    }

    /// <summary>Takes the dirty flag, so the caller uploads once per change.</summary>
    /// <returns><see langword="true"/> when the pixels changed since the last call.</returns>
    internal bool TakeDirty() => Interlocked.Exchange(ref _dirty, 0) != 0;
}
