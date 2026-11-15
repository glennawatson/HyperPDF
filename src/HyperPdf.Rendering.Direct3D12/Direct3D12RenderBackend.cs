// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Direct3D12.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>
/// Draws frames on the GPU with Direct3D 12. A frame's quads are gathered into one vertex array as they are asked for, then uploaded
/// once and drawn in runs that share a texture. Every frame ends by waiting for the GPU, so one frame's objects are free for the next.
/// Every method must run on one thread.
/// </summary>
[DebuggerDisplay("Direct3D12RenderBackend: {Size}")]
public sealed unsafe class Direct3D12RenderBackend : IRenderBackend
{
    /// <summary>The largest channel value.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The slot of the white texture in the view heap.</summary>
    private const int WhiteSlot = 0;

    /// <summary>The GPU textures of tiles that were disposed and wait for the next frame to release them.</summary>
    private readonly ConcurrentQueue<Direct3D12Retired> _retired = new();

    /// <summary>The quads of the current frame.</summary>
    private readonly QuadBatcher<Direct3D12TileSurface> _quads = new();

    /// <summary>The device.</summary>
    private readonly Direct3D12Device _device;

    /// <summary>The pipeline objects.</summary>
    private readonly Direct3D12Pipeline _pipeline;

    /// <summary>The buffer the pixels of changed tiles are copied through.</summary>
    private readonly Direct3D12UploadBuffer _uploads;

    /// <summary>The pass handed out for every frame, so a frame allocates nothing. It is made on the first frame.</summary>
    private Direct3D12FramePass? _pass;

    /// <summary>One once the backend is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="Direct3D12RenderBackend"/> class that draws into a texture, which tests read back.</summary>
    /// <exception cref="InvalidOperationException">No Direct3D 12 device could be made.</exception>
    public Direct3D12RenderBackend()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Direct3D12RenderBackend"/> class.</summary>
    /// <param name="window">The window handle frames are shown in, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">No Direct3D 12 device could be made.</exception>
    public Direct3D12RenderBackend(nint window)
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

