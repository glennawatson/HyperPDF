// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>A tile whose pixels are written on the CPU by any thread and uploaded to a GPU image the first time the render thread draws it.</summary>
[DebuggerDisplay("VulkanTileSurface: {Width} x {Height}")]
internal sealed class VulkanTileSurface : IRenderSurface
{
    /// <summary>The pixels, written by the renderer.</summary>
    private readonly CpuPixelSurface _pixels;

    /// <summary>The queue the render thread empties to free GPU objects of disposed tiles, because they may still be in use.</summary>
    private readonly ConcurrentQueue<VulkanRetired> _retired;

    /// <summary>One when the pixels changed since the last upload.</summary>
    private int _dirty = 1;

    /// <summary>Initializes a new instance of the <see cref="VulkanTileSurface"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="retired">The queue of GPU objects to free.</param>
    internal VulkanTileSurface(int width, int height, ConcurrentQueue<VulkanRetired> retired)
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

    /// <summary>Gets or sets the GPU image, or <see langword="null"/> before the first upload. Only the render thread touches it.</summary>
    internal VulkanImage? Image { get; set; }

    /// <summary>Gets or sets the descriptor set that samples the image with the nearest filter, or zero.</summary>
    internal ulong NearestSet { get; set; }

    /// <summary>Gets or sets the descriptor set that samples the image with the linear filter, or zero.</summary>
    internal ulong LinearSet { get; set; }

    /// <summary>Gets or sets a value indicating whether the image has never been written, so its first upload starts from an undefined layout.</summary>
    internal bool IsFirstUpload { get; set; }

    /// <summary>Gets the pixels to upload.</summary>
    internal ReadOnlySpan<byte> Pixels => _pixels.Pixels;

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
        if (Image is not null)
        {
            _retired.Enqueue(new(Image, NearestSet, LinearSet));
            Image = null;
        }

        _pixels.Dispose();
    }

    /// <summary>Takes the dirty flag, so the caller uploads once per change.</summary>
    /// <returns><see langword="true"/> when the pixels changed since the last call.</returns>
    internal bool TakeDirty() => Interlocked.Exchange(ref _dirty, 0) != 0;
}
