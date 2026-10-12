// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Combined geometry implementation via boolean path operations.</summary>
internal sealed class CombinedGeometryImpl : GeometryImpl
{
    /// <summary>Initializes a new instance of the <see cref = "CombinedGeometryImpl"/> class.</summary>
    /// <param name = "stroke">The stroke path.</param>
    /// <param name = "fill">The fill path.</param>
    public CombinedGeometryImpl(SKPath? stroke, SKPath? fill)
    {
        StrokePath = stroke;
        FillPath = fill;
        var bounds = stroke?.TightBounds ?? default;
        if (fill is not null)
        {
            bounds.Union(fill.TightBounds);
        }

        Bounds = bounds.ToAvaloniaRect();
    }

    /// <inheritdoc/>
    public override Rect Bounds { get; }

    /// <inheritdoc/>
    public override SKPath? StrokePath { get; }

    /// <inheritdoc/>
    public override SKPath? FillPath { get; }

    /// <summary>Creates a combined geometry from two geometry implementations.</summary>
    /// <param name = "combineMode">The boolean operation mode.</param>
    /// <param name = "g1">The first geometry.</param>
    /// <param name = "g2">The second geometry.</param>
    /// <returns>The combined geometry.</returns>
    internal static CombinedGeometryImpl ForceCreate(
        GeometryCombineMode combineMode,
        IGeometryImpl g1,
        IGeometryImpl g2) => g1 is GeometryImpl i1 && g2 is GeometryImpl i2 && TryCreate(combineMode, i1, i2) is { } result ? result : new(null, null);

    /// <summary>Attempts to create a combined geometry from two geometry implementations.</summary>
    /// <param name = "combineMode">The boolean operation mode.</param>
    /// <param name = "g1">The first geometry.</param>
    /// <param name = "g2">The second geometry.</param>
    /// <returns>The combined geometry, or null if operation failed.</returns>
    internal static CombinedGeometryImpl? TryCreate(GeometryCombineMode combineMode, GeometryImpl g1, GeometryImpl g2)
    {
        var op = combineMode switch
        {
            GeometryCombineMode.Intersect => SKPathOp.Intersect,
            GeometryCombineMode.Xor => SKPathOp.Xor,
            GeometryCombineMode.Exclude => SKPathOp.Difference,
            _ => SKPathOp.Union,
        };
        var stroke = g1.StrokePath is not null && g2.StrokePath is not null ? g1.StrokePath.Op(g2.StrokePath, op) : null;
        SKPath? fill = null;
        if (g1.FillPath is not null && g2.FillPath is not null)
        {
            fill = ReferenceEquals(g1.FillPath, g1.StrokePath) && ReferenceEquals(g2.FillPath, g2.StrokePath) ? stroke : g1.FillPath.Op(g2.FillPath, op);
        }

        return stroke is null && fill is null ? null : new(stroke, fill);
    }
}
