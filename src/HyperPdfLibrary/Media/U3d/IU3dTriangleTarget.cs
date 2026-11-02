// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Takes the triangles of a frame after they have been lit and cut at the near plane: the software rasteriser fills them, and the collector keeps them for a GPU painter.</summary>
internal interface IU3dTriangleTarget
{
    /// <summary>Takes a triangle.</summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="style">How to colour the triangle.</param>
    /// <param name="visibility">Which faces to draw: 1 front, 2 back, 3 both.</param>
    void Fill(in U3dRasterVertex a, in U3dRasterVertex b, in U3dRasterVertex c, U3dSurfaceStyle style, uint visibility);
}