        _uploads = new(_device);
        Surfaces = new Direct3D12SurfaceFactory(_retired);
    }

    /// <inheritdoc/>
    public RenderBackendKind Kind => RenderBackendKind.Direct3D12;

    /// <inheritdoc/>
    public IRenderSurfaceFactory Surfaces { get; }

    /// <inheritdoc/>
    public bool IsAlive => Volatile.Read(ref _disposed) == 0;

    /// <summary>Gets the current frame size in pixels.</summary>
    public SurfaceSize Size { get; private set; }

    /// <summary>Gets the quads of the frame being drawn.</summary>
    internal QuadBatcher<Direct3D12TileSurface> Quads => _quads;

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

        // Frames wait for the GPU when they end, so nothing below is in use any more.
        ReleaseRetired();
        _uploads.Dispose();
        _pipeline.Dispose();
        _device.Dispose();

        // Disposing a pass presents its frame, which does nothing once the backend is disposed.
        _pass?.Dispose();
    }

    /// <summary>Draws the gathered runs and finishes the frame.</summary>
    internal void Present()
    {
        if (Size.IsEmpty || !IsAlive || !_device.HasTarget)
        {
            return;
        }

        ReleaseRetired();
        Reserve();
        _device.BeginFrame();
        UploadTiles();
        _pipeline.BeginFrame(_quads.Vertices);
        DrawBatches();
        _device.EndFrame();
    }

    /// <summary>Makes the vertex buffer, the view heap and the upload buffer big enough for the frame, before any command refers to them.</summary>
    private void Reserve()
    {
        var views = 1;
        var bytes = _pipeline.WhitePending ? Direct3D12Textures.UploadSize(1, 1) : 0;
        foreach (ref readonly var batch in _quads.Batches)
        {
            if (batch.Tile is not { } tile)
            {
                continue;
            }

            views++;
            if (tile.Texture == 0 || tile.IsDirty)
            {
                bytes += Direct3D12Textures.UploadSize(tile.Width, tile.Height);
            }
        }

        _pipeline.Reserve(_quads.Vertices.Length, views);
        _uploads.Ensure(bytes);
    }

    /// <summary>Makes the texture of every tile in the frame that has none and records the copy of the pixels of the tiles that changed.</summary>
    private void UploadTiles()
    {
        var cursor = 0;
        if (_pipeline.WhitePending)
        {
            var white = uint.MaxValue;
            Direct3D12Textures.Copy(_device, _uploads, cursor, new(_pipeline.White, 1, 1, sizeof(uint), IsNew: true), new(&white, sizeof(uint)));
            cursor += Direct3D12Textures.UploadSize(1, 1);
            _pipeline.WhiteWritten();
        }

        foreach (ref readonly var batch in _quads.Batches)
        {
            if (batch.Tile is { } tile)
            {
                cursor = UploadTile(tile, cursor);
            }
        }
    }

    /// <summary>Uploads one tile if it is new or changed.</summary>
    /// <param name="tile">The tile.</param>
    /// <param name="cursor">The next free offset in the upload buffer.</param>
    /// <returns>The next free offset after the tile.</returns>
    private int UploadTile(Direct3D12TileSurface tile, int cursor)
    {
        var isNew = tile.Texture == 0;
        if (isNew)
        {
            tile.Texture = (ulong)_device.CreateTexture(tile.Width, tile.Height, 0, Direct3D12Device.StateCopyDestination);
        }

        // Taking the dirty flag clears it, so a tile drawn twice in one frame is uploaded only the first time.
        var dirty = tile.TakeDirty();
        if (!isNew && !dirty)
        {
            return cursor;
        }

        var size = Direct3D12Textures.UploadSize(tile.Width, tile.Height);
        if (cursor + size > _uploads.Capacity)
        {
            // The pixels changed again after the frame was sized. They go up with the next frame.
            tile.MarkDirty();
            if (isNew)
            {
                _device.Transition(tile.Texture, Direct3D12Device.StateCopyDestination, Direct3D12Device.StatePixelResource);
            }

            return cursor;
        }

        Direct3D12Textures.Copy(_device, _uploads, cursor, new(tile.Texture, tile.Width, tile.Height, tile.Stride, isNew), tile.Pixels);
        return cursor + size;
    }

    /// <summary>Draws every run in order, changing the texture and sampler only when a run needs a different one.</summary>
    private void DrawBatches()
    {
        var list = _device.List;
        var nextSlot = WhiteSlot + 1;
        var boundSlot = -1;
        var boundSmooth = -1;
        foreach (ref readonly var batch in _quads.Batches)
        {
            if (batch.IsClear)
            {
                Clear(batch.Color);
                continue;
            }

            var slot = WhiteSlot;
            if (batch.Tile is { } tile)
            {
                slot = nextSlot++;
                _pipeline.WriteView(slot, tile.Texture);
            }

            if (slot != boundSlot)
            {
                _pipeline.BindView(slot);
                boundSlot = slot;
            }

            var smooth = batch.Smooth ? 1 : 0;
            if (smooth != boundSmooth)
            {
                _pipeline.BindSampler(batch.Smooth);
                boundSmooth = smooth;
            }

            CommandListCalls.Draw(list, (uint)batch.Count, (uint)batch.First);
        }
    }

    /// <summary>Clears the frame to a colour.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    private void Clear(uint argb)
    {
        var premultiplied = PremultipliedColor.FromArgb(argb);
        var color = stackalloc float[]
        {
            ((premultiplied >> RedShift) & ChannelMask) / ChannelMax,
            ((premultiplied >> GreenShift) & ChannelMask) / ChannelMax,
            (premultiplied & ChannelMask) / ChannelMax,
            (premultiplied >> AlphaShift) / ChannelMax,
        };
        CommandListCalls.ClearRenderTarget(_device.List, _device.Target, color);
    }

    /// <summary>Releases the GPU textures of tiles that were disposed since the last frame.</summary>
    private void ReleaseRetired()
    {
        while (_retired.TryDequeue(out var retired))
        {
            Direct3D12Calls.Release((void*)retired.Texture);
        }
    }
}
