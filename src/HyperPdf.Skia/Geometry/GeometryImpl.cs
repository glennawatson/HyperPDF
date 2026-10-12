// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Abstract base implementation of <see cref = "IGeometryImpl"/> using SkiaSharp paths.</summary>
internal abstract class GeometryImpl : IGeometryImpl
{
    /// <summary>Lock for stroke caching operations.</summary>
    private readonly Lock _sync = new();

    /// <summary>Cached path measure for contour length.</summary>
    private SKPathMeasure? _pathMeasure;

    /// <summary>Cached pen stroke path.</summary>
    private SKPath? _cachedStrokePath;

    /// <summary>Pen used for the cached stroke path.</summary>
    private IPen? _cachedPen;

    /// <inheritdoc/>
    public abstract Rect Bounds { get; }

    /// <inheritdoc/>
    public double ContourLength
    {
        get
        {
            if (StrokePath is null)
            {
                return 0.0;
            }

            _pathMeasure ??= new SKPathMeasure(StrokePath);
            return _pathMeasure.Length;
        }
    }

    /// <summary>Gets the Skia path used for stroking.</summary>
    public abstract SKPath? StrokePath { get; }

    /// <summary>Gets the Skia path used for filling.</summary>
    public abstract SKPath? FillPath { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ITransformedGeometryImpl WithTransform(Matrix transform) => new TransformedGeometryImpl(this, transform);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPointAtDistance(double distance, out Point point) => TryGetPointAndTangentAtDistance(distance, out point, out _);

    /// <inheritdoc/>
    public bool TryGetPointAndTangentAtDistance(double distance, out Point point, out Point tangent)
    {
        lock (_sync)
        {
            _pathMeasure ??= new SKPathMeasure(StrokePath);
            if (_pathMeasure.GetPositionAndTangent((float)distance, out var position, out var direction))
            {
                point = new(position.X, position.Y);
                tangent = new(direction.X, direction.Y);
                return true;
            }

            point = default;
            tangent = default;
            return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetSegment(double startDistance, double stopDistance, bool startOnBeginFigure, [NotNullWhen(true)] out IGeometryImpl? segmentGeometry)
    {
        lock (_sync)
        {
            _pathMeasure ??= new SKPathMeasure(StrokePath);
            using var builder = new SKPathBuilder();
            if (_pathMeasure.GetSegment((float)startDistance, (float)stopDistance, builder, startOnBeginFigure))
            {
                var path = builder.Detach();
                segmentGeometry = new StreamGeometryImpl(path, path);
                return true;
            }

            segmentGeometry = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public bool FillContains(Point point) => FillPath is not null && FillPath.Contains((float)point.X, (float)point.Y);

    /// <inheritdoc/>
    public bool StrokeContains(IPen? pen, Point point)
    {
        if (StrokePath is null || pen is null || pen.Thickness <= 0)
        {
            return false;
        }

        lock (_sync)
        {
            UpdateStrokedPath(pen);
            return _cachedStrokePath is not null && _cachedStrokePath.Contains((float)point.X, (float)point.Y);
        }
    }

    /// <inheritdoc/>
    public IGeometryImpl? Intersect(IGeometryImpl geometry) => geometry is not GeometryImpl other ? null : CombinedGeometryImpl.TryCreate(GeometryCombineMode.Intersect, this, other);

    /// <inheritdoc/>
    public Rect GetRenderBounds(IPen? pen)
    {
        if (pen is null || pen.Thickness <= 0 || StrokePath is null)
        {
            return Bounds;
        }

        lock (_sync)
        {
            UpdateStrokedPath(pen);
            var bounds = _cachedStrokePath?.TightBounds.ToAvaloniaRect() ?? Bounds;
            if (FillPath is not null && FillPath != StrokePath)
            {
                bounds = bounds.Union(FillPath.TightBounds.ToAvaloniaRect());
            }

            return bounds;
        }
    }

    /// <inheritdoc/>
    public IGeometryImpl GetWidenedGeometry(IPen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        return StrokePath is not null && CreateStrokedPath(StrokePath, pen) is { } path ? new StreamGeometryImpl(path, path) : new StreamGeometryImpl(new SKPath(), null);
    }

    /// <summary>Creates a stroked path from a source path and pen.</summary>
    /// <param name = "path">The source path.</param>
    /// <param name = "pen">The pen.</param>
    /// <returns>The stroked path, or null if zero thickness.</returns>
    internal static SKPath? CreateStrokedPath(SKPath path, IPen pen)
    {
        if (pen.Thickness <= 0)
        {
            return null;
        }

        using var paint = new SKPaint
        {
            IsStroke = true,
            StrokeWidth = (float)pen.Thickness,
            StrokeCap = pen.LineCap.ToSKStrokeCap(),
            StrokeJoin = pen.LineJoin.ToSKStrokeJoin(),
            StrokeMiter = (float)pen.MiterLimit,
        };
        if (pen.DashStyle?.Dashes is { Count: > 0 } dashes)
        {
            var intervals = new float[dashes.Count];
            for (var i = 0; i < dashes.Count; i++)
            {
                intervals[i] = (float)(dashes[i] * pen.Thickness);
            }

            paint.PathEffect = SKPathEffect.CreateDash(intervals, (float)(pen.DashStyle.Offset * pen.Thickness));
        }

        using var builder = new SKPathBuilder();
        _ = paint.GetFillPath(path, builder);
        paint.PathEffect?.Dispose();
        return builder.Detach();
    }

    /// <summary>Updates the cached stroked path for the given pen.</summary>
    /// <param name = "pen">The pen.</param>
    private void UpdateStrokedPath(IPen pen)
    {
        if (_cachedPen == pen && _cachedStrokePath is not null)
        {
            return;
        }

        _cachedStrokePath?.Dispose();
        _cachedPen = pen;
        _cachedStrokePath = StrokePath is not null ? CreateStrokedPath(StrokePath, pen) : null;
    }
}
