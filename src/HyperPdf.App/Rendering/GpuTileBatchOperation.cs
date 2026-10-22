// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using HyperPdf.Rendering.Skia;
using HyperPdf.Skia;

namespace HyperPdf.App.Rendering;

/// <summary>Hands a tile batch to Avalonia's compositor and passes it the Skia lease the compositor gives back.</summary>
[DebuggerDisplay("GpuTileBatchOperation: {Count} tiles in {Bounds}")]
internal sealed class GpuTileBatchOperation : ICustomDrawOperation
{
    /// <summary>The batch that owns the tiles and draws them.</summary>
    private readonly SkiaTileBatch _batch;

    /// <summary>Initializes a new instance of the <see cref="GpuTileBatchOperation"/> class.</summary>
    /// <param name="batch">The batch, which the operation disposes.</param>
    internal GpuTileBatchOperation(SkiaTileBatch batch)
    {
        _batch = batch;
        var area = batch.Bounds;
        Bounds = new(area.Left, area.Top, area.Width, area.Height);
    }

    /// <inheritdoc/>
    public Rect Bounds { get; }

    /// <summary>Gets the number of tiles in the batch.</summary>
    internal int Count => _batch.Count;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HitTest(Point p) => false;

    /// <inheritdoc/>
    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other) || (other is GpuTileBatchOperation operation && _batch.Equals(operation._batch));

    /// <inheritdoc/>
    public void Render(ImmediateDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
        {
            _batch.RenderUnavailable();
            return;
        }

        try
        {
            using var lease = feature.Lease();
            _batch.Render(new(lease.SkCanvas, lease.GrContext, lease.ImageRetirement, lease.SurfaceColorType));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            _batch.RenderUnavailable();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _batch.Dispose();
}
