// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The steps from clip space to the screen that every way of drawing a frame shares.</summary>
internal static class U3dScreenSpace
{
    /// <summary>One half, the middle of the screen in normalised coordinates.</summary>
    private const float Half = 0.5F;

    /// <summary>The visibility bit for front faces.</summary>
    private const uint FrontFaces = 1;

    /// <summary>The visibility bit for back faces.</summary>
    private const uint BackFaces = 2;

    /// <summary>Converts a clip-space position to screen coordinates with the depth in Z and the reciprocal of W in W.</summary>
    /// <param name="clip">The clip-space position.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    /// <returns>The screen position: X to the right and Y downward in pixels.</returns>
    internal static Vector4 Project(Vector4 clip, int width, int height)
    {
        var inverse = 1 / clip.W;
        return new(((clip.X * inverse * Half) + Half) * width, (Half - (clip.Y * inverse * Half)) * height, clip.Z * inverse, inverse);
    }

    /// <summary>Gets twice the signed area of a triangle in screen coordinates.</summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <returns>The area; negative for a triangle that faces the viewer, as the screen's Y axis points down.</returns>
    internal static float Edge(Vector4 a, Vector4 b, Vector4 c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    /// <summary>Determines whether a face is drawn for a node's visibility setting.</summary>
    /// <param name="area">The triangle's signed screen area.</param>
    /// <param name="visibility">1 front faces, 2 back faces, 3 both.</param>
    /// <returns><see langword="true"/> when the face is drawn.</returns>
    internal static bool IsVisible(float area, uint visibility) => area != 0 && (visibility & (area < 0 ? FrontFaces : BackFaces)) != 0;
}
