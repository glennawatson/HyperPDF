// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Metal;

/// <summary>
/// Draws frames on the GPU with Metal. A frame's quads are gathered into one vertex array as they are asked for, then copied to the
/// GPU once and drawn in runs that share a texture. Every method must run on one thread.
/// </summary>
[DebuggerDisplay("MetalRenderBackend: {Size}")]
public sealed class MetalRenderBackend : IRenderBackend
{
    /// <summary>The textures of tiles that were disposed and wait for the next frame to release them.</summary>
    private readonly ConcurrentQueue<ulong> _retired = new();

    /// <summary>The quads of the current frame.</summary>
    private readonly QuadBatcher<MetalTileSurface> _quads = new();

    /// <summary>The Metal objects.</summary>
    private readonly MetalDevice _device;

    /// <summary>The pass handed out for every frame, so a frame allocates nothing. It is made on the first frame.</summary>
    private MetalFramePass? _pass;

    /// <summary>One once the backend is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="MetalRenderBackend"/> class that draws into a texture, which tests read back.</summary>
    /// <exception cref="InvalidOperationException">The machine has no GPU Metal can use, or Metal rejected the shaders.</exception>
    public MetalRenderBackend()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MetalRenderBackend"/> class.</summary>
    /// <param name="layer">The CAMetalLayer frames are shown in, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">The machine has no GPU Metal can use, or Metal rejected the shaders.</exception>
    public MetalRenderBackend(nint layer)
    {
        _device = new(layer);
        Surfaces = new MetalSurfaceFactory(_retired);
    }

    /// <inheritdoc/>
    public RenderBackendKind Kind => RenderBackendKind.Metal;

    /// <inheritdoc/>
    public IRenderSurfaceFactory Surfaces { get; }

    /// <inheritdoc/>
    public bool IsAlive => Volatile.Read(ref _disposed) == 0;

    /// <summary>Gets the current frame size in pixels.</summary>
    public SurfaceSize Size { get; private set; }

    /// <summary>Gets the quads of the frame being drawn.</summary>
    internal QuadBatcher<MetalTileSurface> Quads => _quads;

    /// <inheritdoc/>
    public void Resize(in SurfaceSize size)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        if (size == Size)
        {
            return;
        }

        Size = size.IsEmpty ? default : size;
        _device.Resize(Size.Width, Size.Height);
    }

    /// <inheritdoc/>
    public IFramePass BeginFrame()
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        _quads.Reset();
        return _pass ??= new(this);
    }

    /// <summary>Reads the frame just drawn as premultiplied BGRA with the top line first, the layout the CPU backend uses. Only a backend made without a layer can be read.</summary>
    /// <param name="destination">Receives the pixels. It must hold at least width * height * 4 bytes.</param>
    public void ReadPixels(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, Size.Width * Size.Height * sizeof(uint));
        _device.ReadPixels(destination);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        MetalDevice.ReleaseRetired(_retired);
        _device.Dispose();

        // Disposing a pass presents its frame, which does nothing once the backend is disposed.
        _pass?.Dispose();
    }

    /// <summary>Draws the gathered runs and finishes the frame.</summary>
    internal void Present()
    {
        if (Size.IsEmpty || !IsAlive)
        {
            return;
        }

        MetalDevice.ReleaseRetired(_retired);
        _device.DrawFrame(_quads);
    }
}
