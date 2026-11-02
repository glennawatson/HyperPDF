// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Cuts triangles at the near plane of the view, so a triangle that reaches behind the camera is drawn only where it is in front.</summary>
internal static class U3dClipper
{
    /// <summary>The length of the buffer <see cref="Clip"/> needs: four kept corners at most, and room for the writes of the last step that are not kept.</summary>
    internal const int BufferLength = 6;

    /// <summary>The corners of a triangle.</summary>
    private const int Corners = 3;

    /// <summary>Keeps the part of a triangle in front of the near plane, which in clip space is where Z is not below zero.</summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="output">A buffer of <see cref="BufferLength"/> corners; the first corners are the kept part.</param>
    /// <returns>The number of corners written: 0 when the triangle is wholly behind the plane, 3 when wholly in front, and 3 or 4 when it is cut.</returns>
    internal static int Clip(in U3dRasterVertex a, in U3dRasterVertex b, in U3dRasterVertex c, Span<U3dRasterVertex> output)
    {
        Span<U3dRasterVertex> corners = [a, b, c];
        var count = 0;
        for (var i = 0; i < Corners; i++)
        {
            var current = corners[i];
            var next = corners[(i + 1) % Corners];
            var currentIn = current.Clip.Z >= 0;
            output[count] = current;
            count += currentIn ? 1 : 0;
            output[count] = current.Lerp(next, current.Clip.Z / (current.Clip.Z - next.Clip.Z));
            count += currentIn != (next.Clip.Z >= 0) ? 1 : 0;
        }

        return count;
    }
}
