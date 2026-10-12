// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Ellipse geometry implementation.</summary>
internal sealed class EllipseGeometryImpl : GeometryImpl
{
    /// <summary>The cached Skia path.</summary>
    private readonly Lazy<SKPath> _path;

    /// <summary>Initializes a new instance of the <see cref = "EllipseGeometryImpl"/> class.</summary>
    /// <param name = "rect">The ellipse bounds.</param>
    public EllipseGeometryImpl(Rect rect)
    {
        Bounds = rect;
        _path = new(() =>
        {
            using var p = new SKPathBuilder();
            p.AddOval(rect.ToSKRect());
            return p.Detach();
        });
    }

    /// <inheritdoc/>
    public override Rect Bounds { get; }

    /// <inheritdoc/>
    public override SKPath StrokePath => _path.Value;

    /// <inheritdoc/>
    public override SKPath FillPath => _path.Value;
}
