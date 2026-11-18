// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Rendering.Cpu;

/// <summary>
/// One draw a frame pass was asked to make, held until the frame ends. Two frames whose draws are the same, one for one, leave
/// the same pixels, so the second is not drawn. A picture is the same when it is the same surface and has not been written since.
/// </summary>
/// <param name="Kind">What the draw is.</param>
/// <param name="Area">Where it lands, or what it clips to.</param>
/// <param name="Argb">The colour of a clear or a fill.</param>
/// <param name="Surface">The picture drawn.</param>
/// <param name="Version">How often the picture had been written when the draw was asked for.</param>
/// <param name="Smooth">Whether a scaled picture is sampled smoothly.</param>
/// <param name="Opacity">The opacity set.</param>
[DebuggerDisplay("PassCommand: {Kind}")]
internal readonly record struct PassCommand(PassCommandKind Kind, LayoutRect Area, uint Argb, CpuPixelSurface? Surface, int Version, bool Smooth, double Opacity)
{
    /// <summary>Tells whether another draw is the same draw, comparing each field directly so a frame of draws is compared without looking up comparers.</summary>
    /// <param name="other">The other draw.</param>
    /// <returns><see langword="true"/> when every field is the same.</returns>
    public bool Equals(PassCommand other) =>
        Kind == other.Kind
        && Argb == other.Argb
        && Version == other.Version
        && Smooth == other.Smooth
        && ReferenceEquals(Surface, other.Surface)
        && Same(Area.X, other.Area.X)
        && Same(Area.Y, other.Area.Y)
        && Same(Area.Width, other.Area.Width)
        && Same(Area.Height, other.Area.Height)
        && Same(Opacity, other.Opacity);

    /// <summary>Gets a hash that agrees with <see cref="Equals(PassCommand)"/>.</summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => HashCode.Combine(Kind, Argb, Version, Area.X, Area.Y, Surface);

    /// <summary>Tells whether two numbers hold the same bits, which is stricter than being equal: a frame is only left undrawn when nothing could differ.</summary>
    /// <param name="first">One number.</param>
    /// <param name="second">The other.</param>
    /// <returns><see langword="true"/> when the bits are the same.</returns>
    private static bool Same(double first, double second) => BitConverter.DoubleToInt64Bits(first) == BitConverter.DoubleToInt64Bits(second);
}
