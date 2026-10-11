// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using PdfViewerLite.Skia;
using SkiaSharp;

namespace PdfViewerLite.App.Rendering;

/// <summary>Composes a prepared tile using the viewer's current Skia graphics context.</summary>
[DebuggerDisplay("GpuTileDrawOperation: {Bounds}")]
internal sealed class GpuTileDrawOperation : ICustomDrawOperation
{
    /// <summary>Sampling for scaled previews and zoom stand-ins.</summary>
    private static readonly SKSamplingOptions SmoothSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Sampling for pixel-aligned sharp tiles.</summary>
    private static readonly SKSamplingOptions SharpSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>The tile retained until the scene releases this operation.</summary>
    private readonly GpuPreparedRenderSurface _tile;

    /// <summary>Whether the destination scales a preview or an earlier zoom level.</summary>
    private readonly bool _smooth;

    /// <summary>One after this operation is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="GpuTileDrawOperation"/> class.</summary>
    /// <param name="tile">The prepared tile, retained through composition.</param>
    /// <param name="bounds">The destination in the canvas's local coordinates.</param>
    /// <param name="smooth">Whether to sample a scaled tile smoothly.</param>
    private GpuTileDrawOperation(GpuPreparedRenderSurface tile, in Rect bounds, bool smooth)
    {
        _tile = tile;
        Bounds = bounds;
        _smooth = smooth;
    }

    /// <inheritdoc/>
    public Rect Bounds { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HitTest(Point p) => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

    /// <inheritdoc/>
    public void Render(ImmediateDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Volatile.Read(ref _disposed) != 0 || !_tile.TryRetainForRender())
        {
            return;
        }

        try
        {
            if (TryDrawGpu(context))
            {
                return;
            }

            DrawSoftware(context);
        }
        finally
        {
            _tile.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _tile.Release();
        }
    }

    /// <summary>Creates an operation only while the tile is owned by the cache.</summary>
    /// <param name="tile">The prepared tile.</param>
    /// <param name="bounds">The destination.</param>
    /// <param name="smooth">Whether to smooth a scaled tile.</param>
    /// <returns>An operation, or null when the tile was released.</returns>
    internal static GpuTileDrawOperation? TryCreate(GpuPreparedRenderSurface tile, in Rect bounds, bool smooth) =>
        tile.TryRetain() ? new(tile, bounds, smooth) : null;

    /// <summary>Draws a retained GPU image through the active compositor context.</summary>
    /// <param name="context">The immediate context.</param>
    /// <returns>Whether a GPU image was drawn.</returns>
    private bool TryDrawGpu(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
        {
            _tile.MarkGpuUnavailable();
            return false;
        }

        try
        {
            using var lease = feature.Lease();
            if (lease.GrContext is not { } gpu)
            {
                _tile.MarkGpuUnavailable();
                return false;
            }

            var rect = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom);
            return _tile.DrawGpu(lease.SkCanvas, gpu, lease.SurfaceColorType, rect, _smooth ? SmoothSampling : SharpSampling);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            _tile.MarkGpuUnavailable();
            return false;
        }
    }

    /// <summary>Draws software pixels prepared by the worker before this operation entered the scene.</summary>
    /// <param name="context">The immediate context.</param>
    private void DrawSoftware(ImmediateDrawingContext context)
    {
        if (_tile.SoftwareBitmap is { } bitmap)
        {
            context.DrawBitmap(bitmap, new(0, 0, _tile.Width, _tile.Height), Bounds);
        }
    }
}
