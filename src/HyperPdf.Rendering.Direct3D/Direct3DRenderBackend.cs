// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Direct3D.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>
/// Draws frames on the GPU with Direct3D 11. A frame's quads are gathered into one vertex array as they are asked for, then uploaded
/// once and drawn in runs that share a texture. Every method must run on one thread.
/// </summary>
[DebuggerDisplay("Direct3DRenderBackend: {Size}")]
public sealed unsafe class Direct3DRenderBackend : IRenderBackend
{
    /// <summary>The vertices of one quad, as two triangles.</summary>
    private const int QuadVertices = 6;

    /// <summary>The largest channel value.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The mask of the alpha and green channels, which keep their place when red and blue swap.</summary>
    private const uint AlphaGreenMask = 0xFF00FF00;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The DXGI format of 8 bit BGRA.</summary>
    private const uint FormatBgra = 87;

    /// <summary>The bind flag of a texture a shader reads.</summary>
    private const uint BindShaderResource = 0x8;

    /// <summary>The vertices the first vertex array holds.</summary>
    private const int FirstVertexCapacity = 4096;

    /// <summary>The GPU objects of tiles that were disposed and wait for the next frame to release them.</summary>
    private readonly ConcurrentQueue<Direct3DRetired> _retired = new();

    /// <summary>The runs of the current frame, reused each frame.</summary>
    private readonly List<Direct3DBatch> _batches = [];

    /// <summary>The device.</summary>
    private readonly Direct3DDevice _device;

    /// <summary>The pipeline objects.</summary>
    private readonly Direct3DPipeline _pipeline;

    /// <summary>The vertices of the current frame, reused each frame and grown when a frame has more.</summary>
    private Direct3DVertex[] _vertices = new Direct3DVertex[FirstVertexCapacity];

    /// <summary>The number of vertices in the current frame.</summary>
    private int _vertexCount;

    /// <summary>The pass handed out for every frame, so a frame allocates nothing. It is made on the first frame.</summary>
    private Direct3DFramePass? _pass;

    /// <summary>One once the backend is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="Direct3DRenderBackend"/> class that draws into a texture, which tests read back.</summary>
    /// <exception cref="InvalidOperationException">No Direct3D 11 device could be made.</exception>
    public Direct3DRenderBackend()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Direct3DRenderBackend"/> class.</summary>
    /// <param name="window">The window handle frames are shown in, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">No Direct3D 11 device could be made.</exception>
    public Direct3DRenderBackend(nint window)
    {
        _device = new(window);
        try
        {
            _pipeline = new(_device);
        }
        catch (InvalidOperationException)
        {
            _device.Dispose();
            throw;
        }

        Surfaces = new Direct3DSurfaceFactory(_retired);
    }

    /// <inheritdoc/>
    public RenderBackendKind Kind => RenderBackendKind.Direct3D11;

    /// <inheritdoc/>
    public IRenderSurfaceFactory Surfaces { get; }

    /// <inheritdoc/>
    public bool IsAlive => Volatile.Read(ref _disposed) == 0;

