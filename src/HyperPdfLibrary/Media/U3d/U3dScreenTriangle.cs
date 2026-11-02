// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A lit triangle in screen coordinates, ready for a painter that draws triangles itself, such as a GPU canvas.</summary>
/// <param name="A">The first corner in pixels, with Y running down.</param>
/// <param name="B">The second corner.</param>
/// <param name="C">The third corner.</param>
/// <param name="Depth">The mean depth of the corners from 0 near to 1 far, used to draw far triangles first.</param>
/// <param name="ColorA">The lit colour of the first corner, each channel from 0 to 1.</param>
/// <param name="ColorB">The lit colour of the second corner.</param>
/// <param name="ColorC">The lit colour of the third corner.</param>
/// <param name="UvA">The texture coordinate of the first corner.</param>
/// <param name="UvB">The texture coordinate of the second corner.</param>
/// <param name="UvC">The texture coordinate of the third corner.</param>
/// <param name="Opacity">How opaque the triangle is, from 0 to 1.</param>
/// <param name="Texture">The texture drawn over the triangle, or <see langword="null"/>.</param>
[DebuggerDisplay("U3dScreenTriangle: depth {Depth}")]
public readonly record struct U3dScreenTriangle(
    Vector2 A,
    Vector2 B,
    Vector2 C,
    float Depth,
    Vector3 ColorA,
    Vector3 ColorB,
    Vector3 ColorC,
    Vector2 UvA,
    Vector2 UvB,
    Vector2 UvC,
    float Opacity,
    U3dTexture? Texture);
