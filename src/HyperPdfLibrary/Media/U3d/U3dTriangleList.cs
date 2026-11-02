// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The triangles of a frame, lit and cut and placed on the screen, for a painter that draws triangles itself.</summary>
/// <param name="Width">The frame's width in pixels.</param>
/// <param name="Height">The frame's height in pixels.</param>
/// <param name="Background">The frame's background: red, green, blue and alpha, each from 0 to 1.</param>
/// <param name="Triangles">The triangles, far to near, so that drawing them in order hides what is behind.</param>
/// <param name="ViewName">The name of the view node the frame is drawn from; empty for the camera that framed the whole model.</param>
[DebuggerDisplay("U3dTriangleList: {Triangles.Length} triangles")]
public sealed record U3dTriangleList(int Width, int Height, Vector4 Background, U3dScreenTriangle[] Triangles, string ViewName);