    /// <summary>Gets the current frame size in pixels.</summary>
    public SurfaceSize Size { get; private set; }

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
        _batches.Clear();
        _vertexCount = 0;
        return _pass ??= new(this);
    }

    /// <summary>Reads the frame just drawn as premultiplied BGRA with the top line first, the layout the CPU backend uses. Only a backend made without a window can be read.</summary>
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

        ReleaseRetired();
        _pipeline.Dispose();
        _device.Dispose();

        // Disposing a pass presents its frame, which does nothing once the backend is disposed.
        _pass?.Dispose();
    }

    /// <summary>Adds a clear of the whole frame.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddClear(uint argb) => _batches.Add(new(IsClear: true, null, Smooth: false, _vertexCount, 0, argb));

    /// <summary>Adds a quad to the frame, joining the run before it when both use the same texture and sampler.</summary>
    /// <param name="tile">The tile the quad shows, or <see langword="null"/> for a solid fill.</param>
    /// <param name="smooth">Whether the tile is sampled smoothly.</param>
    /// <param name="area">Where the quad goes, in frame pixels.</param>
    /// <param name="argb">The colour as 0xAARRGGBB, white for a tile.</param>
    internal void AddQuad(Direct3DTileSurface? tile, bool smooth, in LayoutRect area, uint argb)
    {
        if (_vertexCount + QuadVertices > _vertices.Length)
        {
            Array.Resize(ref _vertices, _vertices.Length + _vertices.Length);
        }

        var color = ToVertexColor(argb);
        var left = (float)area.X;
        var top = (float)area.Y;
        var right = (float)(area.X + area.Width);
        var bottom = (float)(area.Y + area.Height);
        var topLeft = new Direct3DVertex(left, top, 0, 0, color);
        var topRight = new Direct3DVertex(right, top, 1, 0, color);
        var bottomLeft = new Direct3DVertex(left, bottom, 0, 1, color);
        var bottomRight = new Direct3DVertex(right, bottom, 1, 1, color);

        // Two triangles that share the diagonal from the top right to the bottom left.
        ReadOnlySpan<Direct3DVertex> quad = [topLeft, topRight, bottomLeft, topRight, bottomRight, bottomLeft];
        quad.CopyTo(_vertices.AsSpan(_vertexCount, QuadVertices));
        Join(tile, smooth);
        _vertexCount += QuadVertices;
    }

    /// <summary>Draws the gathered runs and finishes the frame.</summary>
    internal void Present()
    {
        if (Size.IsEmpty || !IsAlive || _device.Target is null)
        {
            return;
        }

        ReleaseRetired();
        UploadTiles();
        _pipeline.BeginFrame(_vertices.AsSpan(0, _vertexCount));
        DrawBatches();
        _device.Present();
    }

    /// <summary>Converts a 0xAARRGGBB colour to the premultiplied vertex colour, which has red in the low byte.</summary>
    /// <param name="argb">The colour.</param>
    /// <returns>The vertex colour.</returns>
    private static uint ToVertexColor(uint argb)
    {
        var premultiplied = CpuFill.Premultiply(argb);
        return (premultiplied & AlphaGreenMask) | ((premultiplied >> RedShift) & ChannelMask) | ((premultiplied & ChannelMask) << RedShift);
    }

    /// <summary>Extends the last run by one quad, or starts a new run when the texture or sampler changes.</summary>
    /// <param name="tile">The tile, or <see langword="null"/> for a solid fill.</param>
    /// <param name="smooth">Whether the tile is sampled smoothly.</param>
    private void Join(Direct3DTileSurface? tile, bool smooth)
    {
        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (!last.IsClear && ReferenceEquals(last.Tile, tile) && last.Smooth == smooth)
            {
                _batches[^1] = last with { Count = last.Count + QuadVertices };
                return;
            }
        }

        _batches.Add(new(IsClear: false, tile, smooth, _vertexCount, QuadVertices, 0));
    }

    /// <summary>Makes the texture of every tile in the frame that has none and uploads the pixels of the tiles that changed.</summary>
    private void UploadTiles()
    {
        foreach (var batch in _batches)
        {
            if (batch.Tile is not { } tile)
            {
                continue;
            }

            if (tile.Texture == 0)
            {
                var texture = _device.CreateTexture(new((uint)tile.Width, (uint)tile.Height, 1, 1, FormatBgra, 1, 0, 0, BindShaderResource, 0, 0));
                void* view;
                var made = Direct3DCalls.CreateShaderResourceView(_device.Device, texture, &view);
                if (made < 0)
                {
                    Direct3DCalls.Release(texture);
                    HResult.Check(made, "make a tile view");
                }

                tile.Texture = (ulong)texture;
                tile.View = (ulong)view;
            }

            // Taking the dirty flag clears it, so a tile drawn twice in one frame is uploaded only the first time.
            if (tile.TakeDirty())
            {
                fixed (byte* pixels = tile.Pixels)
                {
                    Direct3DCalls.UpdateSubresource(_device.Context, (void*)tile.Texture, pixels, (uint)tile.Stride);
                }
            }
        }
    }

    /// <summary>Draws every run in order, changing the texture and sampler only when a run needs a different one.</summary>
    private void DrawBatches()
    {
        var context = _device.Context;
        void* boundView = null;
        void* boundSampler = null;
        foreach (var batch in _batches)
        {
            if (batch.IsClear)
            {
                Clear(batch.Color);
                continue;
            }

            var view = batch.Tile is { } tile ? (void*)tile.View : _pipeline.WhiteView;
            if (view != boundView)
            {
                Direct3DCalls.SetPixelTexture(context, view);
                boundView = view;
            }

            var sampler = batch.Smooth ? _pipeline.Linear : _pipeline.Nearest;
            if (sampler != boundSampler)
            {
                Direct3DCalls.SetPixelSampler(context, sampler);
                boundSampler = sampler;
            }

            Direct3DCalls.Draw(context, (uint)batch.Count, (uint)batch.First);
        }
    }

    /// <summary>Clears the frame to a colour.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    private void Clear(uint argb)
    {
        var premultiplied = CpuFill.Premultiply(argb);
        var color = stackalloc float[]
        {
            ((premultiplied >> RedShift) & ChannelMask) / ChannelMax,
            ((premultiplied >> GreenShift) & ChannelMask) / ChannelMax,
            (premultiplied & ChannelMask) / ChannelMax,
            (premultiplied >> AlphaShift) / ChannelMax,
        };
        Direct3DCalls.ClearRenderTarget(_device.Context, _device.Target, color);
    }

    /// <summary>Releases the GPU objects of tiles that were disposed since the last frame.</summary>
    private void ReleaseRetired()
    {
        while (_retired.TryDequeue(out var retired))
        {
            Direct3DCalls.Release((void*)retired.View);
            Direct3DCalls.Release((void*)retired.Texture);
        }
    }
}
