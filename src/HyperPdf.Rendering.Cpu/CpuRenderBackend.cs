// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Draws frames into a managed BGRA buffer. A platform head copies each finished frame to its window from the <see cref="FramePresented"/> callback.</summary>
[DebuggerDisplay("CpuRenderBackend: {Size}")]
public sealed class CpuRenderBackend : IRenderBackend
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The called when a frame is finished and ready to show.</summary>
    private readonly FramePresented _presented;

    /// <summary>The frame buffer.</summary>
    private byte[] _frame = [];

    /// <summary>Whether the backend was disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CpuRenderBackend"/> class.</summary>
    /// <param name="presented">Receives each finished frame so the head can copy it to the window.</param>
    public CpuRenderBackend(FramePresented presented)
    {
        ArgumentNullException.ThrowIfNull(presented);
        _presented = presented;
    }

    /// <inheritdoc/>
    public RenderBackendKind Kind => RenderBackendKind.Cpu;

    /// <inheritdoc/>
    public IRenderSurfaceFactory Surfaces { get; } = new CpuSurfaceFactory();

    /// <inheritdoc/>
    public bool IsAlive => !_disposed;

    /// <summary>Gets the current frame size in pixels.</summary>
    public SurfaceSize Size { get; private set; }

    /// <summary>Gets the bytes per frame line.</summary>
    public int Stride => Size.Width * BytesPerPixel;

    /// <summary>Gets the shape of the frame buffer.</summary>
    public PixelGrid Grid => new(Size.Width, Size.Height, Stride);

    /// <summary>Gets the current frame pixels as premultiplied BGRA.</summary>
    public ReadOnlySpan<byte> FramePixels => _frame.AsSpan(0, Stride * Size.Height);

    /// <inheritdoc/>
    public void Resize(in SurfaceSize size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (size == Size)
        {
            return;
        }

        Size = size.IsEmpty ? default : size;
        _frame = Size.IsEmpty ? [] : new byte[Stride * Size.Height];
    }

    /// <inheritdoc/>
    public IFramePass BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new CpuFramePass(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        _frame = [];
    }

    /// <summary>Gets the writable frame pixels for a pass.</summary>
    /// <returns>The pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> WritableFrame() => _frame.AsSpan(0, Stride * Size.Height);

    /// <summary>Hands a finished frame to the head.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Present() => _presented(FramePixels, Size, Stride);
}
