// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A picture of a model drawn from one view.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Rgba">The pixels, four bytes each in red, green, blue, alpha order, row by row from the top.</param>
/// <param name="ViewName">The name of the view node the frame was drawn from; empty for the camera that framed the whole model.</param>
/// <param name="TrianglesDrawn">The number of triangles that covered at least one pixel.</param>
[DebuggerDisplay("U3dFrame: {Width}x{Height} {TrianglesDrawn} triangles")]
public sealed record U3dFrame(int Width, int Height, byte[] Rgba, string ViewName, int TrianglesDrawn);
