// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Geometry group implementation aggregating multiple geometries.</summary>
internal sealed class GeometryGroupImpl : GeometryImpl
{
    /// <summary>Initializes a new instance of the <see cref = "GeometryGroupImpl"/> class.</summary>
    /// <param name = "fillRule">The fill rule.</param>
    /// <param name = "children">The child geometries.</param>
    public GeometryGroupImpl(FillRule fillRule, IReadOnlyList<IGeometryImpl> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var fillType = fillRule == FillRule.NonZero ? SKPathFillType.Winding : SKPathFillType.EvenOdd;
        var count = children.Count;
        using var strokeBuilder = new SKPathBuilder { FillType = fillType };
        var requiresFillPass = false;
        for (var i = 0; i < count; ++i)
        {
            if (children[i] is not GeometryImpl geo)
            {
                continue;
            }

            if (geo.StrokePath is not null)
            {
                strokeBuilder.AddPath(geo.StrokePath);
            }

            if (!ReferenceEquals(geo.StrokePath, geo.FillPath))
            {
                requiresFillPass = true;
            }
        }

        var stroke = strokeBuilder.Detach();
        StrokePath = stroke;
        if (requiresFillPass)
        {
            using var fillBuilder = new SKPathBuilder { FillType = fillType };
            for (var i = 0; i < count; ++i)
            {
                if (children[i] is GeometryImpl { FillPath: { } fillPath })
                {
                    fillBuilder.AddPath(fillPath);
                }
            }

            FillPath = fillBuilder.Detach();
        }
        else
        {
            FillPath = stroke;
        }

        Bounds = stroke.TightBounds.ToAvaloniaRect();
    }

    /// <inheritdoc/>
    public override Rect Bounds { get; }

    /// <inheritdoc/>
    public override SKPath StrokePath { get; }

    /// <inheritdoc/>
    public override SKPath FillPath { get; }
}
