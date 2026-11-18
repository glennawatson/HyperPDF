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

    /// <summary>The small pictures kept at the size they are drawn at, so a frame copies them instead of scaling them again.</summary>
    private readonly ScaledCopyCache _scaled = new();

    /// <summary>The pass handed out for every frame.</summary>
    private CpuFramePass? _pass;

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

    /// <summary>Gets a number that names the frame buffer in use; it changes each time a resize makes a new one.</summary>
    internal int FrameBuffer { get; private set; }

    /// <summary>Gets the frames that were presented without drawing, because their draws were those of the frame before.</summary>
    internal int UnchangedFrames { get; private set; }

    /// <summary>Gets or sets the times the kept pixels of a group of draws were put back in place of drawing the group.</summary>
    internal int KeptRestores { get; set; }

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
        FrameBuffer++;
    }

    /// <inheritdoc/>
    public IFramePass BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _pass ??= new(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        _scaled.Dispose();

        // Disposing a pass presents its frame, which does nothing once the backend is disposed.
        _pass?.Dispose();
        _frame = [];
    }

    /// <summary>Paints a rectangle of the frame with a colour, over what is there.</summary>
    /// <param name="rows">The pixels, inside the frame.</param>
    /// <param name="argb">The colour.</param>
    internal void Fill(in BlitRows rows, uint argb)
    {
        if (rows.IsEmpty)
        {
            return;
        }

        var frame = WritableFrame();
        var stride = Stride;
        var length = (rows.Right - rows.Left) * BytesPerPixel;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            CpuFill.Over(frame.Slice((row * stride) + (rows.Left * BytesPerPixel), length), argb);
        }
    }

    /// <summary>Draws a surface into the frame, scaled to the size asked for.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="request">Where the surface goes and how.</param>
    internal void Blit(CpuPixelSurface surface, in BlitRequest request)
    {
        var frame = WritableFrame();
        var grid = Grid;
        if (!_scaled.TryDraw(frame, grid, surface, request))
        {
            CpuBlit.Draw(frame, grid, surface.Pixels, surface.Grid, request);
        }
    }

    /// <summary>Gets the writable frame pixels for a pass.</summary>
    /// <returns>The pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> WritableFrame() => _frame.AsSpan(0, Stride * Size.Height);

    /// <summary>Hands the head the frame it already has, when a frame's draws were those of the frame before and nothing was drawn.</summary>
    internal void PresentUnchanged()
    {
        if (_disposed)
        {
            return;
        }

        // Nothing was drawn, so the pictures kept at their drawn size are neither used nor aged by this frame.
        UnchangedFrames++;
        _presented(FramePixels, Size, Stride);
    }

    /// <summary>Hands a finished frame to the head.</summary>
    internal void Present()
    {
        if (_disposed)
        {
            return;
        }

        _scaled.EndFrame();
        _presented(FramePixels, Size, Stride);
    }
}
