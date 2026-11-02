// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A texture resource with its decoded pixels.</summary>
/// <param name="Name">The resource's name.</param>
/// <param name="Width">The width the declaration gives.</param>
/// <param name="Height">The height the declaration gives.</param>
/// <param name="Rgba">The pixels, four bytes each in red, green, blue, alpha order, or an empty array when the picture could not be decoded.</param>
[DebuggerDisplay("U3dTexture: {Name} {Width}x{Height}")]
public sealed record U3dTexture(string Name, int Width, int Height, byte[] Rgba)
{
    /// <summary>Gets a value indicating whether the picture was decoded.</summary>
    public bool IsDecoded => Rgba.Length > 0;
}
