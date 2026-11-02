// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Keeps the triangles of a frame instead of filling them, so that a painter with its own triangle drawing, such as a GPU canvas, can draw them.</summary>
/// <param name="width">The frame's width in pixels.</param>
/// <param name="height">The frame's height in pixels.</param>
internal sealed class U3dTriangleCollector(int width, int height) : IU3dTriangleTarget
{
    /// <summary>The number of corners whose depths are averaged.</summary>
    private const float Corners = 3;

    /// <summary>The triangles kept, in the order they arrived.</summary>
    private readonly List<U3dScreenTriangle> _triangles = [];

    /// <summary>Gets the triangles kept, ordered far to near.</summary>
    /// <returns>The triangles.</returns>
    internal U3dScreenTriangle[] FarToNear()
    {
        var all = _triangles.ToArray();
        Array.Sort(all, static (left, right) => right.Depth.CompareTo(left.Depth));
        return all;
    }

    /// <inheritdoc/>
    void IU3dTriangleTarget.Fill(in U3dRasterVertex a, in U3dRasterVertex b, in U3dRasterVertex c, U3dSurfaceStyle style, uint visibility)
    {
        var screenA = U3dScreenSpace.Project(a.Clip, width, height);
        var screenB = U3dScreenSpace.Project(b.Clip, width, height);
        var screenC = U3dScreenSpace.Project(c.Clip, width, height);
        if (!U3dScreenSpace.IsVisible(U3dScreenSpace.Edge(screenA, screenB, screenC), visibility))
        {
            return;
        }

        _triangles.Add(new(
            new(screenA.X, screenA.Y),
            new(screenB.X, screenB.Y),
            new(screenC.X, screenC.Y),
            (screenA.Z + screenB.Z + screenC.Z) / Corners,
            a.Color,
            b.Color,
            c.Color,
            a.Uv,
            b.Uv,
            c.Uv,
            style.Opacity,
            style.Texture is { IsDecoded: true } texture ? texture : null));
    }
}
