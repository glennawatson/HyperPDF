// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>A move from one user space rectangle to another, scaling each axis, as the PDFium engine moves annotations.</summary>
/// <param name="Source">The rectangle before.</param>
/// <param name="Target">The rectangle after.</param>
[DebuggerDisplay("RectangleMap: {Source} -> {Target}")]
internal readonly record struct RectangleMap(PdfRectangle Source, PdfRectangle Target)
{
    /// <summary>The smallest side, in points, a rectangle is scaled from or to.</summary>
    private const float MinSide = 1;

    /// <summary>Gets a move that leaves points where they are.</summary>
    internal static RectangleMap Identity { get; } = new(new(0, 0, 1, 1), new(0, 0, 1, 1));

    /// <summary>Gets the horizontal scale.</summary>
    private float ScaleX => Source.Width > MinSide ? Math.Max(MinSide, Target.Width) / Source.Width : 1;

    /// <summary>Gets the vertical scale.</summary>
    private float ScaleY => Source.Height > MinSide ? Math.Max(MinSide, Target.Height) / Source.Height : 1;

    /// <summary>Moves points in place.</summary>
    /// <param name="points">The points.</param>
    internal void Apply(Span<Vector2> points)
    {
        var scale = new Vector2(ScaleX, ScaleY);
        var from = new Vector2(Source.Left, Source.Bottom);
        var to = new Vector2(Target.Left, Target.Bottom);
        foreach (ref var point in points)
        {
            point = to + ((point - from) * scale);
        }
    }

    /// <summary>Moves one point.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The moved point.</returns>
    internal Vector2 Apply(Vector2 point) =>
        new(Target.Left + ((point.X - Source.Left) * ScaleX), Target.Bottom + ((point.Y - Source.Bottom) * ScaleY));
}
