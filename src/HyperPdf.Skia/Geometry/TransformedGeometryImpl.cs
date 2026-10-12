// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Transformed geometry implementation.</summary>
internal sealed class TransformedGeometryImpl : GeometryImpl, ITransformedGeometryImpl
{
    /// <summary>Initializes a new instance of the <see cref = "TransformedGeometryImpl"/> class.</summary>
    /// <param name = "source">The source geometry.</param>
    /// <param name = "transform">The transformation matrix.</param>
    public TransformedGeometryImpl(GeometryImpl source, in Matrix transform)
    {
        ArgumentNullException.ThrowIfNull(source);
        SourceGeometry = source;
        Transform = transform;
        var matrix = transform.ToSKMatrix();
        var transformedPath = (source.StrokePath is { } original ? new SKPath(original) : null);
        transformedPath?.Transform(matrix);
        StrokePath = transformedPath;
        Bounds = transformedPath?.TightBounds.ToAvaloniaRect() ?? default;
        if (ReferenceEquals(source.StrokePath, source.FillPath))
        {
            FillPath = transformedPath;
        }
        else if (source.FillPath is not null)
        {
            var fill = new SKPath(source.FillPath);
            fill.Transform(matrix);
            FillPath = fill;
        }
    }

    /// <inheritdoc/>
    public override SKPath? StrokePath { get; }

    /// <inheritdoc/>
    public override SKPath? FillPath { get; }

    /// <inheritdoc/>
    public IGeometryImpl SourceGeometry { get; }

    /// <inheritdoc/>
    public Matrix Transform { get; }

    /// <inheritdoc/>
    public override Rect Bounds { get; }
}
