// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Line geometry implementation.</summary>
internal sealed class LineGeometryImpl : GeometryImpl
{
    /// <summary>The cached Skia path.</summary>
    private readonly Lazy<SKPath> _path;

    /// <summary>Initializes a new instance of the <see cref = "LineGeometryImpl"/> class.</summary>
    /// <param name = "p1">The start point.</param>
    /// <param name = "p2">The end point.</param>
    public LineGeometryImpl(Point p1, Point p2)
    {
        Bounds = new Rect(p1, p2).Normalize();
        _path = new(() =>
        {
            using var p = new SKPathBuilder();
            p.MoveTo(p1.ToSKPoint());
            p.LineTo(p2.ToSKPoint());
            return p.Detach();
        });
    }

    /// <inheritdoc/>
    public override Rect Bounds { get; }

    /// <inheritdoc/>
    public override SKPath StrokePath => _path.Value;

    /// <inheritdoc/>
    public override SKPath? FillPath => null;
}
