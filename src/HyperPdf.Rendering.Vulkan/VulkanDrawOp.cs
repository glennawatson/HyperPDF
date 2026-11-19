// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>One drawing operation of a frame, kept until the frame ends so every upload happens before the render pass starts.</summary>
/// <param name="Kind">What to draw.</param>
/// <param name="Tile">The tile, for a tile draw.</param>
/// <param name="X">The left edge in pixels.</param>
/// <param name="Y">The top edge in pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Color">The colour, for a clear or fill.</param>
/// <param name="Smooth">Whether a tile is sampled smoothly.</param>
[DebuggerDisplay("VulkanDrawOp {Kind}")]
internal readonly record struct VulkanDrawOp(VulkanDrawKind Kind, VulkanTileSurface? Tile, float X, float Y, float Width, float Height, VulkanColor Color, bool Smooth)
{
    /// <summary>Tells whether another operation is the same operation, comparing each field directly.</summary>
    /// <param name="other">The other operation.</param>
    /// <returns><see langword="true"/> when every field is the same.</returns>
    /// <remarks>The bits of the numbers are compared, which is stricter than equality, so a frame is only kept when nothing could differ.</remarks>
    public bool Equals(VulkanDrawOp other) => SameShape(other) && SameColor(other);

    /// <summary>Gets a hash that agrees with <see cref="Equals(VulkanDrawOp)"/>.</summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => HashCode.Combine(Kind, Tile, X, Y, Smooth);

    /// <summary>Tells whether two numbers hold the same bits.</summary>
    /// <param name="first">One number.</param>
    /// <param name="second">The other.</param>
    /// <returns><see langword="true"/> when the bits are the same.</returns>
    private static bool Same(float first, float second) => BitConverter.SingleToInt32Bits(first) == BitConverter.SingleToInt32Bits(second);

    /// <summary>Tells whether another operation is the same kind of draw, of the same tile, in the same place.</summary>
    /// <param name="other">The other operation.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private bool SameShape(in VulkanDrawOp other) =>
        Kind == other.Kind
        && Smooth == other.Smooth
        && ReferenceEquals(Tile, other.Tile)
        && Same(X, other.X)
        && Same(Y, other.Y)
        && Same(Width, other.Width)
        && Same(Height, other.Height);

    /// <summary>Tells whether another operation has the same colour.</summary>
    /// <param name="other">The other operation.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private bool SameColor(in VulkanDrawOp other) =>
        Same(Color.Red, other.Color.Red)
        && Same(Color.Green, other.Color.Green)
        && Same(Color.Blue, other.Color.Blue)
        && Same(Color.Alpha, other.Color.Alpha);
}
