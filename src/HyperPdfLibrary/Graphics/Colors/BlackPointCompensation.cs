// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A per-channel linear map of D50 XYZ that moves a source black point to a destination black point and leaves the D50
/// white point where it is.
/// </summary>
/// <param name="Scale">The factor applied to each of X, Y and Z.</param>
/// <param name="Offset">The amount added to each of X, Y and Z after scaling.</param>
internal readonly record struct BlackPointCompensation(Float3 Scale, Float3 Offset)
{
    /// <summary>The largest difference between black points that counts as equal.</summary>
    private const float Tolerance = 1e-6F;

    /// <summary>Builds the map between two black points.</summary>
    /// <param name="source">The source black point in D50 XYZ.</param>
    /// <param name="destination">The destination black point in D50 XYZ.</param>
    /// <returns>The map, or <see langword="null"/> when the black points are the same or the source is the white point.</returns>
    internal static BlackPointCompensation? Between(Float3 source, Float3 destination)
    {
        if (IsSame(source, destination))
        {
            return null;
        }

        var x = Axis(source.X, destination.X, IccPcs.WhiteX);
        var y = Axis(source.Y, destination.Y, IccPcs.WhiteY);
        var z = Axis(source.Z, destination.Z, IccPcs.WhiteZ);
        if (x is null || y is null || z is null)
        {
            return null;
        }

        var (xScale, xOffset) = x.Value;
        var (yScale, yOffset) = y.Value;
        var (zScale, zOffset) = z.Value;
        return new(new(xScale, yScale, zScale), new(xOffset, yOffset, zOffset));
    }

    /// <summary>Applies the map.</summary>
    /// <param name="xyz">The colour in D50 XYZ.</param>
    /// <returns>The compensated colour.</returns>
    internal Float3 Apply(Float3 xyz) => new(
        (Scale.X * xyz.X) + Offset.X,
        (Scale.Y * xyz.Y) + Offset.Y,
        (Scale.Z * xyz.Z) + Offset.Z);

    /// <summary>Scales the rows of a matrix that gives D50 XYZ, so it gives XYZ already multiplied by <see cref="Scale"/>.</summary>
    /// <param name="toXyz">The matrix whose rows give X, Y and Z.</param>
    /// <returns>The scaled matrix; adding <see cref="Offset"/> to its result completes the map.</returns>
    internal Matrix3 ScaleRows(in Matrix3 toXyz) => new(
        Scale.X * toXyz.A,
        Scale.X * toXyz.B,
        Scale.X * toXyz.C,
        Scale.Y * toXyz.D,
        Scale.Y * toXyz.E,
        Scale.Y * toXyz.F,
        Scale.Z * toXyz.G,
        Scale.Z * toXyz.H,
        Scale.Z * toXyz.I);

    /// <summary>Determines whether two black points are equal within the tolerance, so the map would change nothing.</summary>
    /// <param name="source">The source black point.</param>
    /// <param name="destination">The destination black point.</param>
    /// <returns><see langword="true"/> when every axis agrees.</returns>
    private static bool IsSame(Float3 source, Float3 destination) =>
        MathF.Abs(source.X - destination.X) < Tolerance
        && MathF.Abs(source.Y - destination.Y) < Tolerance
        && MathF.Abs(source.Z - destination.Z) < Tolerance;

    /// <summary>Builds the map along one axis.</summary>
    /// <param name="source">The source black point on this axis.</param>
    /// <param name="destination">The destination black point on this axis.</param>
    /// <param name="white">The white point on this axis.</param>
    /// <returns>The scale and offset, or <see langword="null"/> when the source equals the white point.</returns>
    private static AxisMap? Axis(float source, float destination, float white)
    {
        var span = source - white;
        return MathF.Abs(span) < Tolerance
            ? null
            : new((destination - white) / span, -white * (destination - source) / span);
    }

    /// <summary>The scale and offset along one axis.</summary>
    /// <param name="Scale">The factor.</param>
    /// <param name="Offset">The amount added after scaling.</param>
    private readonly record struct AxisMap(float Scale, float Offset);
}
