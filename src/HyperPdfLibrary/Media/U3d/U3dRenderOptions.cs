// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>How to draw a frame of a model.</summary>
/// <param name="Width">The frame's width in pixels.</param>
/// <param name="Height">The frame's height in pixels.</param>
/// <param name="ViewName">The name of the view node to draw from, or <see langword="null"/> for the first view node, or a camera that frames the whole model when there is none.</param>
/// <param name="Background">The red, green, blue and alpha of the frame before anything is drawn, each from 0 to 1.</param>
[DebuggerDisplay("U3dRenderOptions: {Width}x{Height}")]
public sealed record U3dRenderOptions(int Width, int Height, string? ViewName, Vector4 Background)
{
    /// <summary>Gets the most pixels a frame may have, which keeps a request for a huge picture from using the memory.</summary>
    public static int MaxPixels { get; } = 16_777_216;

    /// <summary>Gets a value indicating whether the size is one a frame can have.</summary>
    public bool IsValid => Width > 0 && Height > 0 && (long)Width * Height <= MaxPixels;
}
